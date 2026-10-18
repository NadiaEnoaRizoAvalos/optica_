using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommercialSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommercialSettings", x => x.Id);
                    table.CheckConstraint("CK_CommercialSettings_Currency", "Id = 1 AND Currency GLOB '[A-Z][A-Z][A-Z]'");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    NameKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Brand = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BrandKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ModelKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.UniqueConstraint("AK_Products_Id_Category", x => new { x.Id, x.Category });
                    table.CheckConstraint("CK_Products_Profile", "Category BETWEEN 1 AND 4 AND IsActive IN (0,1) AND length(trim(Name)) BETWEEN 1 AND 160");
                    table.ForeignKey(
                        name: "FK_Products_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductSuppliers",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SupplierId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSuppliers", x => new { x.ProductId, x.SupplierId });
                    table.CheckConstraint("CK_ProductSuppliers_Active", "IsActive IN (0,1)");
                    table.ForeignKey(
                        name: "FK_ProductSuppliers_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductSuppliers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CodeKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Unit = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitsPerPackage = table.Column<int>(type: "INTEGER", nullable: false),
                    FrameColor = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    FrameEyeSizeHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    FrameBridgeHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    FrameTempleHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    LensMaterial = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LensIndexThousandths = table.Column<int>(type: "INTEGER", nullable: true),
                    LensTreatment = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    SphereMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    CylinderMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    Axis = table.Column<int>(type: "INTEGER", nullable: true),
                    AdditionMilli = table.Column<int>(type: "INTEGER", nullable: true),
                    ContactBaseCurveHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    ContactDiameterHundredths = table.Column<int>(type: "INTEGER", nullable: true),
                    PriceMinor = table.Column<long>(type: "INTEGER", nullable: true),
                    PriceCurrency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SpecificationLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.CheckConstraint("CK_Variants_AdditionMilli", "AdditionMilli IS NULL OR (typeof(AdditionMilli) = 'integer' AND AdditionMilli BETWEEN -2147483648 AND 2147483647)");
                    table.CheckConstraint("CK_Variants_Axis", "Axis IS NULL OR (typeof(Axis) = 'integer' AND Axis BETWEEN 0 AND 180)");
                    table.CheckConstraint("CK_Variants_CodeState", "length(trim(Code)) BETWEEN 1 AND 80 AND IsActive IN (0,1) AND SpecificationLocked IN (0,1)");
                    table.CheckConstraint("CK_Variants_Contact", "Category = 3 OR (ContactBaseCurveHundredths IS NULL AND ContactDiameterHundredths IS NULL)");
                    table.CheckConstraint("CK_Variants_ContactBaseCurveHundredths", "ContactBaseCurveHundredths IS NULL OR (typeof(ContactBaseCurveHundredths) = 'integer' AND ContactBaseCurveHundredths > 0 AND ContactBaseCurveHundredths <= 2147483647)");
                    table.CheckConstraint("CK_Variants_ContactDiameterHundredths", "ContactDiameterHundredths IS NULL OR (typeof(ContactDiameterHundredths) = 'integer' AND ContactDiameterHundredths > 0 AND ContactDiameterHundredths <= 2147483647)");
                    table.CheckConstraint("CK_Variants_CylinderMilli", "CylinderMilli IS NULL OR (typeof(CylinderMilli) = 'integer' AND CylinderMilli BETWEEN -2147483648 AND 2147483647)");
                    table.CheckConstraint("CK_Variants_Frame", "Category = 1 OR (FrameColor IS NULL AND FrameEyeSizeHundredths IS NULL AND FrameBridgeHundredths IS NULL AND FrameTempleHundredths IS NULL)");
                    table.CheckConstraint("CK_Variants_FrameBridgeHundredths", "FrameBridgeHundredths IS NULL OR (typeof(FrameBridgeHundredths) = 'integer' AND FrameBridgeHundredths > 0 AND FrameBridgeHundredths <= 2147483647)");
                    table.CheckConstraint("CK_Variants_FrameEyeSizeHundredths", "FrameEyeSizeHundredths IS NULL OR (typeof(FrameEyeSizeHundredths) = 'integer' AND FrameEyeSizeHundredths > 0 AND FrameEyeSizeHundredths <= 2147483647)");
                    table.CheckConstraint("CK_Variants_FrameTempleHundredths", "FrameTempleHundredths IS NULL OR (typeof(FrameTempleHundredths) = 'integer' AND FrameTempleHundredths > 0 AND FrameTempleHundredths <= 2147483647)");
                    table.CheckConstraint("CK_Variants_Lens", "Category = 2 OR (LensMaterial IS NULL AND LensIndexThousandths IS NULL AND LensTreatment IS NULL)");
                    table.CheckConstraint("CK_Variants_LensIndexThousandths", "LensIndexThousandths IS NULL OR (typeof(LensIndexThousandths) = 'integer' AND LensIndexThousandths > 0 AND LensIndexThousandths <= 2147483647)");
                    table.CheckConstraint("CK_Variants_Powers", "Category IN (2,3) OR (SphereMilli IS NULL AND CylinderMilli IS NULL AND Axis IS NULL AND AdditionMilli IS NULL)");
                    table.CheckConstraint("CK_Variants_Price", "(PriceMinor IS NULL AND PriceCurrency IS NULL) OR (PriceMinor IS NOT NULL AND typeof(PriceMinor) = 'integer' AND PriceMinor >= 0 AND PriceCurrency IS NOT NULL AND PriceCurrency GLOB '[A-Z][A-Z][A-Z]')");
                    table.CheckConstraint("CK_Variants_SphereMilli", "SphereMilli IS NULL OR (typeof(SphereMilli) = 'integer' AND SphereMilli BETWEEN -2147483648 AND 2147483647)");
                    table.CheckConstraint("CK_Variants_Unit", "(Category = 3 AND Unit = 2 AND typeof(UnitsPerPackage) = 'integer' AND UnitsPerPackage > 0) OR (Category IN (1,2,4) AND Unit = 1 AND UnitsPerPackage = 1)");
                    table.ForeignKey(
                        name: "FK_ProductVariants_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductVariants_Products_ProductId_Category",
                        columns: x => new { x.ProductId, x.Category },
                        principalTable: "Products",
                        principalColumns: new[] { "Id", "Category" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CommercialSettings",
                columns: new[] { "Id", "Currency", "Version" },
                values: new object[] { 1, "ARS", new Guid("5671154c-b49e-4594-85c9-236d8004fd72") });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Category_NameKey_BrandKey_ModelKey",
                table: "Products",
                columns: new[] { "Category", "NameKey", "BrandKey", "ModelKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CreatedByUserId",
                table: "Products",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive_NameKey",
                table: "Products",
                columns: new[] { "IsActive", "NameKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuppliers_SupplierId",
                table: "ProductSuppliers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_CodeKey",
                table: "ProductVariants",
                column: "CodeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_CreatedByUserId",
                table: "ProductVariants",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_Category",
                table: "ProductVariants",
                columns: new[] { "ProductId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_IsActive_CodeKey",
                table: "ProductVariants",
                columns: new[] { "ProductId", "IsActive", "CodeKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommercialSettings");

            migrationBuilder.DropTable(
                name: "ProductSuppliers");

            migrationBuilder.DropTable(
                name: "ProductVariants");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
