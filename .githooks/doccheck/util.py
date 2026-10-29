"""Utilitarios: listar docs do repo, ler texto, achar secoes."""
import re
from pathlib import Path

from .config import ignorado

EXTENSOES = {".md", ".markdown"}


def docs(raiz: Path, cfg: dict) -> list[Path]:
    saida = []
    for arq in raiz.rglob("*"):
        if not arq.is_file() or arq.suffix.lower() not in EXTENSOES:
            continue
        rel = arq.relative_to(raiz).as_posix()
        if ignorado(rel, cfg):
            continue
        if any(parte in {".git", "node_modules", "bin", "obj", ".venv", "venv"}
               for parte in arq.parts):
            continue
        saida.append(arq)
    return sorted(saida)


def ler(arq: Path) -> str:
    return arq.read_text(encoding="utf-8", errors="ignore")


def nome(arq: Path, raiz: Path | None = None) -> str:
    return arq.relative_to(raiz).as_posix() if raiz else arq.name


def secoes(texto: str):
    """(titulo, linhas do corpo) de cada heading.

    O corpo vai até o próximo heading de nivel IGUAL OU MAIOR: uma seção com
    subseções (`## versão` + `### Adicionado`) não é vazia.
    """
    linhas = texto.split("\n")
    titulos = [(i, len(re.match(r"^(#{2,4})", l).group(1)))
               for i, l in enumerate(linhas) if re.match(r"^#{2,4}\s", l)]
    for pos, (i, nivel) in enumerate(titulos):
        fim = len(linhas)
        for j, nivel_seguinte in titulos[pos + 1:]:
            if nivel_seguinte <= nivel:
                fim = j
                break
        titulo = re.sub(r"^#{2,4}\s", "", linhas[i]).strip()
        yield titulo, linhas[i + 1:fim]


def secao_api(texto: str, alvo: str) -> str:
    """Concatena o corpo das secoes cujo titulo contem `alvo` (ex.: api)."""
    partes = []
    for titulo, corpo in secoes(texto):
        if alvo.lower() in titulo.lower():
            partes.append("\n".join(corpo))
    return "\n".join(partes)
