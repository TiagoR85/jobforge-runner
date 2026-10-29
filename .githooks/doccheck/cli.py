"""CLI: `doccheck check | init | install`."""
import argparse
import json
import sys
from pathlib import Path

from . import __version__, config
from .adapters import detectar
from .instalar import instalar
from .rules import rodar


def _raiz(valor: str | None) -> Path:
    return Path(valor or ".").resolve()


def cmd_check(args) -> int:
    raiz = _raiz(args.caminho)
    rel = rodar(raiz, config.carregar(raiz))
    if args.json:
        print(json.dumps({
            "raiz": rel.raiz, "ok": rel.ok,
            "achados": [vars(a) for a in rel.achados],
            "puladas": rel.puladas,
        }, ensure_ascii=False, indent=2))
    else:
        for pulada in rel.puladas:
            print(f"- pulada: {pulada}")
        for achado in rel.achados:
            print(achado)
        if rel.ok:
            print(f"documentação ok ({raiz.name})")
        else:
            print(f"{len(rel.erros)} problema(s) de documentação ({raiz.name})")
    return 0 if rel.ok or args.nao_falhar else 1


def cmd_init(args) -> int:
    raiz = _raiz(args.caminho)
    nomes = [a.NOME for a in detectar(raiz)]
    arq = config.salvar_inicial(raiz, nomes[0] if len(nomes) == 1 else None)
    print(f"criado {arq.name} (stacks: {', '.join(nomes) or 'nenhuma detectada'})")
    return 0


def cmd_install(args) -> int:
    return instalar(_raiz(args.caminho))


def cmd_status(args) -> int:
    from .status import coletar, resumo

    raiz = _raiz(args.caminho)
    linhas = coletar(raiz)
    numero = resumo(linhas)
    if args.json:
        print(json.dumps({"resumo": numero, "repos": linhas},
                         ensure_ascii=False, indent=2))
        return 1 if numero["atrasado"] or numero["ausente"] else 0

    for linha in linhas:
        versao = linha["versao"] or "-"
        marca = {"em dia": "  ", "atrasado": "!!", "ausente": "--"}[linha["estado"]]
        print(f"{marca} {linha['repo']:26} {versao:8} {linha['estado']}")
    print(f"\ninstalada: {numero['versao_instalada']} · em dia {numero['em dia']} · "
          f"atrasado {numero['atrasado']} · ausente {numero['ausente']} "
          f"(de {numero['total']})")
    return 1 if numero["atrasado"] or numero["ausente"] else 0


def cmd_review(args) -> int:
    from .review import revisar

    raiz = _raiz(args.caminho)
    resultado = revisar(raiz, ref=args.ref, usar_ia=args.ia,
                        max_usd=args.max_usd, modelo=args.model)
    if args.json:
        print(json.dumps(resultado, ensure_ascii=False, indent=2))
        return 0

    if resultado.get("pulado"):
        print(f"- IA não usada: {resultado['pulado']}")
    print(f"contexto: {len(resultado['docs'])} doc(s) afetado(s), "
          f"{len(resultado['achados'])} achado(s) do check, "
          f"diff de {args.ref}..HEAD ({len(resultado['contexto'])} chars)")
    for achado in resultado["achados"]:
        print(achado)
    if resultado.get("resposta"):
        print("\n" + resultado["resposta"])
        uso = resultado.get("uso") or {}
        print(f"\n(modelo {resultado.get('modelo')} · "
              f"{uso.get('prompt_tokens', 0)} in / "
              f"{uso.get('completion_tokens', 0)} out · "
              f"US$ {resultado['custo_usd']:.6f})")
    elif not args.ia:
        print("\ncontexto pronto — rode com --ia ou use --json para levar a outro modelo")
    return 0


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(
        prog="doccheck",
        description="Verifica se a documentação e o código dizem a mesma coisa.")
    parser.add_argument("--version", action="version", version=__version__)
    sub = parser.add_subparsers(dest="comando", required=True)

    p_check = sub.add_parser("check", help="roda as regras (default: repo atual)")
    p_check.add_argument("caminho", nargs="?")
    p_check.add_argument("--json", action="store_true")
    p_check.add_argument("--nao-falhar", action="store_true",
                         help="sai 0 mesmo com achados (para relatório)")
    p_check.set_defaults(funcao=cmd_check)

    p_init = sub.add_parser("init", help="cria .doccheck.json com a stack detectada")
    p_init.add_argument("caminho", nargs="?")
    p_init.set_defaults(funcao=cmd_init)

    p_inst = sub.add_parser("install", help="vendoriza em .githooks + hook de pre-commit")
    p_inst.add_argument("caminho", nargs="?")
    p_inst.set_defaults(funcao=cmd_install)

    p_rev = sub.add_parser(
        "review", help="revisão semântica do diff + docs (--ia usa modelo; fora do hook)")
    p_rev.add_argument("caminho", nargs="?")
    p_rev.add_argument("--ia", action="store_true",
                       help="envia o contexto a um endpoint OpenAI-compatível")
    p_rev.add_argument("--ref", default="HEAD~1", help="base do diff (default HEAD~1)")
    p_rev.add_argument("--max-usd", type=float, default=0.50,
                       help="teto de custo estimado antes de enviar (default 0.50)")
    p_rev.add_argument("--model", default=None)
    p_rev.add_argument("--json", action="store_true")
    p_rev.set_defaults(funcao=cmd_review)

    p_status = sub.add_parser(
        "status", help="quem tem o vendor e em que versão (pasta com os repos)")
    p_status.add_argument("caminho", nargs="?")
    p_status.add_argument("--json", action="store_true")
    p_status.set_defaults(funcao=cmd_status)

    args = parser.parse_args(argv)
    try:
        return args.funcao(args)
    except Exception as exc:
        print(f"doccheck: {type(exc).__name__}: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
