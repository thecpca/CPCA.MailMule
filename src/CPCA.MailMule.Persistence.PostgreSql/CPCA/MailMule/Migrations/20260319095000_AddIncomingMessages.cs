using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CPCA.MailMule.Migrations
{
    public partial class AddIncomingMessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncomingMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MailboxConfigId = table.Column<long>(type: "bigint", nullable: false),
                    Uid = table.Column<long>(type: "bigint", nullable: false),
                    UidValidity = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    DestinationMailboxConfigId = table.Column<long>(type: "bigint", nullable: true),
                    DiscoveredUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StateChangedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorDetail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomingMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncomingMessages_MailboxConfigId_State",
                table: "IncomingMessages",
                columns: new[] { "MailboxConfigId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_IncomingMessages_MailboxConfigId_Uid",
                table: "IncomingMessages",
                columns: new[] { "MailboxConfigId", "Uid" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncomingMessages");
        }
    }
}