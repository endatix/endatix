using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Reporting.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class InitialReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.CreateTable(
                name: "ExportFormats",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SerializationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SettingsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportFormats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlattenedSubmissions",
                schema: "reporting",
                columns: table => new
                {
                    SubmissionId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    FormId = table.Column<long>(type: "bigint", nullable: false),
                    DataJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IntegrationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Integration_LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Integration_LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Integration_ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlattenedSubmissions", x => x.SubmissionId);
                });

            migrationBuilder.CreateTable(
                name: "FormSchemas",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    FormId = table.Column<long>(type: "bigint", nullable: false),
                    FormDefinitionRevision = table.Column<long>(type: "bigint", nullable: false),
                    FlatteningMap = table.Column<string>(type: "jsonb", nullable: false),
                    Codebook = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormSchemas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SurveyTypeExportMappings",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    SurveyTypeId = table.Column<long>(type: "bigint", nullable: true),
                    ExportFormatId = table.Column<long>(type: "bigint", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyTypeExportMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyTypeExportMappings_ExportFormats_ExportFormatId",
                        column: x => x.ExportFormatId,
                        principalSchema: "reporting",
                        principalTable: "ExportFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExportFormats_TenantId_Name",
                schema: "reporting",
                table: "ExportFormats",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlattenedSubmissions_TenantId_FormId_SubmissionId",
                schema: "reporting",
                table: "FlattenedSubmissions",
                columns: new[] { "TenantId", "FormId", "SubmissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_FormSchemas_TenantId_FormId",
                schema: "reporting",
                table: "FormSchemas",
                columns: new[] { "TenantId", "FormId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurveyTypeExportMappings_ExportFormatId",
                schema: "reporting",
                table: "SurveyTypeExportMappings",
                column: "ExportFormatId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyTypeExportMappings_TenantId",
                schema: "reporting",
                table: "SurveyTypeExportMappings",
                column: "TenantId",
                unique: true,
                filter: "\"IsDefault\" = true AND \"SurveyTypeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyTypeExportMappings_TenantId_SurveyTypeId",
                schema: "reporting",
                table: "SurveyTypeExportMappings",
                columns: new[] { "TenantId", "SurveyTypeId" },
                unique: true,
                filter: "\"IsDefault\" = true AND \"SurveyTypeId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlattenedSubmissions",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "FormSchemas",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "SurveyTypeExportMappings",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "ExportFormats",
                schema: "reporting");
        }
    }
}
