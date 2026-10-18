using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_Deltas",
                table: "StockMovements");

            migrationBuilder.CreateTable(
                name: "StockDeliveryProofs",
                columns: table => new
                {
                    ReservationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockDeliveryProofs", x => x.ReservationId);
                    table.ForeignKey(
                        name: "FK_StockDeliveryProofs_OpticalWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "OpticalWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDeliveryProofs_StockReservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "StockReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockHolds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Origin = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleasedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    RemovedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedContact = table.Column<bool>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockHolds", x => x.Id);
                    table.CheckConstraint("CK_StockHolds_Profile", "Origin IN (1,2,3,4) AND typeof(Quantity) = 'integer' AND Quantity > 0 AND typeof(ReleasedQuantity) = 'integer' AND ReleasedQuantity >= 0 AND typeof(RemovedQuantity) = 'integer' AND RemovedQuantity >= 0 AND ReleasedQuantity + RemovedQuantity <= Quantity AND OpenedContact IN (0,1) AND (OpenedContact = 0 OR ReleasedQuantity = 0) AND length(trim(Reason)) BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_StockHolds_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockHolds_StockBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "StockBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockCustomerReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReservationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HoldId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ContactPackageOpened = table.Column<bool>(type: "INTEGER", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<string>(type: "TEXT", nullable: false),
                    ReturnedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCustomerReturns", x => x.Id);
                    table.CheckConstraint("CK_StockCustomerReturns_Profile", "typeof(Quantity) = 'integer' AND Quantity > 0 AND (ContactPackageOpened IS NULL OR ContactPackageOpened IN (0,1)) AND length(trim(Reason)) BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_StockCustomerReturns_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCustomerReturns_StockHolds_HoldId",
                        column: x => x.HoldId,
                        principalTable: "StockHolds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCustomerReturns_StockReservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "StockReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HoldId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Approved = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProductMatches = table.Column<bool>(type: "INTEGER", nullable: false),
                    ItemIntact = table.Column<bool>(type: "INTEGER", nullable: false),
                    SealIntact = table.Column<bool>(type: "INTEGER", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReviews", x => x.Id);
                    table.CheckConstraint("CK_StockReviews_Profile", "typeof(Quantity) = 'integer' AND Quantity > 0 AND Role IN (1,2) AND Approved IN (0,1) AND ProductMatches IN (0,1) AND ItemIntact IN (0,1) AND (SealIntact IS NULL OR SealIntact IN (0,1)) AND (Approved = 0 OR (ProductMatches = 1 AND ItemIntact = 1)) AND length(trim(Reason)) BETWEEN 1 AND 500 AND length(RequestHash) = 64 AND RequestHash NOT GLOB '*[^0-9A-F]*'");
                    table.ForeignKey(
                        name: "FK_StockReviews_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReviews_StockHolds_HoldId",
                        column: x => x.HoldId,
                        principalTable: "StockHolds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockWithdrawals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HoldId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SupplierId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockWithdrawals", x => x.Id);
                    table.CheckConstraint("CK_StockWithdrawals_Profile", "typeof(Quantity) = 'integer' AND Quantity > 0 AND ((Kind = 6 AND SupplierId IS NULL) OR (Kind = 10 AND SupplierId IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_StockWithdrawals_StockBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "StockBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockWithdrawals_StockHolds_HoldId",
                        column: x => x.HoldId,
                        principalTable: "StockHolds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockWithdrawals_StockMovements_Id",
                        column: x => x.Id,
                        principalTable: "StockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockWithdrawals_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_Deltas",
                table: "StockMovements",
                sql: "typeof(PhysicalDelta) = 'integer' AND typeof(ReservedDelta) = 'integer' AND typeof(QuarantineDelta) = 'integer' AND ((Kind = 1 AND ReservationId IS NULL AND PhysicalDelta > 0 AND ReservedDelta = 0 AND QuarantineDelta BETWEEN 0 AND PhysicalDelta) OR (Kind = 2 AND ReservationId IS NOT NULL AND PhysicalDelta = 0 AND ReservedDelta > 0 AND QuarantineDelta = 0) OR (Kind = 3 AND ReservationId IS NOT NULL AND PhysicalDelta = 0 AND ReservedDelta < 0 AND QuarantineDelta = 0) OR (Kind = 4 AND ReservationId IS NOT NULL AND PhysicalDelta < 0 AND PhysicalDelta = ReservedDelta AND QuarantineDelta = 0) OR (Kind IN (5,6,10) AND ReservationId IS NULL AND ReservedDelta = 0 AND PhysicalDelta <> 0 AND (Kind = 5 OR PhysicalDelta < 0) AND (QuarantineDelta = 0 OR QuarantineDelta = PhysicalDelta)) OR (Kind = 7 AND ReservationId IS NULL AND PhysicalDelta = 0 AND ReservedDelta = 0 AND QuarantineDelta > 0) OR (Kind = 8 AND ReservationId IS NULL AND PhysicalDelta = 0 AND ReservedDelta = 0 AND QuarantineDelta < 0) OR (Kind = 9 AND ReservationId IS NOT NULL AND PhysicalDelta > 0 AND ReservedDelta = 0 AND QuarantineDelta = PhysicalDelta))");

            migrationBuilder.CreateIndex(
                name: "IX_StockCustomerReturns_ActorUserId",
                table: "StockCustomerReturns",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCustomerReturns_HoldId",
                table: "StockCustomerReturns",
                column: "HoldId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockCustomerReturns_ReservationId_ReturnedAtUtc_Id",
                table: "StockCustomerReturns",
                columns: new[] { "ReservationId", "ReturnedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockDeliveryProofs_WorkId",
                table: "StockDeliveryProofs",
                column: "WorkId");

            migrationBuilder.CreateIndex(
                name: "IX_StockHolds_BatchId_CreatedAtUtc_Id",
                table: "StockHolds",
                columns: new[] { "BatchId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockHolds_CreatedByUserId",
                table: "StockHolds",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReviews_ActorUserId",
                table: "StockReviews",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReviews_HoldId_ReviewedAtUtc_Id",
                table: "StockReviews",
                columns: new[] { "HoldId", "ReviewedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockWithdrawals_BatchId",
                table: "StockWithdrawals",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockWithdrawals_HoldId",
                table: "StockWithdrawals",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_StockWithdrawals_SupplierId",
                table: "StockWithdrawals",
                column: "SupplierId");

            // Conservar el saldo previo como un ingreso histórico a cuarentena, sin cambiar el lote ni su físico.
            migrationBuilder.Sql("""
                INSERT INTO StockHolds
                    (Id, BatchId, Origin, Quantity, ReleasedQuantity, RemovedQuantity, OpenedContact, Reason, CreatedByUserId, CreatedAtUtc, Version)
                SELECT Id, Id, 1, Quarantined, 0, 0, 0,
                    'Cuarentena previa conservada al ampliar el inventario', ReceivedByUserId, ReceivedAtUtc, Id
                FROM StockBatches WHERE Quarantined > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockCustomerReturns");

            migrationBuilder.DropTable(
                name: "StockDeliveryProofs");

            migrationBuilder.DropTable(
                name: "StockReviews");

            migrationBuilder.DropTable(
                name: "StockWithdrawals");

            migrationBuilder.DropTable(
                name: "StockHolds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_Deltas",
                table: "StockMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_Deltas",
                table: "StockMovements",
                sql: "typeof(PhysicalDelta) = 'integer' AND typeof(ReservedDelta) = 'integer' AND typeof(QuarantineDelta) = 'integer' AND ((Kind = 1 AND ReservationId IS NULL AND PhysicalDelta > 0 AND ReservedDelta = 0 AND QuarantineDelta BETWEEN 0 AND PhysicalDelta) OR (Kind = 2 AND ReservationId IS NOT NULL AND PhysicalDelta = 0 AND ReservedDelta > 0 AND QuarantineDelta = 0) OR (Kind = 3 AND ReservationId IS NOT NULL AND PhysicalDelta = 0 AND ReservedDelta < 0 AND QuarantineDelta = 0) OR (Kind = 4 AND ReservationId IS NOT NULL AND PhysicalDelta < 0 AND PhysicalDelta = ReservedDelta AND QuarantineDelta = 0))");
        }
    }
}
