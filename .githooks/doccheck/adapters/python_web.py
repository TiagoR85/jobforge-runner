"""Flask (Blueprint url_prefix + @route) e FastAPI (APIRouter prefix + @get)."""
import re
from pathlib import Path

from .base import juntar

NOME = "python_web"
IGNORAR = {".venv", "venv", "node_modules", "site-packages", ".git", "build", "dist"}


def _arquivos(raiz: Path):
    for arq in raiz.rglob("*.py"):
        if any(parte in IGNORAR for parte in arq.parts):
            continue
        yield arq


def detectar(raiz: Path) -> bool:
    return any(_arquivos(raiz))


def rotas(raiz: Path) -> set[str]:
    saida: set[str] = set()
    for arq in _arquivos(raiz):
        texto = arq.read_text(encoding="utf-8", errors="ignore")
        achado = re.search(
            r'(?:Blueprint|APIRouter)\([^)]*?prefix\s*=\s*["\']([^"\']+)', texto)
        prefixo = achado.group(1) if achado else ""
        for dec in re.findall(
                r'@\w+\.(?:route|get|post|put|patch|delete)\(\s*["\']([^"\']*)["\']',
                texto):
            saida.add(juntar(prefixo, dec))
    return saida
