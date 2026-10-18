using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Suppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    NameKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    SuppliesProducts = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLaboratory = table.Column<bool>(type: "INTEGER", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    TaxId = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    TaxKey = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Services = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                    table.CheckConstraint("CK_Suppliers_Active", "IsActive IN (0,1)");
                    table.CheckConstraint("CK_Suppliers_Kind", "SuppliesProducts IN (0,1) AND IsLaboratory IN (0,1) AND (SuppliesProducts = 1 OR IsLaboratory = 1)");
                    table.CheckConstraint("CK_Suppliers_Name", "length(trim(Name)) BETWEEN 1 AND 160");
                    table.ForeignKey(
                        name: "FK_Suppliers_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CreatedByUserId",
                table: "Suppliers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_IsActive_IsLaboratory_NameKey",
                table: "Suppliers",
                columns: new[] { "IsActive", "IsLaboratory", "NameKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_NameKey",
                table: "Suppliers",
                column: "NameKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TaxKey",
                table: "Suppliers",
                column: "TaxKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Suppliers");
        }
    }
}
