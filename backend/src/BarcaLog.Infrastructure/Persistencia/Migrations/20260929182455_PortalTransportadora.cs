using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarcaLog.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class PortalTransportadora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Usuarios_Papel",
                table: "Usuarios");

            migrationBuilder.AddColumn<int>(
                name: "TransportadoraId",
                table: "Usuarios",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_TransportadoraId",
                table: "Usuarios",
                column: "TransportadoraId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Usuarios_EscopoTransportadora",
                table: "Usuarios",
                sql: "([Papel] = N'Transportadora' AND [TransportadoraId] IS NOT NULL) OR ([Papel] <> N'Transportadora' AND [TransportadoraId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Usuarios_Papel",
                table: "Usuarios",
                sql: "[Papel] IN (N'Operador', N'Gestor', N'Auditor', N'Transportadora')");

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Transportadoras_TransportadoraId",
                table: "Usuarios",
                column: "TransportadoraId",
                principalTable: "Transportadoras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Transportadoras_TransportadoraId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_TransportadoraId",
                table: "Usuarios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Usuarios_EscopoTransportadora",
                table: "Usuarios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Usuarios_Papel",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TransportadoraId",
                table: "Usuarios");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Usuarios_Papel",
                table: "Usuarios",
                sql: "[Papel] IN (N'Operador', N'Gestor', N'Auditor')");
        }
    }
}
