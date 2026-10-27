"""Conferência independente dos relatórios: recalcula no SQL Server (SQL próprio, sem usar o
código do BarcaLog) cada indicador, tabela e gráfico da prévia e compara com o que a API devolve.

Uso (ambiente local, com o container barcalog-sql e a API rodando):
    export MSSQL_SA_PASSWORD=...  BARCALOG_TOKEN=<token de um usuário interno>
    MODO=coluna python3 backend/tools/conferir_relatorios.py   # espera como o sistema guarda (0,01 h)
    MODO=exato  python3 backend/tools/conferir_relatorios.py   # espera exata em segundos
Os cenários de período usam os dados reais de 2026; ajuste no fim do arquivo para outros meses.
"""
import json, os, subprocess, sys, urllib.request, urllib.parse, datetime as dt, math

SENHA = os.environ["MSSQL_SA_PASSWORD"]
TOKEN = os.environ["BARCALOG_TOKEN"]
API = os.environ.get("BARCALOG_API", "http://localhost:5080")
resultados = []


MODO = os.environ.get("MODO", "coluna")


def sql(q):
    if MODO == "coluna":
        q = q.replace("DATEDIFF_BIG(SECOND, m.DataMarcacao, m.DataLiberacao)/3600.0", "CAST(m.EsperaHoras AS float)").replace("DATEDIFF_BIG(SECOND, DataMarcacao, DataLiberacao)/3600.0", "CAST(EsperaHoras AS float)")
    out = subprocess.run(["docker", "exec", "barcalog-sql", "/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", "localhost", "-U", "sa",
                          "-P", SENHA, "-d", "BarcaLog", "-W", "-h", "-1", "-s", "\t", "-Q", "SET NOCOUNT ON; " + q],
                         capture_output=True, text=True, check=True).stdout
    return [l.split("\t") for l in out.strip().split("\n") if l.strip()]


def previa(params):
    url = f"{API}/api/v1/relatorios/previa?" + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers={"Authorization": f"Bearer {TOKEN}"})
    return json.load(urllib.request.urlopen(req))


# ---------- formatação pt-BR (mesma regra descrita em docs: 13h44, 1.234, 85,3%) ----------
def num(n, casas=0):
    s = f"{n:,.{casas}f}"
    return s.replace(",", "X").replace(".", ",").replace("X", ".")


def pct(v, casas=1):
    return "—" if v is None else num(v, casas) + "%"


def dur(h):
    if h is None: return "—"
    m = int(math.floor(abs(h) * 60 + 0.5))
    return f"{m // 60}h{m % 60:02d}"


def checar(cenario, item, esperado, obtido):
    norm = lambda v: [float(x) for x in v] if isinstance(v, list) and v and isinstance(v[0], (int, float)) else v
    ok = str(norm(esperado)) == str(norm(obtido))
    resultados.append((cenario, item, esperado, obtido, ok))


def filtro_sql(terminal=None, operador=None, carga=None):
    w = []
    if terminal: w.append(f"TerminalId = '{terminal}'")
    if operador: w.append(f"Operador = '{operador}'")
    if carga: w.append(f"Carga = '{carga}'")
    return (" AND " + " AND ".join(w)) if w else ""


def resumo_sql(de, ate, f):
    fim = (dt.date.fromisoformat(ate) + dt.timedelta(days=1)).isoformat()
    q = f"""
    WITH e AS (SELECT DATEDIFF_BIG(SECOND, DataMarcacao, DataLiberacao)/3600.0 AS h FROM Marcacoes
               WHERE DataMarcacao >= '{de}' AND DataMarcacao < '{fim}' {f})
    SELECT (SELECT COUNT(*) FROM e),
           (SELECT COUNT(*) FROM Marcacoes WHERE DataLiberacao >= '{de}' AND DataLiberacao < '{fim}' {f}),
           (SELECT COUNT(*) FROM e WHERE h IS NOT NULL),
           (SELECT AVG(h) FROM e),
           (SELECT TOP 1 PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY h) OVER () FROM e WHERE h IS NOT NULL),
           (SELECT TOP 1 PERCENTILE_CONT(0.9) WITHIN GROUP (ORDER BY h) OVER () FROM e WHERE h IS NOT NULL),
           (SELECT COUNT(*) FROM e WHERE h <= 24),
           (SELECT COUNT(*) FROM e WHERE h > 72)"""
    r = sql(q)[0]
    ent, sai, conc = int(r[0]), int(r[1]), int(r[2])
    flt = lambda x: None if x in ("NULL", "") else float(x)
    return dict(entradas=ent, saidas=sai, concluidas=conc, media=flt(r[3]), mediana=flt(r[4]), p90=flt(r[5]),
                ate24=None if conc == 0 else round(int(r[6]) * 100 / conc, 1), longas=int(r[7]))


def bloco(doc, secao, tipo, titulo=None):
    for s in doc["secoes"]:
        if s["id"] != secao: continue
        for b in s["blocos"]:
            if b["tipo"] == tipo and (titulo is None or b.get("titulo", "").startswith(titulo)):
                return b
    return None


def conferir(nome, params, terminal=None, operador=None, carga=None):
    doc = previa(params)
    de, ate = params["de"], params["ate"]
    dias = (dt.date.fromisoformat(ate) - dt.date.fromisoformat(de)).days + 1
    f = filtro_sql(terminal, operador, carga)
    a = resumo_sql(de, ate, f)
    d0 = dt.date.fromisoformat(de)
    ant_de, ant_ate = (d0 - dt.timedelta(days=dias)).isoformat(), (d0 - dt.timedelta(days=1)).isoformat()
    b = resumo_sql(ant_de, ant_ate, f)

    kpi = {i["rotulo"]: i for i in bloco(doc, "resumo", "indicadores")["itens"]}
    checar(nome, "Entradas", num(a["entradas"]), kpi["Entradas de carretas"]["valor"])
    checar(nome, "Média por dia", num(round(a["entradas"] / dias, 1), 1), kpi["Média por dia"]["valor"])
    checar(nome, "Liberações (saídas)", num(a["saidas"]), kpi["Liberações (saídas)"]["valor"])
    checar(nome, "Espera mediana", dur(a["mediana"]), kpi["Espera mediana"]["valor"])
    checar(nome, "Espera média", dur(a["media"]), kpi["Espera média"]["valor"])
    checar(nome, "Liberadas até 24 h", pct(a["ate24"]), kpi["Liberadas em até 24 h"]["valor"])
    checar(nome, "Esperas > 72 h", num(a["longas"]), kpi["Esperas acima de 72 h"]["valor"])
    if "Ainda sem liberação" in kpi or a["entradas"] != a["concluidas"]:
        checar(nome, "Sem liberação", num(a["entradas"] - a["concluidas"]), kpi.get("Ainda sem liberação", {}).get("valor"))
    if b["entradas"]:
        checar(nome, "Média por dia (anterior)", "antes: " + num(round(b["entradas"] / dias, 1), 1), kpi["Média por dia"]["comparacao"])
        checar(nome, "Espera mediana (anterior)", dur(b["mediana"]), kpi["Espera mediana"]["comparacao"].split("antes: ")[1].rstrip(")"))

    # Tabela por terminal (seção terminais ou distribuição)
    t = bloco(doc, "terminais", "tabela") or bloco(doc, "distribuicao", "tabela", "Por terminal")
    if t:
        linhas = sql(f"""WITH e AS (SELECT t.Nome, DATEDIFF_BIG(SECOND, m.DataMarcacao, m.DataLiberacao)/3600.0 h FROM Marcacoes m JOIN Terminais t ON t.Id = m.TerminalId
                     WHERE m.DataMarcacao >= '{de}' AND m.DataMarcacao < DATEADD(day, 1, CAST('{ate}' AS date)) {f.replace('TerminalId', 'm.TerminalId').replace('Operador', 'm.Operador').replace('Carga', 'm.Carga')})
                     SELECT DISTINCT Nome, COUNT(*) OVER (PARTITION BY Nome),
                        PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY h) OVER (PARTITION BY Nome),
                        PERCENTILE_CONT(0.9) WITHIN GROUP (ORDER BY h) OVER (PARTITION BY Nome),
                        SUM(CASE WHEN h <= 24 THEN 1 ELSE 0 END) OVER (PARTITION BY Nome),
                        SUM(CASE WHEN h IS NOT NULL THEN 1 ELSE 0 END) OVER (PARTITION BY Nome),
                        SUM(CASE WHEN h > 72 THEN 1 ELSE 0 END) OVER (PARTITION BY Nome) FROM e""")
        obt = {l[0]: l for l in t["linhas"]}
        for nomeT, n, med, p90, a24, conc, lon in linhas:
            o = obt.get(nomeT)
            esperado = [nomeT, num(int(n)), pct(round(int(n) * 100 / a["entradas"], 1)), dur(float(med)), dur(float(p90)),
                        pct(round(int(a24) * 100 / int(conc), 1)), num(int(lon))]
            checar(nome, f"Terminal {nomeT}", " | ".join(esperado), " | ".join(o) if o else None)

    # Histórico mensal (até o fim do período)
    h = bloco(doc, "historico", "tabela", "Histórico mensal")
    if h:
        meses = sql(f"""SELECT FORMAT(DataMarcacao, 'yyyy-MM'), COUNT(*), SUM(CASE WHEN Carga LIKE 'Soja%' THEN 1 ELSE 0 END),
                        SUM(CASE WHEN Carga LIKE 'Milho%' THEN 1 ELSE 0 END) FROM Marcacoes
                        WHERE DataMarcacao < DATEADD(day, 1, CAST('{ate}' AS date)) {f} GROUP BY FORMAT(DataMarcacao, 'yyyy-MM') ORDER BY 1""")
        obt = h["linhas"]
        checar(nome, "Histórico: nº de meses", len(meses), len(obt))
        for (m, n, s, mi), o in zip(meses, obt):
            outras = int(n) - int(s) - int(mi)
            checar(nome, f"Histórico {m}", f"{num(int(n))} | {num(int(s))} | {num(int(mi))} | {num(outras)}", " | ".join(o[1:5]))

    # Faixas de espera
    g = bloco(doc, "permanencia", "grafico")
    if g:
        fx = sql(f"""WITH e AS (SELECT DATEDIFF_BIG(SECOND, DataMarcacao, DataLiberacao)/3600.0 h FROM Marcacoes
                   WHERE DataMarcacao >= '{de}' AND DataMarcacao < DATEADD(day, 1, CAST('{ate}' AS date)) {f} AND DataLiberacao IS NOT NULL)
                   SELECT SUM(CASE WHEN h <= 6 THEN 1 ELSE 0 END), SUM(CASE WHEN h > 6 AND h <= 12 THEN 1 ELSE 0 END),
                          SUM(CASE WHEN h > 12 AND h <= 24 THEN 1 ELSE 0 END), SUM(CASE WHEN h > 24 AND h <= 48 THEN 1 ELSE 0 END),
                          SUM(CASE WHEN h > 48 AND h <= 72 THEN 1 ELSE 0 END), SUM(CASE WHEN h > 72 THEN 1 ELSE 0 END) FROM e""")[0]
        checar(nome, "Faixas de espera", [int(x) for x in fx], [int(v) for v in g["series"][0]["valores"]])

    # Maiores esperas
    r = bloco(doc, "permanencia", "tabela")
    if r:
        top = sql(f"""SELECT TOP {len(r['linhas'])} MovimentoId FROM Marcacoes WHERE DataMarcacao >= '{de}' AND DataMarcacao < DATEADD(day, 1, CAST('{ate}' AS date)) {f}
                    AND DataLiberacao IS NOT NULL ORDER BY DATEDIFF_BIG(SECOND, DataMarcacao, DataLiberacao) DESC, DataMarcacao""")
        checar(nome, "Maiores esperas (movimentos)", [x[0] for x in top][:5], [l[0] for l in r["linhas"]][:5])

    # Dias de maior movimento por terminal
    secao_pico = "dias-pico" if any(s["id"] == "dias-pico" for s in doc["secoes"]) else ("picos" if any(s["id"] == "picos" for s in doc["secoes"]) else "gargalos")
    tp = bloco(doc, secao_pico, "tabela", "Os ")
    if tp:
        dias_t = sql(f"""SELECT TOP 3 CAST(m.DataMarcacao AS date), t.Nome, COUNT(*) n FROM Marcacoes m JOIN Terminais t ON t.Id = m.TerminalId
                       WHERE m.DataMarcacao >= '{de}' AND m.DataMarcacao < DATEADD(day, 1, CAST('{ate}' AS date)) {f.replace('TerminalId', 'm.TerminalId').replace('Operador', 'm.Operador').replace('Carga', 'm.Carga')}
                       GROUP BY CAST(m.DataMarcacao AS date), t.Nome ORDER BY n DESC, 1""")
        checar(nome, "Dias de pico (top 3)", [f"{dt.date.fromisoformat(d).strftime('%d/%m/%Y')} {t} {num(int(n))}" for d, t, n in dias_t],
               [f"{l[0][:10]} {l[1]} {l[2]}" for l in tp["linhas"][:3]])
        mx = sql(f"""SELECT MAX(n) FROM (SELECT COUNT(*) n FROM Marcacoes WHERE DataMarcacao >= '{de}' AND DataMarcacao < DATEADD(day, 1, CAST('{ate}' AS date)) {f}
                    GROUP BY CAST(DataMarcacao AS date), TerminalId) x""")[0][0]
        quadro = bloco(doc, secao_pico, "quadro", "Dias acima")
        if quadro:
            checar(nome, "Maior dia de um terminal < 1.000", True, int(mx) <= 1000 and "Nenhum dia" in quadro["itens"][0]["valor"])

    # Por hora (pico)
    gh = next((bb for s in doc["secoes"] for bb in s["blocos"] if bb["tipo"] == "grafico" and bb["titulo"].startswith("Média de entradas e liberações por hora")), None)
    if gh:
        horas = sql(f"""SELECT DATEPART(hour, DataMarcacao), COUNT(*) FROM Marcacoes WHERE DataMarcacao >= '{de}' AND DataMarcacao < DATEADD(day, 1, CAST('{ate}' AS date)) {f}
                     GROUP BY DATEPART(hour, DataMarcacao)""")
        esperado = {int(hh): round(int(n) / dias, 2) for hh, n in horas}
        obtido = gh["series"][0]["valores"]
        checar(nome, "Entradas por hora (24 valores)", [esperado.get(i, 0.0) for i in range(24)], [round(v, 2) for v in obtido])
    return doc


if __name__ == "__main__":
    conferir("Gerencial ago/2026, todos", {"tipo": "gerencial", "de": "2026-08-01", "ate": "2026-08-31"})
    conferir("360 abr/2026, todos", {"tipo": "analise360", "de": "2026-04-01", "ate": "2026-04-30"})
    conferir("Operacional ago/2026, Hidrovias", {"tipo": "operacional", "de": "2026-08-01", "ate": "2026-08-31", "terminalId": "hidrovias"}, terminal="hidrovias")
    conferir("Gerencial jul/2026, carga Milho", {"tipo": "gerencial", "de": "2026-07-01", "ate": "2026-07-31", "carga": "Milho"}, carga="Milho")
    conferir("Operacional 01–15/05/2026, op. BUNGE", {"tipo": "operacional", "de": "2026-05-01", "ate": "2026-05-15", "operador": "BUNGE"}, operador="BUNGE")
    ok = sum(1 for r in resultados if r[4])
    for c, i, e, o, k in resultados:
        if not k: print(f"DIFERENÇA [{c}] {i}\n   banco: {e}\n   relat: {o}")
    print(f"\n{ok} de {len(resultados)} conferências batem")
    json.dump([dict(cenario=c, item=i, banco=str(e), relatorio=str(o), ok=k) for c, i, e, o, k in resultados],
              open(f"conferencia-relatorios-{MODO}.json", "w"), ensure_ascii=False, indent=1)
