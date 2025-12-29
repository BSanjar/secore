using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<History> Histories { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoicePayment> InvoicePayments { get; set; }

    public virtual DbSet<InvoiceService> InvoiceServices { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<OrganizationClient> OrganizationClients { get; set; }

    public virtual DbSet<OrganizationClientsAdditionalField> OrganizationClientsAdditionalFields { get; set; }

    public virtual DbSet<OrganizationField> OrganizationFields { get; set; }

    public virtual DbSet<OrganizationService> OrganizationServices { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RolePermission> RolePermissions { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=77.73.69.143;Database=secore;Username=secore;Password=secore");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<History>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("history");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Invoice)
                .HasComment("если действие по инвойсу то id инвойса")
                .HasColumnType("character varying")
                .HasColumnName("invoice");
            entity.Property(e => e.TypeHistory)
                .HasComment("user_edited\r\ninvoice_edited")
                .HasColumnType("character varying")
                .HasColumnName("type_history");
            entity.Property(e => e.UserEditor)
                .HasComment("Пользователь который совершил действие")
                .HasColumnType("character varying")
                .HasColumnName("user_editor");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoice_pk");

            entity.ToTable("invoice");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.AutoProlongation)
                .HasDefaultValueSql("false")
                .HasComment("автопролонгация, если указан true - то при оплате за этот инвойс автоматом создается след запись в табилице графика платежей")
                .HasColumnName("auto_prolongation");
            entity.Property(e => e.Balance)
                .HasComment("сумма баланса, если сумма в минусе то долг.\r\nСумма указывается в тыйынах")
                .HasColumnName("balance");
            entity.Property(e => e.Client)
                .HasColumnType("character varying")
                .HasColumnName("client");
            entity.Property(e => e.DateCreated)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_created");
            entity.Property(e => e.DateStartInvoice)
                .HasComment("Дата начал инвойса, т.е с этого дня будет учитываться платеж")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_start_invoice");
            entity.Property(e => e.FixedSumm)
                .HasComment("фиксированная сумма платежа, если не указан или 0 то сумма для платежа любая сумма")
                .HasColumnName("fixed_summ");
            entity.Property(e => e.Hassameaccount)
                .HasComment("если true - то это есть еще другой счет с таким же лицевым счетом и у которых общий баланс")
                .HasColumnName("hassameaccount");
            entity.Property(e => e.InvoiceStatus)
                .HasComment("actual\r\nsuspended\r\nclosed")
                .HasColumnType("character varying")
                .HasColumnName("invoice_status");
            entity.Property(e => e.NameInvoice)
                .HasColumnType("character varying")
                .HasColumnName("name_invoice");
            entity.Property(e => e.NextStartInvoice)
                .HasComment("Дата начала следующего периода инвойса, если автопролонгация или инвойс установлен на несколько периодов")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("next_start_invoice");
            entity.Property(e => e.PayCode)
                .HasColumnType("character varying")
                .HasColumnName("pay_code");
            entity.Property(e => e.Periodicity)
                .HasComment("периодичность оплаты:\r\ndaily - ежедневно\r\nweekly - еженедельно\r\nmonthly - ежемесячно\r\nyearly - ежегодно\r\nесли указывается конкретное число то значит каждые указанное число дней. \r\nт.е если к примеру 30 то каждые 30 дней.\r\noneTime - однаразовый и прием в любой момент\r\nany - прием в любой момент")
                .HasColumnType("character varying")
                .HasColumnName("periodicity");
            entity.Property(e => e.UserCreater)
                .HasColumnType("character varying")
                .HasColumnName("user_creater");

            entity.HasOne(d => d.ClientNavigation).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.Client)
                .HasConstraintName("invoice_fk_1");

            entity.HasOne(d => d.UserCreaterNavigation).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.UserCreater)
                .HasConstraintName("invoice_fk");
        });

        modelBuilder.Entity<InvoicePayment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoice_payments_pk");

            entity.ToTable("invoice_payments", tb => tb.HasComment("записи - за какие периоды оплачены"));

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.DateFrom)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_from");
            entity.Property(e => e.DateTo)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_to");
            entity.Property(e => e.Invoice)
                .HasColumnType("character varying")
                .HasColumnName("invoice");
            entity.Property(e => e.PaymentStatus)
                .HasComment("paid - уже оплатил\r\nnon_paid - еще не оплатил\r\nanulated - в таком случае д\\с возвращается обратно на баланс по инвойсу")
                .HasColumnType("character varying")
                .HasColumnName("payment_status");
            entity.Property(e => e.PaymentSumm)
                .HasComment("сумма оплаты")
                .HasColumnType("character varying")
                .HasColumnName("payment_summ");
            entity.Property(e => e.PeriodValue)
                .HasComment("какой месяц или год\r\nесли периодичность месяц или год")
                .HasColumnType("character varying")
                .HasColumnName("period_value");

            entity.HasOne(d => d.InvoiceNavigation).WithMany(p => p.InvoicePayments)
                .HasForeignKey(d => d.Invoice)
                .HasConstraintName("invoice_payments_fk");
        });

        modelBuilder.Entity<InvoiceService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoice_services_pk");

            entity.ToTable("invoice_services");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Invoice)
                .HasColumnType("character varying")
                .HasColumnName("invoice");
            entity.Property(e => e.Service)
                .HasColumnType("character varying")
                .HasColumnName("service");
            entity.Property(e => e.ServiceSumm)
                .HasComment("стоимость сервиса")
                .HasColumnName("service_summ");

            entity.HasOne(d => d.InvoiceNavigation).WithMany(p => p.InvoiceServices)
                .HasForeignKey(d => d.Invoice)
                .HasConstraintName("invoice_services_fk");

            entity.HasOne(d => d.ServiceNavigation).WithMany(p => p.InvoiceServices)
                .HasForeignKey(d => d.Service)
                .HasConstraintName("invoice_services_fk_1");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_pk");

            entity.ToTable("organization");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.AllowedHassameaccount)
                .HasComment("если true - то организации могут создавать счета к оплате с одинаковыми л\\с")
                .HasColumnName("allowed_hassameaccount");
            entity.Property(e => e.ApiLogin)
                .HasComment("Логин для API")
                .HasColumnType("character varying")
                .HasColumnName("api_login");
            entity.Property(e => e.ApiPassword)
                .HasColumnType("character varying")
                .HasColumnName("api_password");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
            entity.Property(e => e.Organizationtype)
                .HasComment("standart\r\ndetsad\r\nschool\r\nmedclinic")
                .HasColumnType("character varying")
                .HasColumnName("organizationtype");
            entity.Property(e => e.Paymentreminderdaysbefore)
                .HasComment("Количество дней за которую будет начинатся отправка уведомлений по оплате на организацию")
                .HasColumnName("paymentreminderdaysbefore");
        });

        modelBuilder.Entity<OrganizationClient>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_cients_pk");

            entity.ToTable("organization_clients");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.ClientAdres)
                .HasColumnType("character varying")
                .HasColumnName("client_adres");
            entity.Property(e => e.ClientBalance)
                .HasComment("баланс в тыйынах")
                .HasColumnName("client_balance");
            entity.Property(e => e.ClientEmail)
                .HasColumnType("character varying")
                .HasColumnName("client_email");
            entity.Property(e => e.ClientInn)
                .HasColumnType("character varying")
                .HasColumnName("client_inn");
            entity.Property(e => e.ClientLogo)
                .HasColumnType("character varying")
                .HasColumnName("client_logo");
            entity.Property(e => e.ClientName)
                .HasColumnType("character varying")
                .HasColumnName("client_name");
            entity.Property(e => e.ClientPhone)
                .HasColumnType("character varying")
                .HasColumnName("client_phone");
            entity.Property(e => e.ClientStatus)
                .HasDefaultValueSql("0")
                .HasComment("0\\1")
                .HasColumnName("client_status");
            entity.Property(e => e.ClinetType)
                .HasComment("fiz\\jur")
                .HasColumnType("character varying")
                .HasColumnName("clinet_type");
            entity.Property(e => e.CreatedDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_date");
            entity.Property(e => e.Organization)
                .HasColumnType("character varying")
                .HasColumnName("organization");
            entity.Property(e => e.UpdatedDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_date");
            entity.Property(e => e.UserCreater)
                .HasColumnType("character varying")
                .HasColumnName("user_creater");

            entity.HasOne(d => d.OrganizationNavigation).WithMany(p => p.OrganizationClients)
                .HasForeignKey(d => d.Organization)
                .HasConstraintName("organization_clients_fk");

            entity.HasOne(d => d.UserCreaterNavigation).WithMany(p => p.OrganizationClients)
                .HasForeignKey(d => d.UserCreater)
                .HasConstraintName("organization_clients_fk2");
        });

        modelBuilder.Entity<OrganizationClientsAdditionalField>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_clients_additional_fields_pk");

            entity.ToTable("organization_clients_additional_fields");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Field)
                .HasComment("ссылка на organization_fields")
                .HasColumnType("character varying")
                .HasColumnName("field");
            entity.Property(e => e.OrganizationClient)
                .HasComment("ссылка на клиента")
                .HasColumnType("character varying")
                .HasColumnName("organization_client");
            entity.Property(e => e.Value)
                .HasComment("значение переменной\\поля")
                .HasColumnType("character varying")
                .HasColumnName("value");

            entity.HasOne(d => d.FieldNavigation).WithMany(p => p.OrganizationClientsAdditionalFields)
                .HasForeignKey(d => d.Field)
                .HasConstraintName("organization_clients_additional_fields_fk");

            entity.HasOne(d => d.OrganizationClientNavigation).WithMany(p => p.OrganizationClientsAdditionalFields)
                .HasForeignKey(d => d.OrganizationClient)
                .HasConstraintName("organization_clients_additional_fields_fk_1");
        });

        modelBuilder.Entity<OrganizationField>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_fields_pk");

            entity.ToTable("organization_fields");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.FieldName)
                .HasColumnType("character varying")
                .HasColumnName("field_name");
            entity.Property(e => e.FieldSelectValues)
                .HasComment("варианты для выбора чз - ;")
                .HasColumnType("character varying")
                .HasColumnName("field_select_values");
            entity.Property(e => e.FieldType)
                .HasComment("int\r\nstring\r\nmoney\r\nselected\r\ndatetime")
                .HasColumnType("character varying")
                .HasColumnName("field_type");
            entity.Property(e => e.Filterbyfield)
                .HasDefaultValueSql("false")
                .HasColumnName("filterbyfield");
            entity.Property(e => e.Isdeleted)
                .HasDefaultValueSql("0")
                .HasColumnName("isdeleted");
            entity.Property(e => e.Organization)
                .HasColumnType("character varying")
                .HasColumnName("organization");

            entity.HasOne(d => d.OrganizationNavigation).WithMany(p => p.OrganizationFields)
                .HasForeignKey(d => d.Organization)
                .HasConstraintName("organization_fields_fk");
        });

        modelBuilder.Entity<OrganizationService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_services_pk");

            entity.ToTable("organization_services");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.FixedSum)
                .HasDefaultValueSql("0")
                .HasComment("если 1 то услуга с фиксированной суммой")
                .HasColumnName("fixed_sum");
            entity.Property(e => e.Isdeleted)
                .HasDefaultValueSql("0")
                .HasColumnName("isdeleted");
            entity.Property(e => e.MaxSumm)
                .HasComment("макс сумма в тыйынах")
                .HasColumnName("max_summ");
            entity.Property(e => e.MinSumm)
                .HasComment("мин сумма в тыйынах")
                .HasColumnName("min_summ");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
            entity.Property(e => e.Organization)
                .HasColumnType("character varying")
                .HasColumnName("organization");
            entity.Property(e => e.ServiceSumm)
                .HasComment("если fixed_sum = 1, то тут будет значение фиксированной суммы")
                .HasColumnName("service_summ");

            entity.HasOne(d => d.OrganizationNavigation).WithMany(p => p.OrganizationServices)
                .HasForeignKey(d => d.Organization)
                .HasConstraintName("organization_services_fk");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("permissions_pkey");

            entity.ToTable("permissions");

            entity.HasIndex(e => e.Area, "idx_permissions_area");

            entity.HasIndex(e => e.Category, "idx_permissions_category");

            entity.HasIndex(e => e.Code, "idx_permissions_code");

            entity.HasIndex(e => e.Isdeleted, "idx_permissions_isdeleted");

            entity.HasIndex(e => e.Code, "permissions_code_key").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Area)
                .HasColumnType("character varying")
                .HasColumnName("area");
            entity.Property(e => e.Category)
                .HasColumnType("character varying")
                .HasColumnName("category");
            entity.Property(e => e.Code)
                .HasColumnType("character varying")
                .HasColumnName("code");
            entity.Property(e => e.Description)
                .HasColumnType("character varying")
                .HasColumnName("description");
            entity.Property(e => e.Isdeleted)
                .HasDefaultValueSql("0")
                .HasColumnName("isdeleted");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pk");

            entity.ToTable("roles");

            entity.HasIndex(e => e.Organization, "idx_roles_organization");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.AvilableServices)
                .HasComment("перечисляется id сервисов чз ;")
                .HasColumnType("character varying")
                .HasColumnName("avilable_services");
            entity.Property(e => e.Isdeleted)
                .HasDefaultValueSql("0")
                .HasColumnName("isdeleted");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
            entity.Property(e => e.Organization)
                .HasColumnType("character varying")
                .HasColumnName("organization");
            entity.Property(e => e.Rights)
                .HasComment("права пользователей")
                .HasColumnType("character varying")
                .HasColumnName("rights");

            entity.HasOne(d => d.OrganizationNavigation).WithMany(p => p.Roles)
                .HasForeignKey(d => d.Organization)
                .HasConstraintName("roles_fk_organization");
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("role_permissions_pkey");

            entity.ToTable("role_permissions");

            entity.HasIndex(e => e.Isdeleted, "idx_role_permissions_isdeleted");

            entity.HasIndex(e => e.Permission, "idx_role_permissions_permission");

            entity.HasIndex(e => e.Role, "idx_role_permissions_role");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Isdeleted)
                .HasDefaultValueSql("0")
                .HasColumnName("isdeleted");
            entity.Property(e => e.Permission)
                .HasColumnType("character varying")
                .HasColumnName("permission");
            entity.Property(e => e.Role)
                .HasColumnType("character varying")
                .HasColumnName("role");

            entity.HasOne(d => d.PermissionNavigation).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.Permission)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("role_permissions_fk_1");

            entity.HasOne(d => d.RoleNavigation).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.Role)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("role_permissions_fk");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("transactions_pk");

            entity.ToTable("transactions");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Invoice)
                .HasColumnType("character varying")
                .HasColumnName("invoice");
            entity.Property(e => e.Summ)
                .HasComment("сумма в тыйынах")
                .HasColumnName("summ");
            entity.Property(e => e.TransactionDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("transaction_date");
            entity.Property(e => e.TransactionStatus)
                .HasComment("success\r\nerror")
                .HasColumnType("character varying")
                .HasColumnName("transaction_status");
            entity.Property(e => e.TransactionSumm)
                .HasComment("сумма транзакции в тыйынах вмесе с комиссией")
                .HasColumnName("transaction_summ");
            entity.Property(e => e.TransactionType)
                .HasComment("debit - приход\r\ncredit - расход")
                .HasColumnType("character varying")
                .HasColumnName("transaction_type");

            entity.HasOne(d => d.InvoiceNavigation).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.Invoice)
                .HasConstraintName("transactions_fk");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pk");

            entity.ToTable("users");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.AccessFailedCount)
                .HasDefaultValueSql("0")
                .HasColumnName("access_failed_count");
            entity.Property(e => e.Email)
                .HasColumnType("character varying")
                .HasColumnName("email");
            entity.Property(e => e.EmailConfirmed)
                .HasDefaultValueSql("false")
                .HasColumnName("email_confirmed");
            entity.Property(e => e.GoogleEmail)
                .HasColumnType("character varying")
                .HasColumnName("google_email");
            entity.Property(e => e.GoogleId)
                .HasColumnType("character varying")
                .HasColumnName("google_id");
            entity.Property(e => e.Isdeleted)
                .HasDefaultValueSql("0")
                .HasColumnName("isdeleted");
            entity.Property(e => e.LastLogin)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_login");
            entity.Property(e => e.LockoutEnd)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("lockout_end");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
            entity.Property(e => e.Organization)
                .HasColumnType("character varying")
                .HasColumnName("organization");
            entity.Property(e => e.Password)
                .HasColumnType("character varying")
                .HasColumnName("password");
            entity.Property(e => e.Phone)
                .HasColumnType("character varying")
                .HasColumnName("phone");
            entity.Property(e => e.Role)
                .HasComment("user\r\nadmin\r\nsuperadmin")
                .HasColumnType("character varying")
                .HasColumnName("role");
            entity.Property(e => e.TwoFactorCode)
                .HasColumnType("character varying")
                .HasColumnName("two_factor_code");
            entity.Property(e => e.TwoFactorCodeExpire)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("two_factor_code_expire");
            entity.Property(e => e.TwoFactorEnabled)
                .HasDefaultValueSql("false")
                .HasColumnName("two_factor_enabled");
            entity.Property(e => e.TwoFactorSecret)
                .HasColumnType("character varying")
                .HasColumnName("two_factor_secret");
            entity.Property(e => e.TwoFactorType)
                .HasColumnType("character varying")
                .HasColumnName("two_factor_type");

            entity.HasOne(d => d.OrganizationNavigation).WithMany(p => p.Users)
                .HasForeignKey(d => d.Organization)
                .HasConstraintName("users_fk");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_roles_pk");

            entity.ToTable("user_roles");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Isdeleted)
                .HasDefaultValueSql("0")
                .HasColumnName("isdeleted");
            entity.Property(e => e.Role)
                .HasColumnType("character varying")
                .HasColumnName("role");
            entity.Property(e => e.User)
                .HasColumnType("character varying")
                .HasColumnName("user");

            entity.HasOne(d => d.RoleNavigation).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.Role)
                .HasConstraintName("user_roles_fk_1");

            entity.HasOne(d => d.UserNavigation).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.User)
                .HasConstraintName("user_roles_fk");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
