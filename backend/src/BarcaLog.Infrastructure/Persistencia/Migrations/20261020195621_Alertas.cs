using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarcaLog.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Alertas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alertas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Chave = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Regra = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Prioridade = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TerminalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Periodo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Indicador = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ValorObservado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Limite = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Evidencia = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AcaoRecomendada = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    IdentificadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Responsavel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VersaoLinha = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alertas", x => x.Id);
                    table.CheckConstraint("CK_Alertas_Estado", "[Estado] IN (N'Novo', N'EmAnalise', N'Resolvido', N'Descartado')");
                    table.CheckConstraint("CK_Alertas_Prioridade", "[Prioridade] IN (N'Baixa', N'Media', N'Alta')");
                    table.CheckConstraint("CK_Alertas_Tipo", "[Tipo] IN (N'Historico', N'Operacional', N'Previsao', N'Dados')");
                });

            migrationBuilder.CreateTable(
                name: "EventosAlerta",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlertaId = table.Column<int>(type: "int", nullable: false),
                    Quando = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Autor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Acao = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosAlerta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosAlerta_Alertas_AlertaId",
                        column: x => x.AlertaId,
                        principalTable: "Alertas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alertas_Ativo_Estado_Tipo",
                table: "Alertas",
                columns: new[] { "Ativo", "Estado", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_Alertas_Chave",
                table: "Alertas",
                column: "Chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventosAlerta_AlertaId",
                table: "EventosAlerta",
                column: "AlertaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventosAlerta");

            migrationBuilder.DropTable(
                name: "Alertas");
        }
    }
}
