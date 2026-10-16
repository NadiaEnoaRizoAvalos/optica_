using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BusinessConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Profile_TradeName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Profile_Address = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Profile_Province = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Profile_Locality = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Profile_Phone = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Profile_WhatsApp = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Profile_Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    Profile_Cuit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Profile_AdditionalDetails = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LogoPng = table.Column<byte[]>(type: "BLOB", nullable: true),
                    LogoVersion = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessSettings", x => x.Id);
                    table.CheckConstraint("CK_BusinessSettings_Singleton", "Id = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessSettings");
        }
    }
}
