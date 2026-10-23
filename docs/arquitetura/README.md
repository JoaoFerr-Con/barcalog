# Arquitetura do BarcaLog

| Documento | Conteúdo |
|---|---|
| [estado-atual.md](estado-atual.md) | O que existe no código hoje: stack, componentes, maturidade por módulo, onde ficam as regras |
| [arquitetura-futura.md](arquitetura-futura.md) | Aplicativo móvel (offline, sincronização, SQLite × WatermelonDB), regras de integração e infraestrutura futura (Docker, RabbitMQ, Kubernetes) — preparação, não implementação |
| [registro-de-decisoes.md](registro-de-decisoes.md) | Registro central (DEC-001…): decisões confirmadas e verificadas no código, divergências, hipóteses, alternativas com critérios e pendências de negócio |
| [../PRODUTO.md](../PRODUTO.md) | Documento Mestre do Produto: visão, perfis, módulos, jornada, modelo atual × proposto, roadmap |
| [../dicionario-de-dados/README.md](../dicionario-de-dados/README.md) | Dicionário de dados gerado do código (banco, enums, API) + regras, indicadores e integrações |

Diretriz: a arquitetura vigente é a do código e da documentação atual. Os documentos comerciais
de julho/agosto de 2026 valem para visão, requisitos funcionais e regras de negócio; a
arquitetura técnica neles (Java, PostgreSQL/PostGIS, RabbitMQ, Kubernetes, Redis) não é
referência obrigatória. Tecnologias futuras só entram com autorização e justificativa ligada a
um problema real.
