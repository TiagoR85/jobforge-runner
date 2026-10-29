"""Express/Node: app.get/post, router.* e prefixos de app.use."""
import re
from pathlib import Path

from .base import juntar, normalizar

NOME = "node_express"
ARQUIVOS = {".js", ".ts", ".mjs", ".cjs"}


def _arquivos(raiz: Path):
    for arq in raiz.rglob("*"):
        if arq.suffix in ARQUIVOS and "node_modules" not in arq.parts and arq.is_file():
            yield arq


def detectar(raiz: Path) -> bool:
    pkg = raiz / "package.json"
    if not pkg.is_file():
        return False
    texto = pkg.read_text(encoding="utf-8", errors="ignore")
    return "express" in texto or "fastify" in texto or "koa" in texto


def rotas(raiz: Path) -> set[str]:
    prefixos: set[str] = set()
    brutas: set[str] = set()
    for arq in _arquivos(raiz):
        texto = arq.read_text(encoding="utf-8", errors="ignore")
        prefixos |= set(re.findall(
            r'\.use\(\s*["\']([^"\']+)["\']', texto))
        brutas |= set(re.findall(
            r'\.(?:get|post|put|patch|delete)\(\s*["\']([^"\']+)["\']', texto))
    saida = {normalizar(r) for r in brutas}
    for prefixo in prefixos:
        for rota in brutas:
            saida.add(juntar(prefixo, rota))
    return saida
