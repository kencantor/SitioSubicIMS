using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SitioSubicIMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class SMSAlertModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Readings_AspNetUsers_UserID",
                table: "Readings");

            migrationBuilder.AlterColumn<string>(
                name: "UserID",
                table: "Readings",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateTable(
                name: "SMSAlerts",
                columns: table => new
                {
                    SMSAlertID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AllowSMSAlerts = table.Column<bool>(type: "bit", nullable: false),
                    AllowReadingAlerts = table.Column<bool>(type: "bit", nullable: false),
                    AllowBillingAlerts = table.Column<bool>(type: "bit", nullable: false),
                    AllowPaymentAlerts = table.Column<bool>(type: "bit", nullable: false),
                    MessageHeader = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TwilioAccountSID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TwilioAuthToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TwilioFromPhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SMSAlerts", x => x.SMSAlertID);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_Readings_AspNetUsers_UserID",
                table: "Readings",
                column: "UserID",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Readings_AspNetUsers_UserID",
                table: "Readings");

            migrationBuilder.DropTable(
                name: "SMSAlerts");

            migrationBuilder.AlterColumn<string>(
                name: "UserID",
                table: "Readings",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Readings_AspNetUsers_UserID",
                table: "Readings",
                column: "UserID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
