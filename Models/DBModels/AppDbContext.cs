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

    public virtual DbSet<Appointment> Appointments { get; set; }

    public virtual DbSet<AppointmentService> AppointmentServices { get; set; }

    public virtual DbSet<AppointmentMedicalTemplate> AppointmentMedicalTemplates { get; set; }

    public virtual DbSet<UserWorkSchedule> UserWorkSchedules { get; set; }

    public virtual DbSet<UserWorkScheduleOverride> UserWorkScheduleOverrides { get; set; }

    public virtual DbSet<AppointmentSetting> AppointmentSettings { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Specialization> Specializations { get; set; }

    public virtual DbSet<ServiceSpecialization> ServiceSpecializations { get; set; }

    public virtual DbSet<UserDepartment> UserDepartments { get; set; }

    public virtual DbSet<UserSpecialization> UserSpecializations { get; set; }

    public virtual DbSet<InvoicePayment> InvoicePayments { get; set; }

    public virtual DbSet<InvoiceQr> InvoiceQrs { get; set; }

    public virtual DbSet<InvoiceService> InvoiceServices { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<OrganizationClient> OrganizationClients { get; set; }

    public virtual DbSet<OrgClientGroup> OrgClientGroups { get; set; }

    public virtual DbSet<OrganizationClientsAdditionalField> OrganizationClientsAdditionalFields { get; set; }

    public virtual DbSet<OrganizationField> OrganizationFields { get; set; }

    public virtual DbSet<OrganizationService> OrganizationServices { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RolePermission> RolePermissions { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<Agent> Agents { get; set; }
    public virtual DbSet<OrganizationSettings> OrganizationSettings { get; set; }
    public virtual DbSet<Commission> Commissions { get; set; }
    public virtual DbSet<CommissionTier> CommissionTiers { get; set; }
    public virtual DbSet<AgentCommission> AgentCommissions { get; set; }
    public virtual DbSet<OrganizationSubscription> OrganizationSubscriptions { get; set; }
    public virtual DbSet<OrganizationSubscriptionPayment> OrganizationSubscriptionPayments { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Only for design-time / scaffolding. Runtime uses DI + ConnectionStrings from config/env.
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=appdb;Username=appuser;Password=883448");
        }
    }

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
            entity.Property(e => e.DateEndInvoice)
                .HasComment("Дата окончания инвойса")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_end_invoice");
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
            entity.Property(e => e.QrMode)
                .HasColumnType("character varying")
                .HasColumnName("qr_mode");
            entity.Property(e => e.FromAppointments)
                .HasDefaultValue(false)
                .HasColumnName("from_appointments");
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

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("appointments_pk");

            entity.ToTable("appointments");

            entity.HasIndex(e => e.OrganizationId, "idx_appointments_organization_id");
            entity.HasIndex(e => e.PatientId, "idx_appointments_patient_id");
            entity.HasIndex(e => e.DoctorId, "idx_appointments_doctor_id");
            entity.HasIndex(e => e.StartsAt, "idx_appointments_starts_at");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.OrganizationId)
                .HasColumnType("character varying")
                .HasColumnName("organization_id");
            entity.Property(e => e.PatientId)
                .HasColumnType("character varying")
                .HasColumnName("patient_id");
            entity.Property(e => e.DoctorId)
                .HasColumnType("character varying")
                .HasColumnName("doctor_id");
            entity.Property(e => e.Title)
                .HasColumnType("character varying")
                .HasColumnName("title");
            entity.Property(e => e.Phone)
                .HasColumnType("character varying")
                .HasColumnName("phone");
            entity.Property(e => e.Email)
                .HasColumnType("character varying")
                .HasColumnName("email");
            entity.Property(e => e.Notes)
                .HasColumnType("text")
                .HasColumnName("notes");
            entity.Property(e => e.ReferralSource)
                .HasColumnType("character varying")
                .HasColumnName("referral_source");
            entity.Property(e => e.PaymentType)
                .HasColumnType("character varying")
                .HasColumnName("payment_type");
            entity.Property(e => e.AppointmentStatus)
                .HasColumnType("character varying")
                .HasColumnName("appointment_status");
            entity.Property(e => e.IsActive)
                .HasDefaultValueSql("true")
                .HasColumnName("is_active");
            entity.Property(e => e.StartsAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("starts_at");
            entity.Property(e => e.EndsAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ends_at");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.CreatedBy)
                .HasColumnType("character varying")
                .HasColumnName("created_by");

            entity.HasOne(d => d.Organization)
                .WithMany(p => p.Appointments)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("appointments_fk_organization");

            entity.HasOne(d => d.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("appointments_fk_patient");

            entity.HasOne(d => d.Doctor)
                .WithMany(p => p.Appointments)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("appointments_fk_doctor");
        });

        modelBuilder.Entity<InvoiceQr>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoice_qr_pk");

            entity.ToTable("invoice_qr");

            entity.HasIndex(e => e.InvoiceId, "idx_invoice_qr_invoice_id");

            entity.HasIndex(e => e.PayCode, "idx_invoice_qr_pay_code");

            entity.HasIndex(e => e.Transaction, "idx_invoice_qr_transaction");

            entity.HasIndex(e => e.PayCode, "ux_invoice_qr_pay_code")
                .IsUnique()
                .HasFilter("(pay_code IS NOT NULL AND pay_code <> '')");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.InvoiceId)
                .HasColumnType("character varying")
                .HasColumnName("invoice_id");
            entity.Property(e => e.PayCode)
                .HasColumnType("character varying")
                .HasColumnName("pay_code");
            entity.Property(e => e.Transaction)
                .HasColumnType("character varying")
                .HasColumnName("transaction");
            entity.Property(e => e.Status)
                .HasColumnType("character varying")
                .HasColumnName("status");
            entity.Property(e => e.QrLink)
                .HasColumnType("text")
                .HasColumnName("qr_link");
            entity.Property(e => e.QrCodeBase64)
                .HasColumnType("text")
                .HasColumnName("qr_code_base64");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.DisabledAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("disabled_at");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceQrs)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("invoice_qr_fk_invoice");
        });

        modelBuilder.Entity<AppointmentService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("appointment_services_pk");

            entity.ToTable("appointment_services");

            entity.HasIndex(e => e.AppointmentId, "idx_appointment_services_appointment_id");
            entity.HasIndex(e => e.OrganizationServiceId, "idx_appointment_services_org_service_id");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.AppointmentId)
                .HasColumnType("character varying")
                .HasColumnName("appointment_id");
            entity.Property(e => e.OrganizationServiceId)
                .HasColumnType("character varying")
                .HasColumnName("organization_service_id");
            entity.Property(e => e.ServiceName)
                .HasColumnType("character varying")
                .HasColumnName("service_name");
            entity.Property(e => e.PriceTyiyn)
                .HasColumnType("numeric(18,2)")
                .HasColumnName("price_tyiyn");
            entity.Property(e => e.Quantity)
                .HasDefaultValueSql("1")
                .HasColumnName("quantity");

            entity.HasOne(d => d.Appointment)
                .WithMany(p => p.AppointmentServices)
                .HasForeignKey(d => d.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("appointment_services_fk_appointment");

            entity.HasOne(d => d.OrganizationService)
                .WithMany(p => p.AppointmentServices)
                .HasForeignKey(d => d.OrganizationServiceId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("appointment_services_fk_org_service");
        });

        modelBuilder.Entity<AppointmentMedicalTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("appointment_medical_templates_pk");

            entity.ToTable("appointment_medical_templates");

            entity.HasIndex(e => e.OrganizationId, "idx_appointment_medical_templates_org_id");
            entity.HasIndex(e => e.TemplateType, "idx_appointment_medical_templates_type");
            entity.HasIndex(e => e.IsActive, "idx_appointment_medical_templates_active");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.OrganizationId)
                .HasColumnType("character varying")
                .HasColumnName("organization_id");
            entity.Property(e => e.TemplateType)
                .HasColumnType("character varying")
                .HasColumnName("template_type");
            entity.Property(e => e.Title)
                .HasColumnType("character varying")
                .HasColumnName("title");
            entity.Property(e => e.Content)
                .HasColumnType("text")
                .HasColumnName("content");
            entity.Property(e => e.SortOrder)
                .HasDefaultValueSql("0")
                .HasColumnName("sort_order");
            entity.Property(e => e.IsActive)
                .HasDefaultValueSql("true")
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<UserWorkSchedule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_work_schedules_pk");

            entity.ToTable("user_work_schedules");

            entity.HasIndex(e => e.OrganizationId, "idx_user_work_schedules_organization_id");
            entity.HasIndex(e => e.UserId, "idx_user_work_schedules_user_id");
            entity.HasIndex(e => new { e.UserId, e.DayOfWeek }, "ux_user_work_schedules_user_day").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.OrganizationId)
                .HasColumnType("character varying")
                .HasColumnName("organization_id");
            entity.Property(e => e.UserId)
                .HasColumnType("character varying")
                .HasColumnName("user_id");
            entity.Property(e => e.DayOfWeek)
                .HasColumnName("day_of_week");
            entity.Property(e => e.StartTime)
                .HasColumnType("time without time zone")
                .HasColumnName("start_time");
            entity.Property(e => e.EndTime)
                .HasColumnType("time without time zone")
                .HasColumnName("end_time");
            entity.Property(e => e.IsActive)
                .HasDefaultValueSql("true")
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Organization)
                .WithMany(p => p.UserWorkSchedules)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_work_schedules_fk_organization");

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserWorkSchedules)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_work_schedules_fk_user");
        });

        modelBuilder.Entity<UserWorkScheduleOverride>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_work_schedule_overrides_pk");

            entity.ToTable("user_work_schedule_overrides");

            entity.HasIndex(e => e.OrganizationId, "idx_user_work_schedule_overrides_organization_id");
            entity.HasIndex(e => e.UserId, "idx_user_work_schedule_overrides_user_id");
            entity.HasIndex(e => new { e.UserId, e.WorkDate }, "ux_user_work_schedule_overrides_user_date").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.OrganizationId)
                .HasColumnType("character varying")
                .HasColumnName("organization_id");
            entity.Property(e => e.UserId)
                .HasColumnType("character varying")
                .HasColumnName("user_id");
            entity.Property(e => e.WorkDate)
                .HasColumnType("date")
                .HasColumnName("work_date");
            entity.Property(e => e.IsWorking)
                .HasDefaultValueSql("true")
                .HasColumnName("is_working");
            entity.Property(e => e.StartTime)
                .HasColumnType("time without time zone")
                .HasColumnName("start_time");
            entity.Property(e => e.EndTime)
                .HasColumnType("time without time zone")
                .HasColumnName("end_time");
            entity.Property(e => e.Comment)
                .HasColumnType("character varying")
                .HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Organization)
                .WithMany(p => p.UserWorkScheduleOverrides)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_work_schedule_overrides_fk_organization");

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserWorkScheduleOverrides)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_work_schedule_overrides_fk_user");
        });

        modelBuilder.Entity<AppointmentSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("appointment_settings_pk");

            entity.ToTable("appointment_settings");

            entity.HasIndex(e => e.OrganizationId, "idx_appointment_settings_org_id");
            entity.HasIndex(e => e.UserId, "idx_appointment_settings_user_id");
            entity.HasIndex(e => new { e.OrganizationId, e.UserId }, "ux_appointment_settings_org_user").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.OrganizationId)
                .HasColumnType("character varying")
                .HasColumnName("organization_id");
            entity.Property(e => e.UserId)
                .HasColumnType("character varying")
                .HasColumnName("user_id");
            entity.Property(e => e.AppointmentDurationMinutes)
                .HasDefaultValueSql("30")
                .HasColumnName("appointment_duration_minutes");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Organization)
                .WithMany(p => p.AppointmentSettings)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("appointment_settings_fk_organization");

            entity.HasOne(d => d.User)
                .WithMany(p => p.AppointmentSettings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("appointment_settings_fk_user");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("departments_pk");

            entity.ToTable("departments");

            entity.HasIndex(e => e.OrganizationId, "idx_departments_organization_id");
            entity.HasIndex(e => new { e.OrganizationId, e.Name }, "ux_departments_org_name").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.OrganizationId)
                .HasColumnType("character varying")
                .HasColumnName("organization_id");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
            entity.Property(e => e.Code)
                .HasColumnType("character varying")
                .HasColumnName("code");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValueSql("true")
                .HasColumnName("is_active");
            entity.Property(e => e.SortOrder)
                .HasDefaultValueSql("0")
                .HasColumnName("sort_order");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Organization)
                .WithMany(p => p.Departments)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("departments_fk_organization");
        });

        modelBuilder.Entity<Specialization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("specializations_pk");

            entity.ToTable("specializations");

            entity.HasIndex(e => e.OrganizationId, "idx_specializations_organization_id");
            entity.HasIndex(e => e.DepartmentId, "idx_specializations_department_id");
            entity.HasIndex(e => new { e.OrganizationId, e.Name }, "ux_specializations_org_name").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.OrganizationId)
                .HasColumnType("character varying")
                .HasColumnName("organization_id");
            entity.Property(e => e.DepartmentId)
                .HasColumnType("character varying")
                .HasColumnName("department_id");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValueSql("true")
                .HasColumnName("is_active");
            entity.Property(e => e.SortOrder)
                .HasDefaultValueSql("0")
                .HasColumnName("sort_order");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Organization)
                .WithMany(p => p.Specializations)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("specializations_fk_organization");

            entity.HasOne(d => d.Department)
                .WithMany(p => p.Specializations)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("specializations_fk_department");
        });

        modelBuilder.Entity<UserDepartment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_departments_pk");

            entity.ToTable("user_departments");

            entity.HasIndex(e => e.UserId, "idx_user_departments_user_id");
            entity.HasIndex(e => e.DepartmentId, "idx_user_departments_department_id");
            entity.HasIndex(e => new { e.UserId, e.DepartmentId }, "ux_user_departments_user_department").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.UserId)
                .HasColumnType("character varying")
                .HasColumnName("user_id");
            entity.Property(e => e.DepartmentId)
                .HasColumnType("character varying")
                .HasColumnName("department_id");
            entity.Property(e => e.IsPrimary)
                .HasDefaultValueSql("false")
                .HasColumnName("is_primary");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserDepartments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_departments_fk_user");

            entity.HasOne(d => d.Department)
                .WithMany(p => p.UserDepartments)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_departments_fk_department");
        });

        modelBuilder.Entity<UserSpecialization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_specializations_pk");

            entity.ToTable("user_specializations");

            entity.HasIndex(e => e.UserId, "idx_user_specializations_user_id");
            entity.HasIndex(e => e.SpecializationId, "idx_user_specializations_specialization_id");
            entity.HasIndex(e => new { e.UserId, e.SpecializationId }, "ux_user_specializations_user_specialization").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.UserId)
                .HasColumnType("character varying")
                .HasColumnName("user_id");
            entity.Property(e => e.SpecializationId)
                .HasColumnType("character varying")
                .HasColumnName("specialization_id");
            entity.Property(e => e.IsPrimary)
                .HasDefaultValueSql("false")
                .HasColumnName("is_primary");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserSpecializations)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_specializations_fk_user");

            entity.HasOne(d => d.Specialization)
                .WithMany(p => p.UserSpecializations)
                .HasForeignKey(d => d.SpecializationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_specializations_fk_specialization");
        });

        modelBuilder.Entity<ServiceSpecialization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("service_specializations_pk");

            entity.ToTable("service_specializations");

            entity.HasIndex(e => e.OrganizationServiceId, "idx_service_specializations_service_id");
            entity.HasIndex(e => e.SpecializationId, "idx_service_specializations_specialization_id");
            entity.HasIndex(e => new { e.OrganizationServiceId, e.SpecializationId }, "ux_service_specializations_service_specialization").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.OrganizationServiceId)
                .HasColumnType("character varying")
                .HasColumnName("organization_service_id");
            entity.Property(e => e.SpecializationId)
                .HasColumnType("character varying")
                .HasColumnName("specialization_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasOne(d => d.OrganizationService)
                .WithMany(p => p.ServiceSpecializations)
                .HasForeignKey(d => d.OrganizationServiceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("service_specializations_fk_service");

            entity.HasOne(d => d.Specialization)
                .WithMany(p => p.ServiceSpecializations)
                .HasForeignKey(d => d.SpecializationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("service_specializations_fk_specialization");
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
                .HasComment("сумма оплаты (тыйыны)")
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
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
            entity.Property(e => e.Organizationtype)
                .HasComment("standart\r\ndetsad\r\nmedclinic")
                .HasColumnType("character varying")
                .HasColumnName("organizationtype");
            entity.Property(e => e.IsActive)
                .HasComment("false — доступ заблокирован (истёк период подписки)")
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.HasOne(e => e.Settings)
                .WithOne(s => s.Organization)
                .HasForeignKey<OrganizationSettings>(s => s.OrganizationId)
                .HasConstraintName("organization_settings_organization_fk");
        });

        modelBuilder.Entity<OrganizationSettings>(entity =>
        {
            entity.HasKey(e => e.OrganizationId).HasName("organization_settings_pk");
            entity.ToTable("organization_settings");

            entity.Property(e => e.OrganizationId)
                .HasColumnType("character varying")
                .HasColumnName("organization_id");
            entity.Property(e => e.DisableInvoiceServiceSelection)
                .HasComment("отключить выбор услуги при создании счёта; ввод названия и цены вручную")
                .HasColumnName("disable_invoice_service_selection");
            entity.Property(e => e.AllowedHassameaccount)
                .HasComment("если true - организации могут создавать счета с одинаковыми л/с")
                .HasColumnName("allowed_hassameaccount");
            entity.Property(e => e.InvoicePayCodeMode)
                .HasComment("new_only | duplicate_only | both — режим выбора л/с при создании счёта")
                .HasColumnType("character varying(32)")
                .HasColumnName("invoice_pay_code_mode");
            entity.Property(e => e.Paymentreminderdaysbefore)
                .HasComment("за сколько дней до срока начинать напоминания по оплате")
                .HasColumnName("paymentreminderdaysbefore");
            entity.Property(e => e.Email)
                .HasColumnType("character varying")
                .HasColumnName("email");
            entity.Property(e => e.WhatsappPhone)
                .HasColumnType("character varying")
                .HasColumnName("whatsapp_phone");
            entity.Property(e => e.ContactPhone)
                .HasColumnType("character varying")
                .HasColumnName("contact_phone");
            entity.Property(e => e.DirectorFullName)
                .HasColumnType("character varying")
                .HasColumnName("director_full_name");
            entity.Property(e => e.Address)
                .HasColumnType("character varying")
                .HasColumnName("address");
            entity.Property(e => e.LogoPath)
                .HasColumnType("character varying")
                .HasColumnName("logo_path");
            entity.Property(e => e.BillingType)
                .HasComment("subscription или комбинация комиссий через флаги")
                .HasColumnType("character varying")
                .HasColumnName("billing_type");
            entity.Property(e => e.DefaultQrMode)
                .HasColumnType("character varying")
                .HasColumnName("default_qr_mode");
            entity.Property(e => e.UseLowerCommissionFromOrg)
                .HasColumnName("use_lower_commission_from_org");
            entity.Property(e => e.CommissionId)
                .HasColumnType("character varying")
                .HasColumnName("commission_id");
            entity.Property(e => e.UseUpperCommissionFromAgent)
                .HasColumnName("use_upper_commission_from_agent");
            entity.Property(e => e.UseLowerCommissionToAgent)
                .HasColumnName("use_lower_commission_to_agent");

            entity.HasOne(d => d.Commission).WithMany()
                .HasForeignKey(d => d.CommissionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("organization_settings_commission_fk");
        });

        modelBuilder.Entity<OrganizationSubscription>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_subscription_pk");
            entity.ToTable("organization_subscription");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.OrganizationId)
                .HasColumnType("character varying")
                .HasColumnName("organization_id");
            entity.Property(e => e.PriceTyiyn)
                .HasColumnType("numeric(18,2)")
                .HasColumnName("price_tyiyn");
            entity.Property(e => e.PeriodType)
                .HasColumnType("character varying")
                .HasColumnName("period_type");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Organization).WithOne(p => p.Subscription)
                .HasForeignKey<OrganizationSubscription>(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("organization_subscription_organization_fk");
        });

        modelBuilder.Entity<OrganizationSubscriptionPayment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_subscription_payment_pk");
            entity.ToTable("organization_subscription_payment");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.OrganizationId)
                .HasColumnType("character varying")
                .HasColumnName("organization_id");
            entity.Property(e => e.PaidAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("paid_at");
            entity.Property(e => e.AmountTyiyn)
                .HasColumnType("numeric(18,2)")
                .HasColumnName("amount_tyiyn");
            entity.Property(e => e.PeriodStart)
                .HasColumnType("date")
                .HasColumnName("period_start");
            entity.Property(e => e.PeriodEnd)
                .HasColumnType("date")
                .HasColumnName("period_end");
            entity.Property(e => e.Note)
                .HasColumnType("character varying")
                .HasColumnName("note");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Organization).WithMany(p => p.SubscriptionPayments)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("organization_subscription_payment_organization_fk");
        });

        modelBuilder.Entity<Commission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("commission_pk");
            entity.ToTable("commission");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
            entity.Property(e => e.CommissionKind)
                .HasComment("percent | fixed | mixed | single_tier | progressive")
                .HasColumnType("character varying")
                .HasColumnName("commission_kind");
            entity.Property(e => e.Rate)
                .HasColumnType("numeric(18,6)")
                .HasColumnName("rate");
            entity.Property(e => e.FixedAmount)
                .HasColumnType("numeric(18,2)")
                .HasColumnName("fixed_amount");
            entity.Property(e => e.MinFee)
                .HasColumnType("numeric(18,2)")
                .HasColumnName("min_fee");
            entity.Property(e => e.MaxFee)
                .HasColumnType("numeric(18,2)")
                .HasColumnName("max_fee");

            entity.HasMany(e => e.Tiers).WithOne(t => t.Commission)
                .HasForeignKey(t => t.CommissionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("commission_tier_commission_fk");
        });

        modelBuilder.Entity<CommissionTier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("commission_tier_pk");
            entity.ToTable("commission_tier");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.CommissionId)
                .HasColumnType("character varying")
                .HasColumnName("commission_id");
            entity.Property(e => e.AmountFrom)
                .HasColumnType("numeric(18,2)")
                .HasColumnName("amount_from");
            entity.Property(e => e.AmountTo)
                .HasColumnType("numeric(18,2)")
                .HasColumnName("amount_to");
            entity.Property(e => e.Rate)
                .HasColumnType("numeric(18,6)")
                .HasColumnName("rate");
            entity.Property(e => e.SortOrder)
                .HasColumnName("sort_order");
        });

        modelBuilder.Entity<AgentCommission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("agent_commission_pk");
            entity.ToTable("agent_commission");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.AgentId)
                .HasColumnType("character varying")
                .HasColumnName("agent_id");
            entity.Property(e => e.OrganizationId)
                .HasColumnType("character varying")
                .HasColumnName("organization_id");
            entity.Property(e => e.CommissionId)
                .HasColumnType("character varying")
                .HasColumnName("commission_id");
            entity.Property(e => e.LowerCommissionId)
                .HasColumnType("character varying")
                .HasColumnName("lower_commission_id");

            entity.HasOne(d => d.Agent).WithMany(p => p.AgentCommissions)
                .HasForeignKey(d => d.AgentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("agent_commission_agent_fk");
            entity.HasOne(d => d.Organization).WithMany(p => p.AgentCommissions)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("agent_commission_organization_fk");
            entity.HasOne(d => d.Commission).WithMany(p => p.AgentCommissions)
                .HasForeignKey(d => d.CommissionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("agent_commission_commission_fk");
            entity.HasOne(d => d.LowerCommission).WithMany()
                .HasForeignKey(d => d.LowerCommissionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("agent_commission_lower_commission_fk");

            entity.HasIndex(e => new { e.AgentId, e.OrganizationId })
                .IsUnique()
                .HasDatabaseName("agent_commission_agent_organization_uq");
        });

        modelBuilder.Entity<OrgClientGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("org_client_groups_pk");

            entity.ToTable("org_client_groups");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
            entity.Property(e => e.ParentGroupId)
                .HasColumnType("character varying")
                .HasColumnName("parent_group_id");
            entity.Property(e => e.OrganizationId)
                .HasColumnType("character varying")
                .HasColumnName("organization_id");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(0)
                .HasColumnName("is_deleted");
            entity.Property(e => e.Logo)
                .HasColumnType("character varying")
                .HasColumnName("logo");
            entity.Property(e => e.CreatedDate)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_date");

            entity.HasOne(d => d.ParentGroup).WithMany(p => p.ChildGroups)
                .HasForeignKey(d => d.ParentGroupId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("org_client_groups_parent_fk");

            entity.HasOne(d => d.Organization).WithMany(p => p.OrgClientGroups)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("org_client_groups_organization_fk");
        });

        modelBuilder.Entity<OrganizationClient>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_cients_pk");

            entity.ToTable("organization_clients");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.ClientAddress)
                .HasColumnType("character varying")
                .HasColumnName("client_address");
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
            entity.Property(e => e.ClientType)
                .HasComment("fiz\\jur")
                .HasColumnType("character varying")
                .HasColumnName("client_type");
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
            entity.Property(e => e.OrgClientGroupId)
                .HasColumnType("character varying")
                .HasColumnName("org_client_group_id");

            entity.HasOne(d => d.OrganizationNavigation).WithMany(p => p.OrganizationClients)
                .HasForeignKey(d => d.Organization)
                .HasConstraintName("organization_clients_fk");

            entity.HasOne(d => d.UserCreaterNavigation).WithMany(p => p.OrganizationClients)
                .HasForeignKey(d => d.UserCreater)
                .HasConstraintName("organization_clients_fk2");

            entity.HasOne(d => d.OrgClientGroup).WithMany(p => p.OrganizationClients)
                .HasForeignKey(d => d.OrgClientGroupId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("organization_clients_org_client_group_fk");
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
            entity.Property(e => e.Agent)
                .HasColumnType("character varying")
                .HasColumnName("agent");
            entity.Property(e => e.PaymentInvoice)
                .HasColumnType("character varying")
                .HasColumnName("payment_invoice");
            entity.Property(e => e.TxnId)
                .HasColumnType("character varying")
                .HasColumnName("txn_id");
            entity.Property(e => e.TransactionSystem)
                .HasColumnType("character varying")
                .HasColumnName("transaction_system");
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
            entity.Property(e => e.LowerCommissionFromOrg)
                .HasComment("нижняя комиссия от организации (тыйыны)")
                .HasColumnType("numeric(18,2)")
                .HasColumnName("lower_commission_from_org");
            entity.Property(e => e.UpperCommissionFromAgent)
                .HasComment("верхняя комиссия от агента (тыйыны)")
                .HasColumnType("numeric(18,2)")
                .HasColumnName("upper_commission_from_agent");
            entity.Property(e => e.LowerCommissionToAgent)
                .HasComment("нижняя комиссия к агенту (тыйыны)")
                .HasColumnType("numeric(18,2)")
                .HasColumnName("lower_commission_to_agent");
            entity.Property(e => e.ParentTransaction)
                .HasComment("исходная транзакция при возврате (credit)")
                .HasColumnType("character varying")
                .HasColumnName("parent_transaction");

            entity.HasOne(d => d.InvoiceNavigation).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.Invoice)
                .HasConstraintName("transactions_fk");

            entity.HasOne(d => d.AgentNavigation).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.Agent)
                .HasConstraintName("transactions_fk_agent");

            entity.HasOne(d => d.ParentTransactionNavigation).WithMany(p => p.InverseParentTransaction)
                .HasForeignKey(d => d.ParentTransaction)
                .HasConstraintName("transactions_fk_parent");
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
            entity.Property(e => e.StartPage)
                .HasColumnType("character varying")
                .HasColumnName("start_page");
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
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notifications_pk");

            entity.Property(e => e.Status).HasDefaultValueSql("'new'").HasComment("new - новое, ожидает обработки\r\nprocessing - в процессе обработки\r\nsent - отправлено\r\nfailed - ошибка отправки");
            entity.Property(e => e.RetryCount).HasDefaultValueSql("0");
            entity.Property(e => e.Channel).HasComment("email, telegram, whatsapp");
            entity.Property(e => e.ContactInfo).HasComment("email, телефон, telegram chat_id");

            entity.HasOne(d => d.ClientNavigation).WithMany(p => p.Notifications).HasConstraintName("notifications_fk");
        });

        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("agent_pk");

            entity.ToTable("agent");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.ApiLogin)
                .HasColumnType("character varying")
                .HasColumnName("api_login");
            entity.Property(e => e.ApiPassword)
                .HasColumnType("character varying")
                .HasColumnName("api_psw");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");

            entity.Property(e => e.AllowlistIp)
                .HasColumnType("character varying")
                .HasColumnName("allowlistip");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
