import { EMPRESAS } from "../data/registry.js";

export default function SeletorEmpresa({ valor, aoMudar }) {
  return (
    <select
      value={valor}
      onChange={e => aoMudar(e.target.value)}
      style={{
        padding: "8px 12px",
        borderRadius: 10,
        border: "1px solid var(--borda)",
        background: "var(--superficie)",
        fontSize: 13,
        fontWeight: 600,
        color: "var(--tinta)"
      }}
    >
      <option value="todas">Todas as empresas</option>
      {EMPRESAS.map(e => (
        <option key={e.id} value={e.id}>{e.nome}</option>
      ))}
    </select>
  );
}
