using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRealtimeOutboxLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "lock_token",
                table: "news_outbox_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until_utc",
                table: "news_outbox_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_news_outbox_events_lock_token_locked_until_utc",
                table: "news_outbox_events",
                columns: new[] { "lock_token", "locked_until_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_news_outbox_events_lock_token_locked_until_utc",
                table: "news_outbox_events");

            migrationBuilder.DropColumn(
                name: "lock_token",
                table: "news_outbox_events");

            migrationBuilder.DropColumn(
                name: "locked_until_utc",
                table: "news_outbox_events");
        }
    }
}
