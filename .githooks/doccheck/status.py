"""Painel de deriva: quais repos têm o vendor do doccheck e em que versão.

Lê `.githooks/doccheck/__init__.py` de cada repositório — sem confiar em memória,
sem consultar o GitHub. Serve para responder "quem está atrasado?" antes de
culpar o hook por não pegar algo.
"""
from pathlib import Path

from . import __version__

REL_VENDOR = Path(".githooks") / "doccheck" / "__init__.py"


def versao_do_repo(repo: Path) -> str | None:
    init = repo / REL_VENDOR
    if not init.is_file():
        return None
    for linha in init.read_text(encoding="utf-8", errors="ignore").splitlines():
        if linha.startswith("__version__"):
            return linha.split("=", 1)[1].strip().strip('"').strip("'")
    return "?"


def coletar(raiz: Path) -> list[dict]:
    linhas = []
    for repo in sorted(p for p in raiz.iterdir() if (p / ".git").exists()):
        versao = versao_do_repo(repo)
        if versao is None:
            estado = "ausente"
        elif versao == __version__:
            estado = "em dia"
        else:
            estado = "atrasado"
        linhas.append({"repo": repo.name, "versao": versao, "estado": estado})
    return linhas


def resumo(linhas: list[dict]) -> dict:
    contagem = {"em dia": 0, "atrasado": 0, "ausente": 0}
    for linha in linhas:
        contagem[linha["estado"]] += 1
    return {"versao_instalada": __version__, **contagem, "total": len(linhas)}
