using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CPCA.MailMule.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Configuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    InactivityTimeoutMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MailboxConfigs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MailboxType = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImapHost = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ImapPort = table.Column<int>(type: "integer", nullable: false),
                    Security = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    EncryptedPassword = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    InboxFolderPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    OutboxFolderPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SentFolderPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TrashFolderPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PollIntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    DeleteMessage = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastPolledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    UndoWindowSeconds = table.Column<int>(type: "integer", nullable: false),
                    PageSize = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ApplicationSettings",
                columns: new[] { "Id", "InactivityTimeoutMinutes" },
                values: new object[] { 1, 30 });

            migrationBuilder.InsertData(
                table: "UserSettings",
                columns: new[] { "Id", "PageSize", "UndoWindowSeconds" },
                values: new object[] { 1, 25, 15 });

            migrationBuilder.CreateIndex(
                name: "IX_MailboxConfigs_MailboxType_SortOrder",
                table: "MailboxConfigs",
                columns: new[] { "MailboxType", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSettings");

            migrationBuilder.DropTable(
                name: "MailboxConfigs");

            migrationBuilder.DropTable(
                name: "UserSettings");
        }
    }
}
