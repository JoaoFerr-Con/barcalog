/*
  BarcaLog — usuários do banco com MENOR PRIVILÉGIO.

  Rodar UMA vez, como administrador (sa ou equivalente), trocando as senhas.
  Senhas: gere aleatórias (ex.: `openssl rand -base64 32`) e guarde no cofre
  de segredos do ambiente — nunca neste arquivo nem no Git.

  - barcalog_app       → usado pela API em produção. Lê/grava dados, NÃO altera
                         schema, NÃO apaga tabela, NÃO lê outros bancos.
  - barcalog_migracoes → usado só no deploy pra rodar as migrations (DDL).
                         Fica desabilitado no dia a dia (ALTER LOGIN ... DISABLE).

  Connection string da API:
    Server=...;Database=BarcaLog;User Id=barcalog_app;Password=...;Encrypt=True
*/

USE [master];
GO
CREATE LOGIN [barcalog_app] WITH PASSWORD = N'<TROQUE-senha-forte-app>', CHECK_POLICY = ON, DEFAULT_DATABASE = [BarcaLog];
CREATE LOGIN [barcalog_migracoes] WITH PASSWORD = N'<TROQUE-senha-forte-migracoes>', CHECK_POLICY = ON, DEFAULT_DATABASE = [BarcaLog];
GO

USE [BarcaLog];
GO
CREATE USER [barcalog_app] FOR LOGIN [barcalog_app];
ALTER ROLE [db_datareader] ADD MEMBER [barcalog_app];
ALTER ROLE [db_datawriter] ADD MEMBER [barcalog_app];
-- Log de auditoria é só-inclusão pra aplicação: nem a API consegue apagar/alterar histórico.
DENY UPDATE, DELETE ON OBJECT::[dbo].[LogsAuditoria] TO [barcalog_app];
-- Histórico de migrations é só do usuário de migração.
DENY INSERT, UPDATE, DELETE ON OBJECT::[dbo].[__EFMigrationsHistory] TO [barcalog_app];
GO

CREATE USER [barcalog_migracoes] FOR LOGIN [barcalog_migracoes];
ALTER ROLE [db_ddladmin] ADD MEMBER [barcalog_migracoes];
ALTER ROLE [db_datareader] ADD MEMBER [barcalog_migracoes];
ALTER ROLE [db_datawriter] ADD MEMBER [barcalog_migracoes];
GO

-- Fora da janela de deploy:
-- ALTER LOGIN [barcalog_migracoes] DISABLE;
