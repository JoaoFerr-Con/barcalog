using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarcaLog.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class SituacaoForaDoPorto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Veiculos_StatusPortaria",
                table: "Veiculos");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Veiculos_StatusPortaria",
                table: "Veiculos",
                sql: "[StatusPortaria] IN (N'NoPatio', N'Aguardando', N'NoPorto', N'DescargaFinalizada', N'ForaDoPorto')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: a situação anterior não existia; volta ao comportamento antigo (cadastro = "Aguardando").
            migrationBuilder.Sql("UPDATE [Veiculos] SET [StatusPortaria] = N'Aguardando' WHERE [StatusPortaria] = N'ForaDoPorto';");
            migrationBuilder.DropCheckConstraint(
                name: "CK_Veiculos_StatusPortaria",
                table: "Veiculos");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Veiculos_StatusPortaria",
                table: "Veiculos",
                sql: "[StatusPortaria] IN (N'NoPatio', N'Aguardando', N'NoPorto', N'DescargaFinalizada')");
        }
    }
}
