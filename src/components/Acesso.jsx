import { useState } from "react";
import { QRCodeSVG } from "qrcode.react";
import { entrar, sair, trocarSenha, configurarMfa, ativarMfa } from "../auth/autenticacao.js";
import { mensagemErro } from "../api/cliente.js";

// Telas de acesso compartilhadas pelo painel interno e pelo Portal do
// Transportador: login (com etapa de código MFA), troca de senha provisória
// e cadastro obrigatório do MFA. Toda validação real acontece na API.

const estiloErro = { color: "var(--vermelho-500)", fontSize: 13, margin: 0 };
const estiloAviso = { color: "var(--navio-700)", fontSize: 13, margin: 0, background: "var(--azul-100)", padding: "8px 10px", borderRadius: 8 };

function Campo({ id, rotulo, icone, ...props }) {
  return (
    <div className="campo">
      <label htmlFor={id}>{rotulo}</label>
      <div className="campo-icone">
        <span className="material-symbols-outlined" aria-hidden="true">{icone}</span>
        <input id={id} {...props} />
      </div>
    </div>
  );
}

/**
 * Formulário de login. `aoEntrar(sessao)` é chamado depois do login aceito —
 * quem usa decide se o papel serve pra aquela área (a API também decide).
 */
export function FormularioLogin({ placeholderEmail = "nome@empresa.com", aviso, textoBotao = "Acessar sistema", aoEntrar }) {
  const [email, setEmail] = useState("");
  const [senha, setSenha] = useState("");
  const [codigo, setCodigo] = useState("");
  const [pedeCodigo, setPedeCodigo] = useState(false);
  const [erro, setErro] = useState("");
  const [enviando, setEnviando] = useState(false);

  async function aoEnviar(e) {
    e.preventDefault();
    if (enviando) return;
    setErro("");
    setEnviando(true);
    try {
      const r = await entrar(email, senha, pedeCodigo ? codigo : undefined);
      if (r.ok) {
        setSenha("");
        aoEntrar?.(r.sessao);
      } else if (r.mfa) {
        setPedeCodigo(true);
      } else {
        // Mensagem genérica do servidor: não revela se o e-mail existe.
        setErro(mensagemErro(r.erro));
        if (pedeCodigo) setCodigo("");
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <form onSubmit={aoEnviar} style={{ display: "flex", flexDirection: "column", gap: 18 }} noValidate={false}>
      {aviso && <p role="status" style={estiloAviso}>{aviso}</p>}
      <Campo id="login-email" rotulo="E-mail" icone="mail" type="email" autoComplete="username" value={email}
        onChange={e => setEmail(e.target.value)} placeholder={placeholderEmail} required maxLength={200} disabled={pedeCodigo} />
      <Campo id="login-senha" rotulo="Senha" icone="lock" type="password" autoComplete="current-password" value={senha}
        onChange={e => setSenha(e.target.value)} placeholder="••••••••" required maxLength={128} disabled={pedeCodigo} />
      {pedeCodigo && (
        <Campo id="login-codigo" rotulo="Código do app autenticador" icone="pin" inputMode="numeric" autoComplete="one-time-code"
          pattern="[0-9]{6}" maxLength={6} value={codigo} onChange={e => setCodigo(e.target.value.replace(/\D/g, ""))}
          placeholder="000000" required autoFocus />
      )}
      {erro && <p role="alert" style={estiloErro}>{erro}</p>}
      <button type="submit" className="botao botao--primario" style={{ padding: "13px 18px" }} disabled={enviando}>
        {enviando ? "Entrando…" : pedeCodigo ? "Confirmar código" : textoBotao}
      </button>
      {pedeCodigo && (
        <button type="button" className="botao botao--fantasma" onClick={() => { setPedeCodigo(false); setCodigo(""); setSenha(""); setErro(""); }}>
          Voltar
        </button>
      )}
      <p style={{ textAlign: "center", fontSize: 12, color: "var(--tinta-suave)", margin: 0 }}>
        Esqueceu a senha? Peça a um gestor para gerar uma senha provisória.
      </p>
    </form>
  );
}

/** Senha provisória: o token só permite trocar a senha. */
function TrocarSenha() {
  const [atual, setAtual] = useState("");
  const [nova, setNova] = useState("");
  const [confirmacao, setConfirmacao] = useState("");
  const [erro, setErro] = useState("");
  const [enviando, setEnviando] = useState(false);

  async function aoEnviar(e) {
    e.preventDefault();
    setErro("");
    if (nova !== confirmacao) return setErro("A confirmação não confere com a nova senha.");
    setEnviando(true);
    try {
      await trocarSenha(atual, nova); // sucesso encerra a sessão e volta pro login
    } catch (err) {
      setErro(mensagemErro(err));
      setEnviando(false);
    }
  }

  return (
    <form onSubmit={aoEnviar} style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <p style={{ fontSize: 13, color: "var(--tinta-suave)", margin: 0 }}>
        Sua senha é provisória. Defina uma senha nova para continuar (mínimo de 12 caracteres, sem ser uma senha comum).
      </p>
      <Campo id="ts-atual" rotulo="Senha atual" icone="lock" type="password" autoComplete="current-password" value={atual} onChange={e => setAtual(e.target.value)} required maxLength={128} />
      <Campo id="ts-nova" rotulo="Nova senha" icone="key" type="password" autoComplete="new-password" value={nova} onChange={e => setNova(e.target.value)} required minLength={12} maxLength={128} />
      <Campo id="ts-conf" rotulo="Confirmar nova senha" icone="key" type="password" autoComplete="new-password" value={confirmacao} onChange={e => setConfirmacao(e.target.value)} required minLength={12} maxLength={128} />
      {erro && <p role="alert" style={estiloErro}>{erro}</p>}
      <button type="submit" className="botao botao--primario" disabled={enviando}>{enviando ? "Salvando…" : "Trocar senha"}</button>
    </form>
  );
}

/** MFA obrigatório (ex.: Gestor): gera o segredo, mostra o QR uma vez e confirma com um código. */
function ConfigurarMfa() {
  const [senha, setSenha] = useState("");
  const [config, setConfig] = useState(null);
  const [codigo, setCodigo] = useState("");
  const [erro, setErro] = useState("");
  const [enviando, setEnviando] = useState(false);

  async function gerar(e) {
    e.preventDefault();
    setErro("");
    setEnviando(true);
    try {
      setConfig(await configurarMfa(senha));
      setSenha("");
    } catch (err) {
      setErro(mensagemErro(err));
    } finally {
      setEnviando(false);
    }
  }

  async function confirmar(e) {
    e.preventDefault();
    setErro("");
    setEnviando(true);
    try {
      await ativarMfa(codigo); // sucesso encerra a sessão e volta pro login
    } catch (err) {
      setErro(mensagemErro(err));
      setEnviando(false);
    }
  }

  if (!config) {
    return (
      <form onSubmit={gerar} style={{ display: "flex", flexDirection: "column", gap: 16 }}>
        <p style={{ fontSize: 13, color: "var(--tinta-suave)", margin: 0 }}>
          Seu perfil exige verificação em duas etapas. Confirme sua senha para gerar o código do app autenticador
          (Google Authenticator, Microsoft Authenticator, Aegis…).
        </p>
        <Campo id="mfa-senha" rotulo="Senha atual" icone="lock" type="password" autoComplete="current-password" value={senha} onChange={e => setSenha(e.target.value)} required maxLength={128} />
        {erro && <p role="alert" style={estiloErro}>{erro}</p>}
        <button type="submit" className="botao botao--primario" disabled={enviando}>{enviando ? "Gerando…" : "Gerar QR code"}</button>
      </form>
    );
  }

  return (
    <form onSubmit={confirmar} style={{ display: "flex", flexDirection: "column", gap: 16, alignItems: "stretch" }}>
      <p style={{ fontSize: 13, color: "var(--tinta-suave)", margin: 0 }}>
        Escaneie o QR code no app autenticador e digite o código de 6 dígitos. Ele aparece <b>só esta vez</b>.
      </p>
      <div style={{ display: "flex", justifyContent: "center", padding: 12, background: "#fff", borderRadius: 12 }}>
        <QRCodeSVG value={config.uriOtpauth} size={180} />
      </div>
      <details>
        <summary style={{ fontSize: 12, cursor: "pointer" }}>Não consigo escanear</summary>
        <p className="mono" style={{ fontSize: 12, wordBreak: "break-all", marginTop: 6 }}>{config.segredo}</p>
      </details>
      <Campo id="mfa-codigo" rotulo="Código de 6 dígitos" icone="pin" inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6}
        value={codigo} onChange={e => setCodigo(e.target.value.replace(/\D/g, ""))} required autoFocus />
      {erro && <p role="alert" style={estiloErro}>{erro}</p>}
      <button type="submit" className="botao botao--primario" disabled={enviando}>{enviando ? "Confirmando…" : "Ativar"}</button>
    </form>
  );
}

/** Tela mostrada quando o token veio com restrição (trocar-senha / configurar-mfa). */
export function TelaRestricao({ sessao }) {
  const titulo = sessao.restricao === "trocar-senha" ? "Defina sua senha" : "Ative a verificação em duas etapas";
  return (
    <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", padding: 16, background: "var(--bg)" }}>
      <div className="cartao" style={{ width: "100%", maxWidth: 420 }}>
        <div className="cartao__cabecalho">
          <h3>{titulo}</h3>
          <p>{sessao.usuario?.email}</p>
        </div>
        <div className="cartao__corpo" style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {sessao.restricao === "trocar-senha" ? <TrocarSenha /> : <ConfigurarMfa />}
          <button type="button" className="botao botao--fantasma" onClick={sair}>Sair</button>
        </div>
      </div>
    </div>
  );
}
