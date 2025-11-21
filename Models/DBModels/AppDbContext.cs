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

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=77.73.69.143;Port=5432;Database=secore;Username=secore;Password=secore");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<History>(entity =>
        {
            entity.Property(e => e.Invoice).HasComment("если действие по инвойсу то id инвойса");
            entity.Property(e => e.TypeHistory).HasComment("user_edited\r\ninvoice_edited");
            entity.Property(e => e.UserEditor).HasComment("Пользователь который совершил действие");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoice_pk");

            entity.Property(e => e.AutoProlongation)
                .HasDefaultValueSql("false")
                .HasComment("автопролонгация, если указан true - то при оплате за этот инвойс автоматом создается след запись в табилице графика платежей");
            entity.Property(e => e.Balance).HasComment("сумма баланса, если сумма в минусе то долг.\r\nСумма указывается в тыйынах");
            entity.Property(e => e.DateStartInvoice).HasComment("Дата начал инвойса, т.е с этого дня будет учитываться платеж");
            entity.Property(e => e.FixedSumm).HasComment("фиксированная сумма платежа, если не указан или 0 то сумма для платежа любая сумма");
            entity.Property(e => e.InvoiceStatus).HasComment("actual\r\nsuspended\r\nclosed");
            entity.Property(e => e.NextStartInvoice).HasComment("Дата начала следующего периода инвойса, если автопролонгация или инвойс установлен на несколько периодов");
            entity.Property(e => e.Periodicity).HasComment("периодичность оплаты:\r\ndaily - ежедневно\r\nweekly - еженедельно\r\nmonthly - ежемесячно\r\nyearly - ежегодно\r\nесли указывается конкретное число то значит каждые указанное число дней. \r\nт.е если к примеру 30 то каждые 30 дней.\r\noneTime - однаразовый и прием в любой момент\r\nany - прием в любой момент");

            entity.HasOne(d => d.ClientNavigation).WithMany(p => p.Invoices).HasConstraintName("invoice_fk_1");

            entity.HasOne(d => d.UserCreaterNavigation).WithMany(p => p.Invoices).HasConstraintName("invoice_fk");
        });

        modelBuilder.Entity<InvoicePayment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoice_payments_pk");

            entity.ToTable("invoice_payments", tb => tb.HasComment("записи - за какие периоды оплачены"));

            entity.Property(e => e.PaymentStatus).HasComment("paid - уже оплатил\r\nnon_paid - еще не оплатил\r\nanulated - в таком случае д\\с возвращается обратно на баланс по инвойсу");
            entity.Property(e => e.PaymentSumm).HasComment("сумма оплаты");
            entity.Property(e => e.PeriodValue).HasComment("какой месяц или год\r\nесли периодичность месяц или год");

            entity.HasOne(d => d.InvoiceNavigation).WithMany(p => p.InvoicePayments).HasConstraintName("invoice_payments_fk");
        });

        modelBuilder.Entity<InvoiceService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoice_services_pk");

            entity.Property(e => e.ServiceSumm).HasComment("стоимость сервиса");

            entity.HasOne(d => d.InvoiceNavigation).WithMany(p => p.InvoiceServices).HasConstraintName("invoice_services_fk");

            entity.HasOne(d => d.ServiceNavigation).WithMany(p => p.InvoiceServices).HasConstraintName("invoice_services_fk_1");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_pk");

            entity.Property(e => e.Organizationtype).HasComment("standart\r\ndetsad\r\nschool\r\nmedclinic");
        });

        modelBuilder.Entity<OrganizationClient>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_cients_pk");

            entity.Property(e => e.ClientBalance).HasComment("баланс в тыйынах");
            entity.Property(e => e.ClientStatus)
                .HasDefaultValueSql("0")
                .HasComment("0\\1");
            entity.Property(e => e.ClinetType).HasComment("fiz\\jur");

            entity.HasOne(d => d.OrganizationNavigation).WithMany(p => p.OrganizationClients).HasConstraintName("organization_clients_fk");

            entity.HasOne(d => d.UserCreaterNavigation).WithMany(p => p.OrganizationClients).HasConstraintName("organization_clients_fk2");
        });

        modelBuilder.Entity<OrganizationClientsAdditionalField>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_clients_additional_fields_pk");

            entity.Property(e => e.Field).HasComment("ссылка на organization_fields");
            entity.Property(e => e.OrganizationClient).HasComment("ссылка на клиента");
            entity.Property(e => e.Value).HasComment("значение переменной\\поля");

            entity.HasOne(d => d.FieldNavigation).WithMany(p => p.OrganizationClientsAdditionalFields).HasConstraintName("organization_clients_additional_fields_fk");

            entity.HasOne(d => d.OrganizationClientNavigation).WithMany(p => p.OrganizationClientsAdditionalFields).HasConstraintName("organization_clients_additional_fields_fk_1");
        });

        modelBuilder.Entity<OrganizationField>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_fields_pk");

            entity.Property(e => e.FieldSelectValues).HasComment("варианты для выбора чз - ;");
            entity.Property(e => e.FieldType).HasComment("int\r\nstring\r\nmoney\r\nselected\r\ndatetime");
            entity.Property(e => e.Filterbyfield).HasDefaultValueSql("false");
            entity.Property(e => e.Isdeleted).HasDefaultValueSql("0");

            entity.HasOne(d => d.OrganizationNavigation).WithMany(p => p.OrganizationFields).HasConstraintName("organization_fields_fk");
        });

        modelBuilder.Entity<OrganizationService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_services_pk");

            entity.Property(e => e.FixedSum)
                .HasDefaultValueSql("0")
                .HasComment("если 1 то услуга с фиксированной суммой");
            entity.Property(e => e.Isdeleted).HasDefaultValueSql("0");
            entity.Property(e => e.MaxSumm).HasComment("макс сумма в тыйынах");
            entity.Property(e => e.MinSumm).HasComment("мин сумма в тыйынах");
            entity.Property(e => e.ServiceSumm).HasComment("если fixed_sum = 1, то тут будет значение фиксированной суммы");

            entity.HasOne(d => d.OrganizationNavigation).WithMany(p => p.OrganizationServices).HasConstraintName("organization_services_fk");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pk");

            entity.Property(e => e.AvilableServices).HasComment("перечисляется id сервисов чз ;");
            entity.Property(e => e.Isdeleted).HasDefaultValueSql("0");
            entity.Property(e => e.Rights).HasComment("права пользователей");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("transactions_pk");

            entity.Property(e => e.Summ).HasComment("сумма в тыйынах");
            entity.Property(e => e.TransactionStatus).HasComment("success\r\nerror");
            entity.Property(e => e.TransactionSumm).HasComment("сумма транзакции в тыйынах вмесе с комиссией");
            entity.Property(e => e.TransactionType).HasComment("debit - приход\r\ncredit - расход");

            entity.HasOne(d => d.InvoiceNavigation).WithMany(p => p.Transactions).HasConstraintName("transactions_fk");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pk");

            entity.Property(e => e.Isdeleted).HasDefaultValueSql("0");
            entity.Property(e => e.Role).HasComment("user\r\nadmin\r\nsuperadmin");

            entity.HasOne(d => d.OrganizationNavigation).WithMany(p => p.Users).HasConstraintName("users_fk");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_roles_pk");

            entity.Property(e => e.Isdeleted).HasDefaultValueSql("0");

            entity.HasOne(d => d.RoleNavigation).WithMany(p => p.UserRoles).HasConstraintName("user_roles_fk_1");

            entity.HasOne(d => d.UserNavigation).WithMany(p => p.UserRoles).HasConstraintName("user_roles_fk");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
