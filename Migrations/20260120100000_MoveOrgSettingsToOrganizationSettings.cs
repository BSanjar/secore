using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class MoveOrgSettingsToOrganizationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Добавить колонки в organization_settings
            migrationBuilder.AddColumn<bool>(
                name: "allowed_hassameaccount",
                table: "organization_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "если true - организации могут создавать счета с одинаковыми л/с");

            migrationBuilder.AddColumn<int>(
                name: "paymentreminderdaysbefore",
                table: "organization_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "за сколько дней до срока начинать напоминания по оплате");

            // 2. Скопировать данные из organization в organization_settings (для существующих строк)
            migrationBuilder.Sql(@"
                UPDATE organization_settings s
                SET allowed_hassameaccount = o.allowed_hassameaccount,
                    paymentreminderdaysbefore = o.paymentreminderdaysbefore
                FROM organization o
                WHERE o.id = s.organization_id
            ");

            // 3. Вставить строки в organization_settings для организаций, у которых их ещё нет
            migrationBuilder.Sql(@"
                INSERT INTO organization_settings (organization_id, disable_invoice_service_selection, allowed_hassameaccount, paymentreminderdaysbefore)
                SELECT o.id, false, COALESCE(o.allowed_hassameaccount, false), COALESCE(o.paymentreminderdaysbefore, 0)
                FROM organization o
                WHERE NOT EXISTS (SELECT 1 FROM organization_settings s WHERE s.organization_id = o.id)
            ");

            // 4. Удалить колонки из organization
            migrationBuilder.DropColumn(name: "allowed_hassameaccount", table: "organization");
            migrationBuilder.DropColumn(name: "paymentreminderdaysbefore", table: "organization");
            migrationBuilder.DropColumn(name: "api_login", table: "organization");
            migrationBuilder.DropColumn(name: "api_password", table: "organization");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allowed_hassameaccount",
                table: "organization",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "paymentreminderdaysbefore",
                table: "organization",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "api_login",
                table: "organization",
                type: "character varying",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "api_password",
                table: "organization",
                type: "character varying",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE organization o
                SET allowed_hassameaccount = s.allowed_hassameaccount,
                    paymentreminderdaysbefore = s.paymentreminderdaysbefore
                FROM organization_settings s
                WHERE s.organization_id = o.id
            ");

            migrationBuilder.DropColumn(name: "allowed_hassameaccount", table: "organization_settings");
            migrationBuilder.DropColumn(name: "paymentreminderdaysbefore", table: "organization_settings");
        }
    }
}
