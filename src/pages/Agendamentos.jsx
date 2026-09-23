// Módulo de Agendamentos — em desenvolvimento.
// Diferente das outras telas, agendamento não é um dado que se lê e
// mostra: é um dado transacional, criado e disputado por várias pessoas
// ao mesmo tempo (transportadora agenda, terminal confirma, portaria
// consulta). Isso exige um backend com banco de dados e API próprios —
// o BarcaLog hoje é 100% frontend, então esse módulo aguarda essa decisão
// de infraestrutura antes de ganhar funcionalidade real.
export default function Agendamentos() {
  return (
    <div className="cartao">
      <div className="cartao__corpo" style={{ display: "flex", flexDirection: "column", alignItems: "center", textAlign: "center", padding: "48px 24px" }}>
        <span className="material-symbols-outlined" style={{ fontSize: 40, color: "var(--navio-700)", marginBottom: 12 }}>calendar_month</span>
        <h3 style={{ marginBottom: 8 }}>Módulo de Agendamentos</h3>
        <p style={{ fontSize: 13, color: "var(--tinta-suave)", maxWidth: 480 }}>
          Em desenvolvimento. Agendamento é um dado vivo — criado pela transportadora, confirmado pelo terminal,
          consultado na portaria em tempo real — e isso exige um backend com banco de dados e API própria, que o
          BarcaLog ainda não tem (hoje ele é um sistema 100% frontend). A interface entra assim que essa decisão
          de infraestrutura for tomada.
        </p>
      </div>
    </div>
  );
}
