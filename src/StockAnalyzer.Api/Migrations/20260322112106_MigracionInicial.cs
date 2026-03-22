using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class MigracionInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recomendaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Accion = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Confianza = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    RSI = table.Column<double>(type: "REAL", nullable: false),
                    Sentimiento = table.Column<double>(type: "REAL", nullable: false),
                    NoticiasAnalizadas = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recomendaciones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recomendaciones_Ticker_Fecha",
                table: "Recomendaciones",
                columns: new[] { "Ticker", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Recomendaciones");
        }
    }
}
