"""Astro: rotas de arquivo (src/pages/**), incluindo [slug] dinamico."""
import re
from pathlib import Path

from .base import normalizar

NOME = "astro"
SUFIXOS = {".astro", ".md", ".mdx", ".ts", ".js"}


def detectar(raiz: Path) -> bool:
    return (raiz / "src" / "pages").is_dir()


def rotas(raiz: Path) -> set[str]:
    base = raiz / "src" / "pages"
    saida: set[str] = set()
    for arq in base.rglob("*"):
        if not arq.is_file() or arq.suffix.lower() not in SUFIXOS:
            continue
        rel = arq.relative_to(base).with_suffix("").as_posix()
        if rel == "index":
            rel = ""
        elif rel.endswith("/index"):
            rel = rel[:-len("/index")]
        rota = "/" + rel.strip("/")
        rota = re.sub(r"\[([^\]]+)\]", r"<\1>", rota)
        saida.add(normalizar(rota))
    return saida
