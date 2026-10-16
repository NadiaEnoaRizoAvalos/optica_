using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QualityWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Professionals",
                type: "TEXT",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Professionals",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Specialty",
                table: "Professionals",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFollowUp",
                table: "ClinicalRevisions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReportedBy",
                table: "ClinicalRevisions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReportedSymptoms",
                table: "ClinicalRevisions",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OpticalWorks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PrescriptionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PrescriptionRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Product = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Frame = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    TechnicalApproved = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeliveryApproved = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpticalWorks", x => x.Id);
                    table.CheckConstraint("CK_OpticalWork_Prescription", "(PrescriptionId IS NULL) = (PrescriptionRevisionId IS NULL)");
                    table.CheckConstraint("CK_OpticalWork_State", "Status BETWEEN 0 AND 5 AND Round > 0 AND Type BETWEEN 0 AND 2 AND (Status NOT IN (4, 5) OR (TechnicalApproved = 1 AND DeliveryApproved = 1))");
                    table.ForeignKey(
                        name: "FK_OpticalWorks_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpticalWorks_ClinicalRecords_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "ClinicalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpticalWorks_ClinicalRevisions_PrescriptionRevisionId",
                        column: x => x.PrescriptionRevisionId,
                        principalTable: "ClinicalRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpticalWorks_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityTemplates",
                columns: table => new
                {
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Stage = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityTemplates", x => new { x.Type, x.Stage });
                });

            migrationBuilder.CreateTable(
                name: "QualityControls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    Stage = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    TemplateVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityControls", x => x.Id);
                    table.CheckConstraint("CK_QualityControl_Approval", "Stage IN (0, 1) AND Status IN (0, 1, 2) AND Role IN (0, 1) AND (Stage <> 0 OR Role = 0) AND (Status <> 2 OR ApprovedAtUtc IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_QualityControls_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityControls_OpticalWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "OpticalWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkId = table.Column<Guid>(type: "TEXT", nullable: false),
                    From = table.Column<int>(type: "INTEGER", nullable: false),
                    To = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkTransitions_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkTransitions_OpticalWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "OpticalWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityTemplateItem",
                columns: table => new
                {
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Stage = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Required = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityTemplateItem", x => new { x.Type, x.Stage, x.Key });
                    table.ForeignKey(
                        name: "FK_QualityTemplateItem_QualityTemplates_Type_Stage",
                        columns: x => new { x.Type, x.Stage },
                        principalTable: "QualityTemplates",
                        principalColumns: new[] { "Type", "Stage" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityCheckItem",
                columns: table => new
                {
                    ControlId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Applicable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Required = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityCheckItem", x => new { x.ControlId, x.Key });
                    table.CheckConstraint("CK_QualityCheckItem_Status", "Status BETWEEN 0 AND 3 AND (Status <> 3 OR (Applicable = 0 AND Notes IS NOT NULL AND length(trim(Notes)) > 0))");
                    table.ForeignKey(
                        name: "FK_QualityCheckItem_QualityControls_ControlId",
                        column: x => x.ControlId,
                        principalTable: "QualityControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpticalWorks_CreatedByUserId",
                table: "OpticalWorks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OpticalWorks_CustomerId_CreatedAtUtc",
                table: "OpticalWorks",
                columns: new[] { "CustomerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OpticalWorks_PrescriptionId",
                table: "OpticalWorks",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_OpticalWorks_PrescriptionRevisionId",
                table: "OpticalWorks",
                column: "PrescriptionRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityControls_ActorId",
                table: "QualityControls",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityControls_WorkId_AtUtc",
                table: "QualityControls",
                columns: new[] { "WorkId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTransitions_ActorId",
                table: "WorkTransitions",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTransitions_WorkId_AtUtc",
                table: "WorkTransitions",
                columns: new[] { "WorkId", "AtUtc" });

            // Reforzar los vínculos sin reconstruir tablas clínicas existentes en SQLite.
            foreach (var operation in new[] { "INSERT", "UPDATE" })
                migrationBuilder.Sql($"""
                    CREATE TRIGGER TR_OpticalWorks_Prescription_{operation}
                    BEFORE {operation} ON OpticalWorks
                    WHEN NEW.PrescriptionId IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM ClinicalRecords c JOIN ClinicalRevisions r ON r.RecordId = c.Id
                        WHERE c.Id = NEW.PrescriptionId AND c.CustomerId = NEW.CustomerId AND c.Kind IN (0, 1)
                        AND r.Id = NEW.PrescriptionRevisionId)
                    BEGIN SELECT RAISE(ABORT, 'Invalid prescription ownership or revision'); END;
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_OpticalWorks_Prescription_INSERT;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_OpticalWorks_Prescription_UPDATE;");
            migrationBuilder.DropTable(
                name: "QualityCheckItem");

            migrationBuilder.DropTable(
                name: "QualityTemplateItem");

            migrationBuilder.DropTable(
                name: "WorkTransitions");

            migrationBuilder.DropTable(
                name: "QualityControls");

            migrationBuilder.DropTable(
                name: "QualityTemplates");

            migrationBuilder.DropTable(
                name: "OpticalWorks");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Professionals");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Professionals");

            migrationBuilder.DropColumn(
                name: "Specialty",
                table: "Professionals");

            migrationBuilder.DropColumn(
                name: "IsFollowUp",
                table: "ClinicalRevisions");

            migrationBuilder.DropColumn(
                name: "ReportedBy",
                table: "ClinicalRevisions");

            migrationBuilder.DropColumn(
                name: "ReportedSymptoms",
                table: "ClinicalRevisions");
        }
    }
}
