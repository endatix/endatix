using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddCustomQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomQuestions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JsonData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomQuestions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CustomQuestions",
                columns: ["Id", "Name", "Description", "JsonData", "CreatedAt", "IsDeleted", "TenantId"],
                values: [1368840969541124096, "video", "Video question", @"{
   ""name"":""video"",
   ""title"":""Video"",
   ""iconName"":""icon-preview-24x24"",
   ""category"":""choice"",
   ""orderedAfter"":""file"",
   ""defaultQuestionTitle"":""Video"",
   ""inheritBaseProps"":true,
   ""questionJSON"":
   {
      ""type"":""file"",
      ""title"":""Video Upload"",
	  ""description"": ""Upload existing video"",
      ""acceptedTypes"": ""video/*"",
      ""storeDataAsText"": false,
      ""waitForUpload"": true,
      ""maxSize"": 150000000,
      ""needConfirmRemoveFile"": true,
      ""fileOrPhotoPlaceholder"": ""Drag and drop or select a video file to upload. Up to 150 MB"",
      ""filePlaceholder"": ""Drag and drop a video file or click \""Select File\""""
   }
}", DateTime.UtcNow, false, 1]);

            migrationBuilder.CreateIndex(
                name: "IX_CustomQuestions_TenantId",
                table: "CustomQuestions",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomQuestions");
        }
    }
}
