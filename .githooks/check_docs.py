"""Checagem de documentação para o hook de pre-commit (só stdlib).

Regras, adaptadas ao que o repositório tem:
1. README.md existe e não está vazio.
2. Se existem CHANGELOG e STATUS: a versão do STATUS tem que ser a primeira
   versão versionada do CHANGELOG (ignora `[Unreleased]`).
3. Seções `###` do MANUAL.md não podem ficar vazias.
4. Zero-leak: nenhum valor de `.env` (chave com KEY/TOKEN/SECRET/PASSWORD/COOKIE)
   nem padrão de token aparece nos .md do repo.

Uso: python check_docs.py [raiz]   (sai 1 se algo estiver inconsistente)
"""
import re
import sys
from pathlib import Path

RAIZ = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else \
    Path(__file__).resolve().parents[1]

CHANGELOGS = ("CHANGELOG.md", "changelog.md")
STATUSSES = ("STATUS.md", "status.md")
MANUAIS = ("MANUAL.md", "manual.md")

TOKEN_PATTERNS = (
    r"ghp_[A-Za-z0-9]{20,}",
    r"github_pat_[A-Za-z0-9_]{20,}",
    r"gho_[A-Za-z0-9]{20,}",
    r"(?<![A-Za-z0-9_-])sk-[A-Za-z0-9]{20,}",
    r"AKIA[0-9A-Z]{16}",
    r"AIza[0-9A-Za-z_-]{20,}",
    r"xox[baprs]-[A-Za-z0-9-]{10,}",
    r"-----BEGIN [A-Z ]*PRIVATE KEY-----",
)


def _primeiro(nomes):
    return next((RAIZ / n for n in nomes if (RAIZ / n).is_file()), None)


def _ler(caminho):
    return caminho.read_text(encoding="utf-8", errors="ignore")


def _md(raiz):
    arquivos = [raiz / n for n in
                ("README.md", "MANUAL.md", "manual.md", "AGENTS.md",
                 "CHANGELOG.md", "changelog.md", "STATUS.md", "status.md")]
    arquivos += sorted((raiz / "docs").rglob("*.md")) if (raiz / "docs").is_dir() else []
    return [a for a in dict.fromkeys(arquivos) if a.is_file()]


def verificar(raiz=RAIZ):
    falhas = []

    readme = raiz / "README.md"
    if not readme.is_file():
        falhas.append("sem README.md")
    elif len(_ler(readme).strip()) < 50:
        falhas.append("README.md vazio ou quase vazio")

    changelog, status = _primeiro(CHANGELOGS), _primeiro(STATUSSES)
    if changelog and status:
        texto_ch = _ler(changelog)
        texto_st = _ler(status)[:800]
        versoes = re.findall(r"^##\s*\[?v?(\d+\.\d+\.\d+)\]?", texto_ch, re.M)
        datas = re.findall(r"^##\s*\[?(\d{4}-\d{2}-\d{2})\]?", texto_ch, re.M)
        if versoes:
            no_status = re.search(r"v(\d+\.\d+\.\d+)", texto_st)
            if not no_status:
                falhas.append(f"{status.name}: título sem versão (vX.Y.Z)")
            elif no_status.group(1) != versoes[0]:
                falhas.append(f"STATUS v{no_status.group(1)} != "
                              f"{changelog.name} v{versoes[0]}")
        elif datas:
            # changelog por data (sem versão): compara a data mais recente
            data_st = re.search(r"\d{4}-\d{2}-\d{2}", texto_st)
            if data_st and data_st.group(0) != datas[0]:
                falhas.append(f"STATUS {data_st.group(0)} != "
                              f"{changelog.name} {datas[0]}")
        # sem versão e sem data: o repo não versiona doc — não opina

    manual = _primeiro(MANUAIS)
    if manual:
        linhas = _ler(manual).split("\n")
        for i, linha in enumerate(linhas):
            if not linha.startswith("### "):
                continue
            corpo = [x.strip() for x in linhas[i + 1:i + 6] if x.strip()]
            if not corpo or corpo[0].startswith("#"):
                falhas.append(f"{manual.name}: seção vazia "
                              f"'{linha[4:].strip()}'")

    valores = []
    env = raiz / ".env"
    if env.is_file():
        for linha in _ler(env).splitlines():
            linha = linha.strip()
            if not linha or linha.startswith("#") or "=" not in linha:
                continue
            chave, valor = linha.split("=", 1)
            valor = valor.strip().strip('"').strip("'")
            if re.search(r"KEY|TOKEN|SECRET|PASSWORD|COOKIE", chave, re.I) \
                    and len(valor) >= 8:
                valores.append((chave.strip(), valor))
    for caminho in _md(raiz):
        texto = _ler(caminho)
        nome = caminho.relative_to(raiz).as_posix()
        for chave, valor in valores:
            if valor in texto:
                falhas.append(f"{nome}: valor de {chave} no texto")
        for padrao in TOKEN_PATTERNS:
            if re.search(padrao, texto):
                falhas.append(f"{nome}: parece conter token ({padrao[:18]}...)")

    return falhas


def main():
    falhas = verificar()
    if falhas:
        print(f"Documentação inconsistente em {RAIZ.name}:")
        for f in falhas:
            print(f"  - {f}")
        return 1
    print(f"documentação ok ({RAIZ.name})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
