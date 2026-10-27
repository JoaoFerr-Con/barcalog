using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarcaLog.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RegistroAvaliacoesAlertas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvaliacoesAlertas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Inicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Fim = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Autor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sucesso = table.Column<bool>(type: "bit", nullable: false),
                    Erro = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DadosAte = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimaImportacaoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Registros = table.Column<int>(type: "int", nullable: false),
                    DadosNovos = table.Column<bool>(type: "bit", nullable: false),
                    Criados = table.Column<int>(type: "int", nullable: false),
                    Alterados = table.Column<int>(type: "int", nullable: false),
                    Encerrados = table.Column<int>(type: "int", nullable: false),
                    Ativos = table.Column<int>(type: "int", nullable: false),
                    Confiabilidade = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvaliacoesAlertas", x => x.Id);
                    table.CheckConstraint("CK_AvaliacoesAlertas_Origem", "[Origem] IN (N'Automatica', N'Manual', N'Importacao')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesAlertas_Sucesso_Inicio",
                table: "AvaliacoesAlertas",
                columns: new[] { "Sucesso", "Inicio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvaliacoesAlertas");
        }
    }
}
