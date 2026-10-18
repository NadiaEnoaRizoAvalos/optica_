using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesOrdersAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DraftNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    ReceivedMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    RefundedMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    PromisedDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConfirmedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelledByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrders", x => x.Id);
                    table.UniqueConstraint("AK_SalesOrders_Id_Currency", x => new { x.Id, x.Currency });
                    table.CheckConstraint("CK_SalesOrders_Amounts", "typeof(TotalMinor) = 'integer' AND TotalMinor >= 0 AND typeof(ReceivedMinor) = 'integer' AND ReceivedMinor >= 0 AND typeof(RefundedMinor) = 'integer' AND RefundedMinor BETWEEN 0 AND ReceivedMinor AND ReceivedMinor - RefundedMinor <= TotalMinor");
                    table.CheckConstraint("CK_SalesOrders_Cancellation", "(Status <> 3 AND CancelledAtUtc IS NULL AND CancelledByUserId IS NULL AND CancellationReason IS NULL) OR (Status = 3 AND CancelledAtUtc IS NOT NULL AND CancelledByUserId IS NOT NULL AND CancellationReason IS NOT NULL AND length(trim(CancellationReason)) > 0 AND ReceivedMinor = RefundedMinor)");
                    table.CheckConstraint("CK_SalesOrders_Confirmation", "(ConfirmedAtUtc IS NULL AND ConfirmedByUserId IS NULL AND Status <> 2) OR (ConfirmedAtUtc IS NOT NULL AND ConfirmedByUserId IS NOT NULL AND Status <> 1)");
                    table.CheckConstraint("CK_SalesOrders_Profile", "Status IN (1,2,3) AND Currency GLOB '[A-Z][A-Z][A-Z]' AND typeof(DraftNumber) = 'integer' AND DraftNumber > 0");
                    table.ForeignKey(
                        name: "FK_SalesOrders_AspNetUsers_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_AspNetUsers_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderCommandReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderCommandReceipts", x => x.Id);
                    table.CheckConstraint("CK_OrderCommandReceipts_Profile", "length(trim(Action)) BETWEEN 1 AND 80 AND length(RequestHash) = 64 AND RequestHash NOT GLOB '*[^0-9A-F]*'");
                    table.ForeignKey(
                        name: "FK_OrderCommandReceipts_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderCommandReceipts_SalesOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderMoneyEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    OriginalPaymentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Method = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ActorUserId = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderMoneyEntries", x => x.Id);
                    table.UniqueConstraint("AK_OrderMoneyEntries_Id_OrderId", x => new { x.Id, x.OrderId });
                    table.CheckConstraint("CK_OrderMoneyEntries_Original", "(Kind = 1 AND OriginalPaymentId IS NULL AND Reason IS NULL) OR (Kind = 2 AND OriginalPaymentId IS NOT NULL AND OriginalPaymentId <> Id AND Reason IS NOT NULL AND length(trim(Reason)) > 0)");
                    table.CheckConstraint("CK_OrderMoneyEntries_Profile", "typeof(AmountMinor) = 'integer' AND AmountMinor > 0 AND Currency GLOB '[A-Z][A-Z][A-Z]' AND length(trim(Method)) BETWEEN 1 AND 80");
                    table.ForeignKey(
                        name: "FK_OrderMoneyEntries_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderMoneyEntries_OrderMoneyEntries_OriginalPaymentId_OrderId",
                        columns: x => new { x.OriginalPaymentId, x.OrderId },
                        principalTable: "OrderMoneyEntries",
                        principalColumns: new[] { "Id", "OrderId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderMoneyEntries_SalesOrders_OrderId_Currency",
                        columns: x => new { x.OrderId, x.Currency },
                        principalTable: "SalesOrders",
                        principalColumns: new[] { "Id", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DraftNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    VariantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    VariantCode = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Unit = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitsPerPackage = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPriceMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    DiscountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    DiscountReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TotalMinor = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesLines", x => x.Id);
                    table.CheckConstraint("CK_SalesLines_Amounts", "typeof(UnitPriceMinor) = 'integer' AND UnitPriceMinor >= 0 AND typeof(DiscountMinor) = 'integer' AND DiscountMinor >= 0 AND typeof(UnitPriceMinor * Quantity) = 'integer' AND DiscountMinor <= UnitPriceMinor * Quantity AND typeof(TotalMinor) = 'integer' AND TotalMinor = UnitPriceMinor * Quantity - DiscountMinor");
                    table.CheckConstraint("CK_SalesLines_Discount", "DiscountMinor = 0 OR (DiscountReason IS NOT NULL AND length(trim(DiscountReason)) > 0)");
                    table.CheckConstraint("CK_SalesLines_Profile", "typeof(DraftNumber) = 'integer' AND DraftNumber > 0 AND typeof(Position) = 'integer' AND Position > 0 AND typeof(Quantity) = 'integer' AND Quantity > 0 AND Source IN (1,2) AND Category BETWEEN 1 AND 4 AND length(trim(ProductName)) BETWEEN 1 AND 160 AND length(trim(VariantCode)) BETWEEN 1 AND 80");
                    table.CheckConstraint("CK_SalesLines_Units", "(Category = 3 AND Unit = 2 AND typeof(UnitsPerPackage) = 'integer' AND UnitsPerPackage > 0) OR (Category IN (1,2,4) AND Unit = 1 AND UnitsPerPackage = 1)");
                    table.ForeignKey(
                        name: "FK_SalesLines_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLines_SalesOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderCommandReceipts_ActorUserId",
                table: "OrderCommandReceipts",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderCommandReceipts_OrderId",
                table: "OrderCommandReceipts",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderMoneyEntries_ActorUserId",
                table: "OrderMoneyEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderMoneyEntries_OrderId_Currency",
                table: "OrderMoneyEntries",
                columns: new[] { "OrderId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderMoneyEntries_OrderId_OccurredAtUtc_Id",
                table: "OrderMoneyEntries",
                columns: new[] { "OrderId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderMoneyEntries_OriginalPaymentId_OrderId",
                table: "OrderMoneyEntries",
                columns: new[] { "OriginalPaymentId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_OrderId_DraftNumber_Position",
                table: "SalesLines",
                columns: new[] { "OrderId", "DraftNumber", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesLines_VariantId",
                table: "SalesLines",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CancelledByUserId",
                table: "SalesOrders",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_ConfirmedByUserId",
                table: "SalesOrders",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CreatedAtUtc_Id",
                table: "SalesOrders",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CreatedByUserId",
                table: "SalesOrders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustomerId_CreatedAtUtc_Id",
                table: "SalesOrders",
                columns: new[] { "CustomerId", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderCommandReceipts");

            migrationBuilder.DropTable(
                name: "OrderMoneyEntries");

            migrationBuilder.DropTable(
                name: "SalesLines");

            migrationBuilder.DropTable(
                name: "SalesOrders");
        }
    }
}
