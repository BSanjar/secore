using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization_settings",
                columns: table => new
                {
                    organization_id = table.Column<string>(type: "character varying", nullable: false),
                    disable_invoice_service_selection = table.Column<bool>(type: "boolean", nullable: false, comment: "отключить выбор услуги при создании счёта; ввод названия и цены вручную")
                },
                constraints: table =>
                {
                    table.PrimaryKey("organization_settings_pk", x => x.organization_id);
                    table.ForeignKey(
                        name: "organization_settings_organization_fk",
                        column: x => x.organization_id,
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "organization_settings");
        }
    }
}
