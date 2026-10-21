import { useState, useMemo } from "react";
import { QRCodeSVG } from "qrcode.react";
import { useNegativacao } from "../hooks/useNegativacao.js";
import { useApi } from "../hooks/useApi.js";
import { api } from "../api/cliente.js";
import { STATUS_PORTARIA, nomeTerminal } from "../api/rotulos.js";
import { formatarHoras } from "../utils/formatar.js";
import { Carregando, ErroCarga } from "../components/EstadoCarga.jsx";
import { ResumoEstimativa } from "../components/EstimativaProximasHoras.jsx";
import { buscarVeiculoPorPlaca, listarVeiculos } from "../data/negativacaoStore.js";

function minutosDesde(iso) {
  return Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60000));
}

function formatarMinutos(min) {
  if (min < 60) return `${min} min`;
  return formatarHoras(min / 60);
}

export default function Portaria() {
  const negativacao = useNegativacao();
  const [consulta, setConsulta] = useState("");
  const [resultado, setResultado] = useState(null);
  const [naoEncontrado, setNaoEncontrado] = useState(false);
  const veiculos = listarVeiculos();

  // Fila virtual e congestionamento vêm prontos da API. O tempo típico é a
  // mediana da espera no terminal (marcação → liberação, últimos 30 dias com
  // dados) — referência do terminal, não medição da portaria. Atualiza a cada minuto.
  const fila = useApi(sinal => api.get("/api/v1/portaria/fila-virtual", null, sinal), [], { intervaloMs: 60_000 });
  // Mesma estimativa das próximas horas da tela Gargalos e alertas (uma regra só).
  const proximas = useApi(sinal => api.get("/api/v1/alertas/proximas-horas", null, sinal), [], { intervaloMs: 600_000 });

  const tempoTipicoPorTerminal = useMemo(() => {
    const mapa = {};
    (fila.dados?.tempoTipico || []).forEach(t => { mapa[t.terminalId] = t.medianaHoras; });
    return mapa;
  }, [fila.dados]);

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

  const dadosFila = fila.dados;

  return (
    <>
      {fila.erro && <ErroCarga erro={fila.erro} aoTentarDeNovo={fila.recarregar} />}
      {negativacao.erro && <ErroCarga erro={negativacao.erro} aoTentarDeNovo={negativacao.recarregar} />}

      {dadosFila?.congestionado && (
        <div className="cartao" role="alert" style={{ marginBottom: 20, borderColor: "var(--vermelho-500)" }}>
          <div className="cartao__corpo" style={{ display: "flex", alignItems: "center", gap: 12, background: "var(--vermelho-100)", borderRadius: 10 }}>
            <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 26, color: "var(--vermelho-500)" }}>warning</span>
            <div>
              <div style={{ fontWeight: 700, fontSize: 14, color: "var(--vermelho-500)" }}>Alerta de congestionamento</div>
              <div style={{ fontSize: 12.5, color: "var(--tinta-suave)" }}>
                {dadosFila.noPatio} carretas no pátio agora — o limite configurado de {dadosFila.limiarCongestionamento} para uma operação fluida foi atingido.
              </div>
            </div>
          </div>
        </div>
      )}

      <div style={{ marginBottom: 20 }}><ResumoEstimativa r={proximas} /></div>

      <div className="cartao" style={{ marginBottom: 20 }}>
        <div className="cartao__cabecalho">
          <h3>Fila virtual</h3>
          <p>Carretas com situação "No pátio" ou "Aguardando", na ordem em que entraram nessa situação. A estimativa usa o tempo típico de espera do terminal (mediana dos últimos 30 dias com dados) — é uma referência do terminal, não uma medição da portaria.</p>
        </div>
        <div className="cartao__corpo" style={{ overflowX: "auto" }}>
          {!dadosFila ? (fila.carregando ? <Carregando /> : null) : dadosFila.fila.length === 0 ? (
            <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Nenhum veículo parado no pátio ou aguardando no momento.</p>
          ) : (
            <table>
              <thead>
                <tr><th scope="col">#</th><th scope="col">Placa</th><th scope="col">Transportadora</th><th scope="col">Situação</th><th scope="col">Há quanto tempo</th><th scope="col">Estimativa pelo tempo típico do terminal</th></tr>
              </thead>
              <tbody>
                {dadosFila.fila.map(item => (
                  <tr key={item.veiculoId}>
                    <td>{item.posicao}</td>
                    <td className="mono">{item.placa}</td>
                    <td>{item.transportadora}</td>
                    <td>{STATUS_PORTARIA[item.statusPortaria] || item.statusPortaria}</td>
                    <td className="mono">{formatarMinutos(item.minutosDecorridos)}</td>
                    <td>
                      {item.minutosRestantesEstimados === null
                        ? "sem histórico do terminal"
                        : item.alemDoTempoTipico
                          ? <span style={{ color: "var(--critico-texto)", fontWeight: 600 }}>já passou do tempo típico ({formatarHoras(item.tempoTipicoTerminalHoras)})</span>
                          : `cerca de ${formatarMinutos(item.minutosRestantesEstimados)} até o tempo típico`}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      <div className="cartao" style={{ marginBottom: 20 }}>
        <div className="cartao__cabecalho">
          <h3>Frota cadastrada — situação na portaria</h3>
          <p>Todas as carretas cadastradas, pela hora em que entraram na situação atual. Para quem está no pátio, aguardando ou no porto, o tempo além do tempo típico de espera do terminal.</p>
        </div>
        <div className="cartao__corpo" style={{ overflowX: "auto" }}>
          {!negativacao.carregado ? <Carregando /> : (
            <table>
              <thead><tr><th scope="col">Na situação desde</th><th scope="col">Placa</th><th scope="col">Transportadora</th><th scope="col">Terminal</th><th scope="col">Situação</th><th scope="col">Além do tempo típico</th></tr></thead>
              <tbody>
                {[...veiculos].sort((a, b) => new Date(a.statusPortariaDesde) - new Date(b.statusPortariaDesde)).map(v => {
                  const decorrido = minutosDesde(v.statusPortariaDesde);
                  const mediaTerminalMin = tempoTipicoPorTerminal[v.terminal] != null ? tempoTipicoPorTerminal[v.terminal] * 60 : null;
                  const emAndamento = [STATUS_PORTARIA.NoPatio, STATUS_PORTARIA.Aguardando, STATUS_PORTARIA.NoPorto].includes(v.statusPortaria);
                  const atrasoMin = emAndamento && mediaTerminalMin != null ? decorrido - mediaTerminalMin : null;
                  return (
                    <tr key={v.id}>
                      <td className="mono">{new Date(v.statusPortariaDesde).toLocaleString("pt-BR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}</td>
                      <td className="mono">{v.placa}</td>
                      <td>{v.transportadora}</td>
                      <td>{nomeTerminal(v.terminal)}</td>
                      <td>{v.statusPortaria}</td>
                      <td>
                        {atrasoMin === null ? "—" : atrasoMin <= 0 ? <span style={{ color: "var(--verde-500)" }}>dentro do tempo típico</span> : <span style={{ color: "var(--vermelho-500)", fontWeight: 600 }}>+{formatarMinutos(Math.round(atrasoMin))}</span>}
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
          <form onSubmit={aoBuscar} style={{ marginBottom: 24 }} role="search">
            <div className="campo-icone">
              <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 22 }}>search</span>
              <input
                value={consulta}
                onChange={e => setConsulta(e.target.value)}
                placeholder="Digite a placa do veículo (ex: ENM-1001, NGL-3021)"
                aria-label="Placa do veículo"
                maxLength={8}
                style={{ width: "100%", padding: "16px 16px 16px 42px", fontSize: 18, textAlign: "center", textTransform: "uppercase", border: "1px solid var(--borda)", borderRadius: 12 }}
              />
            </div>
          </form>

          {naoEncontrado && (
            <div role="status" style={{ textAlign: "center", color: "var(--tinta-suave)", marginBottom: 20, padding: "16px 12px" }}>
              <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 28, color: "var(--tinta-fraca)" }}>search_off</span>
              <p style={{ margin: "8px 0 0", fontSize: 13.5 }}>Nenhum veículo encontrado com essa placa. Cadastre em "Cadastros".</p>
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
                      <span className="material-symbols-outlined" aria-hidden="true">local_shipping</span> {resultado.modelo || "Veículo"}
                    </span>
                    <span className="selo" style={{ background: "#fff", color: "var(--tinta-suave)" }}>Status atual: {resultado.statusPortaria}</span>
                    {resultado.statusNegativacao === "negativada" && (
                      <span className="selo" style={{ background: "var(--vermelho-100)", color: "var(--vermelho-500)" }}>Negativada — bloqueada para carregamento</span>
                    )}
                  </div>
                </div>
                <span className="placa-chip" style={{ fontSize: 16, padding: "6px 12px" }}>{resultado.placa}</span>
              </div>

              <div style={{
                display: "flex", alignItems: "center", justifyContent: "center", gap: 24, padding: "32px 24px", flexWrap: "wrap",
                border: "1px dashed var(--borda)", borderRadius: 12, background: "#fff"
              }}>
                <QRCodeSVG value={resultado.placa} size={140} title={`QR da placa ${resultado.placa}`} />
                <div style={{ fontSize: 12.5, color: "var(--tinta-suave)", maxWidth: 280 }}>
                  Esse QR codifica a placa <b>{resultado.placa}</b> — a leitura na portaria recupera o veículo direto,
                  sem digitação. Para agendamentos, o QR codifica o código do agendamento (tela Agendamentos).
                </div>
              </div>
            </>
          )}

          {!resultado && (
            <div>
              <p style={{ fontSize: 12.5, fontWeight: 700, color: "var(--tinta-suave)", textTransform: "uppercase", letterSpacing: "0.04em", marginBottom: 10 }}>
                Veículos cadastrados — clique para selecionar
              </p>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {veiculos.map(v => (
                  <button
                    type="button"
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
