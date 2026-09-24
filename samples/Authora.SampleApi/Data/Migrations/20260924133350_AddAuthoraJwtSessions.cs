using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authora.SampleApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthoraJwtSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authora_jwt_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authora_jwt_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "authora_refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SecretHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReplacedById = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authora_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_authora_refresh_tokens_authora_jwt_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "authora_jwt_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_authora_jwt_sessions_SubjectId",
                table: "authora_jwt_sessions",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_authora_refresh_tokens_SessionId",
                table: "authora_refresh_tokens",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authora_refresh_tokens");

            migrationBuilder.DropTable(
                name: "authora_jwt_sessions");
        }
    }
}
