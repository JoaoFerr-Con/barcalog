/*
  BarcaLog — backup e restauração (SQL Server auto-hospedado).
  Em Azure SQL / RDS, use os backups automáticos gerenciados (PITR) e teste a
  restauração do mesmo jeito: backup que nunca foi restaurado não é backup.

  Estratégia sugerida (RPO ~15 min, RTO < 1 h):
    - Modelo de recuperação FULL
    - Backup completo diário, diferencial a cada 6h, log a cada 15 min
    - Cópia fora do servidor (outra região / storage imutável), criptografada
    - Retenção: 30 dias de completos; 7 dias de logs
    - Teste de restauração MENSAL num servidor separado (roteiro abaixo)
*/

ALTER DATABASE [BarcaLog] SET RECOVERY FULL;
GO

-- Backup completo (comprimido, com checksum e criptografia — exige certificado criado antes:
--   CREATE MASTER KEY ...; CREATE CERTIFICATE BarcaLogBackupCert WITH SUBJECT = 'Backup BarcaLog';
--   e faça backup do CERTIFICADO também, senão o backup fica irrecuperável).
BACKUP DATABASE [BarcaLog]
  TO DISK = N'/var/opt/mssql/backup/BarcaLog_full.bak'
  WITH COMPRESSION, CHECKSUM, INIT,
       ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = BarcaLogBackupCert);
GO

BACKUP LOG [BarcaLog]
  TO DISK = N'/var/opt/mssql/backup/BarcaLog_log.trn'
  WITH COMPRESSION, CHECKSUM,
       ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = BarcaLogBackupCert);
GO

-- Verificação (não restaura, só valida a mídia):
RESTORE VERIFYONLY FROM DISK = N'/var/opt/mssql/backup/BarcaLog_full.bak' WITH CHECKSUM;
GO

/*
  Roteiro de teste de restauração (servidor de homologação):
    RESTORE DATABASE [BarcaLog_Restore] FROM DISK = N'.../BarcaLog_full.bak'
      WITH MOVE 'BarcaLog' TO '.../BarcaLog_Restore.mdf',
           MOVE 'BarcaLog_log' TO '.../BarcaLog_Restore_log.ldf',
           NORECOVERY, CHECKSUM;
    RESTORE LOG [BarcaLog_Restore] FROM DISK = N'.../BarcaLog_log.trn' WITH RECOVERY;
    -- conferir: SELECT COUNT(*) FROM dbo.Marcacoes; SELECT MAX(Quando) FROM dbo.LogsAuditoria;
    -- subir a API apontando pra [BarcaLog_Restore] e rodar GET /health/ready
*/
