using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpticaSosa.Infrastructure.Persistence.Migrations;

// SQL revisado a partir del script de EF: reconstruir tablas y recrear los triggers clínicos
// dentro de la misma transacción. El generador estándar no conserva triggers no modelados.
public partial class ProductQualityMatrix : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => Rebuild(migrationBuilder, """
            ALTER TABLE "QualityControls" ADD "WorkType" INTEGER NOT NULL DEFAULT 0;

            UPDATE QualityControls SET WorkType = (SELECT Type FROM OpticalWorks WHERE OpticalWorks.Id = QualityControls.WorkId);

            CREATE UNIQUE INDEX UX_QualityMigration_WorkType ON OpticalWorks (Id, Type);

            CREATE INDEX "IX_QualityControls_WorkId_WorkType" ON "QualityControls" ("WorkId", "WorkType");

            CREATE TABLE "ef_temp_QualityControls" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_QualityControls" PRIMARY KEY,
                "ActorId" TEXT NOT NULL,
                "ApprovedAtUtc" TEXT NULL,
                "AtUtc" TEXT NOT NULL,
                "Notes" TEXT NULL,
                "Role" INTEGER NOT NULL,
                "Round" INTEGER NOT NULL,
                "Stage" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "TemplateVersion" TEXT NOT NULL,
                "WorkId" TEXT NOT NULL,
                "WorkType" INTEGER NOT NULL,
                CONSTRAINT "CK_QualityControl_Approval" CHECK (Stage IN (0, 1) AND Status IN (0, 1, 2) AND Role IN (0, 1) AND WorkType BETWEEN 0 AND 8 AND (Stage <> 0 OR Role = 0 OR WorkType IN (1,4,5,8)) AND (Status <> 2 OR ApprovedAtUtc IS NOT NULL)),
                CONSTRAINT "FK_QualityControls_AspNetUsers_ActorId" FOREIGN KEY ("ActorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_QualityControls_OpticalWorks_WorkId_WorkType" FOREIGN KEY ("WorkId", "WorkType") REFERENCES "OpticalWorks" ("Id", "Type") ON DELETE RESTRICT
            );

            INSERT INTO "ef_temp_QualityControls" ("Id", "ActorId", "ApprovedAtUtc", "AtUtc", "Notes", "Role", "Round", "Stage", "Status", "TemplateVersion", "WorkId", "WorkType")
            SELECT "Id", "ActorId", "ApprovedAtUtc", "AtUtc", "Notes", "Role", "Round", "Stage", "Status", "TemplateVersion", "WorkId", "WorkType"
            FROM "QualityControls";

            CREATE TABLE "ef_temp_OpticalWorks" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_OpticalWorks" PRIMARY KEY,
                "CreatedAtUtc" TEXT NOT NULL,
                "CreatedByUserId" TEXT NOT NULL,
                "CustomerId" TEXT NOT NULL,
                "DeliveryApproved" INTEGER NOT NULL,
                "Frame" TEXT NOT NULL,
                "PrescriptionId" TEXT NULL,
                "PrescriptionRevisionId" TEXT NULL,
                "Product" TEXT NOT NULL,
                "Round" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "TechnicalApproved" INTEGER NOT NULL,
                "Type" INTEGER NOT NULL,
                "Version" TEXT NOT NULL,
                CONSTRAINT "AK_OpticalWorks_Id_Type" UNIQUE ("Id", "Type"),
                CONSTRAINT "CK_OpticalWork_Prescription" CHECK ((PrescriptionId IS NULL) = (PrescriptionRevisionId IS NULL)),
                CONSTRAINT "CK_OpticalWork_State" CHECK (Status BETWEEN 0 AND 5 AND Round > 0 AND Type BETWEEN 0 AND 8 AND (Status NOT IN (4, 5) OR (TechnicalApproved = 1 AND DeliveryApproved = 1))),
                CONSTRAINT "FK_OpticalWorks_AspNetUsers_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_OpticalWorks_ClinicalRecords_PrescriptionId" FOREIGN KEY ("PrescriptionId") REFERENCES "ClinicalRecords" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_OpticalWorks_ClinicalRevisions_PrescriptionRevisionId" FOREIGN KEY ("PrescriptionRevisionId") REFERENCES "ClinicalRevisions" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_OpticalWorks_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT
            );

            INSERT INTO "ef_temp_OpticalWorks" ("Id", "CreatedAtUtc", "CreatedByUserId", "CustomerId", "DeliveryApproved", "Frame", "PrescriptionId", "PrescriptionRevisionId", "Product", "Round", "Status", "TechnicalApproved", "Type", "Version")
            SELECT "Id", "CreatedAtUtc", "CreatedByUserId", "CustomerId", "DeliveryApproved", "Frame", "PrescriptionId", "PrescriptionRevisionId", "Product", "Round", "Status", "TechnicalApproved", "Type", "Version"
            FROM "OpticalWorks";



            DROP TABLE "QualityControls";

            ALTER TABLE "ef_temp_QualityControls" RENAME TO "QualityControls";

            DROP TABLE "OpticalWorks";

            ALTER TABLE "ef_temp_OpticalWorks" RENAME TO "OpticalWorks";



            CREATE INDEX "IX_QualityControls_ActorId" ON "QualityControls" ("ActorId");

            CREATE INDEX "IX_QualityControls_WorkId_AtUtc" ON "QualityControls" ("WorkId", "AtUtc");

            CREATE INDEX "IX_QualityControls_WorkId_WorkType" ON "QualityControls" ("WorkId", "WorkType");

            CREATE INDEX "IX_OpticalWorks_CreatedByUserId" ON "OpticalWorks" ("CreatedByUserId");

            CREATE INDEX "IX_OpticalWorks_CustomerId_CreatedAtUtc" ON "OpticalWorks" ("CustomerId", "CreatedAtUtc");

            CREATE INDEX "IX_OpticalWorks_PrescriptionId" ON "OpticalWorks" ("PrescriptionId");

            CREATE INDEX "IX_OpticalWorks_PrescriptionRevisionId" ON "OpticalWorks" ("PrescriptionRevisionId");

            CREATE TRIGGER TR_OpticalWorks_Prescription_INSERT
            BEFORE INSERT ON OpticalWorks
            WHEN NEW.PrescriptionId IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM ClinicalRecords c JOIN ClinicalRevisions r ON r.RecordId = c.Id
                WHERE c.Id = NEW.PrescriptionId AND c.CustomerId = NEW.CustomerId AND c.Kind IN (0, 1)
                AND r.Id = NEW.PrescriptionRevisionId)
            BEGIN SELECT RAISE(ABORT, 'Invalid prescription ownership or revision'); END;

            CREATE TRIGGER TR_OpticalWorks_Prescription_UPDATE
            BEFORE UPDATE ON OpticalWorks
            WHEN NEW.PrescriptionId IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM ClinicalRecords c JOIN ClinicalRevisions r ON r.RecordId = c.Id
                WHERE c.Id = NEW.PrescriptionId AND c.CustomerId = NEW.CustomerId AND c.Kind IN (0, 1)
                AND r.Id = NEW.PrescriptionRevisionId)
            BEGIN SELECT RAISE(ABORT, 'Invalid prescription ownership or revision'); END;

            CREATE TEMP TABLE QualityMigrationIntegrityGuard (Valid INTEGER NOT NULL CHECK (Valid = 1));
            INSERT INTO QualityMigrationIntegrityGuard (Valid)
            SELECT CASE WHEN EXISTS (SELECT 1 FROM pragma_foreign_key_check) OR EXISTS (
                SELECT 1 FROM OpticalWorks w WHERE w.PrescriptionId IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM ClinicalRecords c JOIN ClinicalRevisions r ON r.RecordId = c.Id
                    WHERE c.Id = w.PrescriptionId AND c.CustomerId = w.CustomerId AND c.Kind IN (0, 1)
                    AND r.Id = w.PrescriptionRevisionId)) THEN 0 ELSE 1 END;
            DROP TABLE QualityMigrationIntegrityGuard;
            """);

    protected override void Down(MigrationBuilder migrationBuilder) => Rebuild(migrationBuilder, """
            DROP INDEX "IX_QualityControls_WorkId_WorkType";

            CREATE TABLE "ef_temp_QualityControls" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_QualityControls" PRIMARY KEY,
                "ActorId" TEXT NOT NULL,
                "ApprovedAtUtc" TEXT NULL,
                "AtUtc" TEXT NOT NULL,
                "Notes" TEXT NULL,
                "Role" INTEGER NOT NULL,
                "Round" INTEGER NOT NULL,
                "Stage" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "TemplateVersion" TEXT NOT NULL,
                "WorkId" TEXT NOT NULL,
                CONSTRAINT "CK_QualityControl_Approval" CHECK (Stage IN (0, 1) AND Status IN (0, 1, 2) AND Role IN (0, 1) AND (Stage <> 0 OR Role = 0) AND (Status <> 2 OR ApprovedAtUtc IS NOT NULL)),
                CONSTRAINT "FK_QualityControls_AspNetUsers_ActorId" FOREIGN KEY ("ActorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_QualityControls_OpticalWorks_WorkId" FOREIGN KEY ("WorkId") REFERENCES "OpticalWorks" ("Id") ON DELETE RESTRICT
            );

            INSERT INTO "ef_temp_QualityControls" ("Id", "ActorId", "ApprovedAtUtc", "AtUtc", "Notes", "Role", "Round", "Stage", "Status", "TemplateVersion", "WorkId")
            SELECT "Id", "ActorId", "ApprovedAtUtc", "AtUtc", "Notes", "Role", "Round", "Stage", "Status", "TemplateVersion", "WorkId"
            FROM "QualityControls";

            CREATE TABLE "ef_temp_OpticalWorks" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_OpticalWorks" PRIMARY KEY,
                "CreatedAtUtc" TEXT NOT NULL,
                "CreatedByUserId" TEXT NOT NULL,
                "CustomerId" TEXT NOT NULL,
                "DeliveryApproved" INTEGER NOT NULL,
                "Frame" TEXT NOT NULL,
                "PrescriptionId" TEXT NULL,
                "PrescriptionRevisionId" TEXT NULL,
                "Product" TEXT NOT NULL,
                "Round" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "TechnicalApproved" INTEGER NOT NULL,
                "Type" INTEGER NOT NULL,
                "Version" TEXT NOT NULL,
                CONSTRAINT "CK_OpticalWork_Prescription" CHECK ((PrescriptionId IS NULL) = (PrescriptionRevisionId IS NULL)),
                CONSTRAINT "CK_OpticalWork_State" CHECK (Status BETWEEN 0 AND 5 AND Round > 0 AND Type BETWEEN 0 AND 2 AND (Status NOT IN (4, 5) OR (TechnicalApproved = 1 AND DeliveryApproved = 1))),
                CONSTRAINT "FK_OpticalWorks_AspNetUsers_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_OpticalWorks_ClinicalRecords_PrescriptionId" FOREIGN KEY ("PrescriptionId") REFERENCES "ClinicalRecords" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_OpticalWorks_ClinicalRevisions_PrescriptionRevisionId" FOREIGN KEY ("PrescriptionRevisionId") REFERENCES "ClinicalRevisions" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_OpticalWorks_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT
            );

            INSERT INTO "ef_temp_OpticalWorks" ("Id", "CreatedAtUtc", "CreatedByUserId", "CustomerId", "DeliveryApproved", "Frame", "PrescriptionId", "PrescriptionRevisionId", "Product", "Round", "Status", "TechnicalApproved", "Type", "Version")
            SELECT "Id", "CreatedAtUtc", "CreatedByUserId", "CustomerId", "DeliveryApproved", "Frame", "PrescriptionId", "PrescriptionRevisionId", "Product", "Round", "Status", "TechnicalApproved", "Type", "Version"
            FROM "OpticalWorks";



            DROP TABLE "QualityControls";

            ALTER TABLE "ef_temp_QualityControls" RENAME TO "QualityControls";

            DROP TABLE "OpticalWorks";

            ALTER TABLE "ef_temp_OpticalWorks" RENAME TO "OpticalWorks";



            CREATE INDEX "IX_QualityControls_ActorId" ON "QualityControls" ("ActorId");

            CREATE INDEX "IX_QualityControls_WorkId_AtUtc" ON "QualityControls" ("WorkId", "AtUtc");

            CREATE INDEX "IX_OpticalWorks_CreatedByUserId" ON "OpticalWorks" ("CreatedByUserId");

            CREATE INDEX "IX_OpticalWorks_CustomerId_CreatedAtUtc" ON "OpticalWorks" ("CustomerId", "CreatedAtUtc");

            CREATE INDEX "IX_OpticalWorks_PrescriptionId" ON "OpticalWorks" ("PrescriptionId");

            CREATE INDEX "IX_OpticalWorks_PrescriptionRevisionId" ON "OpticalWorks" ("PrescriptionRevisionId");

            CREATE TRIGGER TR_OpticalWorks_Prescription_INSERT
            BEFORE INSERT ON OpticalWorks
            WHEN NEW.PrescriptionId IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM ClinicalRecords c JOIN ClinicalRevisions r ON r.RecordId = c.Id
                WHERE c.Id = NEW.PrescriptionId AND c.CustomerId = NEW.CustomerId AND c.Kind IN (0, 1)
                AND r.Id = NEW.PrescriptionRevisionId)
            BEGIN SELECT RAISE(ABORT, 'Invalid prescription ownership or revision'); END;

            CREATE TRIGGER TR_OpticalWorks_Prescription_UPDATE
            BEFORE UPDATE ON OpticalWorks
            WHEN NEW.PrescriptionId IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM ClinicalRecords c JOIN ClinicalRevisions r ON r.RecordId = c.Id
                WHERE c.Id = NEW.PrescriptionId AND c.CustomerId = NEW.CustomerId AND c.Kind IN (0, 1)
                AND r.Id = NEW.PrescriptionRevisionId)
            BEGIN SELECT RAISE(ABORT, 'Invalid prescription ownership or revision'); END;

            CREATE TEMP TABLE QualityMigrationIntegrityGuard (Valid INTEGER NOT NULL CHECK (Valid = 1));
            INSERT INTO QualityMigrationIntegrityGuard (Valid)
            SELECT CASE WHEN EXISTS (SELECT 1 FROM pragma_foreign_key_check) OR EXISTS (
                SELECT 1 FROM OpticalWorks w WHERE w.PrescriptionId IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM ClinicalRecords c JOIN ClinicalRevisions r ON r.RecordId = c.Id
                    WHERE c.Id = w.PrescriptionId AND c.CustomerId = w.CustomerId AND c.Kind IN (0, 1)
                    AND r.Id = w.PrescriptionRevisionId)) THEN 0 ELSE 1 END;
            DROP TABLE QualityMigrationIntegrityGuard;
            """);

    private static void Rebuild(MigrationBuilder migrationBuilder, string sql)
    {
        // SQLite requiere cambiar foreign_keys fuera de una transacción. EF administra y
        // revierte la transacción del cuerpo ante errores; la comprobación impide confirmar huérfanos.
        migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);
        migrationBuilder.Sql(sql);
        migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
    }
}
