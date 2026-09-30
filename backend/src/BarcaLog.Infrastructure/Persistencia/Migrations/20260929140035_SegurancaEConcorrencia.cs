using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarcaLog.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class SegurancaEConcorrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "VersaoLinha",
                table: "Veiculos",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BloqueadoAte",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeveTrocarSenha",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FalhasLoginConsecutivas",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "MfaAtivo",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MfaSegredoCifrado",
                table: "Usuarios",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MfaUltimoPassoUsado",
                table: "Usuarios",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoLoginEm",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "VersaoLinha",
                table: "Usuarios",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersaoToken",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<byte[]>(
                name: "VersaoLinha",
                table: "Transportadoras",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "VersaoLinha",
                table: "Ocorrencias",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "VersaoLinha",
                table: "Contestacoes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "VersaoLinha",
                table: "Condutores",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "VersaoLinha",
                table: "Agendamentos",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChavesIdempotencia",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Escopo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Chave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HashRequisicao = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Concluida = table.Column<bool>(type: "bit", nullable: false),
                    StatusHttp = table.Column<int>(type: "int", nullable: true),
                    CorpoResposta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChavesIdempotencia", x => x.Id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Veiculos_StatusNegativacao",
                table: "Veiculos",
                sql: "[StatusNegativacao] IN (N'Regular', N'Negativada')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Veiculos_StatusPortaria",
                table: "Veiculos",
                sql: "[StatusPortaria] IN (N'NoPatio', N'Aguardando', N'NoPorto', N'DescargaFinalizada')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Usuarios_Falhas",
                table: "Usuarios",
                sql: "[FalhasLoginConsecutivas] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Usuarios_Papel",
                table: "Usuarios",
                sql: "[Papel] IN (N'Operador', N'Gestor', N'Auditor')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ocorrencias_Nivel",
                table: "Ocorrencias",
                sql: "[Nivel] IN (N'N1', N'N2', N'N3')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ocorrencias_Status",
                table: "Ocorrencias",
                sql: "[Status] IN (N'Ativa', N'Contestada', N'Resolvida')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Marcacoes_Ciclo",
                table: "Marcacoes",
                sql: "[Ciclo] >= 0");

            migrationBuilder.CreateIndex(
                name: "UX_Contestacoes_OcorrenciaPendente",
                table: "Contestacoes",
                column: "OcorrenciaId",
                unique: true,
                filter: "[Status] = N'Pendente'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contestacoes_Status",
                table: "Contestacoes",
                sql: "[Status] IN (N'Pendente', N'Aprovada', N'Rejeitada')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Condutores_StatusNegativacao",
                table: "Condutores",
                sql: "[StatusNegativacao] IN (N'Regular', N'Negativada')");

            migrationBuilder.CreateIndex(
                name: "UX_Agendamentos_PlacaHorarioAtivo",
                table: "Agendamentos",
                columns: new[] { "Placa", "Data", "Hora" },
                unique: true,
                filter: "[Status] <> N'Cancelado'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Agendamentos_JanelaConformidade",
                table: "Agendamentos",
                sql: "[JanelaConformidade] IN (N'D0', N'D1', N'D2', N'D3')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Agendamentos_Status",
                table: "Agendamentos",
                sql: "[Status] IN (N'Agendado', N'Confirmado', N'ACaminho', N'AguardandoEntrada', N'EmOperacao', N'Finalizado', N'Atrasado', N'Cancelado')");

            migrationBuilder.CreateIndex(
                name: "IX_ChavesIdempotencia_CriadaEm",
                table: "ChavesIdempotencia",
                column: "CriadaEm");

            migrationBuilder.CreateIndex(
                name: "IX_ChavesIdempotencia_Escopo_Chave",
                table: "ChavesIdempotencia",
                columns: new[] { "Escopo", "Chave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChavesIdempotencia");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Veiculos_StatusNegativacao",
                table: "Veiculos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Veiculos_StatusPortaria",
                table: "Veiculos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Usuarios_Falhas",
                table: "Usuarios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Usuarios_Papel",
                table: "Usuarios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ocorrencias_Nivel",
                table: "Ocorrencias");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ocorrencias_Status",
                table: "Ocorrencias");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Marcacoes_Ciclo",
                table: "Marcacoes");

            migrationBuilder.DropIndex(
                name: "UX_Contestacoes_OcorrenciaPendente",
                table: "Contestacoes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Contestacoes_Status",
                table: "Contestacoes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Condutores_StatusNegativacao",
                table: "Condutores");

            migrationBuilder.DropIndex(
                name: "UX_Agendamentos_PlacaHorarioAtivo",
                table: "Agendamentos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Agendamentos_JanelaConformidade",
                table: "Agendamentos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Agendamentos_Status",
                table: "Agendamentos");

            migrationBuilder.DropColumn(
                name: "VersaoLinha",
                table: "Veiculos");

            migrationBuilder.DropColumn(
                name: "BloqueadoAte",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "DeveTrocarSenha",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FalhasLoginConsecutivas",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MfaAtivo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MfaSegredoCifrado",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "MfaUltimoPassoUsado",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "UltimoLoginEm",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "VersaoLinha",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "VersaoToken",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "VersaoLinha",
                table: "Transportadoras");

            migrationBuilder.DropColumn(
                name: "VersaoLinha",
                table: "Ocorrencias");

            migrationBuilder.DropColumn(
                name: "VersaoLinha",
                table: "Contestacoes");

            migrationBuilder.DropColumn(
                name: "VersaoLinha",
                table: "Condutores");

            migrationBuilder.DropColumn(
                name: "VersaoLinha",
                table: "Agendamentos");
        }
    }
}
