// Extraído de App.jsx pra poder ser usado tanto na Visão Geral (dados mock)
// quanto no Relatório Real (dados reais) sem import circular.
export default function CartaoIndicador({ rotulo, valor, icone, corIcone, corFundoIcone, nota, subida }) {
  return (
    <div className="kpi">
      <div className="kpi__topo">
        <span className="kpi__rotulo">{rotulo}</span>
        <div className="kpi__icone" style={{ background: corFundoIcone, color: corIcone }}>
          <span className="material-symbols-outlined" style={{ fontSize: 18 }}>{icone}</span>
        </div>
      </div>
      <div className="kpi__valor">{valor}</div>
      {nota && (
        <div className={`kpi__nota ${subida ? "subida" : ""}`}>
          {subida && <span className="material-symbols-outlined">trending_up</span>}
          {nota}
        </div>
      )}
    </div>
  );
}
