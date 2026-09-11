import { useMemo } from "react";
import CartaoIndicador from "../components/CartaoIndicador.jsx";
import { useRegistrosReais } from "../hooks/useRegistrosReais.js";
import { totaisPorEmpresa, agruparPorMes, formatarHoras } from "../data/relatorio.js";
import { EMPRESAS } from "../data/registry.js";

// Antes esta tela desenhava 48 carretas com posições sorteadas num mapa
// esquemático, sem GPS real por trás. Isso foi removido por completo.
// Sem uma fonte de geolocalização ao vivo (GPS/PostGIS ou similar), não dá
// pra mostrar posição real — então, em vez de fingir, esta tela virou um
// painel por terminal com os números reais de cada empresa/terminal.
export default function MapaOperacional() {
  const { registros, carregando } = useRegistrosReais("todas");
  const porEmpresa = useMemo(() => totaisPorEmpresa(registros), [registros]);
  const porMesGeral = useMemo(() => agruparPorMes(registros), [registros]);

  const porEmpresaDetalhado = useMemo(() => {
    return EMPRESAS.map(e => {
      const doTerminal = registros.filter(r => r.empresaId === e.id);
      const meses = agruparPorMes(doTerminal);
      const ultimoMes = meses[meses.length - 1];
      const agregado = porEmpresa.find(x => x.empresa === e.nome);
      return {
        ...e,
        total: agregado?.total || 0,
        esperaMedia: agregado?.esperaMedia || 0,
        ultimoMes
      };
    });
  }, [registros, porEmpresa]);

  if (carregando) {
    return <p style={{ color: "var(--tinta-suave)", fontSize: 13 }}>Carregando dados reais…</p>;
  }

  const maiorMesGeral = Math.max(...porMesGeral.map(m => m.total), 1);

  return (
    <>
      <div className="cartao" style={{ marginBottom: 20 }}>
        <div className="cartao__corpo" style={{ fontSize: 13, color: "var(--tinta-suave)", display: "flex", gap: 10, alignItems: "center" }}>
          <span className="material-symbols-outlined" style={{ color: "var(--ambar-600)" }}>info</span>
          Sem GPS/PostGIS integrado ainda, este painel não mostra posição em tempo real de carretas —
          mostra o volume e o tempo de espera reais por terminal, o que já existe hoje na base de dados.
        </div>
      </div>

      <div className="grade-kpi" style={{ marginBottom: 20 }}>
        {porEmpresaDetalhado.map(e => (
          <CartaoIndicador
            key={e.id}
            rotulo={e.nome}
            valor={e.total.toLocaleString("pt-BR")}
            icone="anchor"
            corIcone="var(--navio-700)"
            corFundoIcone="var(--azul-100)"
            nota={`espera média ${formatarHoras(e.esperaMedia)}`}
          />
        ))}
      </div>

      <div className="grade-painel">
        <div className="pilha">
          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Volume Consolidado por Mês</h3>
              <p>Soma dos três terminais</p>
            </div>
            <div className="cartao__corpo">
              <div className="grafico-fluxo">
                {porMesGeral.map(m => (
                  <div key={m.chave} className="grafico-fluxo__col">
                    <span className="grafico-fluxo__valor">{m.total.toLocaleString("pt-BR")}</span>
                    <div className="grafico-fluxo__barra" style={{ height: `${(m.total / maiorMesGeral) * 100}%` }} />
                    <span className="grafico-fluxo__rotulo">{m.rotulo}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>

        <div className="pilha">
          {porEmpresaDetalhado.map(e => (
            <div key={e.id} className="cartao">
              <div className="cartao__cabecalho">
                <h3>{e.nome}</h3>
                <p>{e.ultimoMes ? `Último mês com dados: ${e.ultimoMes.rotulo} — ${e.ultimoMes.total.toLocaleString("pt-BR")} marcações` : "Sem dados"}</p>
              </div>
              <div className="cartao__corpo" style={{ fontSize: 13, color: "var(--tinta-suave)" }}>
                Tempo médio de espera (marcação → liberação): <b>{formatarHoras(e.esperaMedia)}</b>
              </div>
            </div>
          ))}
        </div>
      </div>
    </>
  );
}
