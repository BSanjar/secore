using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    organizationtype = table.Column<string>(type: "character varying", nullable: true, comment: "standart\r\ndetsad\r\nschool\r\nmedclinic"),
                    allowed_hassameaccount = table.Column<bool>(type: "boolean", nullable: false, comment: "если true - то организации могут создавать счета к оплате с одинаковыми л\\с"),
                    paymentreminderdaysbefore = table.Column<int>(type: "integer", nullable: false, comment: "Количество дней за которую будет начинатся отправка уведомлений по оплате на организацию"),
                    api_login = table.Column<string>(type: "character varying", nullable: true, comment: "Логин для API"),
                    api_password = table.Column<string>(type: "character varying", nullable: true)
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
                name: "organization_clients",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    organization = table.Column<string>(type: "character varying", nullable: true),
                    client_name = table.Column<string>(type: "character varying", nullable: true),
                    clinet_type = table.Column<string>(type: "character varying", nullable: true, comment: "fiz\\jur"),
                    client_inn = table.Column<string>(type: "character varying", nullable: true),
                    client_phone = table.Column<string>(type: "character varying", nullable: true),
                    client_adres = table.Column<string>(type: "character varying", nullable: true),
                    client_email = table.Column<string>(type: "character varying", nullable: true),
                    client_balance = table.Column<decimal>(type: "numeric", nullable: true, comment: "баланс в тыйынах"),
                    client_status = table.Column<int>(type: "integer", nullable: true, defaultValueSql: "0", comment: "0\\1"),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    user_creater = table.Column<string>(type: "character varying", nullable: true),
                    client_logo = table.Column<string>(type: "character varying", nullable: true)
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
                name: "invoice",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    date_created = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    user_creater = table.Column<string>(type: "character varying", nullable: true),
                    invoice_status = table.Column<string>(type: "character varying", nullable: true, comment: "actual\r\nsuspended\r\nclosed"),
                    periodicity = table.Column<string>(type: "character varying", nullable: true, comment: "периодичность оплаты:\r\ndaily - ежедневно\r\nweekly - еженедельно\r\nmonthly - ежемесячно\r\nyearly - ежегодно\r\nесли указывается конкретное число то значит каждые указанное число дней. \r\nт.е если к примеру 30 то каждые 30 дней.\r\noneTime - однаразовый и прием в любой момент\r\nany - прием в любой момент"),
                    date_start_invoice = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, comment: "Дата начал инвойса, т.е с этого дня будет учитываться платеж"),
                    balance = table.Column<decimal>(type: "numeric", nullable: true, comment: "сумма баланса, если сумма в минусе то долг.\r\nСумма указывается в тыйынах"),
                    pay_code = table.Column<string>(type: "character varying", nullable: true),
                    client = table.Column<string>(type: "character varying", nullable: true),
                    fixed_summ = table.Column<decimal>(type: "numeric", nullable: true, comment: "фиксированная сумма платежа, если не указан или 0 то сумма для платежа любая сумма"),
                    auto_prolongation = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "false", comment: "автопролонгация, если указан true - то при оплате за этот инвойс автоматом создается след запись в табилице графика платежей"),
                    next_start_invoice = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, comment: "Дата начала следующего периода инвойса, если автопролонгация или инвойс установлен на несколько периодов"),
                    name_invoice = table.Column<string>(type: "character varying", nullable: true),
                    hassameaccount = table.Column<bool>(type: "boolean", nullable: false, comment: "если true - то это есть еще другой счет с таким же лицевым счетом и у которых общий баланс")
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
                name: "invoice_payments",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying", nullable: false),
                    invoice = table.Column<string>(type: "character varying", nullable: true),
                    date_to = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    payment_summ = table.Column<string>(type: "character varying", nullable: true, comment: "сумма оплаты"),
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
                    invoice = table.Column<string>(type: "character varying", nullable: true),
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
                });

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
                name: "IX_transactions_invoice",
                table: "transactions",
                column: "invoice");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role",
                table: "user_roles",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_user",
                table: "user_roles",
                column: "user");

            migrationBuilder.CreateIndex(
                name: "IX_users_organization",
                table: "users",
                column: "organization");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "history");

            migrationBuilder.DropTable(
                name: "invoice_payments");

            migrationBuilder.DropTable(
                name: "invoice_services");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "organization_clients_additional_fields");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "organization_services");

            migrationBuilder.DropTable(
                name: "organization_fields");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "invoice");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "organization_clients");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "organization");
        }
    }
}
