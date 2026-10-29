"""ASP.NET: controllers com [Route]/[Http*] (inclusive herdando a rota da base)
e minimal APIs (MapGet/MapPost).

Heranca importa: `ClientesController : BaseApiController<T...>` nao declara
[Route]; a rota `api/[controller]` vem da classe abstrata e `[controller]` vira
o nome da classe derivada.
"""
import re
from pathlib import Path

from .base import juntar, normalizar

NOME = "aspnet"
IGNORAR = {"bin", "obj", "node_modules", ".git"}

ATRIBUTO = r'\[(?:[^\]"\n]|"[^"\n]*")*\]'
CLASSE = re.compile(
    r"(?P<attrs>(?:" + ATRIBUTO + r"\s*)*)"
    r"public\s+(?P<mods>(?:sealed\s+|abstract\s+|partial\s+)*)class\s+"
    r"(?P<nome>\w+)(?:\s*<[^>{]*>)?\s*(?::\s*(?P<base>\w+))?")
ROTA_ATTR = re.compile(r'\[Route\("([^"]+)"\)\]')
METODO = re.compile(r'\[Http(?:Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]')


def _arquivos(raiz: Path):
    for arq in raiz.rglob("*.cs"):
        if any(parte in IGNORAR for parte in arq.parts):
            continue
        yield arq


def detectar(raiz: Path) -> bool:
    return any(_arquivos(raiz)) and any(True for _ in raiz.rglob("*.csproj"))


def _nome_controller(nome: str) -> str:
    return nome[:-10] if nome.endswith("Controller") else nome


def rotas(raiz: Path) -> set[str]:
    classes: dict[str, dict] = {}
    metodos: dict[str, list[str]] = {}
    minimal: set[str] = set()

    for arq in _arquivos(raiz):
        texto = arq.read_text(encoding="utf-8", errors="ignore")
        for m in CLASSE.finditer(texto):
            nome = m.group("nome")
            attrs = m.group("attrs")
            achado = ROTA_ATTR.search(attrs)
            classes[nome] = {
                "rota": achado.group(1) if achado else None,
                "base": m.group("base"),
                "abstrata": "abstract" in (m.group("mods") or ""),
            }
            corpo = texto[m.end():]
            # corta só na próxima classe de nível superior: classe aninhada
            # (indentada) não encerra o controller
            proxima = re.search(
                r"\n(?:public|internal)\s+(?:sealed\s+|abstract\s+|partial\s+)*class\s+\w+",
                corpo)
            if proxima:
                corpo = corpo[:proxima.start()]
            metodos[nome] = [g or "" for g in METODO.findall(corpo)]
        for rota in re.findall(
                r'\.Map(?:Get|Post|Put|Patch|Delete|Methods)\(\s*"([^"]+)"', texto):
            minimal.add(normalizar(rota))

    def rota_efetiva(nome: str, visitados: tuple = ()) -> str | None:
        info = classes.get(nome) or {}
        if info.get("rota"):
            return info["rota"]
        base = info.get("base")
        if base and base not in visitados and base in classes:
            return rota_efetiva(base, visitados + (nome,))
        return None

    saida = set(minimal)
    for nome, info in classes.items():
        if info["abstrata"]:
            continue
        rota = rota_efetiva(nome)
        if not rota:
            continue
        rota = rota.replace("[controller]", _nome_controller(nome))
        saida.add(normalizar(rota))
        for sub in metodos.get(nome, []):
            saida.add(juntar(rota, sub))
    return saida
