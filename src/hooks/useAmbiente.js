import { useEffect, useState } from "react";
import { api } from "../api/cliente.js";

// Informações do ambiente (GET /api/v1/ambiente), buscadas uma vez por sessão.
// dadosExemplo = o banco tem o cadastro fictício de demonstração (placas,
// transportadoras, condutores, ocorrências, agendamentos, situação de portaria).
let cache = null;

export function useAmbiente() {
  const [ambiente, setAmbiente] = useState(cache);
  useEffect(() => {
    if (cache) return;
    let ativo = true;
    api.get("/api/v1/ambiente").then(r => { cache = r; if (ativo) setAmbiente(r); }).catch(() => {});
    return () => { ativo = false; };
  }, []);
  return ambiente;
}

/** Limpa o cache (logout / troca de usuário). */
export function limparAmbiente() { cache = null; }
