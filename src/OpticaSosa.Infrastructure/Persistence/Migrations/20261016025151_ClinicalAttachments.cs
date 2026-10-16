using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicalAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginalName = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Length = table.Column<long>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    RetiredByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RetirementReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalAttachments", x => x.Id);
                    table.CheckConstraint("CK_ClinicalAttachment_State", "State IN (0, 1, 2, 3) AND Length > 0 AND length(Sha256) = 64");
                    table.ForeignKey(
                        name: "FK_ClinicalAttachments_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalAttachments_AspNetUsers_RetiredByUserId",
                        column: x => x.RetiredByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalAttachments_ClinicalRevisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "ClinicalRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalFilePolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    PhotoMiB = table.Column<int>(type: "INTEGER", nullable: false),
                    PdfMiB = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalFilePolicies", x => x.Id);
                    table.CheckConstraint("CK_ClinicalFilePolicy_Singleton", "Id = 1 AND PhotoMiB > 0 AND PdfMiB > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalAttachments_CreatedByUserId",
                table: "ClinicalAttachments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalAttachments_RetiredByUserId",
                table: "ClinicalAttachments",
                column: "RetiredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalAttachments_RevisionId_CreatedAtUtc_Id",
                table: "ClinicalAttachments",
                columns: new[] { "RevisionId", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicalAttachments");

            migrationBuilder.DropTable(
                name: "ClinicalFilePolicies");
        }
    }
}
