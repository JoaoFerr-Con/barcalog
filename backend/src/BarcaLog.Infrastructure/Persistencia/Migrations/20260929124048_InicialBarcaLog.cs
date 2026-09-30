using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BarcaLog.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class InicialBarcaLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogsAuditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Autor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Acao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Detalhes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quando = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogsAuditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Terminais",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CapacidadeDiariaCarretas = table.Column<int>(type: "int", nullable: false, defaultValue: 1000)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terminais", x => x.Id);
                    table.CheckConstraint("CK_Terminais_Capacidade", "[CapacidadeDiariaCarretas] > 0");
                });

            migrationBuilder.CreateTable(
                name: "Transportadoras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cnpj = table.Column<string>(type: "nvarchar(18)", maxLength: 18, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transportadoras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SenhaHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Papel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Marcacoes",
                columns: table => new
                {
                    MovimentoId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Senha = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Convenio = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CodConvenio = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Operador = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Carga = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Ciclo = table.Column<int>(type: "int", nullable: false),
                    DataMarcacao = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    DataLiberacao = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    TerminalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EsperaHoras = table.Column<decimal>(type: "decimal(10,2)", nullable: true, computedColumnSql: "CONVERT(decimal(10,2), ROUND(DATEDIFF(SECOND, [DataMarcacao], [DataLiberacao]) / 3600.0, 2))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marcacoes", x => x.MovimentoId);
                    table.CheckConstraint("CK_Marcacoes_Liberacao", "[DataLiberacao] IS NULL OR [DataLiberacao] >= [DataMarcacao]");
                    table.ForeignKey(
                        name: "FK_Marcacoes_Terminais_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "Terminais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Agendamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Hora = table.Column<TimeOnly>(type: "time", nullable: false),
                    Placa = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TransportadoraId = table.Column<int>(type: "int", nullable: false),
                    TerminalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Carga = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Motorista = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    JanelaConformidade = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agendamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Agendamentos_Terminais_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "Terminais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Agendamentos_Transportadoras_TransportadoraId",
                        column: x => x.TransportadoraId,
                        principalTable: "Transportadoras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Condutores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TransportadoraId = table.Column<int>(type: "int", nullable: false),
                    PlacaVinculada = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    StatusNegativacao = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Condutores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Condutores_Transportadoras_TransportadoraId",
                        column: x => x.TransportadoraId,
                        principalTable: "Transportadoras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Veiculos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Placa = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TransportadoraId = table.Column<int>(type: "int", nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StatusPortaria = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StatusPortariaDesde = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatusNegativacao = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TerminalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Veiculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Veiculos_Terminais_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "Terminais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Veiculos_Transportadoras_TransportadoraId",
                        column: x => x.TransportadoraId,
                        principalTable: "Transportadoras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ocorrencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nivel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Placa = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TransportadoraId = table.Column<int>(type: "int", nullable: false),
                    CondutorId = table.Column<int>(type: "int", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Local = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Responsavel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ocorrencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ocorrencias_Condutores_CondutorId",
                        column: x => x.CondutorId,
                        principalTable: "Condutores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Ocorrencias_Transportadoras_TransportadoraId",
                        column: x => x.TransportadoraId,
                        principalTable: "Transportadoras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contestacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OcorrenciaId = table.Column<int>(type: "int", nullable: false),
                    TransportadoraId = table.Column<int>(type: "int", nullable: false),
                    Justificativa = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondidoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespostaOperador = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contestacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contestacoes_Ocorrencias_OcorrenciaId",
                        column: x => x.OcorrenciaId,
                        principalTable: "Ocorrencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contestacoes_Transportadoras_TransportadoraId",
                        column: x => x.TransportadoraId,
                        principalTable: "Transportadoras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Terminais",
                columns: new[] { "Id", "CapacidadeDiariaCarretas", "Nome" },
                values: new object[,]
                {
                    { "hidrovias", 1000, "Hidrovias" },
                    { "tgpm", 1000, "TGPM" },
                    { "unitapajos", 1000, "Unitapajós" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agendamentos_Data_Hora",
                table: "Agendamentos",
                columns: new[] { "Data", "Hora" });

            migrationBuilder.CreateIndex(
                name: "IX_Agendamentos_Placa",
                table: "Agendamentos",
                column: "Placa");

            migrationBuilder.CreateIndex(
                name: "IX_Agendamentos_TerminalId_Data",
                table: "Agendamentos",
                columns: new[] { "TerminalId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_Agendamentos_TransportadoraId",
                table: "Agendamentos",
                column: "TransportadoraId");

            migrationBuilder.CreateIndex(
                name: "IX_Condutores_PlacaVinculada",
                table: "Condutores",
                column: "PlacaVinculada");

            migrationBuilder.CreateIndex(
                name: "IX_Condutores_TransportadoraId",
                table: "Condutores",
                column: "TransportadoraId");

            migrationBuilder.CreateIndex(
                name: "IX_Contestacoes_OcorrenciaId_Status",
                table: "Contestacoes",
                columns: new[] { "OcorrenciaId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Contestacoes_TransportadoraId",
                table: "Contestacoes",
                column: "TransportadoraId");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_Acao",
                table: "LogsAuditoria",
                column: "Acao");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_Autor",
                table: "LogsAuditoria",
                column: "Autor");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_Quando",
                table: "LogsAuditoria",
                column: "Quando");

            migrationBuilder.CreateIndex(
                name: "IX_Marcacoes_Convenio",
                table: "Marcacoes",
                column: "Convenio");

            migrationBuilder.CreateIndex(
                name: "IX_Marcacoes_DataMarcacao",
                table: "Marcacoes",
                column: "DataMarcacao");

            migrationBuilder.CreateIndex(
                name: "IX_Marcacoes_Senha",
                table: "Marcacoes",
                column: "Senha");

            migrationBuilder.CreateIndex(
                name: "IX_Marcacoes_TerminalId_DataMarcacao",
                table: "Marcacoes",
                columns: new[] { "TerminalId", "DataMarcacao" });

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_CondutorId",
                table: "Ocorrencias",
                column: "CondutorId");

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_CriadoEm",
                table: "Ocorrencias",
                column: "CriadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_Placa",
                table: "Ocorrencias",
                column: "Placa");

            migrationBuilder.CreateIndex(
                name: "IX_Ocorrencias_TransportadoraId_Nivel_CriadoEm",
                table: "Ocorrencias",
                columns: new[] { "TransportadoraId", "Nivel", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Terminais_Nome",
                table: "Terminais",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transportadoras_Cnpj",
                table: "Transportadoras",
                column: "Cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transportadoras_Nome",
                table: "Transportadoras",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_Placa",
                table: "Veiculos",
                column: "Placa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_StatusNegativacao",
                table: "Veiculos",
                column: "StatusNegativacao");

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_StatusPortaria_StatusPortariaDesde",
                table: "Veiculos",
                columns: new[] { "StatusPortaria", "StatusPortariaDesde" });

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_TerminalId",
                table: "Veiculos",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_TransportadoraId",
                table: "Veiculos",
                column: "TransportadoraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Agendamentos");

            migrationBuilder.DropTable(
                name: "Contestacoes");

            migrationBuilder.DropTable(
                name: "LogsAuditoria");

            migrationBuilder.DropTable(
                name: "Marcacoes");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Veiculos");

            migrationBuilder.DropTable(
                name: "Ocorrencias");

            migrationBuilder.DropTable(
                name: "Terminais");

            migrationBuilder.DropTable(
                name: "Condutores");

            migrationBuilder.DropTable(
                name: "Transportadoras");
        }
    }
}
