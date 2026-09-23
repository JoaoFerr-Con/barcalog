
export default function Agendamentos() {
  return (
    <div className="cartao">
      <div className="cartao__corpo" style={{ display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center", padding: "48px 24px" }}>
        <span className="material-symbols-outlined" style={{ fontSize: 40, color: "var(--navio-700)", marginBottom: 12 }}>calendar_month</span>
        <h3 style={{ marginBottom: 8 }}>Módulo de Agendamentos</h3>
        <p style={{ fontSize: 13, color: "var(--tinta-suave)", maxWidth: 480 }}>
          Em desenvolvimento. Agendamento é o dado criado pela transportadora, confirmado pelo terminal,
          consultado na portaria em tempo real. 
        </p>
      </div>
    </div>
  );
}
