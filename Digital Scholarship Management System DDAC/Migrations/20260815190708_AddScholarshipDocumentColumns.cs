using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Digital_Scholarship_Management_System_DDAC.Migrations
{
    /// <inheritdoc />
    public partial class AddScholarshipDocumentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllocationBudgetDocumentPath",
                table: "Scholarships",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EligibilityCriteriaDocumentPath",
                table: "Scholarships",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PolicyFrameworkDocumentPath",
                table: "Scholarships",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PrivacyPolicyDocumentPath",
                table: "Scholarships",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllocationBudgetDocumentPath",
                table: "Scholarships");

            migrationBuilder.DropColumn(
                name: "EligibilityCriteriaDocumentPath",
                table: "Scholarships");

            migrationBuilder.DropColumn(
                name: "PolicyFrameworkDocumentPath",
                table: "Scholarships");

            migrationBuilder.DropColumn(
                name: "PrivacyPolicyDocumentPath",
                table: "Scholarships");
        }
    }
}
