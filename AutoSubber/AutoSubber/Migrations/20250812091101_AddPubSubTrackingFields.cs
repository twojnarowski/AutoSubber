using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoSubber.Migrations
{
    /// <inheritdoc />
    public partial class AddPubSubTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PubSubLastAttempt",
                table: "Subscriptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PubSubLeaseExpiry",
                table: "Subscriptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PubSubSubscribed",
                table: "Subscriptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PubSubSubscriptionAttempts",
                table: "Subscriptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PubSubLastAttempt",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PubSubLeaseExpiry",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PubSubSubscribed",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PubSubSubscriptionAttempts",
                table: "Subscriptions");
        }
    }
}
