import { useState, useEffect, useCallback, useRef } from "react";

// Hook genérico pra ler dados da API com estados de carregando/erro e
// cancelamento automático (troca de filtro ou tela desmontada não deixa
// resposta velha sobrescrever a nova).
//
// uso: const { dados, carregando, erro, recarregar } = useApi(sinal => api.get(...), [deps]);
export function useApi(buscar, dependencias = [], { intervaloMs } = {}) {
  const [estado, setEstado] = useState({ dados: null, carregando: true, erro: null });
  const buscarRef = useRef(buscar);
  buscarRef.current = buscar;
  const [versao, setVersao] = useState(0);
  const recarregar = useCallback(() => setVersao(v => v + 1), []);

  useEffect(() => {
    const controle = new AbortController();
    setEstado(e => ({ ...e, carregando: true, erro: null }));
    buscarRef.current(controle.signal)
      .then(dados => { if (!controle.signal.aborted) setEstado({ dados, carregando: false, erro: null }); })
      .catch(erro => { if (!controle.signal.aborted && erro?.name !== "AbortError") setEstado(e => ({ ...e, carregando: false, erro })); });
    return () => controle.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...dependencias, versao]);

  useEffect(() => {
    if (!intervaloMs) return undefined;
    const t = setInterval(recarregar, intervaloMs);
    return () => clearInterval(t);
  }, [intervaloMs, recarregar]);

  return { ...estado, recarregar };
}
