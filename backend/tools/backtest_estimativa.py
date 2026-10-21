"""Mede o erro da estimativa das próximas horas (EstimativaJanelas) contra o que aconteceu.

Para cada terminal e cada dia D com dados, refaz a estimativa como o sistema faria
na véspera (base = últimos 30 dias com dados antes de D, dividida pelos dias com
dados) e compara, hora a hora, com as chegadas e liberações reais de D.

Uso (planilhas reais NÃO ficam no repositório):
  python3 -I backend/tools/backtest_estimativa.py terminal=arquivo.xlsx [terminal=arquivo.xlsx ...] [--limite-risco 1.4] [--janela-horas 1|2|3|4|6] [--dias-base 30]

Saídas: erro absoluto médio (carretas/hora), WAPE (soma |erro| ÷ soma real), viés,
e acerto da classificação de "risco de fila" (precisão e cobertura).
Regras de classificação iguais a Application/Metricas/EstimativaJanelas.cs.
"""
import sys
import pandas as pd

DIAS_BASE, DIAS_MIN, DEMANDA_MIN, EXCESSO_MIN = 30, 7, 5.0, 1.0


def classificar(demanda, capacidade, limite):
    if demanda < DEMANDA_MIN or demanda - capacidade < EXCESSO_MIN:
        return "dentro"
    return "risco" if capacidade <= 0 or demanda >= capacidade * limite else "atencao"


def carregar(arquivo):
    d = pd.read_excel(arquivo, dtype=str)
    d["m"] = pd.to_datetime(d.DATA_MARCACAO + d.HORA_MARCACAO, format="%Y%m%d%H:%M:%S")
    d["l"] = pd.to_datetime(d.DATA_LIBERACAO + d.HORA_LIBERACAO, format="%Y%m%d%H:%M:%S", errors="coerce")
    return d[d.l.notna()]  # a estimativa usa só movimentos liberados (mesma base do sistema)


def backtest(d, limite, janela=1, dias_base=DIAS_BASE):
    d = d.assign(dm=d.m.dt.normalize(), hm=d.m.dt.hour // janela, dl=d.l.dt.normalize(), hl=d.l.dt.hour // janela)
    cheg = d.groupby(["dm", "hm"]).size()
    lib = d.groupby(["dl", "hl"]).size()
    dias = sorted(d.dm.unique())
    linhas = []
    for dia in dias:
        base = [x for x in dias if dia - pd.Timedelta(days=dias_base) <= x < dia]
        if len(base) < min(DIAS_MIN, dias_base) or len([x for x in dias if x < dia]) < DIAS_BASE:
            continue
        anterior = base[-1]
        for h in range(24 // janela):
            dem = sum(cheg.get((x, h), 0) for x in base) / len(base)
            cap = sum(lib.get((x, h), 0) for x in base) / len(base)
            real_c, real_l = cheg.get((dia, h), 0), lib.get((dia, h), 0)
            ingenua = cheg.get((anterior, h), 0)  # referência ingênua: mesma faixa no último dia com dados
            esc = 1 / janela  # classificação por hora, como na tela
            linhas.append((dia, h, dem, cap, real_c, real_l, ingenua,
                           classificar(dem * esc, cap * esc, limite), classificar(real_c * esc, real_l * esc, limite)))
    return pd.DataFrame(linhas, columns=["dia", "hora", "prev", "cap", "real", "real_lib", "ingenua", "sit_prev", "sit_real"])


def main():
    args = [a for a in sys.argv[1:] if "=" in a]
    limite = float(sys.argv[sys.argv.index("--limite-risco") + 1]) if "--limite-risco" in sys.argv else 1.4
    janela = int(sys.argv[sys.argv.index("--janela-horas") + 1]) if "--janela-horas" in sys.argv else 1
    dias_base = int(sys.argv[sys.argv.index("--dias-base") + 1]) if "--dias-base" in sys.argv else DIAS_BASE
    for par in args:
        terminal, arquivo = par.split("=", 1)
        r = backtest(carregar(arquivo), limite, janela, dias_base)
        erro = r.prev - r.real
        erro_ing = r.ingenua - r.real
        vp = ((r.sit_prev == "risco") & (r.sit_real == "risco")).sum()
        prev_risco, real_risco = (r.sit_prev == "risco").sum(), (r.sit_real == "risco").sum()
        print(f"{terminal}: {r.dia.nunique()} dias avaliados (base de {dias_base} dias), {len(r)} janelas de {janela} h")
        print(f"  chegadas por janela: real média {r.real.mean():.1f} | erro absoluto médio {erro.abs().mean():.1f} | "
              f"WAPE {erro.abs().sum() / max(r.real.sum(), 1) * 100:.0f}% | viés {erro.mean():+.2f} | "
              f"WAPE da referência ingênua (dia anterior) {erro_ing.abs().sum() / max(r.real.sum(), 1) * 100:.0f}%")
        print(f"  risco de fila: previstas {prev_risco}, ocorridas {real_risco}, acertos {vp} | "
              f"precisão {vp / max(prev_risco, 1) * 100:.0f}% | cobertura {vp / max(real_risco, 1) * 100:.0f}%")


if __name__ == "__main__":
    main()
