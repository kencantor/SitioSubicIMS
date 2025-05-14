using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SitioSubicIMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedApplicationUserModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "AspNetUsers");
        }
    }
}
