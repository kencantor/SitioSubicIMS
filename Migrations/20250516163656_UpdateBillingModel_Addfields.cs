using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SitioSubicIMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBillingModel_Addfields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Arrears",
                table: "Billings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Arrears",
                table: "Billings");
        }
    }
}
