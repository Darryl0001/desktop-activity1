using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquipmentBorrowing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenWhitespaceIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Student_Contact",
                table: "Students");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Student_Required",
                table: "Students");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipment_Description",
                table: "Equipment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipment_Required",
                table: "Equipment");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedNumber",
                table: "Students",
                type: "text",
                nullable: true,
                computedColumnSql: "upper(btrim(\"StudentNumber\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288)))",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComputedColumnSql: "upper(btrim(\"StudentNumber\"))",
                oldStored: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedIdentifier",
                table: "Equipment",
                type: "text",
                nullable: true,
                computedColumnSql: "upper(btrim(\"Identifier\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288)))",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComputedColumnSql: "upper(btrim(\"Identifier\"))",
                oldStored: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Student_Contact",
                table: "Students",
                sql: "\"Contact\" IS NULL OR length(btrim(\"Contact\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Student_Required",
                table: "Students",
                sql: "length(btrim(\"Name\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))) > 0 AND length(btrim(\"StudentNumber\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipment_Description",
                table: "Equipment",
                sql: "\"Description\" IS NULL OR length(btrim(\"Description\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipment_Required",
                table: "Equipment",
                sql: "length(btrim(\"Name\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))) > 0 AND length(btrim(\"Identifier\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288))) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Student_Contact",
                table: "Students");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Student_Required",
                table: "Students");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipment_Description",
                table: "Equipment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Equipment_Required",
                table: "Equipment");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedNumber",
                table: "Students",
                type: "text",
                nullable: true,
                computedColumnSql: "upper(btrim(\"StudentNumber\"))",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComputedColumnSql: "upper(btrim(\"StudentNumber\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288)))",
                oldStored: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedIdentifier",
                table: "Equipment",
                type: "text",
                nullable: true,
                computedColumnSql: "upper(btrim(\"Identifier\"))",
                stored: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComputedColumnSql: "upper(btrim(\"Identifier\", chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(32) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288)))",
                oldStored: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Student_Contact",
                table: "Students",
                sql: "\"Contact\" IS NULL OR length(btrim(\"Contact\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Student_Required",
                table: "Students",
                sql: "length(btrim(\"Name\")) > 0 AND length(btrim(\"StudentNumber\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipment_Description",
                table: "Equipment",
                sql: "\"Description\" IS NULL OR length(btrim(\"Description\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Equipment_Required",
                table: "Equipment",
                sql: "length(btrim(\"Name\")) > 0 AND length(btrim(\"Identifier\")) > 0");
        }
    }
}
