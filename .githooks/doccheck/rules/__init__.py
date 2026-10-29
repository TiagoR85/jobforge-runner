"""Roda as regras habilitadas e devolve o relatorio."""
from pathlib import Path

from ..model import Relatorio
from . import endpoints, estrutura, segredos

REGRAS = (
    ("readme", estrutura.readme),
    ("secoes", estrutura.secoes_vazias),
    ("versao", estrutura.versao),
    ("segredos", segredos.verificar),
    ("endpoints", endpoints.verificar),
)


def rodar(raiz: Path, cfg: dict) -> Relatorio:
    rel = Relatorio(str(raiz))
    for identificador, funcao in REGRAS:
        if not cfg["regras"].get(identificador, True):
            rel.pular(identificador, "desligada na configuração")
            continue
        try:
            funcao(raiz, cfg, rel)
        except Exception as exc:      # regra quebrada nao derruba o hook
            rel.adicionar(identificador,
                          f"erro na regra: {type(exc).__name__}: {exc}")
    return rel
