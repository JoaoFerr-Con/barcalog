import { useState, useEffect } from "react";
import { useApi } from "../hooks/useApi.js";
import { api } from "../api/cliente.js";
import { exportarCSV } from "../utils/exportar.js";
import { Carregando, ErroCarga, Vazio } from "../components/EstadoCarga.jsx";

const POR_PAGINA = 50;

// Log de auditoria gerado automaticamente pelo backend em toda gravação.
// Busca e paginação são feitas no servidor.
export default function Auditoria() {
  const [busca, setBusca] = useState("");
  const [termo, setTermo] = useState("");
  const [pagina, setPagina] = useState(1);

  // Espera o usuário parar de digitar (e exige 3+ letras, como a API) antes de buscar.
  useEffect(() => {
    const t = setTimeout(() => {
      setTermo(busca.trim().length >= 3 ? busca.trim() : "");
      setPagina(1);
    }, 400);
    return () => clearTimeout(t);
  }, [busca]);

  const { dados, carregando, erro, recarregar } = useApi(
    sinal => api.get("/api/v1/auditoria", { texto: termo, pagina, tamanhoPagina: POR_PAGINA }, sinal),
    [termo, pagina]
  );
  const logs = dados?.itens ?? [];

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
          <p>{dados ? `${dados.total.toLocaleString("pt-BR")} registro(s)` : "…"} — quem fez o quê e quando, gerado automaticamente pelo servidor</p>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
          <input
            value={busca}
            onChange={e => setBusca(e.target.value)}
            placeholder="Buscar por autor, ação ou detalhe (3+ letras)…"
            aria-label="Buscar no log de auditoria"
            maxLength={100}
            style={{ padding: "8px 12px", borderRadius: 10, border: "1px solid var(--borda)", fontSize: 13, minWidth: 220 }}
          />
          <button type="button" className="botao botao--fantasma botao-exportar" onClick={exportar} disabled={logs.length === 0}>
            <span className="material-symbols-outlined" aria-hidden="true" style={{ fontSize: 16 }}>download</span> Exportar página (CSV)
          </button>
        </div>
      </div>
      <div className="cartao__corpo" style={{ overflowX: "auto" }}>
        {erro && <ErroCarga erro={erro} aoTentarDeNovo={recarregar} />}
        {carregando && !dados ? <Carregando /> : logs.length === 0 ? (
          <Vazio icone="history" titulo={termo ? "Nada encontrado" : "Nenhum registro de auditoria ainda"} />
        ) : (
          <>
            <table>
              <thead>
                <tr><th scope="col">Data/Hora</th><th scope="col">Autor</th><th scope="col">Ação</th><th scope="col">Detalhes</th></tr>
              </thead>
              <tbody>
                {logs.map(l => (
                  <tr key={l.id}>
                    <td className="mono">{new Date(l.quando).toLocaleString("pt-BR")}</td>
                    <td>{l.autor}</td>
                    <td>{l.acao}</td>
                    <td style={{ maxWidth: 420, whiteSpace: "pre-line", fontSize: 12 }}>{l.detalhes}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="paginacao" style={{ marginTop: 12 }}>
              <span style={{ fontSize: 12.5, color: "var(--tinta-suave)" }}>Página {dados.numeroPagina} de {Math.max(1, dados.totalPaginas)}</span>
              <div className="paginacao__paginas">
                <button type="button" className="pagina-btn" aria-label="Página anterior" disabled={pagina <= 1 || carregando} onClick={() => setPagina(p => p - 1)}>‹</button>
                <button type="button" className="pagina-btn" aria-label="Próxima página" disabled={pagina >= dados.totalPaginas || carregando} onClick={() => setPagina(p => p + 1)}>›</button>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
