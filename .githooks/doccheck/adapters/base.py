"""Normalizacao de rotas: `/api/x/<int:id>` == `/api/x/:id` == `/api/x/{id}`."""
import re


def normalizar(rota: str) -> str:
    if not rota:
        return "/"
    rota = rota.strip().strip("\"'`")
    if not rota.startswith("/"):
        rota = "/" + rota
    rota = re.sub(r"\[[^\]]*\]", "<var>", rota)        # ASP.NET [action]
    rota = re.sub(r"<(?:\w+:)?\w+>", "<var>", rota)    # Flask <int:id> (antes do :id!)
    rota = re.sub(r"\{[^}]*\}", "<var>", rota)         # {id}
    rota = re.sub(r":[A-Za-z_]\w*", "<var>", rota)     # Express :id
    rota = re.sub(r"/+", "/", rota)
    return rota.rstrip("/") or "/"


def juntar(prefixo: str, rota: str) -> str:
    return normalizar(f"{prefixo or ''}/{rota or ''}")


def primeiro_segmento(rota: str) -> str:
    pedacos = [p for p in rota.split("/") if p]
    return "/" + pedacos[0] if pedacos else "/"
