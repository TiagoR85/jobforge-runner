"""Adaptadores por stack: cada um sabe detectar o repo e listar suas rotas."""
from pathlib import Path

from . import aspnet, astro, node_express, python_web

ADAPTADORES = [aspnet, python_web, node_express, astro]


def detectar(raiz: Path, forcado: str | None = None) -> list:
    if forcado:
        return [a for a in ADAPTADORES if a.NOME == forcado]
    return [a for a in ADAPTADORES if a.detectar(raiz)]


def rotas(raiz: Path, forcado: str | None = None) -> set[str]:
    saida: set[str] = set()
    for adaptador in detectar(raiz, forcado):
        saida |= adaptador.rotas(raiz)
    return saida
