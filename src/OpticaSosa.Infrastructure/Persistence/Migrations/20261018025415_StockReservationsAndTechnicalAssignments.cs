using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StockReservationsAndTechnicalAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderTechnicalAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PublicationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Eyes = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreviousId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReplacementReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AssignedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTechnicalAssignments", x => x.Id);
                    table.CheckConstraint("CK_OrderTechnicalAssignments_Profile", "Eyes IN (1,2,3) AND IsCurrent IN (0,1) AND (PreviousId IS NULL OR (PreviousId <> Id AND ReplacementReason IS NOT NULL AND length(trim(ReplacementReason)) > 0))");
                    table.ForeignKey(
                        name: "FK_OrderTechnicalAssignments_AspNetUsers_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderTechnicalAssignments_OrderTechnicalAssignments_PreviousId",
                        column: x => x.PreviousId,
                        principalTable: "OrderTechnicalAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderTechnicalAssignments_SalesLines_LineId",
                        column: x => x.LineId,
                        principalTable: "SalesLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderTechnicalAssignments_TechnicalPublications_PublicationId",
                        column: x => x.PublicationId,
                        principalTable: "TechnicalPublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VariantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Lot = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LotKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ExpiryPrecision = table.Column<int>(type: "INTEGER", nullable: false),
                    OnHand = table.Column<int>(type: "INTEGER", nullable: false),
                    Reserved = table.Column<int>(type: "INTEGER", nullable: false),
                    Quarantined = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBatches", x => x.Id);
                    table.CheckConstraint("CK_StockBatches_Contact", "Category BETWEEN 1 AND 4 AND (Category <> 3 OR (Lot IS NOT NULL AND length(trim(Lot)) > 0 AND LotKey IS NOT NULL AND ExpiresOn IS NOT NULL))");
                    table.CheckConstraint("CK_StockBatches_Counts", "typeof(OnHand) = 'integer' AND OnHand >= 0 AND typeof(Reserved) = 'integer' AND Reserved >= 0 AND typeof(Quarantined) = 'integer' AND Quarantined >= 0 AND Reserved + Quarantined <= OnHand");
                    table.CheckConstraint("CK_StockBatches_Expiry", "(ExpiryPrecision = 0 AND ExpiresOn IS NULL) OR (ExpiryPrecision IN (1,2) AND ExpiresOn IS NOT NULL AND (ExpiryPrecision <> 2 OR ExpiresOn = date(ExpiresOn, 'start of month', '+1 month', '-1 day')))");
                    table.ForeignKey(
                        name: "FK_StockBatches_AspNetUsers_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockBatches_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TechnicalAssignmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClosedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    ClosureReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservations", x => x.Id);
                    table.CheckConstraint("CK_StockReservations_Closure", "(Status = 1 AND ClosedAtUtc IS NULL AND ClosedByUserId IS NULL AND ClosureReason IS NULL) OR (Status IN (2,3) AND ClosedAtUtc IS NOT NULL AND ClosedByUserId IS NOT NULL AND ClosureReason IS NOT NULL AND length(trim(ClosureReason)) > 0)");
                    table.CheckConstraint("CK_StockReservations_Quantity", "typeof(Quantity) = 'integer' AND Quantity > 0");
                    table.ForeignKey(
                        name: "FK_StockReservations_AspNetUsers_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReservations_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReservations_OrderTechnicalAssignments_TechnicalAssignmentId",
                        column: x => x.TechnicalAssignmentId,
                        principalTable: "OrderTechnicalAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReservations_SalesLines_LineId",
                        column: x => x.LineId,
                        principalTable: "SalesLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReservations_StockBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "StockBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReservationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    PhysicalDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    ReservedDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    QuarantineDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_StockMovements_Deltas", "typeof(PhysicalDelta) = 'integer' AND typeof(ReservedDelta) = 'integer' AND typeof(QuarantineDelta) = 'integer' AND ((Kind = 1 AND ReservationId IS NULL AND PhysicalDelta > 0 AND ReservedDelta = 0 AND QuarantineDelta BETWEEN 0 AND PhysicalDelta) OR (Kind = 2 AND ReservationId IS NOT NULL AND PhysicalDelta = 0 AND ReservedDelta > 0 AND QuarantineDelta = 0) OR (Kind = 3 AND ReservationId IS NOT NULL AND PhysicalDelta = 0 AND ReservedDelta < 0 AND QuarantineDelta = 0) OR (Kind = 4 AND ReservationId IS NOT NULL AND PhysicalDelta < 0 AND PhysicalDelta = ReservedDelta AND QuarantineDelta = 0))");
                    table.CheckConstraint("CK_StockMovements_Metadata", "length(trim(Reason)) BETWEEN 1 AND 500 AND length(RequestHash) = 64 AND RequestHash NOT GLOB '*[^0-9A-F]*'");
                    table.ForeignKey(
                        name: "FK_StockMovements_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "StockBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockReservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "StockReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderTechnicalAssignments_AssignedByUserId",
                table: "OrderTechnicalAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderTechnicalAssignments_LineId",
                table: "OrderTechnicalAssignments",
                column: "LineId",
                unique: true,
                filter: "IsCurrent = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OrderTechnicalAssignments_PreviousId",
                table: "OrderTechnicalAssignments",
                column: "PreviousId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderTechnicalAssignments_PublicationId",
                table: "OrderTechnicalAssignments",
                column: "PublicationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_ReceivedByUserId",
                table: "StockBatches",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_VariantId_ExpiresOn_Id",
                table: "StockBatches",
                columns: new[] { "VariantId", "ExpiresOn", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_VariantId_LotKey",
                table: "StockBatches",
                columns: new[] { "VariantId", "LotKey" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ActorUserId",
                table: "StockMovements",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_BatchId_OccurredAtUtc_Id",
                table: "StockMovements",
                columns: new[] { "BatchId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReservationId",
                table: "StockMovements",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_BatchId",
                table: "StockReservations",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_ClosedByUserId",
                table: "StockReservations",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_CreatedByUserId",
                table: "StockReservations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_LineId_Status",
                table: "StockReservations",
                columns: new[] { "LineId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_TechnicalAssignmentId",
                table: "StockReservations",
                column: "TechnicalAssignmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "StockReservations");

            migrationBuilder.DropTable(
                name: "OrderTechnicalAssignments");

            migrationBuilder.DropTable(
                name: "StockBatches");
        }
    }
}
