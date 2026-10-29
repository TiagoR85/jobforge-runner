"""Regras estruturais: README, secoes vazias e versao STATUS x CHANGELOG."""
import re
from pathlib import Path

from ..util import docs, ler, nome, secoes

CHANGELOGS = ("CHANGELOG.md", "changelog.md")
STATUSSES = ("STATUS.md", "status.md")


def _primeiro(raiz: Path, nomes, configurado=None):
    if configurado:
        arq = raiz / configurado
        return arq if arq.is_file() else None
    return next((raiz / n for n in nomes if (raiz / n).is_file()), None)


def readme(raiz: Path, cfg: dict, rel) -> None:
    arq = raiz / "README.md"
    if not arq.is_file():
        rel.adicionar("readme", "sem README.md")
    elif len(ler(arq).strip()) < 50:
        rel.adicionar("readme", "README.md vazio ou quase vazio", "README.md")


def secoes_vazias(raiz: Path, cfg: dict, rel) -> None:
    for arq in docs(raiz, cfg):
        for titulo, corpo in secoes(ler(arq)):
            if re.match(r"^\[?unreleased\]?$", titulo, re.I):
                continue          # convenção Keep a Changelog: vazia até lançar
            # seção com subseção e texto embaixo NÃO é vazia; heading sozinho é
            conteudo = [l for l in corpo
                        if l.strip() and not l.strip().startswith("#")]
            if not conteudo:
                rel.adicionar("secoes", f"seção vazia '{titulo}'", nome(arq, raiz))


def versao(raiz: Path, cfg: dict, rel) -> None:
    changelog = _primeiro(raiz, CHANGELOGS, cfg.get("changelog"))
    status = _primeiro(raiz, STATUSSES, cfg.get("status"))
    if not (changelog and status):
        rel.pular("versao", "sem CHANGELOG+STATUS")
        return

    texto_ch = ler(changelog)
    texto_st = ler(status)[:1000]
    versoes = re.findall(r"^##\s*\[?v?(\d+\.\d+\.\d+)\]?", texto_ch, re.M)
    datas = re.findall(r"^##\s*\[?(\d{4}-\d{2}-\d{2})\]?", texto_ch, re.M)

    if versoes:
        no_status = re.search(r"v(\d+\.\d+\.\d+)", texto_st)
        if not no_status:
            rel.adicionar("versao", "título sem versão (vX.Y.Z)", nome(status, raiz))
        elif no_status.group(1) != versoes[0]:
            rel.adicionar(
                "versao",
                f"STATUS v{no_status.group(1)} != {nome(changelog, raiz)} "
                f"v{versoes[0]}", nome(status, raiz))
    elif datas:
        data_st = re.search(r"\d{4}-\d{2}-\d{2}", texto_st)
        if data_st and data_st.group(0) != datas[0]:
            rel.adicionar(
                "versao",
                f"STATUS {data_st.group(0)} != {nome(changelog, raiz)} {datas[0]}",
                nome(status, raiz))
    else:
        rel.pular("versao", "CHANGELOG sem versão e sem data no topo")
