using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommercialWorkDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "WorkTransitions",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommercialWorks",
                columns: table => new
                {
                    WorkId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PreviousWorkId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReplacementReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    TechnicalMaterialHash = table.Column<string>(type: "TEXT", nullable: true),
                    DeliveryMaterialHash = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommercialWorks", x => x.WorkId);
                    table.CheckConstraint("CK_CommercialWorks_Profile", "IsCurrent IN (0,1) AND (PreviousWorkId IS NULL OR (PreviousWorkId <> WorkId AND length(trim(ReplacementReason)) BETWEEN 1 AND 500)) AND (TechnicalMaterialHash IS NULL OR length(TechnicalMaterialHash)=64) AND (DeliveryMaterialHash IS NULL OR length(DeliveryMaterialHash)=64)");
                    table.ForeignKey(
                        name: "FK_CommercialWorks_CommercialWorks_PreviousWorkId",
                        column: x => x.PreviousWorkId,
                        principalTable: "CommercialWorks",
                        principalColumn: "WorkId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialWorks_OpticalWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "OpticalWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialWorks_SalesOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorUserId = table.Column<string>(type: "TEXT", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderDeliveries_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderDeliveries_SalesOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommercialWorkLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TechnicalAssignmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommercialWorkLines", x => x.Id);
                    table.CheckConstraint("CK_CommercialWorkLines_Current", "IsCurrent IN (0,1)");
                    table.ForeignKey(
                        name: "FK_CommercialWorkLines_CommercialWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "CommercialWorks",
                        principalColumn: "WorkId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialWorkLines_OrderTechnicalAssignments_TechnicalAssignmentId",
                        column: x => x.TechnicalAssignmentId,
                        principalTable: "OrderTechnicalAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommercialWorkLines_SalesLines_LineId",
                        column: x => x.LineId,
                        principalTable: "SalesLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialWorkLines_LineId",
                table: "CommercialWorkLines",
                column: "LineId",
                unique: true,
                filter: "IsCurrent = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialWorkLines_TechnicalAssignmentId",
                table: "CommercialWorkLines",
                column: "TechnicalAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialWorkLines_WorkId",
                table: "CommercialWorkLines",
                column: "WorkId");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialWorks_OrderId_IsCurrent",
                table: "CommercialWorks",
                columns: new[] { "OrderId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialWorks_PreviousWorkId",
                table: "CommercialWorks",
                column: "PreviousWorkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderDeliveries_ActorUserId",
                table: "OrderDeliveries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDeliveries_OrderId",
                table: "OrderDeliveries",
                column: "OrderId",
                unique: true);
            foreach (var operation in new[] { "INSERT", "UPDATE" })
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER TR_CommercialWorks_Ownership_{operation}
                    BEFORE {operation} ON CommercialWorks
                    WHEN NOT EXISTS (SELECT 1 FROM OpticalWorks w JOIN SalesOrders o ON o.CustomerId = w.CustomerId
                        WHERE w.Id = NEW.WorkId AND o.Id = NEW.OrderId)
                        OR (NEW.PreviousWorkId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM CommercialWorks p
                            WHERE p.WorkId = NEW.PreviousWorkId AND p.OrderId = NEW.OrderId))
                    BEGIN SELECT RAISE(ABORT, 'Invalid commercial work ownership'); END;
                    """);
                migrationBuilder.Sql($"""
                    CREATE TRIGGER TR_CommercialWorkLines_Ownership_{operation}
                    BEFORE {operation} ON CommercialWorkLines
                    WHEN NOT EXISTS (SELECT 1 FROM CommercialWorks g JOIN SalesLines l ON l.OrderId = g.OrderId
                        WHERE g.WorkId = NEW.WorkId AND l.Id = NEW.LineId AND
                            ((l.Category IN (2,3) AND EXISTS (SELECT 1 FROM OrderTechnicalAssignments a
                                WHERE a.Id = NEW.TechnicalAssignmentId AND a.LineId = l.Id))
                            OR (l.Category NOT IN (2,3) AND NEW.TechnicalAssignmentId IS NULL)))
                    BEGIN SELECT RAISE(ABORT, 'Invalid commercial material ownership'); END;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TEMP TABLE CommercialWorkDowngradeGuard (Valid INTEGER NOT NULL CHECK (Valid = 1));
                INSERT INTO CommercialWorkDowngradeGuard
                SELECT CASE WHEN EXISTS (SELECT 1 FROM CommercialWorks) OR EXISTS (SELECT 1 FROM OrderDeliveries)
                    OR EXISTS (SELECT 1 FROM WorkTransitions WHERE Reason IS NOT NULL) THEN 0 ELSE 1 END;
                DROP TABLE CommercialWorkDowngradeGuard;
                """);
            migrationBuilder.DropTable(
                name: "CommercialWorkLines");

            migrationBuilder.DropTable(
                name: "OrderDeliveries");

            migrationBuilder.DropTable(
                name: "CommercialWorks");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "WorkTransitions");
        }
    }
}
