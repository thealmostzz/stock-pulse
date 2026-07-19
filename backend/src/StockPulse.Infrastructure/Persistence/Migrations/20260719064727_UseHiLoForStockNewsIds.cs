using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StockPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseHiLoForStockNewsIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "stock_news_hilo",
                incrementBy: 10);

            migrationBuilder.Sql(
                "SELECT setval('stock_news_hilo', GREATEST(1, (SELECT COALESCE(MAX(\"Id\"), 0) / 10 + 1 FROM stock_news)), false);");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "stock_news",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "stock_news_hilo");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "stock_news",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }
    }
}
