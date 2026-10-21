import { useEffect, useState } from "react";
import { api } from "../api/cliente.js";
import { useApi } from "../hooks/useApi.js";
import { useAcao } from "../hooks/useAcao.js";
import { Carregando, ErroCarga, Vazio } from "../components/EstadoCarga.jsx";
import { Cartao, Selo, Aviso, Campo, Abas } from "../components/ui.jsx";
import { dataHoraLocal } from "../utils/formatar.js";

// Configurações operacionais: capacidade dos terminais, limites usados pelos
// indicadores e alertas, e revisão dos convênios. Leitura para todos os perfis
// internos; edição só para Gestor (o backend valida e registra em auditoria).

const PARAMETROS = [
  ["limiteEsperaMedianaHoras", "Espera mediana — limite de alerta", "horas", "Alerta histórico quando a mediana do último mês completo passa deste valor.", 0.5],
  ["metaLiberadas24hPct", "Meta de liberadas em até 24 h", "%", "Referência mostrada junto do indicador.", 1],
  ["limiteEsperaLongaHoras", "Espera longa", "horas", "Carretas acima disso entram em \"esperas longas\" e geram alerta.", 1],
  ["diasSemDadosAlerta", "Dias sem dados para alertar", "dias", "Alerta de dados desatualizados quando um terminal fica este tempo sem marcações importadas.", 1],
  ["limiteRazaoChegadaLiberacao", "Limite de risco de fila", "× a capacidade de atendimento", "Na estimativa das próximas horas, a janela vira \"risco de fila\" quando a demanda prevista chega a este múltiplo da capacidade de atendimento (1,4 = 40% acima). Entre a capacidade e este limite, a janela fica em \"atenção\".", 0.1]
];

export default function Configuracoes({ papel }) {
  const gestor = papel === "Gestor";
  const [aba, setAba] = useState("capacidade");
  const config = useApi(sinal => api.get("/api/v1/configuracoes", null, sinal), []);

  return (
    <div className="pilha">
      {!gestor && <Aviso tipo="info">Somente leitura. Alterações são feitas pelo perfil Gestor e ficam registradas na auditoria.</Aviso>}
      <Abas rotulo="Seções de configuração" ativa={aba} aoMudar={setAba}
        abas={[{ id: "capacidade", rotulo: "Capacidade dos terminais" }, { id: "parametros", rotulo: "Limites e metas" }, { id: "convenios", rotulo: "Convênios" },
          ...(gestor ? [{ id: "usuarios", rotulo: "Acesso por terminal" }] : [])]} />
      {aba === "convenios" ? <Convenios gestor={gestor} />
        : aba === "usuarios" && gestor ? <AcessoTerminal terminais={config.dados?.terminais || []} />
        : config.erro ? <ErroCarga erro={config.erro} aoTentarDeNovo={config.recarregar} />
        : !config.dados ? <Carregando />
        : aba === "capacidade" ? <Capacidades terminais={config.dados.terminais} gestor={gestor} aoSalvar={config.recarregar} />
        : <Parametros p={config.dados.parametros} gestor={gestor} aoSalvar={config.recarregar} />}
    </div>
  );
}

function Capacidades({ terminais, gestor, aoSalvar }) {
  return (
    <Cartao id="cap" titulo="Capacidade dos terminais"
      subtitulo="Sem capacidade informada, o painel não calcula ocupação e a agenda não compara agendamentos com limite — nenhum valor é presumido.">
      <div className="tabela-wrap">
        <table style={{ minWidth: 640 }}>
          <thead><tr><th scope="col">Terminal</th><th scope="col">Carretas por dia</th><th scope="col">Carretas por hora</th>{gestor && <th scope="col"><span className="sr-only">Ações</span></th>}</tr></thead>
          <tbody>{terminais.map(t => <LinhaCapacidade key={t.id} t={t} gestor={gestor} aoSalvar={aoSalvar} />)}</tbody>
        </table>
      </div>
    </Cartao>
  );
}

function LinhaCapacidade({ t, gestor, aoSalvar }) {
  const [dia, setDia] = useState(t.capacidadeDiariaCarretas ?? "");
  const [hora, setHora] = useState(t.capacidadeHoraria ?? "");
  const [executar, salvando] = useAcao();
  useEffect(() => { setDia(t.capacidadeDiariaCarretas ?? ""); setHora(t.capacidadeHoraria ?? ""); }, [t]);
  const mudou = String(dia) !== String(t.capacidadeDiariaCarretas ?? "") || String(hora) !== String(t.capacidadeHoraria ?? "");
  const vazio = <Selo tipo="atencao">não informada</Selo>;

  function salvar() {
    const corpo = { capacidadeDiariaCarretas: dia === "" ? null : Number(dia), capacidadeHoraria: hora === "" ? null : Number(hora) };
    executar(() => api.put(`/api/v1/configuracoes/terminais/${t.id}`, corpo), { sucesso: `Capacidade de ${t.nome} salva.` }).then(ok => { if (ok) aoSalvar(); });
  }

  return (
    <tr>
      <th scope="row">{t.nome}</th>
      <td>{gestor ? <input type="number" min={1} max={100000} value={dia} onChange={e => setDia(e.target.value)} aria-label={`Carretas por dia — ${t.nome}`} placeholder="não informada" style={{ width: 140 }} /> : (t.capacidadeDiariaCarretas ?? vazio)}</td>
      <td>{gestor ? <input type="number" min={1} max={10000} value={hora} onChange={e => setHora(e.target.value)} aria-label={`Carretas por hora — ${t.nome}`} placeholder="não informada" style={{ width: 140 }} /> : (t.capacidadeHoraria ?? vazio)}</td>
      {gestor && <td><button type="button" className="botao botao--secundario botao--pequeno" disabled={!mudou || salvando} onClick={salvar}>Salvar</button></td>}
    </tr>
  );
}

function Parametros({ p, gestor, aoSalvar }) {
  const [valores, setValores] = useState(p);
  const [executar, salvando] = useAcao();
  useEffect(() => setValores(p), [p]);
  const mudou = PARAMETROS.some(([k]) => String(valores[k]) !== String(p[k]));

  function salvar(e) {
    e.preventDefault();
    const corpo = Object.fromEntries(PARAMETROS.map(([k]) => [k, Number(valores[k])]));
    executar(() => api.put("/api/v1/configuracoes/parametros", corpo), { sucesso: "Parâmetros salvos. Os alertas usam os novos valores na próxima avaliação." }).then(ok => { if (ok) aoSalvar(); });
  }

  return (
    <Cartao id="par" titulo="Limites e metas" subtitulo={p.atualizadoEm ? `Última alteração: ${dataHoraLocal(p.atualizadoEm)} por ${p.atualizadoPor}` : "Valores padrão de instalação — revise com a operação."}>
      <form onSubmit={salvar} className="pilha" style={{ gap: 14, maxWidth: 640 }}>
        {PARAMETROS.map(([k, rotulo, unidade, ajuda, passo]) => (
          <Campo key={k} rotulo={`${rotulo} (${unidade})`} ajuda={ajuda}>
            <input type="number" step={passo} value={valores[k]} disabled={!gestor} required
              onChange={e => setValores(v => ({ ...v, [k]: e.target.value }))} style={{ maxWidth: 180 }} />
          </Campo>
        ))}
        {gestor && <div><button type="submit" className="botao botao--primario" disabled={!mudou || salvando}>Salvar parâmetros</button></div>}
      </form>
    </Cartao>
  );
}

function Convenios({ gestor }) {
  const r = useApi(sinal => api.get("/api/v1/convenios", null, sinal), []);
  const [soDeduzidos, setSoDeduzidos] = useState(true);
  if (r.erro) return <ErroCarga erro={r.erro} aoTentarDeNovo={r.recarregar} />;
  if (!r.dados) return <Carregando />;
  const lista = r.dados.filter(c => !soDeduzidos || c.deduzido);
  return (
    <Cartao id="conv" titulo="Convênios"
      subtitulo="Operador e carga de convênios novos são deduzidos do nome na importação. Confirme ou corrija: os relatórios por operador e carga usam estes valores."
      acao={<label className="linha-flex" style={{ fontSize: 13, gap: 6 }}><input type="checkbox" checked={soDeduzidos} onChange={e => setSoDeduzidos(e.target.checked)} /> Só os não revisados ({r.dados.filter(c => c.deduzido).length})</label>}>
      {!lista.length ? <Vazio icone="task_alt" titulo={soDeduzidos ? "Todos os convênios já foram revisados" : "Nenhum convênio cadastrado"} /> : (
        <div className="tabela-wrap">
          <table style={{ minWidth: 720 }}>
            <thead><tr><th scope="col">Código</th><th scope="col">Nome na planilha</th><th scope="col">Operador</th><th scope="col">Carga</th><th scope="col">Situação</th>{gestor && <th scope="col"><span className="sr-only">Ações</span></th>}</tr></thead>
            <tbody>{lista.map(c => <LinhaConvenio key={c.codigo} c={c} gestor={gestor} aoSalvar={r.recarregar} />)}</tbody>
          </table>
        </div>
      )}
    </Cartao>
  );
}

function LinhaConvenio({ c, gestor, aoSalvar }) {
  const [operador, setOperador] = useState(c.operador);
  const [carga, setCarga] = useState(c.carga);
  const [executar, salvando] = useAcao();
  function salvar() {
    executar(() => api.put(`/api/v1/convenios/${encodeURIComponent(c.codigo)}`, { operador: operador.trim(), carga: carga.trim() }), { sucesso: `Convênio ${c.codigo} revisado.` }).then(ok => { if (ok) aoSalvar(); });
  }
  return (
    <tr>
      <td className="id-mono">{c.codigo}</td>
      <td>{c.nome}</td>
      <td>{gestor ? <input value={operador} maxLength={50} onChange={e => setOperador(e.target.value)} aria-label={`Operador do convênio ${c.codigo}`} /> : c.operador}</td>
      <td>{gestor ? <input value={carga} maxLength={50} onChange={e => setCarga(e.target.value)} aria-label={`Carga do convênio ${c.codigo}`} /> : c.carga}</td>
      <td>{c.deduzido ? <Selo tipo="atencao">deduzido do nome</Selo> : <Selo tipo="ok">revisado</Selo>}</td>
      {gestor && <td><button type="button" className="botao botao--secundario botao--pequeno" disabled={salvando || !operador.trim() || !carga.trim()} onClick={salvar}>{c.deduzido ? "Confirmar" : "Salvar"}</button></td>}
    </tr>
  );
}

/**
 * Vínculo de usuários internos a um terminal (Gestor). É permissão, não preferência:
 * aplicado no servidor em todas as consultas por terminal, auditado, e encerra as
 * sessões do usuário (o vínculo vai no token). Usuários do Portal não aparecem aqui.
 */
function AcessoTerminal({ terminais }) {
  const usuarios = useApi(sinal => api.get("/api/v1/usuarios", { tamanhoPagina: 200, ativo: true }, sinal), []);
  if (usuarios.erro) return <ErroCarga erro={usuarios.erro} aoTentarDeNovo={usuarios.recarregar} />;
  if (!usuarios.dados) return <Carregando />;
  const internos = usuarios.dados.itens.filter(u => u.papel !== "Transportadora");
  return (
    <Cartao id="acesso" titulo="Acesso por terminal"
      subtitulo="Usuário vinculado vê e trata só os dados do seu terminal — o servidor aplica em todas as consultas. Sem vínculo, vê todos. Alterar encerra as sessões do usuário.">
      {!internos.length ? <Vazio icone="group" titulo="Nenhum usuário interno ativo" /> : (
        <div className="tabela-wrap">
          <table style={{ minWidth: 640 }}>
            <thead><tr><th scope="col">Usuário</th><th scope="col">Perfil</th><th scope="col">Terminal de acesso</th><th scope="col"><span className="sr-only">Ações</span></th></tr></thead>
            <tbody>{internos.map(u => <LinhaAcesso key={u.id} u={u} terminais={terminais} aoSalvar={usuarios.recarregar} />)}</tbody>
          </table>
        </div>
      )}
    </Cartao>
  );
}

function LinhaAcesso({ u, terminais, aoSalvar }) {
  const [terminal, setTerminal] = useState(u.terminalId || "");
  const [executar, salvando] = useAcao();
  useEffect(() => setTerminal(u.terminalId || ""), [u]);
  const mudou = terminal !== (u.terminalId || "");
  return (
    <tr>
      <td><b>{u.nome}</b><br /><span className="fraco" style={{ fontSize: 12 }}>{u.email}</span></td>
      <td>{u.papel}</td>
      <td>
        <select value={terminal} onChange={e => setTerminal(e.target.value)} aria-label={`Terminal de acesso de ${u.nome}`}>
          <option value="">Todos os terminais</option>
          {terminais.map(t => <option key={t.id} value={t.id}>{t.nome}</option>)}
        </select>
      </td>
      <td>
        <button type="button" className="botao botao--primario" disabled={!mudou || salvando}
          onClick={() => executar(async () => { await api.put(`/api/v1/usuarios/${u.id}/terminal`, { terminalId: terminal || null }); aoSalvar(); },
            { sucesso: `Acesso de ${u.nome} alterado. As sessões desse usuário foram encerradas.` })}>
          {salvando ? "Salvando…" : "Salvar"}
        </button>
      </td>
    </tr>
  );
}
