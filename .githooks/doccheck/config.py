"""Configuracao opcional por repo: `.doccheck.json` na raiz."""
import fnmatch
import json
from pathlib import Path

PADRAO = {
    "ignore": [
        "docs/plans/**", "docs/specs/**", "docs/adr/**",
        "docs/superpowers/plans/**", "docs/superpowers/specs/**",
        "**/docs/superpowers/plans/**", "**/docs/superpowers/specs/**",
        ".impeccable/**", "**/.impeccable/**",
        "skills/**", "**/skills/**", ".agents/**", "**/.agents/**",
        "sdd/**", "**/sdd/**",
        "**/node_modules/**", "**/bin/**", "**/obj/**", "**/.git/**",
    ],
    "regras": {"readme": True, "secoes": True, "versao": True,
               "stack": True, "segredos": True, "endpoints": True},
    "status": None,
    "changelog": None,
    "stack": None,
    "secao_api": "api",
}

ARQUIVO = ".doccheck.json"


def carregar(raiz: Path) -> dict:
    cfg = json.loads(json.dumps(PADRAO))
    arq = raiz / ARQUIVO
    if arq.is_file():
        dados = json.loads(arq.read_text(encoding="utf-8"))
        for chave, valor in dados.items():
            if chave == "regras":
                cfg["regras"].update(valor or {})
            elif chave == "ignore":
                # aditivo: o repo acrescenta exclusões, não perde os padrões
                cfg["ignore"] = list(dict.fromkeys(cfg["ignore"] + (valor or [])))
            else:
                cfg[chave] = valor
    return cfg


def salvar_inicial(raiz: Path, stack: str | None = None) -> Path:
    dados = {"stack": stack, "regras": PADRAO["regras"], "ignore": PADRAO["ignore"]}
    arq = raiz / ARQUIVO
    arq.write_text(json.dumps(dados, indent=2, ensure_ascii=False) + "\n",
                   encoding="utf-8", newline="\n")
    return arq


def ignorado(rel: str, cfg: dict) -> bool:
    return any(fnmatch.fnmatch(rel, padrao) for padrao in cfg["ignore"])
