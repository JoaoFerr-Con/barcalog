# Interface do BarcaLog

Referência visual e de comportamento das telas internas (fase 6). Os números
exibidos vêm sempre da API; as regras de cálculo estão em
[INDICADORES.md](INDICADORES.md).

## Princípios

- **Cor só para estado.** Vermelho = crítico, laranja = alto, amarelo = atenção,
  verde = ok, roxo = previsão. O estado sempre aparece também em texto (selo ou rótulo).
- **Cada número diz de onde vem.** Indicadores têm o botão (i) com a regra de cálculo;
  comparações mostram a base ("vs. 7h06 em julho").
- **Histórico, operação e previsão não se misturam.** Previsões trazem o selo
  "Previsão" e o texto "estimativa"; o que é registro manual (agenda, portaria) é dito.
- **Sem valor presumido.** Sem capacidade cadastrada, não há ocupação nem nível
  por hora — a tela diz que a capacidade não foi configurada.
- **Controles reais.** Todo clique é `<button>`, `<a>` ou campo de formulário; ações
  que gravam ficam visíveis só para o perfil autorizado (o backend valida de novo).

## Dados de demonstração

As planilhas dos terminais não trazem placa, transportadora, motorista, ocorrência,
agendamento nem situação de portaria. Em desenvolvimento (`Seed:DadosExemplo`), o
banco recebe um cadastro **fictício** para testar essas telas. Quando ele existe
(`GET /api/v1/ambiente` → `dadosExemplo`), Portaria, Agendamentos, Negativação, Frota,
Cadastros, o Portal do Transportador, o quadro "Situação agora" da Visão Geral e os
alertas de agendamento atrasado mostram o aviso **"Dados de demonstração"**. Em
produção `Seed:DadosExemplo` é obrigatoriamente `false`.

## Navegação

Rotas por hash (`#/chave`), agrupadas em Operação, Dados, Controle e Administração.
O item "Gargalos e alertas" mostra o número de alertas abertos (atualizado a cada minuto).

| Tela | Rota | Fontes |
|---|---|---|
| Visão Geral | `#/visao-geral` | `/api/v1/indicadores/*`, `/api/v1/alertas`, `/api/v1/agendamentos/resumo`, `/api/v1/portaria/operacao-agora`, `/api/v1/marcacoes` |
| Gargalos e alertas | `#/gargalos` | `/api/v1/alertas*` |
| Agendamentos | `#/agendamentos` | `/api/v1/agendamentos*` |
| Movimentações | `#/movimentacoes` | `/api/v1/marcacoes`, `/api/v1/marcacoes/{id}/detalhe` |
| Importação | `#/importacao` | `/api/v1/importacoes*` |
| Configurações | `#/configuracoes` | `/api/v1/configuracoes*`, `/api/v1/convenios*` |

## Telas

**Visão Geral.** Período padrão = mês mais recente que todos os terminais cobrem
inteiro; comparação com o mês anterior (opcional). Barra "Dados disponíveis até" por
terminal. Cinco indicadores (movimentações, espera mediana, P90, liberadas ≤ 24 h,
esperas longas), alertas que exigem atenção, situação de hoje (registros manuais),
comparação entre terminais, histórico mensal (meses parciais com `*`), chegadas ×
liberações por hora e últimos registros. Exporta CSV e abre a central de relatórios
(PDF/Excel gerados no servidor) com o mesmo período e terminal.

**Gargalos e alertas.** Faixa das próximas 6 horas (estimativa pelo padrão dos
últimos 30 dias com dados), abas por tipo, lista e detalhe com evidência, limite,
origem e histórico. Operador e Gestor podem assumir, registrar ação, resolver,
descartar (texto obrigatório) e reabrir.

**Agendamentos.** Resumo do dia, grade por hora com a capacidade por hora cadastrada
(soma dos terminais quando "todos" e todos têm capacidade), lista com status e QR.

**Movimentações.** Filtros (movimento/senha, terminal, período, convênio, sem
liberação, espera longa, origem), ordenação por marcação ou espera, paginação no
servidor. O detalhe mostra a linha do tempo, liberação em lote e a origem (arquivo,
linha, importação, quem importou).

**Importação.** Enviar → revisar → confirmar. A revisão mostra contagens, sinais de
qualidade, convênios novos e as linhas por classificação; conflitos são decididos por
linha ou em bloco. A confirmação só libera sem conflitos pendentes. Relatório por
linha em CSV. Somente Gestor envia, decide e confirma.

**Configurações.** Capacidade diária e por hora de cada terminal, limites e metas
usados pelos indicadores e alertas, revisão de operador/carga dos convênios.

## Componentes

`src/components/ui.jsx`: `CabecalhoPagina`, `Cartao`, `Kpi`, `Selo`, `Estado`,
`Paginacao`, `Abas`, `Segmentos`, `Campo`, `Aviso`, `Icone`.
Formatação em `src/utils/formatar.js` (durações "13h56", números pt-BR, datas no
horário do porto). Estilos e tokens em `src/styles.css`.

## Verificação

Smoke test com Playwright (Chromium): login como Gestor, todas as telas em 1440 px
sem erro de console nem resposta 4xx/5xx da API, interação em alerta, movimentação,
importação e configurações, e Visão Geral em 390 px sem rolagem horizontal.
