using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Customers.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                schema: "customers",
                table: "customers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                schema: "customers",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                schema: "customers",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "customers",
                table: "customers");
        }
    }
}
