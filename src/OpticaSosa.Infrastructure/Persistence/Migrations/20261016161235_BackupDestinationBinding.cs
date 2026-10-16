using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackupDestinationBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BindingId",
                table: "BackupSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelativeFolder",
                table: "BackupSettings",
                type: "TEXT",
                maxLength: 240,
                nullable: false,
                defaultValue: "OpticaSosaBackups");

            migrationBuilder.AddColumn<Guid>(
                name: "BindingId",
                table: "BackupRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelativeFolder",
                table: "BackupRuns",
                type: "TEXT",
                maxLength: 240,
                nullable: false,
                defaultValue: "OpticaSosaBackups");

            // Una instalación previa debe revisar/vincular el destino antes de reactivar.
            migrationBuilder.Sql("UPDATE BackupSettings SET Enabled = 0, NextUtc = NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BindingId",
                table: "BackupSettings");

            migrationBuilder.DropColumn(
                name: "RelativeFolder",
                table: "BackupSettings");

            migrationBuilder.DropColumn(
                name: "BindingId",
                table: "BackupRuns");

            migrationBuilder.DropColumn(
                name: "RelativeFolder",
                table: "BackupRuns");
        }
    }
}
