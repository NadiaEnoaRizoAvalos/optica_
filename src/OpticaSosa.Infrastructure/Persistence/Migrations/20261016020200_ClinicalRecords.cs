using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicalRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VisitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalRecords", x => x.Id);
                    table.CheckConstraint("CK_ClinicalRecord_Kind", "Kind IN (0, 1, 2) AND RevisionNumber > 0");
                    table.ForeignKey(
                        name: "FK_ClinicalRecords_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalRecords_Visits_CustomerId_VisitId",
                        columns: x => new { x.CustomerId, x.VisitId },
                        principalTable: "Visits",
                        principalColumns: new[] { "CustomerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Professionals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Registration = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Jurisdiction = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    DuplicateKey = table.Column<string>(type: "TEXT", maxLength: 262, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Professionals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Professionals_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ProfessionalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProfessionalName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ProfessionalRegistration = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    ProfessionalJurisdiction = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    PupillaryDistanceHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    CorrectionReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RecordedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalRevisions", x => x.Id);
                    table.CheckConstraint("CK_ClinicalRevision_Correction", "Number = 1 OR (Number > 1 AND CorrectionReason IS NOT NULL AND length(trim(CorrectionReason)) > 0)");
                    table.ForeignKey(
                        name: "FK_ClinicalRevisions_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalRevisions_ClinicalRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "ClinicalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalRevisions_Professionals_ProfessionalId",
                        column: x => x.ProfessionalId,
                        principalTable: "Professionals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EyeMeasurements",
                columns: table => new
                {
                    RevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Side = table.Column<int>(type: "INTEGER", nullable: false),
                    SphereMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    CylinderMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    Axis = table.Column<int>(type: "INTEGER", nullable: true),
                    AdditionMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    PrismMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    Base = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EyeMeasurements", x => new { x.RevisionId, x.Side });
                    table.CheckConstraint("CK_EyeMeasurement_SideAxis", "Side IN (0, 1) AND (Axis IS NULL OR Axis BETWEEN 0 AND 180)");
                    table.ForeignKey(
                        name: "FK_EyeMeasurements_ClinicalRevisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "ClinicalRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalRecords_CustomerId_Id",
                table: "ClinicalRecords",
                columns: new[] { "CustomerId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalRecords_CustomerId_VisitId",
                table: "ClinicalRecords",
                columns: new[] { "CustomerId", "VisitId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalRevisions_ProfessionalId",
                table: "ClinicalRevisions",
                column: "ProfessionalId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalRevisions_RecordedByUserId",
                table: "ClinicalRevisions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalRevisions_RecordId_Number",
                table: "ClinicalRevisions",
                columns: new[] { "RecordId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Professionals_CreatedByUserId",
                table: "Professionals",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Professionals_DuplicateKey",
                table: "Professionals",
                column: "DuplicateKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EyeMeasurements");

            migrationBuilder.DropTable(
                name: "ClinicalRevisions");

            migrationBuilder.DropTable(
                name: "ClinicalRecords");

            migrationBuilder.DropTable(
                name: "Professionals");
        }
    }
}
