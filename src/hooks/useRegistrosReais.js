import { useState, useEffect } from "react";
import { carregarRegistros } from "../data/registry.js";

// Carrega os registros reais da empresa selecionada. Reexecuta sempre que
// `empresaId` muda. `carregando` cobre tanto o carregamento inicial quanto
// a troca de empresa no filtro.
export function useRegistrosReais(empresaId) {
  const [registros, setRegistros] = useState([]);
  const [carregando, setCarregando] = useState(true);

  useEffect(() => {
    let cancelado = false;
    setCarregando(true);
    carregarRegistros(empresaId).then(dados => {
      if (!cancelado) {
        setRegistros(dados);
        setCarregando(false);
      }
    });
    return () => { cancelado = true; };
  }, [empresaId]);

  return { registros, carregando };
}
