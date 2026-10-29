"""Fato: endpoint citado na secao de API existe no codigo (adaptador por stack).

Regra que nao da para avaliar e *pulada*, nunca falha. Endpoint de fora
(OpenCode, GitHub, ...) e ignorado: so cobramos rotas do proprio repo, isto e,
as que comecam por um prefixo que o codigo define.
"""
import re
from pathlib import Path

from ..adapters import detectar, rotas
from ..adapters.base import normalizar, primeiro_segmento
from ..util import docs, ler, nome, secao_api


def verificar(raiz: Path, cfg: dict, rel) -> None:
    if not detectar(raiz, cfg.get("stack")):
        rel.pular("endpoints", "stack não reconhecida")
        return
    conhecidas = rotas(raiz, cfg.get("stack"))
    if not conhecidas:
        rel.pular("endpoints", "nenhuma rota encontrada no código")
        return

    prefixos = {primeiro_segmento(r) for r in conhecidas}
    conhecidas_min = {r.lower() for r in conhecidas}
    prefixos_min = {p.lower() for p in prefixos}
    alvo = cfg.get("secao_api") or "api"
    citados = []
    for arq in docs(raiz, cfg):
        trecho = secao_api(ler(arq), alvo)
        if not trecho:
            continue
        for span in re.findall(r"`(/[^`\s]{2,90})`", trecho):
            bruto = span.split("?")[0].split("#")[0]
            if "*" in bruto:            # `/api/*` é padrão, não rota
                continue
            rota = normalizar(bruto.rstrip(".,;:)"))
            if primeiro_segmento(rota).lower() not in prefixos_min:
                continue
            citados.append((arq, span, rota))

    if not citados:
        rel.pular("endpoints", "nenhuma seção de API citando endpoints")
        return

    for arq, bruto, rota in citados:
        # ASP.NET/Express casam sem diferenciar maiúsculas
        if rota.lower() not in conhecidas_min:
            rel.adicionar(
                "endpoints", f"endpoint citado não existe no código ({bruto})",
                nome(arq, raiz))
