using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LaboratoryOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LaboratoryOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SupplierId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TechnicalAssignmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    QuarantinedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    CancelledQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AuthorizedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorizedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DispatchReference = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    LastReceivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaboratoryOrders", x => x.Id);
                    table.CheckConstraint("CK_LaboratoryOrders_Counts", "typeof(Quantity) = 'integer' AND Quantity > 0 AND typeof(ReceivedQuantity) = 'integer' AND ReceivedQuantity >= 0 AND typeof(CancelledQuantity) = 'integer' AND CancelledQuantity >= 0 AND ReceivedQuantity + CancelledQuantity <= Quantity AND typeof(QuarantinedQuantity) = 'integer' AND QuarantinedQuantity BETWEEN 0 AND ReceivedQuantity");
                    table.CheckConstraint("CK_LaboratoryOrders_Dispatch", "(SentAtUtc IS NULL AND SentByUserId IS NULL AND DispatchReference IS NULL AND ReceivedQuantity = 0 AND LastReceivedAtUtc IS NULL) OR (SentAtUtc IS NOT NULL AND SentByUserId IS NOT NULL AND DispatchReference IS NOT NULL AND length(trim(DispatchReference)) BETWEEN 1 AND 160)");
                    table.CheckConstraint("CK_LaboratoryOrders_ReceiptTime", "(ReceivedQuantity = 0 AND LastReceivedAtUtc IS NULL) OR (ReceivedQuantity > 0 AND LastReceivedAtUtc IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LaboratoryOrders_AspNetUsers_AuthorizedByUserId",
                        column: x => x.AuthorizedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaboratoryOrders_AspNetUsers_SentByUserId",
                        column: x => x.SentByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaboratoryOrders_OrderTechnicalAssignments_TechnicalAssignmentId",
                        column: x => x.TechnicalAssignmentId,
                        principalTable: "OrderTechnicalAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaboratoryOrders_SalesLines_LineId",
                        column: x => x.LineId,
                        principalTable: "SalesLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaboratoryOrders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LaboratoryOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    CancelledQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousEstimatedDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    NewEstimatedDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    LaboratoryConfirmation = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ActorUserId = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaboratoryChanges", x => x.Id);
                    table.CheckConstraint("CK_LaboratoryChanges_Profile", "length(trim(Reason)) BETWEEN 1 AND 500 AND typeof(CancelledQuantity) = 'integer' AND ((Kind = 1 AND CancelledQuantity = 0 AND LaboratoryConfirmation IS NULL) OR (Kind = 2 AND CancelledQuantity > 0 AND PreviousEstimatedDate IS NULL AND NewEstimatedDate IS NULL))");
                    table.ForeignKey(
                        name: "FK_LaboratoryChanges_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaboratoryChanges_LaboratoryOrders_LaboratoryOrderId",
                        column: x => x.LaboratoryOrderId,
                        principalTable: "LaboratoryOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LaboratoryOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    QuarantinedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaboratoryReceipts", x => x.Id);
                    table.CheckConstraint("CK_LaboratoryReceipts_Profile", "typeof(Quantity) = 'integer' AND Quantity > 0 AND typeof(QuarantinedQuantity) = 'integer' AND QuarantinedQuantity BETWEEN 0 AND Quantity AND length(trim(Reason)) BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_LaboratoryReceipts_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaboratoryReceipts_LaboratoryOrders_LaboratoryOrderId",
                        column: x => x.LaboratoryOrderId,
                        principalTable: "LaboratoryOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaboratoryReceipts_StockBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "StockBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryChanges_ActorUserId",
                table: "LaboratoryChanges",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryChanges_LaboratoryOrderId_OccurredAtUtc_Id",
                table: "LaboratoryChanges",
                columns: new[] { "LaboratoryOrderId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryOrders_AuthorizedAtUtc_Id",
                table: "LaboratoryOrders",
                columns: new[] { "AuthorizedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryOrders_AuthorizedByUserId",
                table: "LaboratoryOrders",
                column: "AuthorizedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryOrders_LineId_AuthorizedAtUtc_Id",
                table: "LaboratoryOrders",
                columns: new[] { "LineId", "AuthorizedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryOrders_SentByUserId",
                table: "LaboratoryOrders",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryOrders_SupplierId",
                table: "LaboratoryOrders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryOrders_TechnicalAssignmentId",
                table: "LaboratoryOrders",
                column: "TechnicalAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryReceipts_ActorUserId",
                table: "LaboratoryReceipts",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryReceipts_BatchId",
                table: "LaboratoryReceipts",
                column: "BatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryReceipts_LaboratoryOrderId_ReceivedAtUtc_Id",
                table: "LaboratoryReceipts",
                columns: new[] { "LaboratoryOrderId", "ReceivedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LaboratoryChanges");

            migrationBuilder.DropTable(
                name: "LaboratoryReceipts");

            migrationBuilder.DropTable(
                name: "LaboratoryOrders");
        }
    }
}
