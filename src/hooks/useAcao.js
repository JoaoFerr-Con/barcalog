import { useState, useCallback, useRef } from "react";
import { mensagemErro } from "../api/cliente.js";
import { notificar } from "../components/toast.js";

// Executa uma ação que grava na API:
// - bloqueia execução repetida enquanto a anterior roda (clique duplo);
// - mostra toast de sucesso/erro com mensagem amigável;
// - devolve true/false pra tela decidir se limpa o formulário.
// A Idempotency-Key é gerada pelo store a cada ação; se a rede cair no meio,
// o backend garante que o reenvio com a mesma chave não duplica.
export function useAcao() {
  const [emAndamento, setEmAndamento] = useState(false);
  const rodando = useRef(false);

  const executar = useCallback(async (acao, { sucesso, erro: prefixoErro } = {}) => {
    if (rodando.current) return false;
    rodando.current = true;
    setEmAndamento(true);
    try {
      const resultado = await acao();
      if (sucesso) notificar(typeof sucesso === "function" ? sucesso(resultado) : sucesso, "sucesso");
      return resultado ?? true;
    } catch (e) {
      const msg = e?.name === "ErroApi" ? mensagemErro(e) : (e?.message || "Algo deu errado.");
      notificar(prefixoErro ? `${prefixoErro}: ${msg}` : msg, "erro");
      return false;
    } finally {
      rodando.current = false;
      setEmAndamento(false);
    }
  }, []);

  return [executar, emAndamento];
}
