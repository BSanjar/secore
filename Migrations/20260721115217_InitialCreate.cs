using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    api_login = table.Column<string>(type: "character varying", nullable: true),
                    api_psw = table.Column<string>(type: "character varying", nullable: true),
                    name = table.Column<string>(type: "character varying", nullable: true),
                    allowlistip = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("agent_pk", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "appointment_medical_templates",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    organization_id = table.Column<string>(type: "character varying", nullable: false),
                    template_type = table.Column<string>(type: "character varying", nullable: false),
                    title = table.Column<string>(type: "character varying", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "0"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("appointment_medical_templates_pk", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "commission",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    name = table.Column<string>(type: "character varying", nullable: true),
                    commission_kind = table.Column<string>(type: "character varying", nullable: false, comment: "percent | fixed | mixed | single_tier | progressive"),
                    rate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    fixed_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    min_fee = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    max_fee = table.Column<decimal>(type: "numeric(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("commission_pk", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "history",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: true),
                    type_history = table.Column<string>(type: "character varying", nullable: true, comment: "user_edited\r\ninvoice_edited"),
                    user_editor = table.Column<string>(type: "character varying", nullable: true, comment: "Пользователь который совершил действие"),
                    invoice = table.Column<string>(type: "character varying", nullable: true, comment: "если действие по инвойсу то id инвойса")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "organization",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    name = table.Column<string>(type: "character varying", nullable: true),
                    organizationtype = table.Column<string>(type: "character varying", nullable: true, comment: "standart\r\ndetsad\r\nmedclinic"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "false — доступ заблокирован (истёк период подписки)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("organization_pk", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    name = table.Column<string>(type: "character varying", nullable: true),
                    code = table.Column<string>(type: "character varying", nullable: true),
                    description = table.Column<string>(type: "character varying", nullable: true),
                    category = table.Column<string>(type: "character varying", nullable: true),
                    area = table.Column<string>(type: "character varying", nullable: true),
                    isdeleted = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0")
                },
                constraints: table =>
                {
                    table.PrimaryKey("permissions_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "commission_tier",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    commission_id = table.Column<string>(type: "character varying", nullable: false),
                    amount_from = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    amount_to = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("commission_tier_pk", x => x.id);
                    table.ForeignKey(
                        name: "commission_tier_commission_fk",
                        column: x => x.commission_id,
                        principalTable: "commission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_commission",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    agent_id = table.Column<string>(type: "character varying", nullable: false),
                    organization_id = table.Column<string>(type: "character varying", nullable: false),
                    commission_id = table.Column<string>(type: "character varying", nullable: false),
                    lower_commission_id = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("agent_commission_pk", x => x.id);
                    table.ForeignKey(
                        name: "agent_commission_agent_fk",
                        column: x => x.agent_id,
                        principalTable: "agent",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "agent_commission_commission_fk",
                        column: x => x.commission_id,
                        principalTable: "commission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "agent_commission_lower_commission_fk",
                        column: x => x.lower_commission_id,
                        principalTable: "commission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "agent_commission_organization_fk",
                        column: x => x.organization_id,
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    organization_id = table.Column<string>(type: "character varying", nullable: false),
                    name = table.Column<string>(type: "character varying", nullable: false),
                    code = table.Column<string>(type: "character varying", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "0"),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("departments_pk", x => x.id);
                    table.ForeignKey(
                        name: "departments_fk_organization",
                        column: x => x.organization_id,
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "org_client_groups",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    name = table.Column<string>(type: "character varying", nullable: true),
                    parent_group_id = table.Column<string>(type: "character varying", nullable: true),
                    organization_id = table.Column<string>(type: "character varying", nullable: false),
                    is_deleted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    logo = table.Column<string>(type: "character varying", nullable: true),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("org_client_groups_pk", x => x.id);
                    table.ForeignKey(
                        name: "org_client_groups_organization_fk",
                        column: x => x.organization_id,
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "org_client_groups_parent_fk",
                        column: x => x.parent_group_id,
                        principalTable: "org_client_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_fields",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    field_name = table.Column<string>(type: "character varying", nullable: true),
                    field_type = table.Column<string>(type: "character varying", nullable: true, comment: "int\r\nstring\r\nmoney\r\nselected\r\ndatetime"),
                    field_select_values = table.Column<string>(type: "character varying", nullable: true, comment: "варианты для выбора чз - ;"),
                    isdeleted = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0"),
                    organization = table.Column<string>(type: "character varying", nullable: true),
                    filterbyfield = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false")
                },
                constraints: table =>
                {
                    table.PrimaryKey("organization_fields_pk", x => x.id);
                    table.ForeignKey(
                        name: "organization_fields_fk",
                        column: x => x.organization,
                        principalTable: "organization",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "organization_services",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    name = table.Column<string>(type: "character varying", nullable: true),
                    organization = table.Column<string>(type: "character varying", nullable: true),
                    service_summ = table.Column<decimal>(type: "numeric", nullable: true, comment: "если fixed_sum = 1, то тут будет значение фиксированной суммы"),
                    fixed_sum = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0", comment: "если 1 то услуга с фиксированной суммой"),
                    min_summ = table.Column<decimal>(type: "numeric", nullable: true, comment: "мин сумма в тыйынах"),
                    max_summ = table.Column<decimal>(type: "numeric", nullable: true, comment: "макс сумма в тыйынах"),
                    isdeleted = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0")
                },
                constraints: table =>
                {
                    table.PrimaryKey("organization_services_pk", x => x.id);
                    table.ForeignKey(
                        name: "organization_services_fk",
                        column: x => x.organization,
                        principalTable: "organization",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "organization_settings",
                columns: table => new
                {
                    organization_id = table.Column<string>(type: "character varying", nullable: false),
                    disable_invoice_service_selection = table.Column<bool>(type: "boolean", nullable: false, comment: "отключить выбор услуги при создании счёта; ввод названия и цены вручную"),
                    allowed_hassameaccount = table.Column<bool>(type: "boolean", nullable: false, comment: "если true - организации могут создавать счета с одинаковыми л/с"),
                    invoice_pay_code_mode = table.Column<string>(type: "character varying(32)", nullable: true, comment: "new_only | duplicate_only | both — режим выбора л/с при создании счёта"),
                    paymentreminderdaysbefore = table.Column<int>(type: "integer", nullable: false, comment: "за сколько дней до срока начинать напоминания по оплате"),
                    email = table.Column<string>(type: "character varying", nullable: true),
                    whatsapp_phone = table.Column<string>(type: "character varying", nullable: true),
                    contact_phone = table.Column<string>(type: "character varying", nullable: true),
                    director_full_name = table.Column<string>(type: "character varying", nullable: true),
                    address = table.Column<string>(type: "character varying", nullable: true),
                    logo_path = table.Column<string>(type: "character varying", nullable: true),
                    billing_type = table.Column<string>(type: "character varying", nullable: true, comment: "subscription или комбинация комиссий через флаги"),
                    default_qr_mode = table.Column<string>(type: "character varying", nullable: true),
                    use_lower_commission_from_org = table.Column<bool>(type: "boolean", nullable: false),
                    commission_id = table.Column<string>(type: "character varying", nullable: true),
                    use_upper_commission_from_agent = table.Column<bool>(type: "boolean", nullable: false),
                    use_lower_commission_to_agent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("organization_settings_pk", x => x.organization_id);
                    table.ForeignKey(
                        name: "organization_settings_commission_fk",
                        column: x => x.commission_id,
                        principalTable: "commission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "organization_settings_organization_fk",
                        column: x => x.organization_id,
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organization_subscription",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    organization_id = table.Column<string>(type: "character varying", nullable: false),
                    price_tyiyn = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    period_type = table.Column<string>(type: "character varying", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("organization_subscription_pk", x => x.id);
                    table.ForeignKey(
                        name: "organization_subscription_organization_fk",
                        column: x => x.organization_id,
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organization_subscription_payment",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    organization_id = table.Column<string>(type: "character varying", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount_tyiyn = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    period_start = table.Column<DateTime>(type: "date", nullable: false),
                    period_end = table.Column<DateTime>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("organization_subscription_payment_pk", x => x.id);
                    table.ForeignKey(
                        name: "organization_subscription_payment_organization_fk",
                        column: x => x.organization_id,
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    name = table.Column<string>(type: "character varying", nullable: true),
                    organization = table.Column<string>(type: "character varying", nullable: true),
                    isdeleted = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0"),
                    rights = table.Column<string>(type: "character varying", nullable: true, comment: "права пользователей"),
                    avilable_services = table.Column<string>(type: "character varying", nullable: true, comment: "перечисляется id сервисов чз ;")
                },
                constraints: table =>
                {
                    table.PrimaryKey("roles_pk", x => x.id);
                    table.ForeignKey(
                        name: "roles_fk_organization",
                        column: x => x.organization,
                        principalTable: "organization",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    name = table.Column<string>(type: "character varying", nullable: true),
                    email = table.Column<string>(type: "character varying", nullable: true),
                    phone = table.Column<string>(type: "character varying", nullable: true),
                    password = table.Column<string>(type: "character varying", nullable: true),
                    isdeleted = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0"),
                    organization = table.Column<string>(type: "character varying", nullable: true),
                    role = table.Column<string>(type: "character varying", nullable: true, comment: "user\r\nadmin\r\nsuperadmin"),
                    start_page = table.Column<string>(type: "character varying", nullable: true),
                    google_id = table.Column<string>(type: "character varying", nullable: true),
                    google_email = table.Column<string>(type: "character varying", nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false"),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false"),
                    two_factor_type = table.Column<string>(type: "character varying", nullable: true),
                    two_factor_secret = table.Column<string>(type: "character varying", nullable: true),
                    two_factor_code = table.Column<string>(type: "character varying", nullable: true),
                    two_factor_code_expire = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    access_failed_count = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0"),
                    lockout_end = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    last_login = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pk", x => x.id);
                    table.ForeignKey(
                        name: "users_fk",
                        column: x => x.organization,
                        principalTable: "organization",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "specializations",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    organization_id = table.Column<string>(type: "character varying", nullable: false),
                    department_id = table.Column<string>(type: "character varying", nullable: true),
                    name = table.Column<string>(type: "character varying", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "0"),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("specializations_pk", x => x.id);
                    table.ForeignKey(
                        name: "specializations_fk_department",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "specializations_fk_organization",
                        column: x => x.organization_id,
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    role = table.Column<string>(type: "character varying", nullable: true),
                    permission = table.Column<string>(type: "character varying", nullable: true),
                    isdeleted = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0")
                },
                constraints: table =>
                {
                    table.PrimaryKey("role_permissions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "role_permissions_fk",
                        column: x => x.role,
                        principalTable: "roles",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "role_permissions_fk_1",
                        column: x => x.permission,
                        principalTable: "permissions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "appointment_settings",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    organization_id = table.Column<string>(type: "character varying", nullable: false),
                    user_id = table.Column<string>(type: "character varying", nullable: false),
                    appointment_duration_minutes = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "30"),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("appointment_settings_pk", x => x.id);
                    table.ForeignKey(
                        name: "appointment_settings_fk_organization",
                        column: x => x.organization_id,
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "appointment_settings_fk_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organization_clients",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    organization = table.Column<string>(type: "character varying", nullable: true),
                    client_name = table.Column<string>(type: "character varying", nullable: true),
                    client_type = table.Column<string>(type: "character varying", nullable: true, comment: "fiz\\jur"),
                    client_inn = table.Column<string>(type: "character varying", nullable: true),
                    client_phone = table.Column<string>(type: "character varying", nullable: true),
                    client_address = table.Column<string>(type: "character varying", nullable: true),
                    client_email = table.Column<string>(type: "character varying", nullable: true),
                    client_balance = table.Column<decimal>(type: "numeric", nullable: true, comment: "баланс в тыйынах"),
                    client_status = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0", comment: "0\\1"),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    user_creater = table.Column<string>(type: "character varying", nullable: true),
                    client_logo = table.Column<string>(type: "character varying", nullable: true),
                    client_wa = table.Column<string>(type: "character varying", nullable: true),
                    client_tg = table.Column<string>(type: "character varying", nullable: true),
                    org_client_group_id = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("organization_cients_pk", x => x.id);
                    table.ForeignKey(
                        name: "organization_clients_fk",
                        column: x => x.organization,
                        principalTable: "organization",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "organization_clients_fk2",
                        column: x => x.user_creater,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "organization_clients_org_client_group_fk",
                        column: x => x.org_client_group_id,
                        principalTable: "org_client_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_departments",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    user_id = table.Column<string>(type: "character varying", nullable: false),
                    department_id = table.Column<string>(type: "character varying", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_departments_pk", x => x.id);
                    table.ForeignKey(
                        name: "user_departments_fk_department",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "user_departments_fk_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    user = table.Column<string>(type: "character varying", nullable: true),
                    role = table.Column<string>(type: "character varying", nullable: true),
                    isdeleted = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0")
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_roles_pk", x => x.id);
                    table.ForeignKey(
                        name: "user_roles_fk",
                        column: x => x.user,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "user_roles_fk_1",
                        column: x => x.role,
                        principalTable: "roles",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_work_schedule_overrides",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    organization_id = table.Column<string>(type: "character varying", nullable: false),
                    user_id = table.Column<string>(type: "character varying", nullable: false),
                    work_date = table.Column<DateTime>(type: "date", nullable: false),
                    is_working = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    start_time = table.Column<TimeSpan>(type: "time without time zone", nullable: true),
                    end_time = table.Column<TimeSpan>(type: "time without time zone", nullable: true),
                    comment = table.Column<string>(type: "character varying", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_work_schedule_overrides_pk", x => x.id);
                    table.ForeignKey(
                        name: "user_work_schedule_overrides_fk_organization",
                        column: x => x.organization_id,
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "user_work_schedule_overrides_fk_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_work_schedules",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    organization_id = table.Column<string>(type: "character varying", nullable: false),
                    user_id = table.Column<string>(type: "character varying", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeSpan>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeSpan>(type: "time without time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_work_schedules_pk", x => x.id);
                    table.ForeignKey(
                        name: "user_work_schedules_fk_organization",
                        column: x => x.organization_id,
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "user_work_schedules_fk_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_specializations",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    organization_service_id = table.Column<string>(type: "character varying", nullable: false),
                    specialization_id = table.Column<string>(type: "character varying", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("service_specializations_pk", x => x.id);
                    table.ForeignKey(
                        name: "service_specializations_fk_service",
                        column: x => x.organization_service_id,
                        principalTable: "organization_services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "service_specializations_fk_specialization",
                        column: x => x.specialization_id,
                        principalTable: "specializations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_specializations",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    user_id = table.Column<string>(type: "character varying", nullable: false),
                    specialization_id = table.Column<string>(type: "character varying", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_specializations_pk", x => x.id);
                    table.ForeignKey(
                        name: "user_specializations_fk_specialization",
                        column: x => x.specialization_id,
                        principalTable: "specializations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "user_specializations_fk_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    organization_id = table.Column<string>(type: "character varying", nullable: false),
                    patient_id = table.Column<string>(type: "character varying", nullable: true),
                    doctor_id = table.Column<string>(type: "character varying", nullable: true),
                    title = table.Column<string>(type: "character varying", nullable: true),
                    phone = table.Column<string>(type: "character varying", nullable: true),
                    email = table.Column<string>(type: "character varying", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    referral_source = table.Column<string>(type: "character varying", nullable: true),
                    payment_type = table.Column<string>(type: "character varying", nullable: true),
                    appointment_status = table.Column<string>(type: "character varying", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    starts_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("appointments_pk", x => x.id);
                    table.ForeignKey(
                        name: "appointments_fk_doctor",
                        column: x => x.doctor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "appointments_fk_organization",
                        column: x => x.organization_id,
                        principalTable: "organization",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "appointments_fk_patient",
                        column: x => x.patient_id,
                        principalTable: "organization_clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "invoice",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    date_created = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    user_creater = table.Column<string>(type: "character varying", nullable: true),
                    invoice_status = table.Column<string>(type: "character varying", nullable: true, comment: "actual\r\nsuspended\r\nclosed"),
                    periodicity = table.Column<string>(type: "character varying", nullable: true, comment: "периодичность оплаты:\r\ndaily - ежедневно\r\nweekly - еженедельно\r\nmonthly - ежемесячно\r\nyearly - ежегодно\r\nесли указывается конкретное число то значит каждые указанное число дней. \r\nт.е если к примеру 30 то каждые 30 дней.\r\noneTime - однаразовый и прием в любой момент\r\nany - прием в любой момент"),
                    date_start_invoice = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, comment: "Дата начал инвойса, т.е с этого дня будет учитываться платеж"),
                    date_end_invoice = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, comment: "Дата окончания инвойса"),
                    balance = table.Column<decimal>(type: "numeric", nullable: true, comment: "сумма баланса, если сумма в минусе то долг.\r\nСумма указывается в тыйынах"),
                    pay_code = table.Column<string>(type: "character varying", nullable: true),
                    client = table.Column<string>(type: "character varying", nullable: true),
                    fixed_summ = table.Column<decimal>(type: "numeric", nullable: true, comment: "фиксированная сумма платежа, если не указан или 0 то сумма для платежа любая сумма"),
                    auto_prolongation = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false", comment: "автопролонгация, если указан true - то при оплате за этот инвойс автоматом создается след запись в табилице графика платежей"),
                    next_start_invoice = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, comment: "Дата начала следующего периода инвойса, если автопролонгация или инвойс установлен на несколько периодов"),
                    name_invoice = table.Column<string>(type: "character varying", nullable: true),
                    hassameaccount = table.Column<bool>(type: "boolean", nullable: false, comment: "если true - то это есть еще другой счет с таким же лицевым счетом и у которых общий баланс"),
                    qr_mode = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("invoice_pk", x => x.id);
                    table.ForeignKey(
                        name: "invoice_fk",
                        column: x => x.user_creater,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "invoice_fk_1",
                        column: x => x.client,
                        principalTable: "organization_clients",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    client_id = table.Column<string>(type: "character varying", nullable: true),
                    channel = table.Column<string>(type: "character varying", nullable: true, comment: "email, telegram, whatsapp"),
                    contact_info = table.Column<string>(type: "character varying", nullable: true, comment: "email, телефон, telegram chat_id"),
                    subject = table.Column<string>(type: "character varying", nullable: true),
                    message = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying", nullable: true, defaultValueSql: "'new'", comment: "new - новое, ожидает обработки\r\nprocessing - в процессе обработки\r\nsent - отправлено\r\nfailed - ошибка отправки"),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0"),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("notifications_pk", x => x.id);
                    table.ForeignKey(
                        name: "notifications_fk",
                        column: x => x.client_id,
                        principalTable: "organization_clients",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "organization_clients_additional_fields",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    field = table.Column<string>(type: "character varying", nullable: true, comment: "ссылка на organization_fields"),
                    value = table.Column<string>(type: "character varying", nullable: true, comment: "значение переменной\\поля"),
                    organization_client = table.Column<string>(type: "character varying", nullable: true, comment: "ссылка на клиента")
                },
                constraints: table =>
                {
                    table.PrimaryKey("organization_clients_additional_fields_pk", x => x.id);
                    table.ForeignKey(
                        name: "organization_clients_additional_fields_fk",
                        column: x => x.field,
                        principalTable: "organization_fields",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "organization_clients_additional_fields_fk_1",
                        column: x => x.organization_client,
                        principalTable: "organization_clients",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "appointment_services",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    appointment_id = table.Column<string>(type: "character varying", nullable: false),
                    organization_service_id = table.Column<string>(type: "character varying", nullable: true),
                    service_name = table.Column<string>(type: "character varying", nullable: true),
                    price_tyiyn = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "1")
                },
                constraints: table =>
                {
                    table.PrimaryKey("appointment_services_pk", x => x.id);
                    table.ForeignKey(
                        name: "appointment_services_fk_appointment",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "appointment_services_fk_org_service",
                        column: x => x.organization_service_id,
                        principalTable: "organization_services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "invoice_payments",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    invoice = table.Column<string>(type: "character varying", nullable: true),
                    date_to = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    payment_summ = table.Column<decimal>(type: "numeric", nullable: true, comment: "сумма оплаты (тыйыны)"),
                    payment_status = table.Column<string>(type: "character varying", nullable: true, comment: "paid - уже оплатил\r\nnon_paid - еще не оплатил\r\nanulated - в таком случае д\\с возвращается обратно на баланс по инвойсу"),
                    date_from = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    period_value = table.Column<string>(type: "character varying", nullable: true, comment: "какой месяц или год\r\nесли периодичность месяц или год")
                },
                constraints: table =>
                {
                    table.PrimaryKey("invoice_payments_pk", x => x.id);
                    table.ForeignKey(
                        name: "invoice_payments_fk",
                        column: x => x.invoice,
                        principalTable: "invoice",
                        principalColumn: "id");
                },
                comment: "записи - за какие периоды оплачены");

            migrationBuilder.CreateTable(
                name: "invoice_qr",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    invoice_id = table.Column<string>(type: "character varying", nullable: false),
                    transaction = table.Column<string>(type: "character varying", nullable: true),
                    status = table.Column<string>(type: "character varying", nullable: false),
                    qr_link = table.Column<string>(type: "text", nullable: true),
                    qr_code_base64 = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    disabled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("invoice_qr_pk", x => x.id);
                    table.ForeignKey(
                        name: "invoice_qr_fk_invoice",
                        column: x => x.invoice_id,
                        principalTable: "invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_services",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    invoice = table.Column<string>(type: "character varying", nullable: true),
                    service = table.Column<string>(type: "character varying", nullable: true),
                    service_summ = table.Column<decimal>(type: "numeric", nullable: true, comment: "стоимость сервиса")
                },
                constraints: table =>
                {
                    table.PrimaryKey("invoice_services_pk", x => x.id);
                    table.ForeignKey(
                        name: "invoice_services_fk",
                        column: x => x.invoice,
                        principalTable: "invoice",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "invoice_services_fk_1",
                        column: x => x.service,
                        principalTable: "organization_services",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    transaction_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    transaction_status = table.Column<string>(type: "character varying", nullable: true, comment: "success\r\nerror"),
                    summ = table.Column<decimal>(type: "numeric", nullable: true, comment: "сумма в тыйынах"),
                    transaction_summ = table.Column<decimal>(type: "numeric", nullable: true, comment: "сумма транзакции в тыйынах вмесе с комиссией"),
                    lower_commission_from_org = table.Column<decimal>(type: "numeric(18,2)", nullable: true, comment: "нижняя комиссия от организации (тыйыны)"),
                    upper_commission_from_agent = table.Column<decimal>(type: "numeric(18,2)", nullable: true, comment: "верхняя комиссия от агента (тыйыны)"),
                    lower_commission_to_agent = table.Column<decimal>(type: "numeric(18,2)", nullable: true, comment: "нижняя комиссия к агенту (тыйыны)"),
                    invoice = table.Column<string>(type: "character varying", nullable: true),
                    agent = table.Column<string>(type: "character varying", nullable: true),
                    payment_invoice = table.Column<string>(type: "character varying", nullable: true),
                    txn_id = table.Column<string>(type: "character varying", nullable: true),
                    transaction_system = table.Column<string>(type: "character varying", nullable: true),
                    transaction_type = table.Column<string>(type: "character varying", nullable: true, comment: "debit - приход\r\ncredit - расход")
                },
                constraints: table =>
                {
                    table.PrimaryKey("transactions_pk", x => x.id);
                    table.ForeignKey(
                        name: "transactions_fk",
                        column: x => x.invoice,
                        principalTable: "invoice",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "transactions_fk_agent",
                        column: x => x.agent,
                        principalTable: "agent",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "agent_commission_agent_organization_uq",
                table: "agent_commission",
                columns: new[] { "agent_id", "organization_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_commission_commission_id",
                table: "agent_commission",
                column: "commission_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_commission_lower_commission_id",
                table: "agent_commission",
                column: "lower_commission_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_commission_organization_id",
                table: "agent_commission",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_appointment_medical_templates_active",
                table: "appointment_medical_templates",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_appointment_medical_templates_org_id",
                table: "appointment_medical_templates",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_appointment_medical_templates_type",
                table: "appointment_medical_templates",
                column: "template_type");

            migrationBuilder.CreateIndex(
                name: "idx_appointment_services_appointment_id",
                table: "appointment_services",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "idx_appointment_services_org_service_id",
                table: "appointment_services",
                column: "organization_service_id");

            migrationBuilder.CreateIndex(
                name: "idx_appointment_settings_org_id",
                table: "appointment_settings",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_appointment_settings_user_id",
                table: "appointment_settings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_appointment_settings_org_user",
                table: "appointment_settings",
                columns: new[] { "organization_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_appointments_doctor_id",
                table: "appointments",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "idx_appointments_organization_id",
                table: "appointments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_appointments_patient_id",
                table: "appointments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "idx_appointments_starts_at",
                table: "appointments",
                column: "starts_at");

            migrationBuilder.CreateIndex(
                name: "IX_commission_tier_commission_id",
                table: "commission_tier",
                column: "commission_id");

            migrationBuilder.CreateIndex(
                name: "idx_departments_organization_id",
                table: "departments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ux_departments_org_name",
                table: "departments",
                columns: new[] { "organization_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_client",
                table: "invoice",
                column: "client");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_user_creater",
                table: "invoice",
                column: "user_creater");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_payments_invoice",
                table: "invoice_payments",
                column: "invoice");

            migrationBuilder.CreateIndex(
                name: "idx_invoice_qr_invoice_id",
                table: "invoice_qr",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "idx_invoice_qr_transaction",
                table: "invoice_qr",
                column: "transaction");

            migrationBuilder.CreateIndex(
                name: "ux_invoice_qr_active_per_invoice",
                table: "invoice_qr",
                column: "invoice_id",
                unique: true,
                filter: "((status)::text = 'active'::text)");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_services_invoice",
                table: "invoice_services",
                column: "invoice");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_services_service",
                table: "invoice_services",
                column: "service");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_client_id",
                table: "notifications",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_client_groups_organization_id",
                table: "org_client_groups",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_client_groups_parent_group_id",
                table: "org_client_groups",
                column: "parent_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_clients_org_client_group_id",
                table: "organization_clients",
                column: "org_client_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_clients_organization",
                table: "organization_clients",
                column: "organization");

            migrationBuilder.CreateIndex(
                name: "IX_organization_clients_user_creater",
                table: "organization_clients",
                column: "user_creater");

            migrationBuilder.CreateIndex(
                name: "IX_organization_clients_additional_fields_field",
                table: "organization_clients_additional_fields",
                column: "field");

            migrationBuilder.CreateIndex(
                name: "IX_organization_clients_additional_fields_organization_client",
                table: "organization_clients_additional_fields",
                column: "organization_client");

            migrationBuilder.CreateIndex(
                name: "IX_organization_fields_organization",
                table: "organization_fields",
                column: "organization");

            migrationBuilder.CreateIndex(
                name: "IX_organization_services_organization",
                table: "organization_services",
                column: "organization");

            migrationBuilder.CreateIndex(
                name: "IX_organization_settings_commission_id",
                table: "organization_settings",
                column: "commission_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_subscription_organization_id",
                table: "organization_subscription",
                column: "organization_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_subscription_payment_organization_id",
                table: "organization_subscription_payment",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_permissions_area",
                table: "permissions",
                column: "area");

            migrationBuilder.CreateIndex(
                name: "idx_permissions_category",
                table: "permissions",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "idx_permissions_code",
                table: "permissions",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "idx_permissions_isdeleted",
                table: "permissions",
                column: "isdeleted");

            migrationBuilder.CreateIndex(
                name: "permissions_code_key",
                table: "permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_role_permissions_isdeleted",
                table: "role_permissions",
                column: "isdeleted");

            migrationBuilder.CreateIndex(
                name: "idx_role_permissions_permission",
                table: "role_permissions",
                column: "permission");

            migrationBuilder.CreateIndex(
                name: "idx_role_permissions_role",
                table: "role_permissions",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "idx_roles_organization",
                table: "roles",
                column: "organization");

            migrationBuilder.CreateIndex(
                name: "idx_service_specializations_service_id",
                table: "service_specializations",
                column: "organization_service_id");

            migrationBuilder.CreateIndex(
                name: "idx_service_specializations_specialization_id",
                table: "service_specializations",
                column: "specialization_id");

            migrationBuilder.CreateIndex(
                name: "ux_service_specializations_service_specialization",
                table: "service_specializations",
                columns: new[] { "organization_service_id", "specialization_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_specializations_department_id",
                table: "specializations",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "idx_specializations_organization_id",
                table: "specializations",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ux_specializations_org_name",
                table: "specializations",
                columns: new[] { "organization_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_agent",
                table: "transactions",
                column: "agent");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_invoice",
                table: "transactions",
                column: "invoice");

            migrationBuilder.CreateIndex(
                name: "idx_user_departments_department_id",
                table: "user_departments",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_departments_user_id",
                table: "user_departments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_departments_user_department",
                table: "user_departments",
                columns: new[] { "user_id", "department_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role",
                table: "user_roles",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_user",
                table: "user_roles",
                column: "user");

            migrationBuilder.CreateIndex(
                name: "idx_user_specializations_specialization_id",
                table: "user_specializations",
                column: "specialization_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_specializations_user_id",
                table: "user_specializations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_specializations_user_specialization",
                table: "user_specializations",
                columns: new[] { "user_id", "specialization_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_user_work_schedule_overrides_organization_id",
                table: "user_work_schedule_overrides",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_work_schedule_overrides_user_id",
                table: "user_work_schedule_overrides",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_work_schedule_overrides_user_date",
                table: "user_work_schedule_overrides",
                columns: new[] { "user_id", "work_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_user_work_schedules_organization_id",
                table: "user_work_schedules",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_work_schedules_user_id",
                table: "user_work_schedules",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_work_schedules_user_day",
                table: "user_work_schedules",
                columns: new[] { "user_id", "day_of_week" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_organization",
                table: "users",
                column: "organization");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_commission");

            migrationBuilder.DropTable(
                name: "appointment_medical_templates");

            migrationBuilder.DropTable(
                name: "appointment_services");

            migrationBuilder.DropTable(
                name: "appointment_settings");

            migrationBuilder.DropTable(
                name: "commission_tier");

            migrationBuilder.DropTable(
                name: "history");

            migrationBuilder.DropTable(
                name: "invoice_payments");

            migrationBuilder.DropTable(
                name: "invoice_qr");

            migrationBuilder.DropTable(
                name: "invoice_services");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "organization_clients_additional_fields");

            migrationBuilder.DropTable(
                name: "organization_settings");

            migrationBuilder.DropTable(
                name: "organization_subscription");

            migrationBuilder.DropTable(
                name: "organization_subscription_payment");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "service_specializations");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "user_departments");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_specializations");

            migrationBuilder.DropTable(
                name: "user_work_schedule_overrides");

            migrationBuilder.DropTable(
                name: "user_work_schedules");

            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "organization_fields");

            migrationBuilder.DropTable(
                name: "commission");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "organization_services");

            migrationBuilder.DropTable(
                name: "invoice");

            migrationBuilder.DropTable(
                name: "agent");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "specializations");

            migrationBuilder.DropTable(
                name: "organization_clients");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "org_client_groups");

            migrationBuilder.DropTable(
                name: "organization");
        }
    }
}
