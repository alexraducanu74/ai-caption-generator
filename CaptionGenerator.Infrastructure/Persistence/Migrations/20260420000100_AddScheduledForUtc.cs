using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaptionGenerator.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260420000100_AddScheduledForUtc")]
public partial class AddScheduledForUtc : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ScheduledForUtc",
            table: "Posts",
            type: "timestamp with time zone",
            nullable: false,
            defaultValueSql: "NOW()");

        migrationBuilder.Sql(@"
            UPDATE ""Posts""
            SET ""ScheduledForUtc"" = ""CreatedAtUtc""
            WHERE ""ScheduledForUtc"" IS NULL;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ScheduledForUtc",
            table: "Posts");
    }
}
