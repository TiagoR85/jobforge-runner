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

    args = parser.parse_args(argv)
    try:
        return args.funcao(args)
    except Exception as exc:
        print(f"doccheck: {type(exc).__name__}: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
