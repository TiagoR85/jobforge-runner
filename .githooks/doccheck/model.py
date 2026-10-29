"""Resultado de uma checagem: achado com regra, severidade e mensagem."""
from dataclasses import dataclass, field

ERRO = "erro"
AVISO = "aviso"


@dataclass
class Achado:
    regra: str
    mensagem: str
    caminho: str = ""
    severidade: str = ERRO

    def __str__(self) -> str:
        onde = f"{self.caminho}: " if self.caminho else ""
        marca = "x" if self.severidade == ERRO else "!"
        return f"[{marca}] {self.regra}: {onde}{self.mensagem}"


@dataclass
class Relatorio:
    raiz: str
    achados: list[Achado] = field(default_factory=list)
    puladas: list[str] = field(default_factory=list)

    def adicionar(self, regra, mensagem, caminho="", severidade=ERRO):
        self.achados.append(Achado(regra, mensagem, caminho, severidade))

    def pular(self, regra, motivo):
        self.puladas.append(f"{regra}: {motivo}")

    @property
    def erros(self) -> list[Achado]:
        return [a for a in self.achados if a.severidade == ERRO]

    @property
    def ok(self) -> bool:
        return not self.erros
