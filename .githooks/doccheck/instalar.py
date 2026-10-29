"""Instalacao no repo: vendoriza o pacote em `.githooks/` e aponta o hook."""
import shutil
import subprocess
from pathlib import Path

PACOTE = Path(__file__).resolve().parent

BOOTSTRAP = '''"""Roda o doccheck vendorizado em .githooks (chamado pelo pre-commit)."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from doccheck.cli import main

sys.exit(main(["check"]))
'''

HOOK = """#!/bin/sh
# doccheck: documentacao x codigo. Instalacao: core.hooksPath=.githooks
# Escape consciente: git commit --no-verify
PY=python
command -v python >/dev/null 2>&1 || PY=python3
"$PY" .githooks/check_docs.py || exit 1
if [ -f .githooks/pre-commit.local ]; then
    exec sh .githooks/pre-commit.local
fi
"""

MARCA = "# doccheck:"

LINHA_ATTRS = ".githooks/* text eol=lf"


def instalar(raiz: Path, silencioso: bool = False) -> int:
    githooks = raiz / ".githooks"
    destino = githooks / "doccheck"
    if destino.exists():
        shutil.rmtree(destino)
    shutil.copytree(PACOTE, destino,
                    ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))

    (githooks / "check_docs.py").write_text(BOOTSTRAP, encoding="utf-8", newline="\n")

    # hook que já existia (e não é nosso) é preservado e executado depois:
    # instalar não pode apagar checagem que o repo já tinha.
    hook = githooks / "pre-commit"
    if hook.is_file() and MARCA not in hook.read_text(encoding="utf-8", errors="ignore"):
        shutil.copyfile(hook, githooks / "pre-commit.local")
    hook.write_text(HOOK, encoding="utf-8", newline="\n")

    attrs = raiz / ".gitattributes"
    texto = attrs.read_text(encoding="utf-8") if attrs.is_file() else ""
    if LINHA_ATTRS not in texto:
        attrs.write_text(
            texto + ("" if not texto or texto.endswith("\n") else "\n") + LINHA_ATTRS + "\n",
            encoding="utf-8", newline="\n")

    subprocess.run(["git", "config", "core.hooksPath", ".githooks"],
                   cwd=raiz, capture_output=True, text=True)
    if not silencioso:
        print(f"instalado em {raiz.name}: .githooks/doccheck + pre-commit")
    return 0
