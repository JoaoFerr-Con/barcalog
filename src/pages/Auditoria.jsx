import { useState, useMemo } from "react";
import { useAuditoria } from "../hooks/useAuditoria.js";
import { listarLogs } from "../data/auditoriaStore.js";
import { exportarCSV } from "../utils/exportar.js";

export default function Auditoria() {
  useAuditoria();
  const [busca, setBusca] = useState("");
  const logs = useMemo(() => listarLogs(busca), [busca]);

  function exportar() {
    exportarCSV(
      logs,
      [
        { rotulo: "Data/Hora", valor: l => new Date(l.quando).toLocaleString("pt-BR") },
        { rotulo: "Autor", chave: "autor" },
        { rotulo: "Ação", chave: "acao" },
        { rotulo: "Detalhes", chave: "detalhes" }
      ],
      "auditoria_barcalog"
    );
  }

  return (
    <div className="cartao">
      <div className="cartao__cabecalho" style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 12 }}>
        <div>
          <h3>Log de Auditoria</h3>
          <p>{logs.length.toLocaleString("pt-BR")} registro(s) — quem fez o quê e quando, em toda a aplicação</p>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <input
            value={busca}
            onChange={e => setBusca(e.target.value)}
            placeholder="Buscar por autor, ação ou detalhe..."
            style={{ padding: "8px 12px", borderRadius: 10, border: "1px solid var(--borda)", fontSize: 13, minWidth: 220 }}
          />
          <button className="botao botao--fantasma botao-exportar" onClick={exportar}>
            <span className="material-symbols-outlined" style={{ fontSize: 16 }}>download</span> Exportar CSV
          </button>
        </div>
      </div>
      <div className="cartao__corpo" style={{ overflowX: "auto" }}>
        {logs.length === 0 ? (
          <p style={{ fontSize: 13, color: "var(--tinta-suave)" }}>Nenhum registro de auditoria ainda.</p>
        ) : (
          <table>
            <thead>
              <tr><th>Data/Hora</th><th>Autor</th><th>Ação</th><th>Detalhes</th></tr>
            </thead>
            <tbody>
              {logs.map(l => (
                <tr key={l.id}>
                  <td className="mono">{new Date(l.quando).toLocaleString("pt-BR")}</td>
                  <td>{l.autor}</td>
                  <td>{l.acao}</td>
                  <td style={{ maxWidth: 360 }}>{l.detalhes}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
