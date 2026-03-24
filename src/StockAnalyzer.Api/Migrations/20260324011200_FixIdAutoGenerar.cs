using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixIdAutoGenerar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Solo aplica en PostgreSQL: la migración inicial usó Sqlite:Autoincrement
            // que Postgres ignora, dejando la columna Id sin secuencia.
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    CREATE SEQUENCE IF NOT EXISTS ""Recomendaciones_Id_seq"";
                    ALTER TABLE ""Recomendaciones""
                        ALTER COLUMN ""Id"" SET DEFAULT nextval('""Recomendaciones_Id_seq""');
                    SELECT setval(
                        '""Recomendaciones_Id_seq""',
                        COALESCE((SELECT MAX(""Id"") FROM ""Recomendaciones""), 0) + 1,
                        false);
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    ALTER TABLE ""Recomendaciones"" ALTER COLUMN ""Id"" DROP DEFAULT;
                    DROP SEQUENCE IF EXISTS ""Recomendaciones_Id_seq"";
                ");
            }
        }
    }
}
