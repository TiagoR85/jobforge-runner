"""Zero-leak: valor de .env e padrao de token nao podem aparecer nos .md."""
import re
from pathlib import Path

from ..util import docs, ler, nome

PADROES = (
    r"ghp_[A-Za-z0-9]{20,}",
    r"github_pat_[A-Za-z0-9_]{20,}",
    r"gho_[A-Za-z0-9]{20,}",
    r"(?<![A-Za-z0-9_-])sk-[A-Za-z0-9]{20,}",
    r"AKIA[0-9A-Z]{16}",
    r"AIza[0-9A-Za-z_-]{20,}",
    r"xox[baprs]-[A-Za-z0-9-]{10,}",
    r"-----BEGIN [A-Z ]*PRIVATE KEY-----",
)


def valores_env(raiz: Path) -> list[tuple[str, str]]:
    env = raiz / ".env"
    if not env.is_file():
        return []
    saida = []
    for linha in ler(env).splitlines():
        linha = linha.strip()
        if not linha or linha.startswith("#") or "=" not in linha:
            continue
        chave, valor = linha.split("=", 1)
        valor = valor.strip().strip('"').strip("'")
        if re.search(r"KEY|TOKEN|SECRET|PASS|COOKIE", chave, re.I) and len(valor) >= 8:
            saida.append((chave.strip(), valor))
    return saida


def verificar(raiz: Path, cfg: dict, rel) -> None:
    valores = valores_env(raiz)
    for arq in docs(raiz, cfg):
        texto = ler(arq)
        caminho = nome(arq, raiz)
        for chave, valor in valores:
            if valor in texto:
                rel.adicionar("segredos", f"valor de {chave} no texto", caminho)
        for padrao in PADROES:
            if re.search(padrao, texto):
                rel.adicionar("segredos", f"parece conter token ({padrao[:16]}...)",
                              caminho)
