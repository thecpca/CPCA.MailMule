using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CPCA.MailMule.Migrations
{
    public partial class AddJunkFolderPath : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JunkFolderPath",
                table: "MailboxConfigs",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JunkFolderPath",
                table: "MailboxConfigs");
        }
    }
}
