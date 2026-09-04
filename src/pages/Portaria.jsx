import { useState, useMemo } from "react";
import { useNegativacao } from "../hooks/useNegativacao.js";
import { useRegistrosReais } from "../hooks/useRegistrosReais.js";
import { totaisPorEmpresa, formatarHoras } from "../data/relatorio.js";
import { EMPRESAS } from "../data/registry.js";
import { notificar } from "../components/toast.js";
import {
  buscarVeiculoPorPlaca,
  atualizarStatusPortaria,
  listarVeiculos,
  listarFilaAtual,
  contarNoPatio,
  LIMIAR_CONGESTIONAMENTO
} from "../data/negativacaoStore.js";

const ACOES_PORTARIA = [
  { chave: "No Pátio", rotulo: "Registrar entrada no pátio", icone: "login", cor: "var(--ambar-600)", fundo: "#FBEBD1", borda: "#F2D8A5" },
  { chave: "Aguardando", rotulo: "Registrar saída do pátio", icone: "logout", cor: "var(--tinta-suave)", fundo: "var(--superficie-alt)", borda: "var(--borda)" },
  { chave: "No Porto", rotulo: "Registrar entrada no porto", icone: "directions_boat", cor: "var(--azul-500)", fundo: "var(--azul-100)", borda: "#C3D6F5" },
  { chave: "Descarga Finalizada", rotulo: "Finalizar descarga", icone: "download_done", cor: "var(--verde-500)", fundo: "var(--verde-100)", borda: "#BEE7D2" }
];

function minutosDesde(iso) {
  return Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60000));
}

function formatarMinutos(min) {
  if (min < 60) return `${min} min`;
  return formatarHoras(min / 60);
}

export default function Portaria() {
  useNegativacao();
  const [consulta, setConsulta] = useState("");
  const [resultado, setResultado] = useState(null);
  const [naoEncontrado, setNaoEncontrado] = useState(false);
  const veiculos = listarVeiculos();
  const fila = listarFilaAtual();
  const noPatio = contarNoPatio();
  const congestionado = noPatio >= LIMIAR_CONGESTIONAMENTO;

  // Espera média histórica real por terminal — é o que embasa a estimativa
  // da fila virtual (nada inventado: vem das marcações reais importadas).
  const { registros: registrosReais } = useRegistrosReais("todas");
  const esperaMediaPorTerminal = useMemo(() => {
    const porEmpresa = totaisPorEmpresa(registrosReais);
    const mapa = {};
    EMPRESAS.forEach(e => {
      const encontrado = porEmpresa.find(x => x.empresa === e.nome);
      mapa[e.id] = encontrado ? encontrado.esperaMedia : null;
    });
    return mapa;
  }, [registrosReais]);

  function aoBuscar(e) {
    e.preventDefault();
    const encontrado = buscarVeiculoPorPlaca(consulta);
    setResultado(encontrado);
    setNaoEncontrado(!encontrado);
  }

  function selecionar(v) {
    setResultado(v);
    setNaoEncontrado(false);
    setConsulta(v.placa);
  }

  function aoAcionar(status) {
    if (!resultado) return;
    atualizarStatusPortaria(resultado.id, status);
    setResultado(r => ({ ...r, statusPortaria: status, statusPortariaDesde: new Date().toISOString() }));
    notificar(`${resultado.placa} — status atualizado para "${status}".`, "sucesso");
  }

  return (
    <>
      {congestionado && (
        <div className="cartao" style={{ marginBottom: 20, borderColor: "var(--vermelho-500)" }}>
          <div className="cartao__corpo" style={{ display: "flex", alignItems: "center", gap: 12, background: "var(--vermelho-100)", borderRadius: 10 }}>
            <span className="material-symbols-outlined" style={{ fontSize: 26, color: "var(--vermelho-500)" }}>warning</span>
            <div>
              <div style={{ fontWeight: 700, fontSize: 14, color: "var(--vermelho-500)" }}>Alerta de congestionamento</div>
              <div style={{ fontSize: 12.5, color: "var(--tinta-suave)" }}>
                {noPatio} carretas No Pátio agora — acima do limite de {LIMIAR_CONGESTIONAMENTO} pra operação fluida.
              </div>
            </div>
          </div>
        </div>
      )}

      <div className="cartao" style={{ marginBottom: 20 }}>
        <div className="cartao__cabecalho">
          <h3>Fila Virtual</h3>
          <p>Veículos parados agora, na ordem de chegada — com estimativa baseada na média histórica real do terminal</p>
        </div>
        <div className="cartao__corpo" style={{ overflowX: "auto" }}>
          {fila.length === 0 ? (
            <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Nenhum veículo parado no pátio ou aguardando no momento.</p>
          ) : (
            <table>
              <thead>
                <tr><th>#</th><th>Placa</th><th>Transportadora</th><th>Status</th><th>Há quanto tempo</th><th>Estimativa (média do terminal)</th></tr>
              </thead>
              <tbody>
                {fila.map((v, i) => {
                  const decorrido = minutosDesde(v.statusPortariaDesde);
                  const mediaTerminalHoras = esperaMediaPorTerminal[v.terminal];
                  const restante = mediaTerminalHoras != null ? Math.max(0, mediaTerminalHoras * 60 - decorrido) : null;
                  return (
                    <tr key={v.id}>
                      <td>{i + 1}</td>
                      <td className="mono">{v.placa}</td>
                      <td>{v.transportadora}</td>
                      <td>{v.statusPortaria}</td>
                      <td className="mono">{formatarMinutos(decorrido)}</td>
                      <td>
                        {restante === null
                          ? "sem histórico"
                          : restante === 0
                            ? <span style={{ color: "var(--verde-500)", fontWeight: 600 }}>já além da média</span>
                            : `~${formatarMinutos(restante)} restantes`}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      </div>

      <div className="cartao">
        <div className="cartao__corpo" style={{ maxWidth: 760, margin: "0 auto", paddingTop: 24 }}>
          <form onSubmit={aoBuscar} style={{ marginBottom: 24 }}>
            <div className="campo-icone">
              <span className="material-symbols-outlined" style={{ fontSize: 22 }}>search</span>
              <input
                value={consulta}
                onChange={e => setConsulta(e.target.value)}
                placeholder="Digite a placa do veículo (ex: ENM-1001, NGL-3021)"
                style={{ width: "100%", padding: "16px 16px 16px 42px", fontSize: 18, textAlign: "center", textTransform: "uppercase", border: "1px solid var(--borda)", borderRadius: 12 }}
              />
            </div>
          </form>

          {naoEncontrado && (
            <div style={{ textAlign: "center", color: "var(--tinta-suave)", marginBottom: 20, padding: "16px 12px" }}>
              <span className="material-symbols-outlined" style={{ fontSize: 28, color: "var(--tinta-fraca)" }}>search_off</span>
              <p style={{ margin: "8px 0 0", fontSize: 13.5 }}>Nenhum veículo encontrado com essa placa. Cadastre em "Frotas & Condutores".</p>
            </div>
          )}

          {resultado && (
            <>
              <div style={{
                background: "var(--superficie-alt)", border: "1px solid var(--borda)", borderRadius: 12, padding: 16,
                display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 12, marginBottom: 16
              }}>
                <div>
                  <h3 style={{ fontSize: 17 }}>{resultado.transportadora}</h3>
                  <div style={{ display: "flex", gap: 8, marginTop: 8, flexWrap: "wrap" }}>
                    <span className="selo" style={{ background: "#fff", color: "var(--tinta-suave)" }}>
                      <span className="material-symbols-outlined">local_shipping</span> {resultado.modelo || "Veículo"}
                    </span>
                    <span className="selo" style={{ background: "#fff", color: "var(--tinta-suave)" }}>Status atual: {resultado.statusPortaria}</span>
                  </div>
                </div>
                <div style={{ textAlign: "right" }}>
                  <span className="placa-chip" style={{ fontSize: 16, padding: "6px 12px" }}>{resultado.placa}</span>
                </div>
              </div>
              <div className="grade-acoes">
                {ACOES_PORTARIA.map(acao => (
                  <button
                    key={acao.chave}
                    onClick={() => aoAcionar(acao.chave)}
                    className="acao-portaria"
                    style={{ background: acao.fundo, color: acao.cor, borderColor: acao.borda }}
                  >
                    <span className="material-symbols-outlined">{acao.icone}</span>
                    <span className="rotulo">{acao.rotulo}</span>
                  </button>
                ))}
              </div>
            </>
          )}

          {!resultado && (
            <div>
              <p style={{ fontSize: 12.5, fontWeight: 700, color: "var(--tinta-suave)", textTransform: "uppercase", letterSpacing: "0.04em", marginBottom: 10 }}>
                Veículos cadastrados — clique pra selecionar
              </p>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {veiculos.map(v => (
                  <button
                    key={v.id}
                    onClick={() => selecionar(v)}
                    style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "10px 14px", borderRadius: 10, border: "1px solid var(--borda)", background: "#fff", cursor: "pointer", textAlign: "left" }}
                  >
                    <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                      <span className="placa-chip" style={{ fontSize: 13 }}>{v.placa}</span>
                      <span style={{ fontSize: 13, color: "var(--tinta-suave)" }}>{v.transportadora}</span>
                    </div>
                    <span className="selo" style={{ background: "var(--superficie-alt)", color: "var(--tinta-suave)", fontSize: 11.5 }}>{v.statusPortaria}</span>
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </>
  );
}
