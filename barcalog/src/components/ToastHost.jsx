import { useState, useEffect, useCallback } from "react";
import { assinarToasts } from "./toast.js";

const CORES = {
  sucesso: { fundo: "var(--verde-100)", cor: "var(--verde-500)", icone: "check_circle" },
  aviso: { fundo: "#FBEBD1", cor: "var(--ambar-600)", icone: "warning" },
  erro: { fundo: "var(--vermelho-100)", cor: "var(--vermelho-500)", icone: "error" },
  info: { fundo: "var(--azul-100)", cor: "var(--navio-700)", icone: "info" }
};

export default function ToastHost() {
  const [toasts, setToasts] = useState([]);

  const remover = useCallback(id => {
    setToasts(t => t.filter(x => x.id !== id));
  }, []);

  useEffect(() => {
    return assinarToasts(({ mensagem, tipo, id }) => {
      setToasts(t => [...t, { mensagem, tipo, id }]);
      setTimeout(() => remover(id), 4200);
    });
  }, [remover]);

  if (!toasts.length) return null;

  return (
    <div style={{
      position: "fixed", right: 20, bottom: 20, zIndex: 999,
      display: "flex", flexDirection: "column", gap: 10, maxWidth: 340
    }}>
      {toasts.map(t => {
        const estilo = CORES[t.tipo] || CORES.info;
        return (
          <div
            key={t.id}
            onClick={() => remover(t.id)}
            style={{
              background: "#fff", border: `1px solid ${estilo.fundo}`, borderLeft: `4px solid ${estilo.cor}`,
              borderRadius: 10, padding: "12px 14px", boxShadow: "0 8px 24px rgba(15, 23, 42, 0.12)",
              display: "flex", alignItems: "flex-start", gap: 10, cursor: "pointer",
              animation: "barcalog-toast-in 0.22s ease-out"
            }}
          >
            <span className="material-symbols-outlined" style={{ color: estilo.cor, fontSize: 20 }}>{estilo.icone}</span>
            <span style={{ fontSize: 13, color: "var(--tinta)", lineHeight: 1.4 }}>{t.mensagem}</span>
          </div>
        );
      })}
    </div>
  );
}
