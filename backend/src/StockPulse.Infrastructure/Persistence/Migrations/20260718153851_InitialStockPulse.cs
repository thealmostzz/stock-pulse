using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StockPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialStockPulse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NewsSources",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceCode = table.Column<string>(type: "text", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "watchlists",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ticker = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    Market = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watchlists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stock_news",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceId = table.Column<short>(type: "smallint", nullable: false),
                    ProviderNewsKey = table.Column<string>(type: "text", nullable: true),
                    ExternalUrl = table.Column<string>(type: "text", nullable: false),
                    CanonicalUrl = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    Sentiment = table.Column<int>(type: "integer", nullable: false),
                    SentimentScore = table.Column<decimal>(type: "numeric", nullable: false),
                    ImpactScore = table.Column<decimal>(type: "numeric", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DedupHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RawPayload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    Tags = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_news", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_news_NewsSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_news_tickers",
                columns: table => new
                {
                    NewsId = table.Column<long>(type: "bigint", nullable: false),
                    Ticker = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "numeric", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_news_tickers", x => new { x.NewsId, x.Ticker });
                    table.ForeignKey(
                        name: "FK_stock_news_tickers_stock_news_NewsId",
                        column: x => x.NewsId,
                        principalTable: "stock_news",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_news_DedupHash",
                table: "stock_news",
                column: "DedupHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_news_ImpactScore",
                table: "stock_news",
                column: "ImpactScore",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_stock_news_PublishedAtUtc",
                table: "stock_news",
                column: "PublishedAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_stock_news_SourceId",
                table: "stock_news",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_news_tickers_Ticker_NewsId",
                table: "stock_news_tickers",
                columns: new[] { "Ticker", "NewsId" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_watchlists_Ticker",
                table: "watchlists",
                column: "Ticker",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_news_tickers");

            migrationBuilder.DropTable(
                name: "watchlists");

            migrationBuilder.DropTable(
                name: "stock_news");

            migrationBuilder.DropTable(
                name: "NewsSources");
        }
    }
}
