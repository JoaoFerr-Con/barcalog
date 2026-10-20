# Integração com o Protheus — situação e pendências

**Situação:** não existe código de integração com o Protheus neste repositório, e nenhum
material recebido descreve a interface do Protheus usada pelo porto (rotinas, tabelas,
endpoints REST/SOAP, credenciais ou ambiente de homologação). Por isso nada foi
implementado especificamente para ele — implementar sem especificação significaria inventar
campos e regras.

## O que já existe e pode ser reaproveitado

- `POST /api/v1/integracao/eventos` — recebe lotes de 1 a 1.000 eventos `Marcacao` /
  `Liberacao`, autenticado por `X-Api-Key` (política `Integracao`), com `Idempotency-Key`
  (reenvio não duplica). "Marcacao" cria ou atualiza o movimento pelo `MovimentoId`;
  "Liberacao" fecha o movimento. Eventos inválidos voltam em `erros` com o índice.
- Importação de planilhas (`docs/IMPORTACAO.md`) — caminho manual enquanto não há integração.
- Alerta de dados desatualizados por terminal — avisa quando a fonte para de mandar dados.

## O que falta definir (perguntas para o time do Protheus / TI do porto)

1. **Direção e gatilho:** o Protheus envia (push para o endpoint acima) ou o BarcaLog busca
   (pull)? Com que frequência? Existe fila/evento ou só consulta?
2. **Interface:** REST (TOTVS API / TLPP), SOAP (WSDL), tabela intermediária no banco ou
   arquivo? URL de homologação e de produção.
3. **Autenticação:** usuário de serviço, OAuth2, certificado? Quem emite e rotaciona.
4. **Mapeamento de campos:** de onde vêm `COD_MOVIMENTO`, `SENHA`, `CONVENIO`,
   `COD_CONVENIO`, `CICLO`, marcação e liberação (tabela/campo do Protheus). Há fuso nas datas?
5. **Semântica:** o que "liberação" e "CICLO" significam no processo; como representar
   remarcação e cancelamento.
6. **Terminais:** códigos de filial/empresa de cada terminal (inclui o caso "TGPM - BUNGE").
7. **Volume e janela:** registros por dia, horários de pico, limites de taxa do Protheus.
8. **Erros e reprocessamento:** como o Protheus trata rejeição (400/422) e reenvio.
9. **Dados de retorno:** o Protheus precisa receber algo do BarcaLog (ex.: negativação,
   agendamento)?

## Próximo passo sugerido

Com as respostas acima, criar um adaptador em `Infrastructure/Integracao/` que converta o
formato do Protheus para os eventos já aceitos pela API, com testes de contrato sobre
exemplos reais anonimizados fornecidos pelo porto.
