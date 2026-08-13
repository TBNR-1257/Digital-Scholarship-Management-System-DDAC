using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Digital_Scholarship_Management_System_DDAC.Migrations
{
    /// <inheritdoc />
    public partial class RestructureOfficerReviewerToModeratorProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename existing role rows in place (keeps the same RoleId, so
            // any user already assigned to "Officer"/"Reviewer" automatically
            // becomes "Moderator"/"Provider" without needing to touch AspNetUserRoles.
            migrationBuilder.Sql("UPDATE `AspNetRoles` SET `Name` = 'Moderator', `NormalizedName` = 'MODERATOR' WHERE `Name` = 'Officer';");
            migrationBuilder.Sql("UPDATE `AspNetRoles` SET `Name` = 'Provider', `NormalizedName` = 'PROVIDER' WHERE `Name` = 'Reviewer';");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserId",
                table: "Scholarships",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "DecisionAt",
                table: "Scholarships",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstitutionName",
                table: "Scholarships",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Scholarships",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InstitutionProfiles",
                columns: table => new
                {
                    InstitutionProfileId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InstitutionName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContactEmail = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContactPhone = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RegistrationDocumentPath = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VerificationStatus = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModeratedByUserId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModeratedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ActivatedByUserId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActivatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RejectionReason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionProfiles", x => x.InstitutionProfileId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstitutionProfiles");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Scholarships");

            migrationBuilder.DropColumn(
                name: "DecisionAt",
                table: "Scholarships");

            migrationBuilder.DropColumn(
                name: "InstitutionName",
                table: "Scholarships");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Scholarships");

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    ReviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    Comments = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecommendedDecision = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReviewerId = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Score = table.Column<decimal>(type: "decimal(65,30)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.ReviewId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("UPDATE `AspNetRoles` SET `Name` = 'Officer', `NormalizedName` = 'OFFICER' WHERE `Name` = 'Moderator';");
            migrationBuilder.Sql("UPDATE `AspNetRoles` SET `Name` = 'Reviewer', `NormalizedName` = 'REVIEWER' WHERE `Name` = 'Provider';");
        }
    }
}
