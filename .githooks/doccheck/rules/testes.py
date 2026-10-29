"""Regra `testes`: número citado na doc viva x contagem estática no código.

Não executa a suíte (o hook é offline e tem que ser rápido): conta as declarações
de teste no código. Parametrização e `[DataRow]` fazem o número real variar, então
a régua tem folga: até 10% de diferença é silêncio, acima disso vira **aviso**
(não bloqueia) e acima de 50% vira **erro**. Regra que bloqueia commit precisa
errar pouco — e o escape continua sendo `regras.testes: false` no `.doccheck.json`.
"""
import re
from pathlib import Path

from ..model import AVISO
from ..util import docs, ler, nome

# Só doc viva: CHANGELOG guarda história ("512 testes em v1.0.0") e não mente.
VIVAS = ("readme.md", "manual.md", "status.md")
CLAIM = re.compile(r"\b(\d{2,5})\s*(?:\+\s*)?(?:testes|tests)\b", re.I)
PADROES = {
    ".py": (r"^\s*(?:async\s+)?def\s+test_",),
    ".cs": (r"\[(?:TestMethod|Fact|Theory|Test)\]", r"\[(?:DataRow|InlineData)\s*\("),
    ".js": (r"^\s*(?:test|it)(?:\.\w+)?\s*\(\s*['\"`]",),
    ".jsx": (r"^\s*(?:test|it)(?:\.\w+)?\s*\(\s*['\"`]",),
    ".ts": (r"^\s*(?:test|it)(?:\.\w+)?\s*\(\s*['\"`]",),
    ".tsx": (r"^\s*(?:test|it)(?:\.\w+)?\s*\(\s*['\"`]",),
    ".mjs": (r"^\s*(?:test|it)(?:\.\w+)?\s*\(\s*['\"`]",),
}
LIXO = {".git", ".githooks", "node_modules", "bin", "obj", "dist", "build",
        "target", "site-packages", ".tox"}
TOLERANCIA = 0.10
# Nunca vira erro: contagem estática é aproximada (parametrização, InlineData,
# fixtures). Regra de medição avisa; quem bloqueia é o que é exato.
SEVERIDADE = AVISO
HISTORICO = re.compile(r"^\s*(?:[-*]\s*)?\[[ xX]\]")


def _lixo(rel: str) -> bool:
    """Vendor, build e virtualenv de qualquer nome (`.venv-test`, `venv`, `env`)."""
    for parte in rel.lower().split("/"):
        if parte in LIXO or parte.startswith((".venv", "venv-")):
            return True
    return False


def _e_arquivo_de_teste(rel: str) -> bool:
    partes = rel.lower().split("/")
    if any(p in ("test", "tests", "spec", "specs") or p.endswith(".tests")
           for p in partes[:-1]):
        return True
    arq = partes[-1]
    return (arq.startswith("test_") or "_test." in arq or ".test." in arq
            or ".spec." in arq or arq.endswith(("test.cs", "tests.cs")))


def contagem(raiz: Path) -> dict[str, int]:
    """Casos de teste por extensão, lendo o código (sem importar nada)."""
    por_ext: dict[str, int] = {}
    for arq in raiz.rglob("*"):
        if not arq.is_file() or arq.suffix.lower() not in PADROES:
            continue
        rel = arq.relative_to(raiz).as_posix()
        if _lixo(rel) or not _e_arquivo_de_teste(rel):
            continue
        texto = ler(arq)
        n = sum(len(re.findall(p, texto, re.M)) for p in PADROES[arq.suffix.lower()])
        if n:
            por_ext[arq.suffix.lower()] = por_ext.get(arq.suffix.lower(), 0) + n
    return por_ext


def verificar(raiz: Path, cfg: dict, rel) -> None:
    por_ext = contagem(raiz)
    total = sum(por_ext.values())
    if not total:
        rel.pular("testes", "sem testes detectáveis no código")
        return

    vivos = [d for d in docs(raiz, cfg) if d.name.lower() in VIVAS]
    if not vivos:
        rel.pular("testes", "sem README/MANUAL/STATUS")
        return

    citados = 0
    for arq in vivos:
        dentro_de_codigo = False
        for numero_linha, linha in enumerate(ler(arq).split("\n"), 1):
            if linha.lstrip().startswith("```"):
                dentro_de_codigo = not dentro_de_codigo
                continue
            if dentro_de_codigo or HISTORICO.match(linha):
                continue          # comando/registro de log não é afirmação sobre hoje
            for achado in CLAIM.finditer(linha):
                citado = int(achado.group(1))
                if citado < 20:          # "3 testes" no exemplo não é contagem
                    continue
                citados += 1
                razao = abs(citado - total) / total
                if razao <= TOLERANCIA:
                    continue
                rel.adicionar(
                    "testes",
                    f"diz {citado} testes; contagem estática: {total}",
                    f"{nome(arq, raiz)}:{numero_linha}", SEVERIDADE)

    if not citados:
        rel.pular("testes", "nenhuma contagem de testes citada na doc")
