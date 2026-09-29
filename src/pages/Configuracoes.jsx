// Módulo de Configurações — em desenvolvimento.
// Regras e parâmetros operacionais (capacidade por terminal, limite de
// congestionamento, janelas de funcionamento) hoje ficam no backend
// (appsettings / cadastro de terminais). Uma tela editável precisa de
// endpoints próprios, restritos ao Gestor e auditados.
export default function Configuracoes() {
  return (
    <div className="cartao">
      <div className="cartao__corpo" style={{ display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center", padding: "48px 24px" }}>
        <span className="material-symbols-outlined" style={{ fontSize: 40, color: "var(--navio-700)", marginBottom: 12 }}>settings</span>
        <h3 style={{ marginBottom: 8 }}>Configurações</h3>
        <p style={{ fontSize: 13, color: "var(--tinta-suave)", maxWidth: 480 }}>
          Em desenvolvimento. Parâmetros como capacidade por terminal (hoje: 1.000 carretas/dia) e limite de
          congestionamento (hoje: 3 carretas no pátio) são definidos no servidor. A edição por tela ainda não
          está disponível — alterações são feitas pela equipe técnica, com registro em auditoria.
        </p>
      </div>
    </div>
  );
}
