using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRealtimeOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "news_outbox_events",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    news_id = table.Column<long>(type: "bigint", nullable: false),
                    payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_outbox_events", x => x.event_id);
                    table.ForeignKey(
                        name: "FK_news_outbox_events_stock_news_news_id",
                        column: x => x.news_id,
                        principalTable: "stock_news",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "NewsSources"
                        WHERE "SourceCode" IS NOT NULL
                        GROUP BY "SourceCode"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot create unique index IX_NewsSources_SourceCode because duplicate non-null SourceCode values exist. Resolve or rename duplicate source codes, then rerun the migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_NewsSources_SourceCode",
                table: "NewsSources",
                column: "SourceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_news_outbox_events_delivered_at_utc_next_attempt_at_utc",
                table: "news_outbox_events",
                columns: new[] { "delivered_at_utc", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_news_outbox_events_news_id",
                table: "news_outbox_events",
                column: "news_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "news_outbox_events");

            migrationBuilder.DropIndex(
                name: "IX_NewsSources_SourceCode",
                table: "NewsSources");
        }
    }
}
