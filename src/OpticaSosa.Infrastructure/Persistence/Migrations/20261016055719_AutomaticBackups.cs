using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AutomaticBackups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DueUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RetryUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    DirectoryName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    VolumeId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Files = table.Column<int>(type: "INTEGER", nullable: false),
                    MissedPeriods = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    InstallationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    EveryDays = table.Column<int>(type: "INTEGER", nullable: false),
                    LocalTime = table.Column<string>(type: "TEXT", maxLength: 5, nullable: true),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    VolumeId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    KeepCopies = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    NextUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupSettings", x => x.Id);
                    table.CheckConstraint("CK_BackupSettings_Singleton", "Id = 1");
                    table.CheckConstraint("CK_BackupSettings_Values", "EveryDays BETWEEN 1 AND 365 AND KeepCopies BETWEEN 1 AND 3650");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupRuns_StartedUtc",
                table: "BackupRuns",
                column: "StartedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupRuns");

            migrationBuilder.DropTable(
                name: "BackupSettings");
        }
    }
}
