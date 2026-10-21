import { Aviso } from "./ui.jsx";
import { useAmbiente } from "../hooks/useAmbiente.js";

// Aviso fixo quando o banco tem o cadastro fictício de exemplo. As planilhas dos
// terminais não trazem placa, transportadora nem motorista: esses dados existem
// no BarcaLog só como demonstração até haver cadastro real.
export default function AvisoDemonstracao({ compacto }) {
  const ambiente = useAmbiente();
  if (!ambiente?.dadosExemplo) return null;
  return (
    <Aviso tipo="atencao" icone="science">
      <b>Dados de demonstração.</b>{" "}
      {compacto
        ? "Placas, transportadoras, agenda e situação de portaria são fictícias, criadas para teste."
        : "Placas, transportadoras, motoristas, ocorrências, agendamentos e situação de portaria mostrados aqui são fictícios, criados para testar o sistema. As planilhas dos terminais não trazem esses dados — eles passarão a ser reais quando forem cadastrados ou integrados."}
    </Aviso>
  );
}
