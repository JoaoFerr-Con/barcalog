// Módulo de Configurações — em desenvolvimento.
// Regras e parâmetros operacionais (capacidade por terminal, limite de
// congestionamento, janelas de funcionamento) hoje estão fixos no código
// (metricsEngine.js). Uma tela de configuração de verdade, editável pelo
// usuário, também depende de persistência em backend — mudar isso só no
// navegador de uma pessoa não muda a regra pra ninguém mais.
export default function Configuracoes() {
  return (
    <div className="cartao">
      <div className="cartao__corpo" style={{ display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center", padding: "48px 24px" }}>
        <span className="material-symbols-outlined" style={{ fontSize: 40, color: "var(--navio-700)", marginBottom: 12 }}>settings</span>
        <h3 style={{ marginBottom: 8 }}>Configurações</h3>
        <p style={{ fontSize: 13, color: "var(--tinta-suave)", maxWidth: 480 }}>
          Em desenvolvimento. Parâmetros como capacidade por terminal (hoje: 1.000 carretas/dia) e limite de
          congestionamento (hoje: 3 carretas no pátio) estão fixos no código. Uma tela editável exige persistir
          essas regras num lugar central — o mesmo backend do qual os outros módulos em construção dependem.
        </p>
      </div>
    </div>
  );
}
