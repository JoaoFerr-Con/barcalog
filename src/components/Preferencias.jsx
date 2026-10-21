import { useState } from "react";
import { api, atualizarUsuarioSessao, mensagemErro } from "../api/cliente.js";
import { useApi } from "../hooks/useApi.js";
import Modal from "./Modal.jsx";
import { Aviso, Campo } from "./ui.jsx";

// Preferências pessoais do usuário. São só conveniência (onde abrir, qual
// terminal já vem selecionado): não mudam papel nem vínculo, e o servidor
// recusa uma preferência fora do vínculo.

export default function Preferencias({ usuario, telas, aoFechar }) {
  const terminais = useApi(sinal => api.get("/api/v1/terminais", null, sinal), []);
  const pref = usuario.preferencias || {};
  const [tela, setTela] = useState(pref.telaInicial || "");
  const [terminal, setTerminal] = useState(pref.terminalPreferido || "");
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState(null);
  const [ok, setOk] = useState(false);
  const vinculado = usuario.terminalId;
  const nomeTerminal = id => terminais.dados?.find(t => t.id === id)?.nome || id;
  const rotuloPadrao = telas.find(t => t.chave === pref.telaPadrao)?.rotulo || pref.telaPadrao;

  async function salvar(e) {
    e.preventDefault();
    setSalvando(true); setErro(null); setOk(false);
    try {
      const atualizado = await api.put("/api/v1/auth/me/preferencias", { telaInicial: tela || null, terminalPreferido: terminal || null });
      atualizarUsuarioSessao(atualizado);
      setOk(true);
    } catch (e2) {
      setErro(mensagemErro(e2));
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Modal titulo="Minhas preferências" subtitulo="Só valem para você e não mudam o que você pode acessar." aoFechar={aoFechar}>
      <form className="pilha" style={{ gap: 14 }} onSubmit={salvar}>
        <dl className="definicoes">
          <div><dt>Perfil</dt><dd>{usuario.papel}</dd></div>
          <div><dt>Terminal de acesso</dt><dd>{vinculado ? nomeTerminal(vinculado) : "Todos os terminais"}</dd></div>
        </dl>
        <p style={{ fontSize: 12.5, color: "var(--tinta-suave)", margin: 0 }}>
          Perfil e terminal de acesso são definidos pelo Gestor. {vinculado ? "Todas as consultas mostram apenas o seu terminal — isso é aplicado no servidor." : ""}
        </p>
        <Campo rotulo="Tela ao entrar" ajuda={`Padrão do seu perfil: ${rotuloPadrao}.`}>
          <select value={tela} onChange={e => setTela(e.target.value)}>
            <option value="">Padrão do perfil ({rotuloPadrao})</option>
            {telas.map(t => <option key={t.chave} value={t.chave}>{t.rotulo}</option>)}
          </select>
        </Campo>
        <Campo rotulo="Terminal já selecionado nas telas" ajuda={vinculado ? "Com terminal de acesso definido, só ele pode ser escolhido." : "Você ainda pode trocar o terminal em cada tela."}>
          <select value={terminal} onChange={e => setTerminal(e.target.value)}>
            <option value="">{vinculado ? nomeTerminal(vinculado) : "Todos os terminais"}</option>
            {(terminais.dados || []).filter(t => !vinculado || t.id === vinculado).map(t => <option key={t.id} value={t.id}>{t.nome}</option>)}
          </select>
        </Campo>
        {erro && <Aviso tipo="critico">{erro}</Aviso>}
        {ok && <Aviso tipo="ok">Preferências salvas. A tela inicial vale a partir do próximo acesso.</Aviso>}
        <div className="linha-flex" style={{ justifyContent: "flex-end" }}>
          <button type="button" className="botao botao--fantasma" onClick={aoFechar}>Fechar</button>
          <button type="submit" className="botao botao--primario" disabled={salvando}>{salvando ? "Salvando…" : "Salvar"}</button>
        </div>
      </form>
    </Modal>
  );
}
