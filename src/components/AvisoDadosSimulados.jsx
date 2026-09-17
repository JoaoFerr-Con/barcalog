// Banner de transparência: deixa explícito quando os dados da tela são
// simulados/de demonstração (cadastros de exemplo, sem persistência em
// banco real) — em contraste com os dados REAIS e consolidados das
// marcações importadas (Unitapajós/TGPM/Hidrovias), que alimentam a Visão
// Geral, o Mapa Operacional (Eficiência) e o relatório PDF.
export default function AvisoDadosSimulados({ children }) {
  return (
    <div className="cartao" style={{ marginBottom: 16 }}>
      <div className="cartao__corpo" style={{ display: "flex", alignItems: "flex-start", gap: 10, background: "#FEF3C7", borderRadius: 10 }}>
        <span className="material-symbols-outlined" style={{ color: "var(--ambar-600)", fontSize: 20, flexShrink: 0 }}>info</span>
        <div style={{ fontSize: 12.5, color: "#7A5A0A" }}>
          <b>Dados de demonstração.</b> {children || "Os cadastros e registros mostrados aqui são simulados, só pra ilustrar o funcionamento do módulo — não vêm da base real de marcações."} Os números da Visão Geral e da Eficiência Operacional, esses sim, são consolidados a partir das 103.325 marcações reais importadas dos terminais.
        </div>
      </div>
    </div>
  );
}
