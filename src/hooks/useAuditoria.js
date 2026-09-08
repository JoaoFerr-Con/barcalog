import { useState, useCallback, useEffect } from "react";
import { assinarAuditoria } from "../data/auditoriaStore.js";

export function useAuditoria() {
  const [, forcar] = useState(0);
  const rerenderizar = useCallback(() => forcar(n => n + 1), []);
  useEffect(() => assinarAuditoria(rerenderizar), [rerenderizar]);
}
