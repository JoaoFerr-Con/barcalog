import { useState } from "react";
import { useNegativacao } from "../hooks/useNegativacao.js";
import { notificar } from "../components/toast.js";
import {
  listarVeiculos, cadastrarVeiculo, removerVeiculo,
  listarCondutores, cadastrarCondutor, removerCondutor,
  listarTransportadoras
} from "../data/negativacaoStore.js";

const estiloInput = {
  display: "block", width: "100%", marginTop: 6, padding: "8px 10px",
  borderRadius: 8, border: "1px solid var(--borda)", fontSize: 13, fontFamily: "inherit"
};

// Substitui a antiga tela "Cadastrar Carga". O Sistema de Negativação não
// mira a carga — mira placa de veículo e CPF de motorista, então é isso
// que se cadastra e monitora aqui.
export default function GestaoFrotas() {
  useNegativacao();
  const transportadoras = listarTransportadoras();
  const veiculos = listarVeiculos();
  const condutores = listarCondutores();

  const [veiculoForm, setVeiculoForm] = useState({ placa: "", transportadora: "", modelo: "" });
  const [condutorForm, setCondutorForm] = useState({ nome: "", cpf: "", transportadora: "", placaVinculada: "" });

  function salvarVeiculo(e) {
    e.preventDefault();
    if (!veiculoForm.placa || !veiculoForm.transportadora) return;
    cadastrarVeiculo(veiculoForm);
    notificar(`Veículo ${veiculoForm.placa} cadastrado.`, "sucesso");
    setVeiculoForm({ placa: "", transportadora: "", modelo: "" });
  }

  function salvarCondutor(e) {
    e.preventDefault();
    if (!condutorForm.nome || !condutorForm.cpf || !condutorForm.transportadora) return;
    cadastrarCondutor(condutorForm);
    notificar(`Condutor ${condutorForm.nome} cadastrado.`, "sucesso");
    setCondutorForm({ nome: "", cpf: "", transportadora: "", placaVinculada: "" });
  }

  return (
    <div className="grade-painel">
      <div className="pilha">
        <form onSubmit={salvarVeiculo} className="cartao">
          <div className="cartao__cabecalho">
            <h3>Cadastrar Veículo</h3>
            <p>Placas monitoradas pelo Sistema de Negativação</p>
          </div>
          <div className="cartao__corpo" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
            <label style={{ fontSize: 12, fontWeight: 600 }}>
              Placa
              <input value={veiculoForm.placa} onChange={e => setVeiculoForm(f => ({ ...f, placa: e.target.value.toUpperCase() }))} style={estiloInput} placeholder="ABC-1234" required />
            </label>
            <label style={{ fontSize: 12, fontWeight: 600 }}>
              Transportadora
              <select value={veiculoForm.transportadora} onChange={e => setVeiculoForm(f => ({ ...f, transportadora: e.target.value }))} style={estiloInput} required>
                <option value="">Selecione</option>
                {transportadoras.map(t => <option key={t.id} value={t.nome}>{t.nome}</option>)}
              </select>
            </label>
            <label style={{ fontSize: 12, fontWeight: 600, gridColumn: "1 / -1" }}>
              Modelo / Tipo
              <input value={veiculoForm.modelo} onChange={e => setVeiculoForm(f => ({ ...f, modelo: e.target.value }))} style={estiloInput} placeholder="Carreta graneleira" />
            </label>
          </div>
          <div style={{ padding: "0 20px 20px", display: "flex", justifyContent: "flex-end" }}>
            <button type="submit" className="botao botao--primario">Salvar veículo</button>
          </div>
        </form>

        <div className="cartao">
          <div className="cartao__cabecalho"><h3>Veículos Cadastrados</h3></div>
          <div className="cartao__corpo" style={{ overflowX: "auto" }}>
            <table>
              <thead><tr><th>Placa</th><th>Transportadora</th><th>Modelo</th><th></th></tr></thead>
              <tbody>
                {veiculos.map(v => (
                  <tr key={v.id}>
                    <td className="mono">{v.placa}</td>
                    <td>{v.transportadora}</td>
                    <td>{v.modelo}</td>
                    <td><button className="botao botao--fantasma" onClick={() => { removerVeiculo(v.id); notificar(`Veículo ${v.placa} removido.`, "info"); }}>Remover</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <div className="pilha">
        <form onSubmit={salvarCondutor} className="cartao">
          <div className="cartao__cabecalho">
            <h3>Cadastrar Condutor</h3>
            <p>CPFs monitorados pelo Sistema de Negativação</p>
          </div>
          <div className="cartao__corpo" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
            <label style={{ fontSize: 12, fontWeight: 600, gridColumn: "1 / -1" }}>
              Nome
              <input value={condutorForm.nome} onChange={e => setCondutorForm(f => ({ ...f, nome: e.target.value }))} style={estiloInput} required />
            </label>
            <label style={{ fontSize: 12, fontWeight: 600 }}>
              CPF
              <input value={condutorForm.cpf} onChange={e => setCondutorForm(f => ({ ...f, cpf: e.target.value }))} style={estiloInput} placeholder="000.000.000-00" required />
            </label>
            <label style={{ fontSize: 12, fontWeight: 600 }}>
              Transportadora
              <select value={condutorForm.transportadora} onChange={e => setCondutorForm(f => ({ ...f, transportadora: e.target.value }))} style={estiloInput} required>
                <option value="">Selecione</option>
                {transportadoras.map(t => <option key={t.id} value={t.nome}>{t.nome}</option>)}
              </select>
            </label>
            <label style={{ fontSize: 12, fontWeight: 600, gridColumn: "1 / -1" }}>
              Placa vinculada
              <input list="placas-frota-2" value={condutorForm.placaVinculada} onChange={e => setCondutorForm(f => ({ ...f, placaVinculada: e.target.value }))} style={estiloInput} />
              <datalist id="placas-frota-2">
                {veiculos.map(v => <option key={v.id} value={v.placa} />)}
              </datalist>
            </label>
          </div>
          <div style={{ padding: "0 20px 20px", display: "flex", justifyContent: "flex-end" }}>
            <button type="submit" className="botao botao--primario">Salvar condutor</button>
          </div>
        </form>

        <div className="cartao">
          <div className="cartao__cabecalho"><h3>Condutores Cadastrados</h3></div>
          <div className="cartao__corpo" style={{ overflowX: "auto" }}>
            <table>
              <thead><tr><th>Nome</th><th>CPF</th><th>Transportadora</th><th>Placa</th><th></th></tr></thead>
              <tbody>
                {condutores.map(c => (
                  <tr key={c.id}>
                    <td>{c.nome}</td>
                    <td className="mono">{c.cpf}</td>
                    <td>{c.transportadora}</td>
                    <td className="mono">{c.placaVinculada}</td>
                    <td><button className="botao botao--fantasma" onClick={() => { removerCondutor(c.id); notificar(`Condutor ${c.nome} removido.`, "info"); }}>Remover</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
