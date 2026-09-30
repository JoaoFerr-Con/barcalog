import { useState } from "react";
import { useNegativacao } from "../hooks/useNegativacao.js";
import { useAcao } from "../hooks/useAcao.js";
import { Carregando, ErroCarga, Vazio } from "../components/EstadoCarga.jsx";
import { obterSessao } from "../api/cliente.js";
import { TERMINAIS } from "../api/rotulos.js";
import {
  listarVeiculos, cadastrarVeiculo, removerVeiculo,
  listarCondutores, cadastrarCondutor, removerCondutor,
  listarTransportadoras
} from "../data/negativacaoStore.js";

const estiloInput = {
  display: "block", width: "100%", marginTop: 6, padding: "8px 10px",
  borderRadius: 8, border: "1px solid var(--borda)", fontSize: 13, fontFamily: "inherit"
};

const VEICULO_VAZIO = { placa: "", transportadora: "", modelo: "", terminal: "unitapajos" };
const CONDUTOR_VAZIO = { nome: "", transportadora: "", placaVinculada: "" };

// Cadastro de placas e condutores — alvos das regras de negativação.
// Permissões reais ficam no backend: Operador/Gestor cadastram, só Gestor remove.
export default function GestaoFrotas() {
  const { carregado, erro, recarregar } = useNegativacao();
  const papel = obterSessao()?.usuario?.papel;
  const podeCadastrar = papel === "Operador" || papel === "Gestor";
  const podeRemover = papel === "Gestor";
  const transportadoras = listarTransportadoras();
  const veiculos = listarVeiculos();
  const condutores = listarCondutores();

  const [veiculoForm, setVeiculoForm] = useState(VEICULO_VAZIO);
  const [condutorForm, setCondutorForm] = useState(CONDUTOR_VAZIO);
  const [executarVeiculo, salvandoVeiculo] = useAcao();
  const [executarCondutor, salvandoCondutor] = useAcao();
  const [executarRemocao, removendo] = useAcao();

  async function salvarVeiculo(e) {
    e.preventDefault();
    const ok = await executarVeiculo(() => cadastrarVeiculo(veiculoForm), { sucesso: v => `Veículo ${v.placa} cadastrado.`, erro: "Veículo não cadastrado" });
    if (ok) setVeiculoForm(VEICULO_VAZIO);
  }

  async function salvarCondutor(e) {
    e.preventDefault();
    const ok = await executarCondutor(() => cadastrarCondutor(condutorForm), { sucesso: c => `Condutor ${c.nome} cadastrado.`, erro: "Condutor não cadastrado" });
    if (ok) setCondutorForm(CONDUTOR_VAZIO);
  }

  function remover(tipo, id, rotulo) {
    if (!window.confirm(`Remover ${tipo} ${rotulo}? Esta ação não pode ser desfeita.`)) return;
    executarRemocao(() => (tipo === "o veículo" ? removerVeiculo(id) : removerCondutor(id)), { sucesso: `${rotulo} removido.` });
  }

  if (erro && !carregado) return <ErroCarga erro={erro} aoTentarDeNovo={recarregar} />;
  if (!carregado) return <Carregando />;

  return (
    <div className="grade-painel">
      <div className="pilha">
        {podeCadastrar && (
          <form onSubmit={salvarVeiculo} className="cartao">
            <div className="cartao__cabecalho">
              <h3>Cadastrar Veículo</h3>
              <p>Placas monitoradas pelo Sistema de Negativação (padrão antigo ou Mercosul)</p>
            </div>
            <div className="cartao__corpo" style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 12 }}>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Placa
                <input value={veiculoForm.placa} onChange={e => setVeiculoForm(f => ({ ...f, placa: e.target.value.toUpperCase() }))} style={estiloInput} placeholder="ABC-1234" required maxLength={8} pattern="[A-Za-z]{3}-?[0-9][A-Za-z0-9][0-9]{2}" title="ABC-1234 ou ABC1D23" />
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Transportadora
                <select value={veiculoForm.transportadora} onChange={e => setVeiculoForm(f => ({ ...f, transportadora: e.target.value }))} style={estiloInput} required>
                  <option value="">Selecione</option>
                  {transportadoras.map(t => <option key={t.id} value={t.nome}>{t.nome}</option>)}
                </select>
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Terminal
                <select value={veiculoForm.terminal} onChange={e => setVeiculoForm(f => ({ ...f, terminal: e.target.value }))} style={estiloInput}>
                  {TERMINAIS.map(t => <option key={t.id} value={t.id}>{t.nome}</option>)}
                </select>
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Modelo / Tipo
                <input value={veiculoForm.modelo} onChange={e => setVeiculoForm(f => ({ ...f, modelo: e.target.value }))} style={estiloInput} placeholder="Carreta graneleira" maxLength={100} />
              </label>
            </div>
            <div style={{ padding: "0 20px 20px", display: "flex", justifyContent: "flex-end" }}>
              <button type="submit" className="botao botao--primario" disabled={salvandoVeiculo} aria-busy={salvandoVeiculo}>
                {salvandoVeiculo ? "Salvando…" : "Salvar veículo"}
              </button>
            </div>
          </form>
        )}

        <div className="cartao">
          <div className="cartao__cabecalho"><h3>Veículos Cadastrados</h3><p>{veiculos.length} veículo(s)</p></div>
          <div className="cartao__corpo" style={{ overflowX: "auto" }}>
            {veiculos.length === 0 ? <Vazio icone="local_shipping" titulo="Nenhum veículo cadastrado" /> : (
              <table>
                <thead><tr><th scope="col">Placa</th><th scope="col">Transportadora</th><th scope="col">Modelo</th><th scope="col">Situação</th>{podeRemover && <th scope="col"><span className="sr-only">Ações</span></th>}</tr></thead>
                <tbody>
                  {veiculos.map(v => (
                    <tr key={v.id}>
                      <td className="mono">{v.placa}</td>
                      <td>{v.transportadora}</td>
                      <td>{v.modelo}</td>
                      <td style={{ color: v.statusNegativacao === "negativada" ? "var(--vermelho-500)" : "var(--verde-500)", fontWeight: 600 }}>
                        {v.statusNegativacao === "negativada" ? "Negativada" : "Regular"}
                      </td>
                      {podeRemover && (
                        <td><button type="button" className="botao botao--fantasma" disabled={removendo} onClick={() => remover("o veículo", v.id, `Veículo ${v.placa}`)}>Remover</button></td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      </div>

      <div className="pilha">
        {podeCadastrar && (
          <form onSubmit={salvarCondutor} className="cartao">
            <div className="cartao__cabecalho">
              <h3>Cadastrar Condutor</h3>
              <p>Motoristas monitorados pelo Sistema de Negativação (sem CPF — minimização de dados, LGPD)</p>
            </div>
            <div className="cartao__corpo" style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 12 }}>
              <label style={{ fontSize: 12, fontWeight: 600, gridColumn: "1 / -1" }}>
                Nome
                <input value={condutorForm.nome} onChange={e => setCondutorForm(f => ({ ...f, nome: e.target.value }))} style={estiloInput} required minLength={2} maxLength={150} />
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Transportadora
                <select value={condutorForm.transportadora} onChange={e => setCondutorForm(f => ({ ...f, transportadora: e.target.value }))} style={estiloInput} required>
                  <option value="">Selecione</option>
                  {transportadoras.map(t => <option key={t.id} value={t.nome}>{t.nome}</option>)}
                </select>
              </label>
              <label style={{ fontSize: 12, fontWeight: 600 }}>
                Placa vinculada
                <input list="placas-frota-2" value={condutorForm.placaVinculada} onChange={e => setCondutorForm(f => ({ ...f, placaVinculada: e.target.value.toUpperCase() }))} style={estiloInput} maxLength={8} />
                <datalist id="placas-frota-2">
                  {veiculos.map(v => <option key={v.id} value={v.placa} />)}
                </datalist>
              </label>
            </div>
            <div style={{ padding: "0 20px 20px", display: "flex", justifyContent: "flex-end" }}>
              <button type="submit" className="botao botao--primario" disabled={salvandoCondutor} aria-busy={salvandoCondutor}>
                {salvandoCondutor ? "Salvando…" : "Salvar condutor"}
              </button>
            </div>
          </form>
        )}

        <div className="cartao">
          <div className="cartao__cabecalho"><h3>Condutores Cadastrados</h3><p>{condutores.length} condutor(es)</p></div>
          <div className="cartao__corpo" style={{ overflowX: "auto" }}>
            {condutores.length === 0 ? <Vazio icone="badge" titulo="Nenhum condutor cadastrado" /> : (
              <table>
                <thead><tr><th scope="col">Nome</th><th scope="col">Transportadora</th><th scope="col">Placa</th>{podeRemover && <th scope="col"><span className="sr-only">Ações</span></th>}</tr></thead>
                <tbody>
                  {condutores.map(c => (
                    <tr key={c.id}>
                      <td>{c.nome}</td>
                      <td>{c.transportadora}</td>
                      <td className="mono">{c.placaVinculada || "—"}</td>
                      {podeRemover && (
                        <td><button type="button" className="botao botao--fantasma" disabled={removendo} onClick={() => remover("o condutor", c.id, `Condutor ${c.nome}`)}>Remover</button></td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
