import { useState, useEffect, useCallback } from "react";
import { assinarMudancas } from "../data/negativacaoStore.js";

// Força re-render sempre que o store de negativação muda (registro de
// ocorrência, contestação respondida, etc), em qualquer tela que use isso.
export function useNegativacao() {
  const [, forcar] = useState(0);
  const rerenderizar = useCallback(() => forcar(n => n + 1), []);
  useEffect(() => assinarMudancas(rerenderizar), [rerenderizar]);
}
