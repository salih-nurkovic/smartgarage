using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartGarage.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SignalPerformedAtMileage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PerformedAtMileageKm",
                table: "UserServiceSignals",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PerformedAtMileageKm",
                table: "UserServiceSignals");
        }
    }
}
