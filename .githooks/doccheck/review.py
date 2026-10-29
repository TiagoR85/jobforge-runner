"""Revisão semântica: doc x código — opcionalmente com IA, NUNCA no hook.

`doccheck review` monta um contexto determinístico (diff desde uma ref, os .md
afetados e os achados do check). Com `--ia`, envia esse contexto a um endpoint
OpenAI-compatível e devolve **perguntas com arquivo:linha** — nunca um patch e
nunca um bloqueio.

Config por ambiente (nada no repo):
  DOCCHECK_IA_URL        ex.: https://api.openai.com/v1  (ou /chat/completions)
  DOCCHECK_IA_KEY        chave do provedor
  DOCCHECK_IA_MODEL      default: gpt-4o-mini
  DOCCHECK_IA_PRECO_IN   US$ por 1M tokens de entrada (default 0 = não estima)
  DOCCHECK_IA_PRECO_OUT  US$ por 1M tokens de saída
"""
import json
import os
import subprocess
import urllib.request
from pathlib import Path

MAX_DIFF = 12000
MAX_DOC = 8000
MAX_DOCS = 6
LIMITE_MAX_USD = 0.50
VIVAS = ("README.md", "MANUAL.md", "manual.md", "STATUS.md", "status.md")

INSTRUCAO = """Você revisa documentação técnica. Recebe um diff de código e o
conteúdo atual das principais documentações do repositório (README, MANUAL, STATUS).

Liste APENAS divergências entre o que a documentação afirma e o que o código faz —
inclusive quando o código mudou e a doc não foi atualizada. Formato de cada item,
uma linha:
- arquivo:linha — "a doc diz X" | "o código diz Y" — pergunta objetiva (atualiza a doc ou o código?)

Regras: não invente fato que não esteja no material; se faltar evidência, omita;
máximo 8 itens; sem preâmbulo. Se não houver divergência, responda exatamente:
NADA ENCONTRADO"""


def _git(raiz: Path, *args, limite: int | None = None) -> str:
    resultado = subprocess.run(["git", *args], cwd=raiz, capture_output=True,
                               text=True, encoding="utf-8", errors="replace")
    saida = resultado.stdout or ""
    return saida[:limite] if limite else saida


def docs_afetados(raiz: Path, ref: str) -> list[Path]:
    nomes = _git(raiz, "diff", "--name-only", f"{ref}..HEAD").splitlines()
    return [Path(n) for n in nomes if n.lower().endswith(".md") and (raiz / n).is_file()]


def montar_contexto(raiz: Path, ref: str = "HEAD~1") -> dict:
    from .config import carregar
    from .rules import rodar

    # Docs alteradas no diff + docs vivas: o caso real é código mudar e a doc
    # ficar velha (o diff não traz .md nenhum nesse cenário).
    escolhidos: list[Path] = []
    for arq in docs_afetados(raiz, ref) + [Path(n) for n in VIVAS]:
        if arq not in escolhidos and (raiz / arq).is_file():
            escolhidos.append(arq)

    trechos = []
    for rel in escolhidos[:MAX_DOCS]:
        texto = (raiz / rel).read_text(encoding="utf-8", errors="ignore")
        trechos.append((rel.as_posix(), texto[:MAX_DOC]))

    rel_ = rodar(raiz, carregar(raiz))
    return {
        "ref": ref,
        "diff": _git(raiz, "diff", f"{ref}..HEAD", limite=MAX_DIFF),
        "docs": trechos,
        "achados": [str(a) for a in rel_.achados],
    }


def config_ia(env=None) -> dict | None:
    env = env or os.environ
    base = (env.get("DOCCHECK_IA_URL") or "").strip().rstrip("/")
    if not base:
        return None
    url = base if base.endswith("/chat/completions") else base + "/chat/completions"
    return {"url": url,
            "chave": env.get("DOCCHECK_IA_KEY") or "",
            "modelo": env.get("DOCCHECK_IA_MODEL") or "gpt-4o-mini"}


def montar_prompt(contexto: dict) -> str:
    partes = [INSTRUCAO,
              "\n\n## Achados do check (determinístico)\n",
              "\n".join(contexto["achados"]) or "(nenhum)",
              "\n\n## Diff\n```diff\n", contexto["diff"], "\n```"]
    for nome, texto in contexto["docs"]:
        partes += [f"\n\n## Doc: {nome}\n```markdown\n", texto, "\n```"]
    return "".join(partes)


def estimar_tokens(texto: str) -> int:
    return max(1, len(texto) // 4)


def custo_usd(uso: dict, env=None) -> float:
    env = env or os.environ
    entrada = float(env.get("DOCCHECK_IA_PRECO_IN") or 0)
    saida = float(env.get("DOCCHECK_IA_PRECO_OUT") or 0)
    total = (uso.get("prompt_tokens", 0) * entrada
             + uso.get("completion_tokens", 0) * saida)
    return round(total / 1_000_000, 6)


def _post(url: str, payload: dict, chave: str, timeout: int = 60) -> dict:
    req = urllib.request.Request(
        url, data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json",
                 "Authorization": f"Bearer {chave}"})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


def revisar(raiz: Path, ref: str = "HEAD~1", usar_ia: bool = False,
            max_usd: float = LIMITE_MAX_USD, modelo: str | None = None,
            env=None, transporte=None) -> dict:
    env = env or os.environ
    contexto = montar_contexto(raiz, ref)
    prompt = montar_prompt(contexto)
    saida = {"ref": ref, "usou_ia": False, "resposta": None, "uso": {},
             "custo_usd": 0.0, "achados": contexto["achados"],
             "docs": [nome for nome, _ in contexto["docs"]], "contexto": prompt}

    if not usar_ia:
        return saida
    cfg = config_ia(env)
    if not cfg:
        saida["pulado"] = ("sem DOCCHECK_IA_URL — defina URL/KEY/MODEL no ambiente "
                           "(o contexto segue disponível em --json)")
        return saida

    preco_in = float(env.get("DOCCHECK_IA_PRECO_IN") or 0)
    estimado = estimar_tokens(prompt) / 1_000_000 * preco_in
    if max_usd and preco_in and estimado > max_usd:
        saida["pulado"] = (f"estimativa ~US$ {estimado:.4f} acima de "
                           f"--max-usd {max_usd:.2f} — nada foi enviado")
        return saida

    enviar = transporte or _post
    payload = {"model": modelo or cfg["modelo"], "temperature": 0,
               "messages": [{"role": "user", "content": prompt}]}
    resposta = enviar(cfg["url"], payload, cfg["chave"])
    escolha = (resposta.get("choices") or [{}])[0]
    uso = resposta.get("usage") or {}
    saida.update(usou_ia=True,
                 resposta=(escolha.get("message") or {}).get("content", "").strip(),
                 uso=uso, custo_usd=custo_usd(uso, env), modelo=payload["model"])
    return saida
