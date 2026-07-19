using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRealtimeDeliveryReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "realtime_delivery_receipts",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_realtime_delivery_receipts", x => x.event_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "realtime_delivery_receipts");
        }
    }
}
