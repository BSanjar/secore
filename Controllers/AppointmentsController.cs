using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Services;
using WebApplication1.Services.Cabinets;
using WebApplication1.ViewModels.Appointments;

namespace WebApplication1.Controllers;

[RequireAuth]
public class AppointmentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly OperationsByInvoices _operationsByInvoices;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly NotificationService _notificationService;
    private readonly NotificationRecipientResolver _recipientResolver;

    public AppointmentsController(
        AppDbContext db,
        OperationsByInvoices operationsByInvoices,
        ICurrentTenantService currentTenantService,
        NotificationService notificationService,
        NotificationRecipientResolver recipientResolver)
    {
        _db = db;
        _operationsByInvoices = operationsByInvoices;
        _currentTenantService = currentTenantService;
        _notificationService = notificationService;
        _recipientResolver = recipientResolver;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (PermissionHelper.HasPermission(HttpContext, "appointments.registry.view"))
            return RedirectToAction(nameof(Registry));

        if (PermissionHelper.HasPermission(HttpContext, "appointments.doctor.view"))
            return RedirectToAction(nameof(Doctor));

        if (PermissionHelper.HasPermission(HttpContext, "appointments.view"))
            return RedirectToAction(nameof(Registry));

        return RedirectToAction("AccessDenied", "Home", new { permissionCode = "appointments.registry.view | appointments.doctor.view" });
    }

    [HttpGet]
    [RequirePermission("appointments.doctor.view")]
    public async Task<IActionResult> Doctor()
    {
        var model = await BuildIndexViewModelAsync(limitToCurrentDoctor: true);
        if (model == null)
            return Unauthorized();

        ViewBag.IsDoctorRole = true;
        return View(model);
    }

    [HttpGet]
    [RequirePermission("appointments.registry.view")]
    public async Task<IActionResult> Registry()
    {
        var model = await BuildIndexViewModelAsync(limitToCurrentDoctor: false);
        if (model == null)
            return Unauthorized();

        ViewBag.IsDoctorRole = false;
        ViewData["Title"] = "Расписание записей";
        return View(model);
    }

    [HttpGet]
    [RequirePermission("appointments.edit")]
    public async Task<IActionResult> Templates()
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Appointments))
            return NotFound();

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return Unauthorized();

        var storageReady = await MedicalTemplatesStorageReadyAsync();
        var templates = storageReady
            ? await _db.AppointmentMedicalTemplates
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId)
                .OrderBy(x => x.TemplateType)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .Select(x => new AppointmentMedicalTemplateViewModel
                {
                    Id = x.Id,
                    Type = x.TemplateType,
                    Title = x.Title,
                    Content = x.Content,
                    SortOrder = x.SortOrder,
                    IsActive = x.IsActive
                })
                .ToListAsync()
            : new List<AppointmentMedicalTemplateViewModel>();

        return View(new AppointmentTemplatesIndexViewModel
        {
            StorageReady = storageReady,
            StorageMessage = storageReady
                ? null
                : "Таблица appointment_medical_templates еще не создана. Сначала примените SQL-скрипт шаблонов.",
            Templates = templates
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("appointments.edit")]
    public async Task<IActionResult> SaveTemplate(SaveAppointmentMedicalTemplateRequest request)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Appointments))
            return NotFound();

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return Unauthorized();

        if (!await MedicalTemplatesStorageReadyAsync())
        {
            TempData["Error"] = "Таблица шаблонов еще не создана.";
            return RedirectToAction(nameof(Templates));
        }

        var type = NormalizeTemplateType(request.Type);
        if (type == null)
        {
            TempData["Error"] = "Выберите корректный тип шаблона.";
            return RedirectToAction(nameof(Templates));
        }

        var title = request.Title?.Trim();
        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "Заполните название и текст шаблона.";
            return RedirectToAction(nameof(Templates));
        }

        var sortOrder = request.SortOrder < 0 ? 0 : request.SortOrder;
        if (sortOrder > 10000)
            sortOrder = 10000;

        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        var template = string.IsNullOrWhiteSpace(request.Id)
            ? null
            : await _db.AppointmentMedicalTemplates
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.OrganizationId == organizationId);

        if (template == null)
        {
            template = new AppointmentMedicalTemplate
            {
                Id = Guid.NewGuid().ToString(),
                OrganizationId = organizationId,
                CreatedAt = now
            };
            _db.AppointmentMedicalTemplates.Add(template);
        }

        template.TemplateType = type;
        template.Title = title;
        template.Content = content;
        template.SortOrder = sortOrder;
        template.IsActive = request.IsActive;
        template.UpdatedAt = now;

        await _db.SaveChangesAsync();
        TempData["Message"] = "Шаблон сохранен.";
        return RedirectToAction(nameof(Templates));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("appointments.edit")]
    public async Task<IActionResult> DeleteTemplate(DeleteAppointmentMedicalTemplateRequest request)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Appointments))
            return NotFound();

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            TempData["Error"] = "Не выбран шаблон для удаления.";
            return RedirectToAction(nameof(Templates));
        }

        var entity = await _db.AppointmentMedicalTemplates
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.OrganizationId == organizationId);

        if (entity == null)
        {
            TempData["Error"] = "Шаблон не найден.";
            return RedirectToAction(nameof(Templates));
        }

        _db.AppointmentMedicalTemplates.Remove(entity);
        await _db.SaveChangesAsync();
        TempData["Message"] = "Шаблон удален.";
        return RedirectToAction(nameof(Templates));
    }

    private async Task<AppointmentsIndexViewModel?> BuildIndexViewModelAsync(bool limitToCurrentDoctor)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Appointments))
            return null;

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return null;

        var currentUserId = AuthorizationHelper.GetUserId(HttpContext);
        var canManageAppointments =
            PermissionHelper.HasPermission(HttpContext, "appointments.edit") ||
            PermissionHelper.HasPermission(HttpContext, "appointments.manage");
        var canGenerateInvoice =
            PermissionHelper.HasPermission(HttpContext, "appointments.invoice") ||
            PermissionHelper.HasPermission(HttpContext, "invoices.create");
        var canMarkAsPaidManually =
            PermissionHelper.HasPermission(HttpContext, "appointments.payment.status") ||
            PermissionHelper.HasPermission(HttpContext, "appointments.invoice") ||
            PermissionHelper.HasPermission(HttpContext, "invoices.create") ||
            PermissionHelper.HasPermission(HttpContext, "appointments.manage");

        var patientsCount = await _db.OrganizationClients
            .AsNoTracking()
            .CountAsync(x => x.Organization == organizationId);

        var doctorsQuery = _db.Users
            .AsNoTracking()
            .Where(x => x.Organization == organizationId && (x.Isdeleted == null || x.Isdeleted == 0));

        if (limitToCurrentDoctor && !string.IsNullOrWhiteSpace(currentUserId))
            doctorsQuery = doctorsQuery.Where(x => x.Id == currentUserId);

        var doctorsCount = await doctorsQuery.CountAsync();

        var doctors = await doctorsQuery
            .OrderBy(x => x.Name)
            .Select(x => new SelectOptionViewModel
            {
                Value = x.Id,
                Label = x.Name ?? "Без имени"
            })
            .ToListAsync();

        var departments = await _db.Departments
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new SelectOptionViewModel
            {
                Value = x.Id,
                Label = x.Name
            })
            .ToListAsync();

        var doctorFilters = await doctorsQuery
            .Include(x => x.UserDepartments)
            .OrderBy(x => x.Name)
            .Select(x => new AppointmentDoctorFilterOptionViewModel
            {
                Value = x.Id,
                Label = x.Name ?? "Без имени",
                DepartmentIds = x.UserDepartments.Select(ud => ud.DepartmentId).ToList()
            })
            .ToListAsync();

        var patients = await _db.OrganizationClients
            .AsNoTracking()
            .Where(x => x.Organization == organizationId)
            .OrderBy(x => x.ClientName)
            .Select(x => new PatientLookupViewModel
            {
                Id = x.Id,
                Name = x.ClientName ?? "Без имени",
                Phone = x.ClientPhone,
                Email = x.ClientEmail,
                WhatsApp = x.ClientWa
            })
            .ToListAsync();

        var serviceCatalog = await _db.OrganizationServices
            .AsNoTracking()
            .Where(x => x.Organization == organizationId && x.Isdeleted != 1)
            .OrderBy(x => x.Name)
            .Select(x => new AppointmentServiceCatalogItemViewModel
            {
                Id = x.Id,
                Name = x.Name ?? "Без названия",
                PriceTyiyn = x.ServiceSumm ?? 0
            })
            .ToListAsync();

        var medicalTemplatesReady = await MedicalTemplatesStorageReadyAsync();
        var medicalTemplates = medicalTemplatesReady
            ? await _db.AppointmentMedicalTemplates
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.IsActive)
                .OrderBy(x => x.TemplateType)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .Select(x => new AppointmentMedicalTemplateViewModel
                {
                    Id = x.Id,
                    Type = x.TemplateType,
                    Title = x.Title,
                    Content = x.Content,
                    SortOrder = x.SortOrder,
                    IsActive = x.IsActive
                })
                .ToListAsync()
            : BuildFallbackMedicalTemplates();

        var storageReady = await AppointmentsStorageReadyAsync();
        var doctorSchedulesReady = await DoctorSchedulesStorageReadyAsync();
        var doctorScheduleOverridesReady = await DoctorScheduleOverridesStorageReadyAsync();
        var appointmentSettingsReady = await AppointmentSettingsStorageReadyAsync();
        IReadOnlyList<DoctorScheduleViewModel> doctorSchedules = doctorSchedulesReady
            ? await LoadDoctorSchedulesAsync(organizationId)
            : Array.Empty<DoctorScheduleViewModel>();
        IReadOnlyList<DoctorScheduleOverrideViewModel> doctorScheduleOverrides = doctorScheduleOverridesReady
            ? await LoadDoctorScheduleOverridesAsync(organizationId)
            : Array.Empty<DoctorScheduleOverrideViewModel>();
        IReadOnlyList<AppointmentDurationSettingViewModel> appointmentDurations = appointmentSettingsReady
            ? await LoadAppointmentDurationsAsync(organizationId)
            : Array.Empty<AppointmentDurationSettingViewModel>();

        var eventDoctorId = limitToCurrentDoctor ? currentUserId : null;
        var events = storageReady
            ? await LoadEventsWithDemographicsAsync(organizationId, eventDoctorId)
            : BuildFallbackEvents(doctors, patients);

        ViewBag.CanManageAppointments = canManageAppointments;
        ViewBag.CanGenerateInvoice = canGenerateInvoice;
        ViewBag.CanMarkAsPaidManually = canMarkAsPaidManually;

        return new AppointmentsIndexViewModel
        {
            PatientsCount = patientsCount,
            DoctorsCount = doctorsCount,
            Doctors = doctors,
            Departments = departments,
            DoctorFilters = doctorFilters,
            Patients = patients,
            Services = serviceCatalog
                .Select(x => new SelectOptionViewModel { Value = x.Id, Label = x.Name })
                .ToList(),
            ServiceCatalog = serviceCatalog,
            MedicalTemplates = medicalTemplates,
            DoctorSchedules = doctorSchedules,
            DoctorScheduleOverrides = doctorScheduleOverrides,
            AppointmentDurations = appointmentDurations,
            Events = events,
            StorageReady = storageReady,
            StorageMessage = storageReady
                ? null
                : "Таблицы appointments ещё не созданы. Пока календарь работает на демо-данных, а сохранение записей недоступно до применения SQL-скрипта.",
            DoctorSchedulesReady = doctorSchedulesReady,
            DoctorSchedulesMessage = doctorSchedulesReady
                ? null
                : "Таблица рабочих смен пользователей ещё не создана. Ограничение записи по рабочему времени станет доступно после применения SQL-скрипта."
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("appointments.edit")]
    public async Task<IActionResult> Save([FromBody] SaveAppointmentRequest request)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Appointments))
            return NotFound();

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return Unauthorized();

        var currentUserId = AuthorizationHelper.GetUserId(HttpContext);
        var isDoctorRole = AuthorizationHelper.IsDoctorRole(HttpContext);

        if (!await AppointmentsStorageReadyAsync())
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                success = false,
                message = "Сначала примените SQL-скрипт appointments."
            });
        }

        if (isDoctorRole && !string.IsNullOrWhiteSpace(currentUserId))
            request.DoctorId = currentUserId;

        var validation = ValidateRequest(request);
        if (validation != null)
        {
            return BadRequest(new
            {
                success = false,
                message = validation
            });
        }

        var date = DateTime.SpecifyKind(request.AppointmentDate!.Value.Date, DateTimeKind.Unspecified);
        var startsAt = date.Add(TimeSpan.Parse(request.StartTime!));
        var endsAt = date.Add(TimeSpan.Parse(request.EndTime!));

        if (endsAt <= startsAt)
        {
            return BadRequest(new
            {
                success = false,
                message = "Время окончания должно быть позже времени начала."
            });
        }

        var userId = currentUserId;
        var (patient, isNewPatient) = await ResolvePatientAsync(organizationId, userId, request);
        if (patient == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Не удалось определить пациента для записи."
            });
        }

        if (isNewPatient)
        {
            var genderError = ValidateNewPatientGender(request.PatientGender);
            if (genderError != null)
            {
                return BadRequest(new { success = false, message = genderError });
            }

            var birthError = ValidateNewPatientBirthDate(request.PatientBirthDate);
            if (birthError != null)
            {
                return BadRequest(new { success = false, message = birthError });
            }

            await _recipientResolver.SaveClientDemographicsAsync(
                organizationId,
                patient.Id,
                request.PatientGender!.Trim(),
                request.PatientBirthDate!.Trim(),
                CancellationToken.None);
        }

        User? doctor = null;
        if (!string.IsNullOrWhiteSpace(request.DoctorId))
        {
            doctor = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.DoctorId &&
                    x.Organization == organizationId &&
                    (x.Isdeleted == null || x.Isdeleted == 0));

            if (doctor == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Врач не найден в текущей организации."
                });
            }

            if (await DoctorSchedulesStorageReadyAsync())
            {
                var scheduleValidation = await ValidateDoctorScheduleWithOverridesAsync(organizationId, doctor.Id, startsAt, endsAt);
                if (scheduleValidation != null)
                    return BadRequest(new { success = false, message = scheduleValidation });
            }

            // string.Equals(..., StringComparison) is not translatable by EF Core — use ToLower().
            var overlapExists = await _db.Appointments
                .AsNoTracking()
                .AnyAsync(x =>
                    x.OrganizationId == organizationId &&
                    x.DoctorId == doctor.Id &&
                    x.Id != request.AppointmentId &&
                    x.IsActive &&
                    (x.AppointmentStatus == null ||
                     x.AppointmentStatus.ToLower() != "cancelled") &&
                    x.StartsAt < endsAt &&
                    startsAt < x.EndsAt);

            if (overlapExists)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "У выбранного врача уже есть запись на это время."
                });
            }
        }

        var validServiceIds = await _db.OrganizationServices
            .AsNoTracking()
            .Where(x => x.Organization == organizationId && x.Isdeleted != 1)
            .Select(x => x.Id)
            .ToListAsync();

        var allowedServiceIds = new HashSet<string>(validServiceIds);
        foreach (var service in request.Services.Where(x => !string.IsNullOrWhiteSpace(x.OrganizationServiceId)))
        {
            if (!allowedServiceIds.Contains(service.OrganizationServiceId!))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Одна из услуг не принадлежит текущей организации."
                });
            }
        }

        var isNewAppointment = string.IsNullOrWhiteSpace(request.AppointmentId);

        Appointment? appointment = null;
        string? linkedInvoiceId = null;
        DateTime? invoiceSlotStartsAt = null;
        DateTime? invoiceSlotEndsAt = null;

        if (!isNewAppointment)
        {
            appointment = await _db.Appointments
                .Include(x => x.AppointmentServices)
                .FirstOrDefaultAsync(x => x.Id == request.AppointmentId && x.OrganizationId == organizationId);

            if (appointment == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Запись не найдена."
                });
            }

            if (isDoctorRole && !string.IsNullOrWhiteSpace(currentUserId) && appointment.DoctorId != currentUserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "Недостаточно прав для изменения этой записи."
                });
            }

            if (!string.IsNullOrWhiteSpace(appointment.PatientId))
            {
                invoiceSlotStartsAt = appointment.StartsAt;
                invoiceSlotEndsAt = appointment.EndsAt;
                var linkedInvoice = await FindAppointmentInvoiceBySlotAsync(
                    organizationId,
                    appointment.PatientId,
                    invoiceSlotStartsAt.Value,
                    invoiceSlotEndsAt.Value);
                linkedInvoiceId = linkedInvoice?.Id;
            }
        }

        var pastSlotValidation = ValidateAppointmentSlotNotInPast(startsAt, endsAt, appointment, isNewAppointment);
        if (pastSlotValidation != null)
        {
            return BadRequest(new
            {
                success = false,
                message = pastSlotValidation
            });
        }

        try
        {
        if (appointment == null)
        {
            appointment = new Appointment
            {
                Id = Guid.NewGuid().ToString(),
                OrganizationId = organizationId,
                CreatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
                CreatedBy = userId
            };
            _db.Appointments.Add(appointment);
        }

        appointment.PatientId = patient.Id;
        appointment.DoctorId = doctor?.Id;
        appointment.Title = string.IsNullOrWhiteSpace(request.Title) ? patient.ClientName : request.Title.Trim();
        appointment.Phone = request.Phone?.Trim();
        appointment.Email = request.Email?.Trim();
        appointment.Notes = request.Comment?.Trim();
        appointment.ReferralSource = request.ReferralSource?.Trim();
        var normalizedPaymentType = NormalizePaymentType(request.PaymentType);
        if (!isNewAppointment && normalizedPaymentType == "unpaid")
        {
            var existingPaymentType = NormalizePaymentType(appointment.PaymentType);
            if (existingPaymentType != "unpaid")
                normalizedPaymentType = existingPaymentType;
        }
        if (isNewAppointment && !IsBookingPaymentType(normalizedPaymentType))
        {
            return BadRequest(new
            {
                success = false,
                message = "Выберите тип оплаты для новой записи."
            });
        }

        var canMarkAsPaidManually =
            PermissionHelper.HasPermission(HttpContext, "appointments.payment.status") ||
            PermissionHelper.HasPermission(HttpContext, "appointments.invoice") ||
            PermissionHelper.HasPermission(HttpContext, "invoices.create") ||
            PermissionHelper.HasPermission(HttpContext, "appointments.manage");
        if (IsPaidPaymentType(normalizedPaymentType) &&
            !IsBookingPaymentType(normalizedPaymentType) &&
            !canMarkAsPaidManually)
        {
            return BadRequest(new
            {
                success = false,
                message = "У вас нет прав вручную отмечать прием как оплаченный."
            });
        }

        appointment.PaymentType = normalizedPaymentType;
        appointment.AppointmentStatus = request.AppointmentStatus?.Trim();
        appointment.IsActive = request.IsActive;
        appointment.StartsAt = startsAt;
        appointment.EndsAt = endsAt;
        appointment.UpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

        _db.AppointmentServices.RemoveRange(appointment.AppointmentServices);
        appointment.AppointmentServices.Clear();

        foreach (var service in request.Services)
        {
            var line = new AppointmentService
            {
                Id = Guid.NewGuid().ToString(),
                AppointmentId = appointment.Id,
                OrganizationServiceId = string.IsNullOrWhiteSpace(service.OrganizationServiceId) ? null : service.OrganizationServiceId,
                ServiceName = service.ServiceName?.Trim(),
                PriceTyiyn = service.PriceTyiyn,
                Quantity = Math.Max(1, service.Quantity)
            };

            appointment.AppointmentServices.Add(line);
        }

        await _db.SaveChangesAsync();

        try
        {
            await _notificationService.CreateOrUpdateAppointmentReminderAsync(appointment, patient, doctor);
        }
        catch
        {
            // Напоминание не должно блокировать сохранение записи.
        }

        string? createdInvoiceId = null;
        string? createdInvoiceUrl = null;
        string? invoiceSyncWarning = null;

        if (isNewAppointment &&
            IsBookingPaymentType(normalizedPaymentType) &&
            appointment.AppointmentServices.Count > 0 &&
            !string.IsNullOrWhiteSpace(userId))
        {
            try
            {
                var invoiceResult = await CreateInvoiceForAppointmentAsync(
                    organizationId,
                    userId,
                    appointment,
                    patient,
                    doctor,
                    normalizedPaymentType);
                createdInvoiceId = invoiceResult.InvoiceId;
                createdInvoiceUrl = Url.Action("Details", "Invoices", new { id = invoiceResult.InvoiceId });
            }
            catch (InvalidOperationException ex)
            {
                invoiceSyncWarning = $"Запись сохранена, но счёт не создан: {ex.Message}";
            }
        }
        else if (!isNewAppointment &&
                 !string.IsNullOrWhiteSpace(linkedInvoiceId) &&
                 appointment.AppointmentServices.Count > 0 &&
                 invoiceSlotStartsAt.HasValue &&
                 invoiceSlotEndsAt.HasValue)
        {
            try
            {
                await _operationsByInvoices.SyncAppointmentInvoiceFromBookingAsync(
                    organizationId,
                    linkedInvoiceId,
                    appointment,
                    patient,
                    doctor,
                    normalizedPaymentType,
                    invoiceSlotStartsAt.Value,
                    invoiceSlotEndsAt.Value);
            }
            catch (InvalidOperationException ex)
            {
                invoiceSyncWarning = $"Запись сохранена, но счёт не обновлён: {ex.Message}";
            }
        }
        else if (!isNewAppointment &&
                 string.IsNullOrWhiteSpace(linkedInvoiceId) &&
                 IsBookingPaymentType(normalizedPaymentType) &&
                 appointment.AppointmentServices.Count > 0 &&
                 !string.IsNullOrWhiteSpace(userId))
        {
            try
            {
                var invoiceResult = await CreateInvoiceForAppointmentAsync(
                    organizationId,
                    userId,
                    appointment,
                    patient,
                    doctor,
                    normalizedPaymentType);
                createdInvoiceId = invoiceResult.InvoiceId;
                createdInvoiceUrl = Url.Action("Details", "Invoices", new { id = invoiceResult.InvoiceId });
            }
            catch (InvalidOperationException ex)
            {
                invoiceSyncWarning = $"Запись сохранена, но счёт не создан: {ex.Message}";
            }
        }

        var hasPaidInvoice = await AppointmentHasPaidInvoiceAsync(organizationId, appointment, normalizedPaymentType);

        var eventItem = new AppointmentCalendarEventViewModel
        {
            Id = appointment.Id,
            Title = appointment.Title ?? "Приём",
            DoctorId = appointment.DoctorId,
            Doctor = doctor?.Name ?? "Не назначен",
            PatientId = patient.Id,
            PatientName = patient.ClientName,
            Phone = appointment.Phone,
            Email = appointment.Email,
            Start = appointment.StartsAt,
            End = appointment.EndsAt,
            Status = appointment.IsActive ? "busy" : "available",
            Notes = appointment.Notes ?? string.Empty,
            ReferralSource = appointment.ReferralSource,
            PaymentType = hasPaidInvoice && !IsPaidPaymentType(normalizedPaymentType)
                ? "invoice_paid"
                : appointment.PaymentType,
            HasPaidInvoice = hasPaidInvoice,
            IsActive = appointment.IsActive,
            CreatedAt = appointment.CreatedAt,
            UpdatedAt = appointment.UpdatedAt,
            Services = appointment.AppointmentServices
                .OrderBy(x => x.ServiceName)
                .Select(x => new AppointmentServiceLineViewModel
                {
                    OrganizationServiceId = x.OrganizationServiceId,
                    Name = x.ServiceName ?? "Услуга",
                    PriceTyiyn = x.PriceTyiyn ?? 0,
                    Quantity = x.Quantity
                })
                .ToList()
        };
        await EnrichEventsWithPatientDemographicsAsync(organizationId, new List<AppointmentCalendarEventViewModel> { eventItem });

        return Json(new
        {
            success = true,
            message = invoiceSyncWarning
                ?? (createdInvoiceId != null ? "Запись и счёт созданы." : "Запись сохранена."),
            invoiceId = createdInvoiceId,
            invoiceUrl = createdInvoiceUrl,
            eventItem
        });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = ex.Message
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("appointments.invoice")]
    public async Task<IActionResult> GenerateInvoice([FromBody] GenerateAppointmentInvoiceRequest request)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Appointments))
            return NotFound();

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return Unauthorized();

        var currentUserId = AuthorizationHelper.GetUserId(HttpContext);
        var isDoctorRole = AuthorizationHelper.IsDoctorRole(HttpContext);

        if (string.IsNullOrWhiteSpace(request.AppointmentId))
        {
            return BadRequest(new
            {
                success = false,
                message = "Не выбрана запись для формирования счёта."
            });
        }

        var appointmentQuery = _db.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.AppointmentServices)
            .Where(x => x.Id == request.AppointmentId && x.OrganizationId == organizationId);

        if (isDoctorRole && !string.IsNullOrWhiteSpace(currentUserId))
            appointmentQuery = appointmentQuery.Where(x => x.DoctorId == currentUserId);

        var appointment = await appointmentQuery.FirstOrDefaultAsync();

        if (appointment == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Запись не найдена."
            });
        }

        if (string.IsNullOrWhiteSpace(appointment.PatientId))
        {
            return BadRequest(new
            {
                success = false,
                message = "Для записи не указан пациент. Сначала сохраните запись с пациентом."
            });
        }

        var serviceLines = appointment.AppointmentServices.ToList();
        if (serviceLines.Count == 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "У приёма нет услуг. Добавьте услуги перед формированием счёта."
            });
        }
        var userId = currentUserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Пользователь не авторизован."
            });
        }

        var doctorName = string.IsNullOrWhiteSpace(appointment.Doctor?.Name) ? "Врач" : appointment.Doctor!.Name!;
        var patientName = string.IsNullOrWhiteSpace(appointment.Patient?.ClientName) ? "Пациент" : appointment.Patient!.ClientName!;
        var titleDate = appointment.StartsAt.ToString("dd.MM.yyyy");
        var totalTyiyn = serviceLines.Sum(x => (x.PriceTyiyn ?? 0) * Math.Max(1, x.Quantity));

        var result = await _operationsByInvoices.CreateOneTimeInvoiceAsync(new CreateOneTimeInvoiceInput
        {
            OrganizationId = organizationId,
            UserId = userId,
            ClientId = appointment.PatientId!,
            FixedSumm = totalTyiyn,
            InvoiceName = $"Приём {patientName} • {doctorName} • {titleDate}",
            DateStartInvoice = appointment.StartsAt.Date,
            DateEndInvoice = appointment.StartsAt.Date,
            Balance = 0,
            Hassameaccount = false,
            FromAppointments = true,
            GenerateQrOnCreate = true,
            PaymentDateFrom = appointment.StartsAt,
            PaymentDateTo = appointment.EndsAt,
            PaymentPeriodValue = appointment.StartsAt.ToString("dd.MM.yyyy"),
            PaymentSumm = totalTyiyn,
            ServiceLines = serviceLines.Select(x => new CreateOneTimePaymentServiceLineInput
            {
                OrganizationServiceId = x.OrganizationServiceId,
                ServiceSumm = (x.PriceTyiyn ?? 0) * Math.Max(1, x.Quantity)
            }).ToList()
        });

        return Json(new
        {
            success = true,
            message = "Счёт на оплату сформирован.",
            invoiceId = result.InvoiceId,
            url = Url.Action("Details", "Invoices", new { id = result.InvoiceId })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("appointments.edit")]
    public async Task<IActionResult> Cancel([FromBody] CancelAppointmentRequest request)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Appointments))
            return NotFound();

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.AppointmentId))
            return BadRequest(new { success = false, message = "Не выбрана запись для отмены." });

        var currentUserId = AuthorizationHelper.GetUserId(HttpContext);
        var isDoctorRole = AuthorizationHelper.IsDoctorRole(HttpContext);

        var appointmentQuery = _db.Appointments
            .Include(x => x.Patient)
            .Where(x => x.OrganizationId == organizationId && x.Id == request.AppointmentId);

        if (isDoctorRole && !string.IsNullOrWhiteSpace(currentUserId))
            appointmentQuery = appointmentQuery.Where(x => x.DoctorId == currentUserId);

        var appointment = await appointmentQuery.FirstOrDefaultAsync();
        if (appointment == null)
            return NotFound(new { success = false, message = "Запись не найдена." });

        if (!appointment.IsActive || string.Equals(appointment.AppointmentStatus, "cancelled", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "Запись уже отменена." });

        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

        InvoicePayment? paidPayment = null;
        if (!string.IsNullOrWhiteSpace(appointment.PatientId))
        {
            var invoice = await _db.Invoices
                .Include(i => i.InvoicePayments)
                .Include(i => i.ClientNavigation)
                .Where(i => i.FromAppointments && i.Client == appointment.PatientId)
                .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId)
                .Where(i => i.InvoicePayments.Any(p =>
                    p.DateFrom == appointment.StartsAt &&
                    p.DateTo == appointment.EndsAt))
                .OrderByDescending(i => i.DateCreated)
                .FirstOrDefaultAsync();

            paidPayment = invoice?.InvoicePayments
                .FirstOrDefault(p =>
                    p.DateFrom == appointment.StartsAt &&
                    p.DateTo == appointment.EndsAt &&
                    string.Equals(p.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase));
        }

        var hasPaid = paidPayment != null ||
                      IsPaidPaymentType(appointment.PaymentType) ||
                      await AppointmentHasPaidInvoiceAsync(organizationId, appointment, appointment.PaymentType);

        if (hasPaid && request.RefundPayment)
        {
            if (paidPayment == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Оплата по записи найдена, но позиция счёта для возврата не определена."
                });
            }

            await using var dbTransaction = await _db.Database.BeginTransactionAsync();
            try
            {
                await _operationsByInvoices.RefundInvoicePaymentAsync(paidPayment);
                appointment.PaymentType = "unpaid";
                appointment.AppointmentStatus = "cancelled";
                appointment.IsActive = false;
                appointment.UpdatedAt = now;
                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                throw;
            }

            return Json(new
            {
                success = true,
                message = "Запись отменена, оплата возвращена (созданы транзакции возврата).",
                refunded = true
            });
        }

        if (hasPaid && !request.RefundPayment)
        {
            appointment.AppointmentStatus = "cancelled";
            appointment.IsActive = false;
            appointment.UpdatedAt = now;
            await _db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Запись отменена без возврата суммы.",
                refunded = false
            });
        }

        appointment.AppointmentStatus = "cancelled";
        appointment.IsActive = false;
        appointment.UpdatedAt = now;
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = "Запись отменена.", refunded = false });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("appointments.edit")]
    public async Task<IActionResult> MarkPaid([FromBody] MarkAppointmentPaidRequest request)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Appointments))
            return NotFound();

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return Unauthorized();

        var canMarkAsPaidManually =
            PermissionHelper.HasPermission(HttpContext, "appointments.payment.status") ||
            PermissionHelper.HasPermission(HttpContext, "appointments.invoice") ||
            PermissionHelper.HasPermission(HttpContext, "invoices.create") ||
            PermissionHelper.HasPermission(HttpContext, "appointments.manage");
        if (!canMarkAsPaidManually)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.AppointmentId))
        {
            return BadRequest(new { success = false, message = "Не выбрана запись." });
        }

        var currentUserId = AuthorizationHelper.GetUserId(HttpContext);
        var isDoctorRole = AuthorizationHelper.IsDoctorRole(HttpContext);

        var appointmentQuery = _db.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.AppointmentServices)
            .Where(x => x.OrganizationId == organizationId && x.Id == request.AppointmentId);

        if (isDoctorRole && !string.IsNullOrWhiteSpace(currentUserId))
            appointmentQuery = appointmentQuery.Where(x => x.DoctorId == currentUserId);

        var appointment = await appointmentQuery.FirstOrDefaultAsync();
        if (appointment == null)
            return NotFound(new { success = false, message = "Запись не найдена." });

        if (!AllowsManualMarkPaid(appointment.PaymentType))
        {
            return BadRequest(new
            {
                success = false,
                message = "Для записей с оплатой через QR SECORE статус меняется автоматически после оплаты."
            });
        }

        var paidRanges = await LoadPaidAppointmentRangesAsync(organizationId);
        var alreadyPaid = !string.IsNullOrWhiteSpace(appointment.PatientId) &&
                          paidRanges.Any(p =>
                              p.PatientId == appointment.PatientId &&
                              p.DateFrom <= appointment.StartsAt &&
                              appointment.EndsAt <= p.DateTo);
        if (alreadyPaid || IsPaidPaymentType(appointment.PaymentType))
        {
            return BadRequest(new { success = false, message = "Запись уже отмечена как оплаченная." });
        }

        var invoice = await _db.Invoices
            .Include(i => i.InvoicePayments)
            .Include(i => i.ClientNavigation)
            .Where(i => i.FromAppointments && i.Client == appointment.PatientId)
            .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId)
            .Where(i => i.InvoicePayments.Any(p =>
                p.DateFrom == appointment.StartsAt &&
                p.DateTo == appointment.EndsAt))
            .OrderByDescending(i => i.DateCreated)
            .FirstOrDefaultAsync();

        if (invoice == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Счёт по записи не найден. Сначала создайте счёт при регистрации."
            });
        }

        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        var paymentsToMark = invoice.InvoicePayments
            .Where(p => p.DateFrom == appointment.StartsAt && p.DateTo == appointment.EndsAt)
            .Where(p => !string.Equals(p.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (paymentsToMark.Count == 0)
        {
            return BadRequest(new { success = false, message = "Нет неоплаченных позиций счёта для этой записи." });
        }

        var bookingPaymentType = appointment.PaymentType;

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            await _operationsByInvoices.ApplyManualAppointmentMarkPaidTransactionsAsync(
                organizationId,
                invoice,
                paymentsToMark,
                bookingPaymentType);

            foreach (var payment in paymentsToMark)
            {
                payment.PaymentStatus = "paid";
            }

            appointment.PaymentType = "invoice_paid";
            appointment.UpdatedAt = now;
            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();
        }
        catch (InvalidOperationException ex)
        {
            await dbTransaction.RollbackAsync();
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }

        var doctor = appointment.Doctor;
        var patient = appointment.Patient;

        var eventItem = new AppointmentCalendarEventViewModel
        {
            Id = appointment.Id,
            Title = appointment.Title ?? "Приём",
            DoctorId = appointment.DoctorId,
            Doctor = doctor != null ? (doctor.Name ?? "Не назначен") : "Не назначен",
            PatientId = appointment.PatientId,
            PatientName = patient?.ClientName,
            Phone = appointment.Phone,
            Email = appointment.Email,
            Start = appointment.StartsAt,
            End = appointment.EndsAt,
            Status = appointment.IsActive ? "busy" : "available",
            Notes = appointment.Notes ?? string.Empty,
            ReferralSource = appointment.ReferralSource,
            PaymentType = appointment.PaymentType,
            HasPaidInvoice = true,
            IsActive = appointment.IsActive,
            CreatedAt = appointment.CreatedAt,
            UpdatedAt = appointment.UpdatedAt,
            Services = appointment.AppointmentServices
                .OrderBy(x => x.ServiceName)
                .Select(x => new AppointmentServiceLineViewModel
                {
                    OrganizationServiceId = x.OrganizationServiceId,
                    Name = x.ServiceName ?? "Услуга",
                    PriceTyiyn = x.PriceTyiyn ?? 0,
                    Quantity = x.Quantity
                })
                .ToList()
        };
        await EnrichEventsWithPatientDemographicsAsync(organizationId, new List<AppointmentCalendarEventViewModel> { eventItem });

        return Json(new
        {
            success = true,
            message = "Запись и счёт отмечены как оплаченные.",
            eventItem
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetPaymentQr(string appointmentId)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Appointments))
            return NotFound();

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(appointmentId))
            return BadRequest(new { success = false, message = "Не выбрана запись." });

        var currentUserId = AuthorizationHelper.GetUserId(HttpContext);
        var isDoctorRole = AuthorizationHelper.IsDoctorRole(HttpContext);

        var appointmentQuery = _db.Appointments
            .Include(x => x.AppointmentServices)
            .Where(x => x.OrganizationId == organizationId && x.Id == appointmentId);

        if (isDoctorRole && !string.IsNullOrWhiteSpace(currentUserId))
            appointmentQuery = appointmentQuery.Where(x => x.DoctorId == currentUserId);

        var appointment = await appointmentQuery.FirstOrDefaultAsync();
        if (appointment == null)
            return NotFound(new { success = false, message = "Запись не найдена." });

        if (!string.Equals(appointment.PaymentType, "qr_secore", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(appointment.PaymentType, "qr_secore_paid", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "QR SECORE доступен только для записей с этим типом оплаты." });
        }

        var invoice = await FindAppointmentInvoiceAsync(organizationId, appointment);
        if (invoice == null)
        {
            return BadRequest(new { success = false, message = "Счёт по записи не найден." });
        }

        var qr = await _db.InvoiceQrs
            .Where(q => q.InvoiceId == invoice.Id)
            .OrderByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync();

        var amountTyiyn = appointment.AppointmentServices
            .Sum(x => (x.PriceTyiyn ?? 0) * Math.Max(1, x.Quantity));

        return Json(new
        {
            success = true,
            invoiceId = invoice.Id,
            payCode = invoice.PayCode ?? qr?.PayCode,
            amountTyiyn,
            qrLink = qr?.QrLink,
            qrCodeBase64 = qr?.QrCodeBase64,
            status = qr?.Status
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAppointmentInvoice(string appointmentId)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Appointments))
            return NotFound();

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(appointmentId))
            return BadRequest(new { success = false, message = "Не выбрана запись." });

        var currentUserId = AuthorizationHelper.GetUserId(HttpContext);
        var isDoctorRole = AuthorizationHelper.IsDoctorRole(HttpContext);

        var appointmentQuery = _db.Appointments
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.Id == appointmentId);

        if (isDoctorRole && !string.IsNullOrWhiteSpace(currentUserId))
            appointmentQuery = appointmentQuery.Where(x => x.DoctorId == currentUserId);

        var appointment = await appointmentQuery.FirstOrDefaultAsync();
        if (appointment == null)
            return NotFound(new { success = false, message = "Запись не найдена." });

        if (await AppointmentHasPaidInvoiceAsync(organizationId, appointment, appointment.PaymentType ?? string.Empty))
        {
            return BadRequest(new { success = false, message = "Счёт уже оплачен." });
        }

        var invoice = await FindAppointmentInvoiceAsync(organizationId, appointment);
        if (invoice == null)
            return NotFound(new { success = false, message = "Счёт по записи не найден." });

        return Json(new
        {
            success = true,
            invoiceId = invoice.Id,
            payCode = invoice.PayCode
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendInvoiceWhatsApp([FromBody] SendAppointmentInvoiceWhatsAppRequest request)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Appointments))
            return NotFound();

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.AppointmentId))
            return BadRequest(new { success = false, message = "Не выбрана запись." });

        var currentUserId = AuthorizationHelper.GetUserId(HttpContext);
        var isDoctorRole = AuthorizationHelper.IsDoctorRole(HttpContext);

        var appointmentQuery = _db.Appointments
            .Include(x => x.Patient)
            .Include(x => x.AppointmentServices)
            .Where(x => x.OrganizationId == organizationId && x.Id == request.AppointmentId);

        if (isDoctorRole && !string.IsNullOrWhiteSpace(currentUserId))
            appointmentQuery = appointmentQuery.Where(x => x.DoctorId == currentUserId);

        var appointment = await appointmentQuery.FirstOrDefaultAsync();
        if (appointment == null)
            return NotFound(new { success = false, message = "Запись не найдена." });

        var patient = appointment.Patient;
        if (patient == null)
            return BadRequest(new { success = false, message = "У записи нет пациента." });

        var whatsapp = patient.ClientWa?.Trim();
        if (string.IsNullOrWhiteSpace(whatsapp) && !string.IsNullOrWhiteSpace(appointment.Phone))
            whatsapp = appointment.Phone.Trim();
        if (string.IsNullOrWhiteSpace(whatsapp))
            return BadRequest(new { success = false, message = "У клиента не указан WhatsApp." });

        var invoice = !string.IsNullOrWhiteSpace(request.InvoiceId)
            ? await _db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId && i.Client == appointment.PatientId)
            : await FindAppointmentInvoiceAsync(organizationId, appointment);

        if (invoice == null)
            return BadRequest(new { success = false, message = "Счёт по записи не найден." });

        var qr = await _db.InvoiceQrs
            .Where(q => q.InvoiceId == invoice.Id)
            .OrderByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync();

        var amountTyiyn = appointment.AppointmentServices
            .Sum(x => (x.PriceTyiyn ?? 0) * Math.Max(1, x.Quantity));
        var amountText = amountTyiyn.ToString("N2");
        var clientName = string.IsNullOrWhiteSpace(patient.ClientName) ? "клиент" : patient.ClientName.Trim();
        var payCode = invoice.PayCode ?? qr?.PayCode ?? "—";
        var pdfUrl = Url.Action("DownloadPdf", "Invoices", new { id = invoice.Id }, Request.Scheme)
                     ?? $"/Invoices/DownloadPdf?id={invoice.Id}";

        var subject = "Счёт на оплату приёма";
        var message =
            $"Здравствуйте, {clientName}!\n\n" +
            $"Счёт на оплату приёма {appointment.StartsAt:dd.MM.yyyy HH:mm}.\n" +
            $"Сумма: {amountText} сом\n" +
            $"Лицевой счёт: {payCode}\n" +
            (string.IsNullOrWhiteSpace(qr?.QrLink) ? "" : $"QR-ссылка: {qr.QrLink}\n") +
            $"Скачать счёт: {pdfUrl}";

        await _notificationService.CreateNotificationAsync(
            patient.Id,
            "whatsapp",
            whatsapp,
            subject,
            message,
            metadata: System.Text.Json.JsonSerializer.Serialize(new
            {
                source = "appointment_qr",
                appointmentId = appointment.Id,
                invoiceId = invoice.Id
            }));

        return Json(new
        {
            success = true,
            message = "Счёт отправлен в очередь WhatsApp."
        });
    }

    private async Task<Invoice?> FindAppointmentInvoiceAsync(string organizationId, Appointment appointment)
    {
        if (string.IsNullOrWhiteSpace(appointment.PatientId))
            return null;

        return await FindAppointmentInvoiceBySlotAsync(
            organizationId,
            appointment.PatientId,
            appointment.StartsAt,
            appointment.EndsAt);
    }

    private async Task<Invoice?> FindAppointmentInvoiceBySlotAsync(
        string organizationId,
        string patientId,
        DateTime startsAt,
        DateTime endsAt)
    {
        return await _db.Invoices
            .Include(i => i.InvoicePayments)
            .Include(i => i.ClientNavigation)
            .Where(i => i.FromAppointments && i.Client == patientId)
            .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId)
            .Where(i => i.InvoicePayments.Any(p =>
                p.DateFrom == startsAt &&
                p.DateTo == endsAt))
            .OrderByDescending(i => i.DateCreated)
            .FirstOrDefaultAsync();
    }

    private async Task<bool> AppointmentHasPaidInvoiceAsync(
        string organizationId,
        Appointment appointment,
        string paymentType)
    {
        if (IsPaidPaymentType(paymentType))
            return true;

        var invoice = await FindAppointmentInvoiceAsync(organizationId, appointment);
        if (invoice == null)
            return false;

        return invoice.InvoicePayments.Any(p =>
            string.Equals(p.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("appointments.edit")]
    public async Task<IActionResult> SaveDoctorSchedule([FromBody] SaveUserWorkScheduleRequest request)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Appointments))
            return NotFound();

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return Unauthorized();

        if (!await DoctorSchedulesStorageReadyAsync())
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                success = false,
                message = "Схема рабочих смен еще не применена. Сначала выполните SQL-скрипт создания таблицы."
            });
        }

        if (string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest(new { success = false, message = "Выберите врача." });

        if (!TimeSpan.TryParse(request.StartTime, out var startTime))
            return BadRequest(new { success = false, message = "Укажите корректное время начала смены." });

        if (!TimeSpan.TryParse(request.EndTime, out var endTime))
            return BadRequest(new { success = false, message = "Укажите корректное время окончания смены." });

        if (endTime <= startTime)
            return BadRequest(new { success = false, message = "Время окончания смены должно быть позже времени начала." });

        var days = request.DaysOfWeek
            .Where(x => x is >= 1 and <= 7)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (days.Count == 0)
            return BadRequest(new { success = false, message = "Выберите хотя бы один рабочий день." });

        var doctorExists = await _db.Users.AnyAsync(x =>
            x.Id == request.UserId &&
            x.Organization == organizationId &&
            (x.Isdeleted == null || x.Isdeleted == 0));

        if (!doctorExists)
            return BadRequest(new { success = false, message = "Врач не найден в текущей организации." });

        var existing = await _db.UserWorkSchedules
            .Where(x => x.OrganizationId == organizationId && x.UserId == request.UserId)
            .ToListAsync();

        _db.UserWorkSchedules.RemoveRange(existing);

        foreach (var day in days)
        {
            _db.UserWorkSchedules.Add(new UserWorkSchedule
            {
                Id = Guid.NewGuid().ToString(),
                OrganizationId = organizationId,
                UserId = request.UserId,
                DayOfWeek = day,
                StartTime = startTime,
                EndTime = endTime,
                IsActive = true,
                CreatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
                UpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)
            });
        }

        await _db.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = "Смена врача сохранена."
        });
    }

    private static string? ValidateRequest(SaveAppointmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId) && string.IsNullOrWhiteSpace(request.PatientName))
            return "Укажите пациента.";

        if (string.IsNullOrWhiteSpace(request.DoctorId))
            return "Укажите врача.";

        if (request.AppointmentDate == null)
            return "Укажите дату приёма.";

        if (string.IsNullOrWhiteSpace(request.StartTime) || !TimeSpan.TryParse(request.StartTime, out _))
            return "Укажите корректное время начала.";

        if (string.IsNullOrWhiteSpace(request.EndTime) || !TimeSpan.TryParse(request.EndTime, out _))
            return "Укажите корректное время окончания.";

        var isDraft = string.Equals(request.AppointmentStatus, "draft", StringComparison.OrdinalIgnoreCase);
        if (!isDraft && (request.Services == null || request.Services.Count == 0))
            return "Добавьте хотя бы одну услугу.";

        return null;
    }

    private static DateTime TruncateToMinute(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);

    private static bool IsSameAppointmentSlot(DateTime existingStart, DateTime existingEnd, DateTime newStart, DateTime newEnd) =>
        TruncateToMinute(existingStart) == TruncateToMinute(newStart) &&
        TruncateToMinute(existingEnd) == TruncateToMinute(newEnd);

    private static string? ValidateAppointmentSlotNotInPast(
        DateTime startsAt,
        DateTime endsAt,
        Appointment? existingAppointment,
        bool isNewAppointment)
    {
        var now = DateTime.Now;
        if (startsAt >= now)
            return null;

        if (!isNewAppointment &&
            existingAppointment != null &&
            IsSameAppointmentSlot(existingAppointment.StartsAt, existingAppointment.EndsAt, startsAt, endsAt))
        {
            return null;
        }

        return "Нельзя создать или перенести запись на прошедшее время.";
    }

    private async Task<(OrganizationClient? Patient, bool IsNew)> ResolvePatientAsync(string organizationId, string? userId, SaveAppointmentRequest request)
    {
        OrganizationClient? patient = null;

        if (!string.IsNullOrWhiteSpace(request.PatientId))
        {
            patient = await _db.OrganizationClients
                .FirstOrDefaultAsync(x => x.Id == request.PatientId && x.Organization == organizationId);
        }

        var patientName = request.PatientName?.Trim();
        if (patient == null && !string.IsNullOrWhiteSpace(patientName))
        {
            var normalizedName = patientName.ToLower();
            patient = await _db.OrganizationClients
                .FirstOrDefaultAsync(x =>
                    x.Organization == organizationId &&
                    x.ClientName != null &&
                    x.ClientName.ToLower() == normalizedName);
        }

        if (patient != null)
            return (patient, false);

        if (string.IsNullOrWhiteSpace(patientName))
            return (null, false);

        patient = new OrganizationClient
        {
            Id = Guid.NewGuid().ToString(),
            Organization = organizationId,
            ClientName = patientName,
            ClientPhone = request.Phone?.Trim(),
            ClientWa = request.UsePhoneAsWhatsApp ? request.Phone?.Trim() : null,
            ClientType = "fiz",
            ClientStatus = 1,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now,
            UserCreater = userId
        };

        _db.OrganizationClients.Add(patient);
        return (patient, true);
    }

    private static string? ValidateNewPatientGender(string? gender)
    {
        if (string.IsNullOrWhiteSpace(gender))
            return "Укажите пол пациента.";

        var normalized = gender.Trim();
        if (normalized is not ("М" or "Ж" or "M" or "F"))
            return "Укажите корректный пол пациента (М или Ж).";

        return null;
    }

    private static string? ValidateNewPatientBirthDate(string? birthDate)
    {
        if (string.IsNullOrWhiteSpace(birthDate))
            return "Укажите дату рождения пациента.";

        if (!DateTime.TryParse(birthDate, out var parsed) || parsed.Year <= 1900 || parsed.Date > DateTime.Today)
            return "Укажите корректную дату рождения пациента.";

        return null;
    }

    private static TimeSpan ParseTime(string value)
    {
        return TimeSpan.TryParse(value, out var time) ? time : TimeSpan.Zero;
    }

    private async Task<bool> AppointmentsStorageReadyAsync()
    {
        return await CheckTablesExistAsync("appointments", "appointment_services");
    }

    private async Task<bool> DoctorSchedulesStorageReadyAsync()
    {
        return await CheckTablesExistAsync("user_work_schedules");
    }

    private async Task<bool> DoctorScheduleOverridesStorageReadyAsync()
    {
        return await CheckTablesExistAsync("user_work_schedule_overrides");
    }

    private async Task<bool> AppointmentSettingsStorageReadyAsync()
    {
        return await CheckTablesExistAsync("appointment_settings");
    }

    private async Task<bool> MedicalTemplatesStorageReadyAsync()
    {
        return await CheckTablesExistAsync("appointment_medical_templates");
    }

    private async Task<bool> CheckTablesExistAsync(params string[] tableNames)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            var inList = string.Join(", ", tableNames.Select(x => $"'{x}'"));
            command.CommandText = $"""
                select count(*)
                from information_schema.tables
                where table_schema = 'public'
                  and table_name in ({inList})
                """;

            var scalar = await command.ExecuteScalarAsync();
            var count = Convert.ToInt32(scalar);
            return count >= tableNames.Length;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<List<DoctorScheduleViewModel>> LoadDoctorSchedulesAsync(string organizationId)
    {
        return await _db.UserWorkSchedules
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderBy(x => x.UserId)
            .ThenBy(x => x.DayOfWeek)
            .Select(x => new DoctorScheduleViewModel
            {
                DoctorId = x.UserId,
                DayOfWeek = x.DayOfWeek,
                StartTime = x.StartTime.ToString(@"hh\:mm"),
                EndTime = x.EndTime.ToString(@"hh\:mm")
            })
            .ToListAsync();
    }

    private async Task<List<DoctorScheduleOverrideViewModel>> LoadDoctorScheduleOverridesAsync(string organizationId)
    {
        return await _db.UserWorkScheduleOverrides
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.UserId)
            .ThenBy(x => x.WorkDate)
            .Select(x => new DoctorScheduleOverrideViewModel
            {
                UserId = x.UserId,
                WorkDate = x.WorkDate.ToString("yyyy-MM-dd"),
                IsWorking = x.IsWorking,
                StartTime = x.StartTime.HasValue ? x.StartTime.Value.ToString(@"hh\:mm") : null,
                EndTime = x.EndTime.HasValue ? x.EndTime.Value.ToString(@"hh\:mm") : null,
                Comment = x.Comment
            })
            .ToListAsync();
    }

    private async Task<List<AppointmentDurationSettingViewModel>> LoadAppointmentDurationsAsync(string organizationId)
    {
        return await _db.AppointmentSettings
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && !string.IsNullOrWhiteSpace(x.UserId))
            .OrderBy(x => x.UserId)
            .Select(x => new AppointmentDurationSettingViewModel
            {
                UserId = x.UserId,
                DurationMinutes = x.AppointmentDurationMinutes <= 0 ? 30 : x.AppointmentDurationMinutes
            })
            .ToListAsync();
    }

    private async Task<string?> ValidateDoctorScheduleAsync(string organizationId, string doctorId, DateTime startsAt, DateTime endsAt)
    {
        var dayOfWeek = NormalizeDayOfWeek(startsAt);
        var startTime = startsAt.TimeOfDay;
        var endTime = endsAt.TimeOfDay;

        var schedule = await _db.UserWorkSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.OrganizationId == organizationId &&
                x.UserId == doctorId &&
                x.DayOfWeek == dayOfWeek &&
                x.IsActive);

        if (schedule == null)
            return "У врача нет рабочей смены на выбранный день.";

        if (startTime < schedule.StartTime || endTime > schedule.EndTime)
            return $"Запись можно создать только в рамках смены врача: {schedule.StartTime:hh\\:mm} - {schedule.EndTime:hh\\:mm}.";

        return null;
    }

    private static int NormalizeDayOfWeek(DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
    }

    private async Task<string?> ValidateDoctorScheduleWithOverridesAsync(string organizationId, string doctorId, DateTime startsAt, DateTime endsAt)
    {
        var startTime = startsAt.TimeOfDay;
        var endTime = endsAt.TimeOfDay;
        var schedule = await ResolveApplicableDoctorScheduleAsync(organizationId, doctorId, startsAt.Date);

        if (schedule == null)
            return "У выбранного врача на этот день нет активной смены. Настройте её на странице врача.";

        if (startTime < schedule.Value.StartTime || endTime > schedule.Value.EndTime)
            return $"Запись можно создать только в рамках смены врача: {schedule.Value.StartTime:hh\\:mm} - {schedule.Value.EndTime:hh\\:mm}.";

        return null;
    }

    private async Task<(TimeSpan StartTime, TimeSpan EndTime)?> ResolveApplicableDoctorScheduleAsync(string organizationId, string doctorId, DateTime workDate)
    {
        if (await DoctorScheduleOverridesStorageReadyAsync())
        {
            var overrideSchedule = await _db.UserWorkScheduleOverrides
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.OrganizationId == organizationId &&
                    x.UserId == doctorId &&
                    x.WorkDate == workDate);

            if (overrideSchedule != null)
            {
                if (!overrideSchedule.IsWorking || !overrideSchedule.StartTime.HasValue || !overrideSchedule.EndTime.HasValue)
                    return null;

                return (overrideSchedule.StartTime.Value, overrideSchedule.EndTime.Value);
            }
        }

        var dayOfWeek = NormalizeDayOfWeek(workDate);
        var schedule = await _db.UserWorkSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.OrganizationId == organizationId &&
                x.UserId == doctorId &&
                x.DayOfWeek == dayOfWeek &&
                x.IsActive);

        if (schedule == null)
            return null;

        return (schedule.StartTime, schedule.EndTime);
    }

    private async Task<List<AppointmentCalendarEventViewModel>> LoadEventsFromStorageAsync(string organizationId, string? doctorId)
    {
        var appointments = await _db.Appointments
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.AppointmentServices)
            .Where(x => x.OrganizationId == organizationId)
            .Where(x => string.IsNullOrWhiteSpace(doctorId) || x.DoctorId == doctorId)
            .OrderBy(x => x.StartsAt)
            .ToListAsync();

        var paidAppointmentRanges = await LoadPaidAppointmentRangesAsync(organizationId);

        return appointments.Select(x =>
        {
            var hasPaidInvoice = !string.IsNullOrWhiteSpace(x.PatientId) &&
                                 paidAppointmentRanges.Any(p =>
                                     p.PatientId == x.PatientId &&
                                     p.DateFrom <= x.StartsAt &&
                                     x.EndsAt <= p.DateTo);
            return new AppointmentCalendarEventViewModel
            {
                Id = x.Id,
                Title = x.Title ?? "Приём",
                DoctorId = x.DoctorId,
                Doctor = x.Doctor != null ? (x.Doctor.Name ?? "Не назначен") : "Не назначен",
                PatientId = x.PatientId,
                PatientName = x.Patient != null ? x.Patient.ClientName : null,
                Phone = x.Phone,
                Email = x.Email,
                Start = x.StartsAt,
                End = x.EndsAt,
                Status = x.IsActive ? "busy" : "available",
                Notes = x.Notes ?? string.Empty,
                ReferralSource = x.ReferralSource,
                PaymentType = hasPaidInvoice && string.IsNullOrWhiteSpace(x.PaymentType) ? "invoice_paid" : x.PaymentType,
                HasPaidInvoice = hasPaidInvoice || IsPaidPaymentType(x.PaymentType),
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                Services = x.AppointmentServices
                    .OrderBy(s => s.ServiceName)
                    .Select(s => new AppointmentServiceLineViewModel
                    {
                        OrganizationServiceId = s.OrganizationServiceId,
                        Name = s.ServiceName ?? "Услуга",
                        PriceTyiyn = s.PriceTyiyn ?? 0,
                        Quantity = s.Quantity
                    })
                    .ToList()
            };
        }).ToList();
    }

    private async Task EnrichEventsWithPatientDemographicsAsync(
        string organizationId,
        List<AppointmentCalendarEventViewModel> events,
        CancellationToken cancellationToken = default)
    {
        var patientIds = events
            .Where(e => !string.IsNullOrWhiteSpace(e.PatientId))
            .Select(e => e.PatientId!)
            .Distinct()
            .ToList();

        if (patientIds.Count == 0)
            return;

        var demographics = await _recipientResolver.LoadDemographicsAsync(
            organizationId,
            patientIds,
            cancellationToken);

        foreach (var ev in events)
        {
            if (string.IsNullOrWhiteSpace(ev.PatientId))
                continue;
            if (!demographics.TryGetValue(ev.PatientId, out var demo))
                continue;

            ev.PatientGender = NotificationRecipientResolver.FormatGenderDisplay(demo.Gender, demo.GenderRaw);
            ev.PatientBirthDate = demo.BirthDate;
            ev.PatientAge = demo.Age;
        }
    }

    private async Task<List<AppointmentCalendarEventViewModel>> LoadEventsWithDemographicsAsync(
        string organizationId,
        string? doctorId)
    {
        var events = await LoadEventsFromStorageAsync(organizationId, doctorId);
        await EnrichEventsWithPatientDemographicsAsync(organizationId, events);
        return events;
    }

    private async Task<List<PaidAppointmentRange>> LoadPaidAppointmentRangesAsync(string organizationId)
    {
        var paidByInvoicePayments = await _db.Invoices
            .AsNoTracking()
            .Where(x => x.InvoicePayments.Any(p => p.PaymentStatus == "paid"))
            .Where(x => x.Client != null)
            .Where(x => _db.OrganizationClients.Any(c => c.Id == x.Client && c.Organization == organizationId))
            .SelectMany(
                x => x.InvoicePayments
                    .Where(p => p.PaymentStatus == "paid")
                    .DefaultIfEmpty(),
                (invoice, payment) => new
                {
                    PatientId = invoice.Client!,
                    DateFrom = payment != null && payment.DateFrom.HasValue
                        ? payment.DateFrom.Value
                        : (invoice.DateStartInvoice ?? invoice.DateCreated ?? DateTime.MinValue),
                    DateTo = payment != null && payment.DateTo.HasValue
                        ? payment.DateTo.Value
                        : (invoice.DateEndInvoice ?? invoice.DateStartInvoice ?? invoice.DateCreated ?? DateTime.MinValue)
                })
            .ToListAsync();

        var paidByTransactions = await _db.Invoices
            .AsNoTracking()
            .Where(x => x.Client != null)
            .Where(x => x.Transactions.Any(t => t.TransactionStatus == "success"))
            .Where(x => _db.OrganizationClients.Any(c => c.Id == x.Client && c.Organization == organizationId))
            .Select(x => new
            {
                PatientId = x.Client!,
                DateFrom = x.DateStartInvoice ?? x.DateCreated ?? DateTime.MinValue,
                DateTo = x.DateEndInvoice ?? x.DateStartInvoice ?? x.DateCreated ?? DateTime.MinValue
            })
            .ToListAsync();

        return paidByInvoicePayments
            .Concat(paidByTransactions)
            .Where(x => x.DateTo >= x.DateFrom)
            .Select(x => new PaidAppointmentRange(x.PatientId, x.DateFrom, x.DateTo))
            .ToList();
    }

    private sealed record PaidAppointmentRange(string PatientId, DateTime DateFrom, DateTime DateTo);

    private static bool IsBookingPaymentType(string? paymentType)
    {
        var normalized = (paymentType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "qr_secore" or "qr_external" or "card" or "cash";
    }

    private static bool ShouldGenerateQrForBooking(string? paymentType) =>
        string.Equals(paymentType, "qr_secore", StringComparison.OrdinalIgnoreCase);

    private async Task<CreateOneTimeInvoiceResult> CreateInvoiceForAppointmentAsync(
        string organizationId,
        string userId,
        Appointment appointment,
        OrganizationClient patient,
        User? doctor,
        string paymentType)
    {
        var serviceLines = appointment.AppointmentServices.ToList();
        if (serviceLines.Count == 0)
            throw new InvalidOperationException("Добавьте услуги перед созданием счёта.");

        var doctorName = string.IsNullOrWhiteSpace(doctor?.Name) ? "Врач" : doctor!.Name!;
        var patientName = string.IsNullOrWhiteSpace(patient.ClientName) ? "Пациент" : patient.ClientName!;
        var titleDate = appointment.StartsAt.ToString("dd.MM.yyyy");
        var totalTyiyn = serviceLines.Sum(x => (x.PriceTyiyn ?? 0) * Math.Max(1, x.Quantity));

        return await _operationsByInvoices.CreateOneTimeInvoiceAsync(new CreateOneTimeInvoiceInput
        {
            OrganizationId = organizationId,
            UserId = userId,
            ClientId = appointment.PatientId!,
            FixedSumm = totalTyiyn,
            InvoiceName = $"Приём {patientName} • {doctorName} • {titleDate}",
            DateStartInvoice = appointment.StartsAt.Date,
            DateEndInvoice = appointment.StartsAt.Date,
            Balance = 0,
            Hassameaccount = false,
            FromAppointments = true,
            GenerateQrOnCreate = ShouldGenerateQrForBooking(paymentType),
            PaymentDateFrom = appointment.StartsAt,
            PaymentDateTo = appointment.EndsAt,
            PaymentPeriodValue = appointment.StartsAt.ToString("dd.MM.yyyy"),
            PaymentSumm = totalTyiyn,
            ServiceLines = serviceLines.Select(x => new CreateOneTimePaymentServiceLineInput
            {
                OrganizationServiceId = x.OrganizationServiceId,
                ServiceSumm = (x.PriceTyiyn ?? 0) * Math.Max(1, x.Quantity)
            }).ToList()
        });
    }

    private static bool IsPaidPaymentType(string? paymentType)
    {
        var normalized = (paymentType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "online" or "insurance" or "invoice_paid" or "paid" or "qr_secore_paid";
    }

    private static bool AllowsManualMarkPaid(string? paymentType)
    {
        var normalized = (paymentType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "qr_external" or "card" or "cash" or "unpaid";
    }

    private static string NormalizePaymentType(string? paymentType)
    {
        var normalized = (paymentType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "qr_secore_paid" => "qr_secore_paid",
            "qr_secore" => "qr_secore",
            "qr_external" => "qr_external",
            "cash" => "cash",
            "card" => "card",
            "online" => "online",
            "insurance" => "insurance",
            "invoice_paid" => "invoice_paid",
            "paid" => "paid",
            _ => "unpaid"
        };
    }

    private static List<AppointmentCalendarEventViewModel> BuildFallbackEvents(
        IReadOnlyList<SelectOptionViewModel> doctors,
        IReadOnlyList<PatientLookupViewModel> patients)
    {
        var anchorDate = DateTime.Today;
        var primaryDoctor = doctors.FirstOrDefault();
        var secondaryDoctor = doctors.Skip(1).FirstOrDefault() ?? primaryDoctor;
        var primaryPatient = patients.FirstOrDefault();
        var secondaryPatient = patients.Skip(1).FirstOrDefault() ?? primaryPatient;

        return new List<AppointmentCalendarEventViewModel>
        {
            new()
            {
                Id = "demo-1",
                Title = "Первичный приём",
                DoctorId = primaryDoctor?.Value,
                Doctor = primaryDoctor?.Label ?? "Не назначен",
                PatientId = primaryPatient?.Id,
                PatientName = primaryPatient?.Name,
                Start = anchorDate.AddHours(9),
                End = anchorDate.AddHours(9).AddMinutes(30),
                Status = "busy",
                Notes = "Первичный осмотр и сбор анамнеза.",
                CreatedAt = anchorDate.AddHours(9),
                UpdatedAt = anchorDate.AddHours(9)
            },
            new()
            {
                Id = "demo-2",
                Title = "Повторный приём",
                DoctorId = secondaryDoctor?.Value,
                Doctor = secondaryDoctor?.Label ?? "Не назначен",
                PatientId = secondaryPatient?.Id,
                PatientName = secondaryPatient?.Name,
                Start = anchorDate.AddHours(11),
                End = anchorDate.AddHours(11).AddMinutes(45),
                Status = "busy",
                Notes = "Контроль после процедуры.",
                CreatedAt = anchorDate.AddHours(11),
                UpdatedAt = anchorDate.AddHours(11)
            },
            new()
            {
                Id = "demo-3",
                Title = "Свободное окно",
                DoctorId = primaryDoctor?.Value,
                Doctor = primaryDoctor?.Label ?? "Не назначен",
                Start = anchorDate.AddHours(14),
                End = anchorDate.AddHours(14).AddMinutes(30),
                Status = "available",
                Notes = "Слот доступен для записи.",
                CreatedAt = anchorDate.AddHours(14),
                UpdatedAt = anchorDate.AddHours(14)
            }
        };
    }

    private static List<AppointmentMedicalTemplateViewModel> BuildFallbackMedicalTemplates()
    {
        return new List<AppointmentMedicalTemplateViewModel>
        {
            new() { Id = "demo-diagnosis-1", Type = "diagnosis", Title = "ОРВИ", Content = "ОРВИ, острое течение, средней степени тяжести.", SortOrder = 1 },
            new() { Id = "demo-diagnosis-2", Type = "diagnosis", Title = "Гастрит", Content = "Хронический гастрит в стадии обострения.", SortOrder = 2 },
            new() { Id = "demo-recommendation-1", Type = "recommendation", Title = "Общий режим", Content = "Покой, обильное питье, контроль температуры 2 раза в день.", SortOrder = 1 },
            new() { Id = "demo-recommendation-2", Type = "recommendation", Title = "Контроль", Content = "Повторный прием через 3 дня либо раньше при ухудшении состояния.", SortOrder = 2 },
            new() { Id = "demo-research-1", Type = "research", Title = "ОАК", Content = "Направление на общий анализ крови.", SortOrder = 1 },
            new() { Id = "demo-research-2", Type = "research", Title = "УЗИ", Content = "Направление на УЗИ по клиническим показаниям.", SortOrder = 2 }
        };
    }

    private static string? NormalizeTemplateType(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "complaints" => "complaints",
            "diagnosis" => "diagnosis",
            "recommendation" => "recommendation",
            "recommendations" => "recommendations",
            "research" => "research",
            "referral" => "referral",
            "comment" => "comment",
            "comments" => "comments",
            _ => null
        };
    }
}
