using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarcaLog.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ImportacaoPlanilhas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LinhaOrigem",
                table: "Marcacoes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoteImportacaoId",
                table: "Marcacoes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origem",
                table: "Marcacoes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "CargaInicial");

            migrationBuilder.CreateTable(
                name: "Convenios",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Operador = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Carga = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Deduzido = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VersaoLinha = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Convenios", x => x.Codigo);
                });

            migrationBuilder.CreateTable(
                name: "LotesImportacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Arquivo = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    HashSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Formato = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TerminalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CriadoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConcluidoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConcluidoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MarcacaoInicial = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    MarcacaoFinal = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Linhas = table.Column<int>(type: "int", nullable: false),
                    Novos = table.Column<int>(type: "int", nullable: false),
                    Iguais = table.Column<int>(type: "int", nullable: false),
                    Complementares = table.Column<int>(type: "int", nullable: false),
                    Conflitantes = table.Column<int>(type: "int", nullable: false),
                    Rejeitados = table.Column<int>(type: "int", nullable: false),
                    ComAvisos = table.Column<int>(type: "int", nullable: false),
                    AvisosEspera72h = table.Column<int>(type: "int", nullable: false),
                    AvisosSemLiberacao = table.Column<int>(type: "int", nullable: false),
                    AvisosRemarcacao = table.Column<int>(type: "int", nullable: false),
                    AvisosConvenioNovo = table.Column<int>(type: "int", nullable: false),
                    ConveniosNovos = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Inseridos = table.Column<int>(type: "int", nullable: false),
                    Atualizados = table.Column<int>(type: "int", nullable: false),
                    Ignorados = table.Column<int>(type: "int", nullable: false),
                    VersaoLinha = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesImportacao", x => x.Id);
                    table.CheckConstraint("CK_LotesImportacao_Formato", "[Formato] IN (N'Xlsx', N'Csv')");
                    table.CheckConstraint("CK_LotesImportacao_Status", "[Status] IN (N'Analisado', N'Confirmado', N'Cancelado')");
                    table.ForeignKey(
                        name: "FK_LotesImportacao_Terminais_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "Terminais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LinhasImportacao",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoteId = table.Column<int>(type: "int", nullable: false),
                    NumeroLinha = table.Column<int>(type: "int", nullable: false),
                    Classificacao = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Decisao = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Avisos = table.Column<int>(type: "int", maxLength: 32, nullable: false),
                    MovimentoId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Senha = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Convenio = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CodConvenio = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Operador = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Carga = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Ciclo = table.Column<int>(type: "int", nullable: true),
                    DataMarcacao = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    DataLiberacao = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinhasImportacao", x => x.Id);
                    table.CheckConstraint("CK_LinhasImportacao_Classificacao", "[Classificacao] IN (N'Novo', N'Complementar', N'Conflito', N'Rejeitado')");
                    table.CheckConstraint("CK_LinhasImportacao_Decisao", "[Decisao] IN (N'NaoSeAplica', N'Pendente', N'Aplicar', N'Manter')");
                    table.ForeignKey(
                        name: "FK_LinhasImportacao_LotesImportacao_LoteId",
                        column: x => x.LoteId,
                        principalTable: "LotesImportacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Marcacoes_LoteImportacaoId",
                table: "Marcacoes",
                column: "LoteImportacaoId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Marcacoes_Origem",
                table: "Marcacoes",
                sql: "[Origem] IN (N'CargaInicial', N'Planilha', N'Integracao')");

            migrationBuilder.CreateIndex(
                name: "IX_LinhasImportacao_LoteId_Classificacao_NumeroLinha",
                table: "LinhasImportacao",
                columns: new[] { "LoteId", "Classificacao", "NumeroLinha" });

            migrationBuilder.CreateIndex(
                name: "IX_LinhasImportacao_LoteId_MovimentoId",
                table: "LinhasImportacao",
                columns: new[] { "LoteId", "MovimentoId" });

            migrationBuilder.CreateIndex(
                name: "IX_LotesImportacao_CriadoEm",
                table: "LotesImportacao",
                column: "CriadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_LotesImportacao_HashSha256_TerminalId_Status",
                table: "LotesImportacao",
                columns: new[] { "HashSha256", "TerminalId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LotesImportacao_TerminalId",
                table: "LotesImportacao",
                column: "TerminalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Marcacoes_LotesImportacao_LoteImportacaoId",
                table: "Marcacoes",
                column: "LoteImportacaoId",
                principalTable: "LotesImportacao",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Marcacoes_LotesImportacao_LoteImportacaoId",
                table: "Marcacoes");

            migrationBuilder.DropTable(
                name: "Convenios");

            migrationBuilder.DropTable(
                name: "LinhasImportacao");

            migrationBuilder.DropTable(
                name: "LotesImportacao");

            migrationBuilder.DropIndex(
                name: "IX_Marcacoes_LoteImportacaoId",
                table: "Marcacoes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Marcacoes_Origem",
                table: "Marcacoes");

            migrationBuilder.DropColumn(
                name: "LinhaOrigem",
                table: "Marcacoes");

            migrationBuilder.DropColumn(
                name: "LoteImportacaoId",
                table: "Marcacoes");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "Marcacoes");
        }
    }
}
