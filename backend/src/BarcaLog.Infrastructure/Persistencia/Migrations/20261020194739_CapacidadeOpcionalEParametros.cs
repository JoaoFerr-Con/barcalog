using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarcaLog.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CapacidadeOpcionalEParametros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Terminais_Capacidade",
                table: "Terminais");

            migrationBuilder.AlterColumn<int>(
                name: "CapacidadeDiariaCarretas",
                table: "Terminais",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1000);

            migrationBuilder.AddColumn<int>(
                name: "CapacidadeHoraria",
                table: "Terminais",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParametrosOperacionais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    LimiteEsperaMedianaHoras = table.Column<double>(type: "float", nullable: false),
                    MetaLiberadas24hPct = table.Column<double>(type: "float", nullable: false),
                    LimiteEsperaLongaHoras = table.Column<double>(type: "float", nullable: false),
                    DiasSemDadosAlerta = table.Column<int>(type: "int", nullable: false),
                    LimiteRazaoChegadaLiberacao = table.Column<double>(type: "float", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AtualizadoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VersaoLinha = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosOperacionais", x => x.Id);
                    table.CheckConstraint("CK_Parametros_Faixas", "[LimiteEsperaMedianaHoras] > 0 AND [MetaLiberadas24hPct] BETWEEN 0 AND 100 AND [LimiteEsperaLongaHoras] > 0 AND [DiasSemDadosAlerta] > 0 AND [LimiteRazaoChegadaLiberacao] > 0");
                    table.CheckConstraint("CK_Parametros_Unico", "[Id] = 1");
                });

            migrationBuilder.InsertData(
                table: "ParametrosOperacionais",
                columns: new[] { "Id", "AtualizadoEm", "AtualizadoPor", "DiasSemDadosAlerta", "LimiteEsperaLongaHoras", "LimiteEsperaMedianaHoras", "LimiteRazaoChegadaLiberacao", "MetaLiberadas24hPct" },
                values: new object[] { 1, null, null, 2, 72.0, 12.0, 1.3999999999999999, 85.0 });

            // O antigo 1.000/dia era um valor padrão, não medido: vira "não informada".
            // Uma capacidade diferente (gravada à mão) é preservada.
            migrationBuilder.Sql("UPDATE dbo.Terminais SET CapacidadeDiariaCarretas = NULL WHERE CapacidadeDiariaCarretas = 1000;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Terminais_Capacidade",
                table: "Terminais",
                sql: "[CapacidadeDiariaCarretas] IS NULL OR [CapacidadeDiariaCarretas] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Terminais_CapacidadeHoraria",
                table: "Terminais",
                sql: "[CapacidadeHoraria] IS NULL OR [CapacidadeHoraria] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParametrosOperacionais");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Terminais_Capacidade",
                table: "Terminais");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Terminais_CapacidadeHoraria",
                table: "Terminais");

            migrationBuilder.DropColumn(
                name: "CapacidadeHoraria",
                table: "Terminais");

            migrationBuilder.AlterColumn<int>(
                name: "CapacidadeDiariaCarretas",
                table: "Terminais",
                type: "int",
                nullable: false,
                defaultValue: 1000,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Terminais",
                keyColumn: "Id",
                keyValue: "hidrovias",
                column: "CapacidadeDiariaCarretas",
                value: 1000);

            migrationBuilder.UpdateData(
                table: "Terminais",
                keyColumn: "Id",
                keyValue: "tgpm",
                column: "CapacidadeDiariaCarretas",
                value: 1000);

            migrationBuilder.UpdateData(
                table: "Terminais",
                keyColumn: "Id",
                keyValue: "unitapajos",
                column: "CapacidadeDiariaCarretas",
                value: 1000);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Terminais_Capacidade",
                table: "Terminais",
                sql: "[CapacidadeDiariaCarretas] > 0");
        }
    }
}
