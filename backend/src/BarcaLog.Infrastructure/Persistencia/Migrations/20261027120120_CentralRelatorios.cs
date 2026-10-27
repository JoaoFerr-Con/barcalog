using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarcaLog.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CentralRelatorios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RelatoriosGerados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    De = table.Column<DateOnly>(type: "date", nullable: false),
                    Ate = table.Column<DateOnly>(type: "date", nullable: false),
                    TerminalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Abrangencia = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Filtros = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Formato = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    GeradoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GeradoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    HashSha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelatoriosGerados", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RelatoriosGerados_Codigo",
                table: "RelatoriosGerados",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RelatoriosGerados_TerminalId_GeradoEm",
                table: "RelatoriosGerados",
                columns: new[] { "TerminalId", "GeradoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RelatoriosGerados");
        }
    }
}
