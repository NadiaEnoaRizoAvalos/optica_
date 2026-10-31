using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QualityMaterialEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaterialHash",
                table: "QualityControls",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TEMP TABLE MaterialEvidenceDowngradeGuard (Valid INTEGER NOT NULL CHECK (Valid = 1));
                INSERT INTO MaterialEvidenceDowngradeGuard
                SELECT CASE WHEN EXISTS (SELECT 1 FROM QualityControls WHERE MaterialHash IS NOT NULL) THEN 0 ELSE 1 END;
                DROP TABLE MaterialEvidenceDowngradeGuard;
                """);
            migrationBuilder.DropColumn(
                name: "MaterialHash",
                table: "QualityControls");
        }
    }
}
