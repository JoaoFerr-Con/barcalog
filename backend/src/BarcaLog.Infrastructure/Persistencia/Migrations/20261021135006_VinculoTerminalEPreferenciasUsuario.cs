using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarcaLog.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class VinculoTerminalEPreferenciasUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TelaInicial",
                table: "Usuarios",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminalId",
                table: "Usuarios",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminalPreferido",
                table: "Usuarios",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_TerminalId",
                table: "Usuarios",
                column: "TerminalId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Usuarios_TerminalSoInterno",
                table: "Usuarios",
                sql: "[Papel] <> N'Transportadora' OR [TerminalId] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Terminais_TerminalId",
                table: "Usuarios",
                column: "TerminalId",
                principalTable: "Terminais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Terminais_TerminalId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_TerminalId",
                table: "Usuarios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Usuarios_TerminalSoInterno",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TelaInicial",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TerminalPreferido",
                table: "Usuarios");
        }
    }
}
