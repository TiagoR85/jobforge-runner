"""Fato: a versao de stack citada na doc bate com a do codigo.

Hoje cobre .NET (`TargetFramework` do .csproj x ".NET N" no texto) — a
afirmacao verificavel mais comum em README de projeto .NET.
"""
import re
from pathlib import Path

from ..util import docs, ler, nome

RE_DOTNET = re.compile(r"(?<![A-Za-z])\.NET\s*(\d+(?:\.\d+)?)", re.I)


def _csproj(raiz: Path) -> list[Path]:
    return [p for p in raiz.rglob("*.csproj")
            if not any(parte in {"bin", "obj", "node_modules", ".git"}
                       for parte in p.parts)]


def _framework(arquivos: list[Path]) -> str | None:
    for arq in arquivos:
        texto = arq.read_text(encoding="utf-8", errors="ignore")
        achado = re.search(r"<TargetFrameworks?>([^<]+)</TargetFrameworks?>", texto)
        if not achado:
            continue
        for bruto in re.split(r"[;,]", achado.group(1)):
            m = re.match(r"\s*(?:netcoreapp|net)(\d+\.\d+)", bruto.strip(), re.I)
            if m:
                return m.group(1)
    return None


def verificar(raiz: Path, cfg: dict, rel) -> None:
    arquivos = _csproj(raiz)
    if not arquivos:
        rel.pular("stack", "sem .csproj")
        return
    framework = _framework(arquivos)
    if not framework:
        rel.pular("stack", "csproj sem TargetFramework reconhecível")
        return

    major_codigo = framework.split(".")[0]
    for arq in docs(raiz, cfg):
        if arq.name.lower() not in {"readme.md", "manual.md"}:
            continue
        for citado in RE_DOTNET.findall(ler(arq)):
            if citado.split(".")[0] != major_codigo:
                rel.adicionar(
                    "stack",
                    f"doc diz .NET {citado} mas o csproj está em net{framework}",
                    nome(arq, raiz))
