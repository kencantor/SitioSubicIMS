using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SitioSubicIMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AccountModelMobileToContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MobileNumber",
                table: "Accounts",
                newName: "ContactNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ContactNumber",
                table: "Accounts",
                newName: "MobileNumber");
        }
    }
}
