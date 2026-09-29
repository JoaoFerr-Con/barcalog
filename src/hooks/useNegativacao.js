import { useState, useEffect, useCallback } from "react";
import { assinarMudancas, carregar, obterStatusCarga } from "../data/negativacaoStore.js";

// Força re-render sempre que o store de negativação muda e garante que os
// dados da API foram carregados ao abrir a tela. Devolve o estado da carga
// pra tela mostrar "carregando" / erro.
export function useNegativacao() {
  const [, forcar] = useState(0);
  const rerenderizar = useCallback(() => forcar(n => n + 1), []);
  useEffect(() => assinarMudancas(rerenderizar), [rerenderizar]);
  useEffect(() => {
    const { carregado, carregando } = obterStatusCarga();
    if (!carregado && !carregando) carregar();
  }, []);
  return { ...obterStatusCarga(), recarregar: carregar };
}
