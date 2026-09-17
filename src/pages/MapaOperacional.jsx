import { useState, useMemo } from "react";
import { useNegativacao } from "../hooks/useNegativacao.js";
import { listarOcorrencias } from "../data/negativacaoStore.js";

// Módulo de Mapa de Negativação — em desenvolvimento.
// Ainda não existe integração de geolocalização real (GPS/PostGIS) no
// BarcaLog, então isso é uma SIMULAÇÃO: as ocorrências reais (do módulo de
// Negativação) são plotadas em posições aproximadas sobre um mapa
// esquemático de Barcarena, só pra demonstrar como a visualização
// funcionaria com coordenadas reais. Quando o GPS por carreta existir,
// essas posições fixas viram coordenadas de verdade.
const ZONAS = [
  { id: "unitapajos", nome: "Terminal Unitapajós", x: 18, y: 30, w: 20, h: 16 },
  { id: "tgpm", nome: "Terminal TGPM", x: 62, y: 22, w: 20, h: 16 },
  { id: "hidrovias", nome: "Terminal Hidrovias", x: 62, y: 58, w: 20, h: 16 },
  { id: "trevo", nome: "Trevo do Peteca", x: 42, y: 46, w: 14, h: 12 },
  { id: "patio", nome: "Pátio de Triagem", x: 18, y: 58, w: 20, h: 16 },
  { id: "administrativo", nome: "Zona Administrativa", x: 42, y: 76, w: 16, h: 14 }
];

// Mapeia o texto livre de "local" da ocorrência pra uma zona conhecida —
// se não bater com nada, cai no Pátio de Triagem (posição padrão).
function localParaZona(local) {
  const texto = (local || "").toLowerCase();
  if (texto.includes("unitapaj")) return "unitapajos";
  if (texto.includes("tgpm")) return "tgpm";
  if (texto.includes("hidrovia")) return "hidrovias";
  if (texto.includes("trevo") || texto.includes("peteca")) return "trevo";
  if (texto.includes("administr")) return "administrativo";
  return "patio";
}

const COR_NIVEL = { N1: "var(--verde-500)", N2: "var(--ambar-500)", N3: "var(--vermelho-500)" };

export default function MapaOperacional() {
  useNegativacao();
  const [selecionada, setSelecionada] = useState(null);
  const ocorrencias = listarOcorrencias();

  // Posiciona cada ocorrência dentro da zona correspondente, com um
  // leve espalhamento pra não empilhar pinos exatamente no mesmo ponto.
  const pinos = useMemo(() => {
    const porZona = {};
    return ocorrencias.map(oc => {
      const zonaId = localParaZona(oc.local);
      const zona = ZONAS.find(z => z.id === zonaId);
      porZona[zonaId] = (porZona[zonaId] || 0) + 1;
      const indice = porZona[zonaId];
      const offsetX = ((indice * 37) % 100) / 100 * zona.w * 0.7;
      const offsetY = ((indice * 53) % 100) / 100 * zona.h * 0.7;
      return { ...oc, x: zona.x + zona.w * 0.15 + offsetX, y: zona.y + zona.h * 0.15 + offsetY, zonaNome: zona.nome };
    });
  }, [ocorrencias]);

  return (
    <>
      <div className="cartao" style={{ marginBottom: 16 }}>
        <div className="cartao__corpo" style={{ display: "flex", alignItems: "center", gap: 10, background: "var(--azul-100)", borderRadius: 10 }}>
          <span className="material-symbols-outlined" style={{ color: "var(--navio-700)" }}>construction</span>
          <div style={{ fontSize: 12.5, color: "var(--tinta-suave)" }}>
            <b>Módulo em desenvolvimento.</b> Este é um mapa simulado de Barcarena com as ocorrências reais de negativação
            posicionadas por zona aproximada — ainda não há GPS por carreta integrado. Passe o mouse sobre um pino pra ver o motivo da infração.
          </div>
        </div>
      </div>

      <div className="grade-painel conteudo-pagina">
        <div className="pilha" style={{ flex: 2 }}>
          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>Mapa de Ocorrências — Barcarena (simulado)</h3>
              <p>{ocorrencias.length} ocorrência(s) registrada(s) no Sistema de Negativação</p>
            </div>
            <div className="cartao__corpo">
              <div style={{
                position: "relative", width: "100%", aspectRatio: "16/10", background: "linear-gradient(135deg, #EAF2FA, #DCEAF6)",
                borderRadius: 12, border: "1px solid var(--borda)", overflow: "hidden"
              }}>
                {ZONAS.map(z => (
                  <div key={z.id} style={{
                    position: "absolute", left: `${z.x}%`, top: `${z.y}%`, width: `${z.w}%`, height: `${z.h}%`,
                    background: "rgba(11, 37, 69, 0.06)", border: "1px dashed rgba(11, 37, 69, 0.25)", borderRadius: 10,
                    display: "flex", alignItems: "flex-start", justifyContent: "flex-start", padding: 6
                  }}>
                    <span style={{ fontSize: 10, fontWeight: 700, color: "var(--navio-700)", opacity: 0.7 }}>{z.nome}</span>
                  </div>
                ))}
                {pinos.map(p => (
                  <div
                    key={p.id}
                    onMouseEnter={() => setSelecionada(p)}
                    onClick={() => setSelecionada(p)}
                    style={{
                      position: "absolute", left: `${p.x}%`, top: `${p.y}%`, width: 16, height: 16, borderRadius: "50% 50% 50% 0",
                      background: COR_NIVEL[p.nivel] || "var(--tinta-suave)", transform: "translate(-50%, -100%) rotate(-45deg)",
                      cursor: "pointer", border: "2px solid #fff", boxShadow: "0 1px 4px rgba(0,0,0,.3)",
                      transition: "transform .15s"
                    }}
                    title={`${p.placa} — ${p.nivel}`}
                  />
                ))}
              </div>
              <div style={{ display: "flex", gap: 14, marginTop: 10, fontSize: 11, color: "var(--tinta-suave)" }}>
                <span><span style={{ display: "inline-block", width: 8, height: 8, borderRadius: "50%", background: "var(--verde-500)", marginRight: 4 }} />N1 — Leve</span>
                <span><span style={{ display: "inline-block", width: 8, height: 8, borderRadius: "50%", background: "var(--ambar-500)", marginRight: 4 }} />N2 — Moderada</span>
                <span><span style={{ display: "inline-block", width: 8, height: 8, borderRadius: "50%", background: "var(--vermelho-500)", marginRight: 4 }} />N3 — Grave (bloqueio automático)</span>
              </div>
            </div>
          </div>
        </div>

        <div className="pilha" style={{ flex: 1 }}>
          <div className="cartao">
            <div className="cartao__cabecalho">
              <h3>{selecionada ? "Detalhe da Ocorrência" : "Selecione um pino"}</h3>
              <p>{selecionada ? selecionada.zonaNome : "Passe o mouse ou clique num ponto do mapa"}</p>
            </div>
            <div className="cartao__corpo">
              {!selecionada ? (
                <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Nenhuma ocorrência selecionada.</p>
              ) : (
                <>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
                    <span className="placa-chip" style={{ fontSize: 14 }}>{selecionada.placa}</span>
                    <span className="selo" style={{ background: COR_NIVEL[selecionada.nivel] + "22", color: COR_NIVEL[selecionada.nivel], fontWeight: 700 }}>{selecionada.nivel}</span>
                  </div>
                  <p style={{ fontSize: 13, marginBottom: 8 }}><b>Transportadora:</b> {selecionada.transportadora}</p>
                  <p style={{ fontSize: 13, marginBottom: 8 }}><b>Local:</b> {selecionada.local}</p>
                  <p style={{ fontSize: 13, marginBottom: 8 }}><b>Descrição:</b> {selecionada.descricao}</p>
                  <p style={{ fontSize: 12, color: "var(--tinta-fraca)" }}><b>Registrado em:</b> {new Date(selecionada.criadoEm).toLocaleString("pt-BR")}</p>
                  <p style={{ fontSize: 12, color: "var(--tinta-fraca)" }}><b>Status:</b> {selecionada.status}</p>
                </>
              )}
            </div>
          </div>

          <div className="cartao">
            <div className="cartao__cabecalho"><h3>Todas as Ocorrências</h3></div>
            <div className="cartao__corpo" style={{ maxHeight: 260, overflowY: "auto" }}>
              {ocorrencias.length === 0 ? (
                <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Nenhuma ocorrência registrada ainda.</p>
              ) : ocorrencias.map(oc => (
                <button
                  key={oc.id}
                  onClick={() => setSelecionada(pinos.find(p => p.id === oc.id))}
                  style={{
                    width: "100%", display: "flex", justifyContent: "space-between", alignItems: "center",
                    padding: "8px 10px", borderRadius: 8, border: "1px solid var(--borda)", background: selecionada?.id === oc.id ? "var(--superficie-alt)" : "#fff",
                    marginBottom: 6, cursor: "pointer", textAlign: "left"
                  }}
                >
                  <span style={{ fontSize: 12 }}>{oc.placa} · {oc.transportadora}</span>
                  <span className="selo" style={{ background: COR_NIVEL[oc.nivel] + "22", color: COR_NIVEL[oc.nivel], fontSize: 10.5 }}>{oc.nivel}</span>
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
