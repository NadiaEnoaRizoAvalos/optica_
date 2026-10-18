using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TechnicalPublications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TechnicalPublications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Purpose = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedCategory = table.Column<int>(type: "INTEGER", nullable: false),
                    IncludeRight = table.Column<bool>(type: "INTEGER", nullable: false),
                    IncludeLeft = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequestedProduct = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Material = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IndexThousandths = table.Column<int>(type: "INTEGER", nullable: true),
                    Treatment = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    RightHeightHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    LeftHeightHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    RightMonocularHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    LeftMonocularHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    FrameVariantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FrameReference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    MountingInstructions = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    RightContactVariantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RightContactBaseCurveHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    RightContactDiameterHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    LeftContactVariantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LeftContactBaseCurveHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    LeftContactDiameterHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    RightSphereMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    RightCylinderMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    RightAxis = table.Column<int>(type: "INTEGER", nullable: true),
                    RightAdditionMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    RightPrismMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    RightBase = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    LeftSphereMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    LeftCylinderMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    LeftAxis = table.Column<int>(type: "INTEGER", nullable: true),
                    LeftAdditionMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    LeftPrismMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    LeftBase = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    PupillaryDistanceHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    PreviousId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SupersededById = table.Column<Guid>(type: "TEXT", nullable: true),
                    CorrectionReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PublishedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    RevocationReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalPublications", x => x.Id);
                    table.CheckConstraint("CK_TechnicalPublication_ContactMounting", "Purpose = 1 OR (RightHeightHundredths IS NULL AND LeftHeightHundredths IS NULL AND RightMonocularHundredths IS NULL AND LeftMonocularHundredths IS NULL AND FrameVariantId IS NULL AND FrameReference IS NULL AND MountingInstructions IS NULL AND PupillaryDistanceHundredths IS NULL)");
                    table.CheckConstraint("CK_TechnicalPublication_Correction", "(PreviousId IS NULL OR (PreviousId <> Id AND CorrectionReason IS NOT NULL AND length(trim(CorrectionReason)) > 0)) AND (SupersededById IS NULL OR SupersededById <> Id)");
                    table.CheckConstraint("CK_TechnicalPublication_Index", "IndexThousandths IS NULL OR (typeof(IndexThousandths) = 'integer' AND IndexThousandths BETWEEN 1 AND 9999000)");
                    table.CheckConstraint("CK_TechnicalPublication_LeftAdditionMilli", "LeftAdditionMilli IS NULL OR (typeof(LeftAdditionMilli) = 'integer' AND LeftAdditionMilli BETWEEN -9999000 AND 9999000)");
                    table.CheckConstraint("CK_TechnicalPublication_LeftAxis", "LeftAxis IS NULL OR (typeof(LeftAxis) = 'integer' AND LeftAxis BETWEEN 0 AND 180)");
                    table.CheckConstraint("CK_TechnicalPublication_LeftContact", "(Purpose = 2 AND IncludeLeft = 1 AND LeftContactVariantId IS NOT NULL AND LeftContactBaseCurveHundredths IS NOT NULL AND LeftContactDiameterHundredths IS NOT NULL AND LeftContactBaseCurveHundredths > 0 AND LeftContactDiameterHundredths > 0) OR ((Purpose = 1 OR IncludeLeft = 0) AND LeftContactVariantId IS NULL AND LeftContactBaseCurveHundredths IS NULL AND LeftContactDiameterHundredths IS NULL)");
                    table.CheckConstraint("CK_TechnicalPublication_LeftContactBaseCurveHundredths", "LeftContactBaseCurveHundredths IS NULL OR (typeof(LeftContactBaseCurveHundredths) = 'integer' AND LeftContactBaseCurveHundredths BETWEEN 0 AND 999900)");
                    table.CheckConstraint("CK_TechnicalPublication_LeftContactDiameterHundredths", "LeftContactDiameterHundredths IS NULL OR (typeof(LeftContactDiameterHundredths) = 'integer' AND LeftContactDiameterHundredths BETWEEN 0 AND 999900)");
                    table.CheckConstraint("CK_TechnicalPublication_LeftCylinderMilli", "LeftCylinderMilli IS NULL OR (typeof(LeftCylinderMilli) = 'integer' AND LeftCylinderMilli BETWEEN -9999000 AND 9999000)");
                    table.CheckConstraint("CK_TechnicalPublication_LeftEye", "(IncludeLeft = 1 AND LeftSphereMilli IS NOT NULL AND (LeftCylinderMilli IS NULL OR LeftCylinderMilli = 0 OR LeftAxis IS NOT NULL) AND (LeftPrismMilli IS NULL OR LeftPrismMilli = 0 OR (LeftBase IS NOT NULL AND length(trim(LeftBase)) > 0))) OR (IncludeLeft = 0 AND LeftSphereMilli IS NULL AND LeftCylinderMilli IS NULL AND LeftAxis IS NULL AND LeftAdditionMilli IS NULL AND LeftPrismMilli IS NULL AND LeftBase IS NULL AND LeftHeightHundredths IS NULL AND LeftMonocularHundredths IS NULL)");
                    table.CheckConstraint("CK_TechnicalPublication_LeftHeightHundredths", "LeftHeightHundredths IS NULL OR (typeof(LeftHeightHundredths) = 'integer' AND LeftHeightHundredths BETWEEN 0 AND 999900)");
                    table.CheckConstraint("CK_TechnicalPublication_LeftMonocularHundredths", "LeftMonocularHundredths IS NULL OR (typeof(LeftMonocularHundredths) = 'integer' AND LeftMonocularHundredths BETWEEN 0 AND 999900)");
                    table.CheckConstraint("CK_TechnicalPublication_LeftPrismMilli", "LeftPrismMilli IS NULL OR (typeof(LeftPrismMilli) = 'integer' AND LeftPrismMilli BETWEEN -9999000 AND 9999000)");
                    table.CheckConstraint("CK_TechnicalPublication_LeftSphereMilli", "LeftSphereMilli IS NULL OR (typeof(LeftSphereMilli) = 'integer' AND LeftSphereMilli BETWEEN -9999000 AND 9999000)");
                    table.CheckConstraint("CK_TechnicalPublication_PupillaryDistance", "PupillaryDistanceHundredths IS NULL OR (typeof(PupillaryDistanceHundredths) = 'integer' AND PupillaryDistanceHundredths BETWEEN 0 AND 999900)");
                    table.CheckConstraint("CK_TechnicalPublication_Purpose", "Purpose IN (1,2) AND RequestedCategory = Purpose + 1 AND IncludeRight IN (0,1) AND IncludeLeft IN (0,1) AND (IncludeRight = 1 OR IncludeLeft = 1)");
                    table.CheckConstraint("CK_TechnicalPublication_Revocation", "(RevokedAtUtc IS NULL AND RevokedByUserId IS NULL AND RevocationReason IS NULL) OR (RevokedAtUtc IS NOT NULL AND RevokedByUserId IS NOT NULL AND RevocationReason IS NOT NULL AND length(trim(RevocationReason)) > 0)");
                    table.CheckConstraint("CK_TechnicalPublication_RightAdditionMilli", "RightAdditionMilli IS NULL OR (typeof(RightAdditionMilli) = 'integer' AND RightAdditionMilli BETWEEN -9999000 AND 9999000)");
                    table.CheckConstraint("CK_TechnicalPublication_RightAxis", "RightAxis IS NULL OR (typeof(RightAxis) = 'integer' AND RightAxis BETWEEN 0 AND 180)");
                    table.CheckConstraint("CK_TechnicalPublication_RightContact", "(Purpose = 2 AND IncludeRight = 1 AND RightContactVariantId IS NOT NULL AND RightContactBaseCurveHundredths IS NOT NULL AND RightContactDiameterHundredths IS NOT NULL AND RightContactBaseCurveHundredths > 0 AND RightContactDiameterHundredths > 0) OR ((Purpose = 1 OR IncludeRight = 0) AND RightContactVariantId IS NULL AND RightContactBaseCurveHundredths IS NULL AND RightContactDiameterHundredths IS NULL)");
                    table.CheckConstraint("CK_TechnicalPublication_RightContactBaseCurveHundredths", "RightContactBaseCurveHundredths IS NULL OR (typeof(RightContactBaseCurveHundredths) = 'integer' AND RightContactBaseCurveHundredths BETWEEN 0 AND 999900)");
                    table.CheckConstraint("CK_TechnicalPublication_RightContactDiameterHundredths", "RightContactDiameterHundredths IS NULL OR (typeof(RightContactDiameterHundredths) = 'integer' AND RightContactDiameterHundredths BETWEEN 0 AND 999900)");
                    table.CheckConstraint("CK_TechnicalPublication_RightCylinderMilli", "RightCylinderMilli IS NULL OR (typeof(RightCylinderMilli) = 'integer' AND RightCylinderMilli BETWEEN -9999000 AND 9999000)");
                    table.CheckConstraint("CK_TechnicalPublication_RightEye", "(IncludeRight = 1 AND RightSphereMilli IS NOT NULL AND (RightCylinderMilli IS NULL OR RightCylinderMilli = 0 OR RightAxis IS NOT NULL) AND (RightPrismMilli IS NULL OR RightPrismMilli = 0 OR (RightBase IS NOT NULL AND length(trim(RightBase)) > 0))) OR (IncludeRight = 0 AND RightSphereMilli IS NULL AND RightCylinderMilli IS NULL AND RightAxis IS NULL AND RightAdditionMilli IS NULL AND RightPrismMilli IS NULL AND RightBase IS NULL AND RightHeightHundredths IS NULL AND RightMonocularHundredths IS NULL)");
                    table.CheckConstraint("CK_TechnicalPublication_RightHeightHundredths", "RightHeightHundredths IS NULL OR (typeof(RightHeightHundredths) = 'integer' AND RightHeightHundredths BETWEEN 0 AND 999900)");
                    table.CheckConstraint("CK_TechnicalPublication_RightMonocularHundredths", "RightMonocularHundredths IS NULL OR (typeof(RightMonocularHundredths) = 'integer' AND RightMonocularHundredths BETWEEN 0 AND 999900)");
                    table.CheckConstraint("CK_TechnicalPublication_RightPrismMilli", "RightPrismMilli IS NULL OR (typeof(RightPrismMilli) = 'integer' AND RightPrismMilli BETWEEN -9999000 AND 9999000)");
                    table.CheckConstraint("CK_TechnicalPublication_RightSphereMilli", "RightSphereMilli IS NULL OR (typeof(RightSphereMilli) = 'integer' AND RightSphereMilli BETWEEN -9999000 AND 9999000)");
                    table.ForeignKey(
                        name: "FK_TechnicalPublications_AspNetUsers_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalPublications_AspNetUsers_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalPublications_ClinicalRevisions_SourceRevisionId",
                        column: x => x.SourceRevisionId,
                        principalTable: "ClinicalRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalPublications_ProductVariants_FrameVariantId",
                        column: x => x.FrameVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalPublications_ProductVariants_LeftContactVariantId",
                        column: x => x.LeftContactVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalPublications_ProductVariants_RightContactVariantId",
                        column: x => x.RightContactVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalPublications_Products_ProductId_RequestedCategory",
                        columns: x => new { x.ProductId, x.RequestedCategory },
                        principalTable: "Products",
                        principalColumns: new[] { "Id", "Category" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalPublications_TechnicalPublications_PreviousId",
                        column: x => x.PreviousId,
                        principalTable: "TechnicalPublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalPublications_TechnicalPublications_SupersededById",
                        column: x => x.SupersededById,
                        principalTable: "TechnicalPublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalPublications_FrameVariantId",
                table: "TechnicalPublications",
                column: "FrameVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalPublications_LeftContactVariantId",
                table: "TechnicalPublications",
                column: "LeftContactVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalPublications_PreviousId",
                table: "TechnicalPublications",
                column: "PreviousId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalPublications_ProductId_RequestedCategory",
                table: "TechnicalPublications",
                columns: new[] { "ProductId", "RequestedCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalPublications_PublishedByUserId",
                table: "TechnicalPublications",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalPublications_RevokedByUserId",
                table: "TechnicalPublications",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalPublications_RightContactVariantId",
                table: "TechnicalPublications",
                column: "RightContactVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalPublications_SourceRevisionId_PublishedAtUtc_Id",
                table: "TechnicalPublications",
                columns: new[] { "SourceRevisionId", "PublishedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalPublications_SupersededById",
                table: "TechnicalPublications",
                column: "SupersededById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnicalPublications");
        }
    }
}
