using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SitioSubicIMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class BillingsAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Billings",
                columns: table => new
                {
                    BillingID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillingNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReadingID = table.Column<int>(type: "int", nullable: false),
                    BillingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RatePerCubicMeter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinimumConsumption = table.Column<int>(type: "int", nullable: false),
                    MinimumCharge = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PenaltyRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    VATRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisconnectionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BillingStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billings", x => x.BillingID);
                    table.ForeignKey(
                        name: "FK_Billings_Readings_ReadingID",
                        column: x => x.ReadingID,
                        principalTable: "Readings",
                        principalColumn: "ReadingID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Billings_ReadingID",
                table: "Billings",
                column: "ReadingID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Billings");
        }
    }
}
