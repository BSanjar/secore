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

    public AppointmentsController(
        AppDbContext db,
        OperationsByInvoices operationsByInvoices,
        ICurrentTenantService currentTenantService,
        NotificationService notificationService)
    {
        _db = db;
        _operationsByInvoices = operationsByInvoices;
        _currentTenantService = currentTenantService;
        _notificationService = notificationService;
    }

    [HttpGet]
    [RequirePermission("appointments.view")]
    public async Task<IActionResult> Index()
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Appointments))
            return NotFound();

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return Unauthorized();

        var currentUserId = AuthorizationHelper.GetUserId(HttpContext);
        var isDoctorRole = AuthorizationHelper.IsDoctorRole(HttpContext);
        var canManageAppointments =
            PermissionHelper.HasPermission(HttpContext, "appointments.edit") ||
            PermissionHelper.HasPermission(HttpContext, "appointments.manage");
        var canGenerateInvoice =
            PermissionHelper.HasPermission(HttpContext, "appointments.invoice") ||
            PermissionHelper.HasPermission(HttpContext, "invoices.create");

        var patientsCount = await _db.OrganizationClients
            .AsNoTracking()
            .CountAsync(x => x.Organization == organizationId);

        var doctorsQuery = _db.Users
            .AsNoTracking()
            .Where(x => x.Organization == organizationId && (x.Isdeleted == null || x.Isdeleted == 0));

        if (isDoctorRole && !string.IsNullOrWhiteSpace(currentUserId))
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

        var storageReady = await AppointmentsStorageReadyAsync();
        var doctorSchedulesReady = await DoctorSchedulesStorageReadyAsync();
        var doctorScheduleOverridesReady = await DoctorScheduleOverridesStorageReadyAsync();
        IReadOnlyList<DoctorScheduleViewModel> doctorSchedules = doctorSchedulesReady
            ? await LoadDoctorSchedulesAsync(organizationId)
            : Array.Empty<DoctorScheduleViewModel>();
        IReadOnlyList<DoctorScheduleOverrideViewModel> doctorScheduleOverrides = doctorScheduleOverridesReady
            ? await LoadDoctorScheduleOverridesAsync(organizationId)
            : Array.Empty<DoctorScheduleOverrideViewModel>();

        var events = storageReady
            ? await LoadEventsFromStorageAsync(organizationId, isDoctorRole ? currentUserId : null)
            : BuildFallbackEvents(doctors, patients);

        ViewBag.IsDoctorRole = isDoctorRole;
        ViewBag.CanManageAppointments = canManageAppointments;
        ViewBag.CanGenerateInvoice = canGenerateInvoice;

        return View(new AppointmentsIndexViewModel
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
            DoctorSchedules = doctorSchedules,
            DoctorScheduleOverrides = doctorScheduleOverrides,
            Events = events,
            StorageReady = storageReady,
            StorageMessage = storageReady
                ? null
                : "Таблицы appointments ещё не созданы. Пока календарь работает на демо-данных, а сохранение записей недоступно до применения SQL-скрипта.",
            DoctorSchedulesReady = doctorSchedulesReady,
            DoctorSchedulesMessage = doctorSchedulesReady
                ? null
                : "Таблица рабочих смен пользователей ещё не создана. Ограничение записи по рабочему времени станет доступно после применения SQL-скрипта."
        });
    }

    [HttpPost]
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
        var patient = await ResolvePatientAsync(organizationId, userId, request);
        if (patient == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Не удалось определить пациента для записи."
            });
        }

        if (isDoctorRole && !string.IsNullOrWhiteSpace(currentUserId))
            request.DoctorId = currentUserId;

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

            var overlapExists = await _db.Appointments
                .AsNoTracking()
                .AnyAsync(x =>
                    x.OrganizationId == organizationId &&
                    x.DoctorId == doctor.Id &&
                    x.Id != request.AppointmentId &&
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

        var appointment = string.IsNullOrWhiteSpace(request.AppointmentId)
            ? null
            : await _db.Appointments
                .Include(x => x.AppointmentServices)
                .FirstOrDefaultAsync(x => x.Id == request.AppointmentId && x.OrganizationId == organizationId);

        if (appointment != null && isDoctorRole && !string.IsNullOrWhiteSpace(currentUserId) && appointment.DoctorId != currentUserId)
        {
            return Forbid();
        }

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
        appointment.PaymentType = request.PaymentType?.Trim();
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
        await _notificationService.CreateOrUpdateAppointmentReminderAsync(appointment, patient, doctor);

        return Json(new
        {
            success = true,
            message = "Запись сохранена.",
            eventItem = new AppointmentCalendarEventViewModel
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
                PaymentType = appointment.PaymentType,
                IsActive = appointment.IsActive,
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
            }
        });
    }

    [HttpPost]
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
            PayCode = $"MED-{ParsersHelper.NowForTimestamp():yyyyMMddHHmmss}",
            DateStartInvoice = appointment.StartsAt.Date,
            DateEndInvoice = appointment.StartsAt.Date,
            Balance = 0,
            Hassameaccount = false,
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

        if (request.AppointmentDate == null)
            return "Укажите дату приёма.";

        if (string.IsNullOrWhiteSpace(request.StartTime) || !TimeSpan.TryParse(request.StartTime, out _))
            return "Укажите корректное время начала.";

        if (string.IsNullOrWhiteSpace(request.EndTime) || !TimeSpan.TryParse(request.EndTime, out _))
            return "Укажите корректное время окончания.";

        return null;
    }

    private async Task<OrganizationClient?> ResolvePatientAsync(string organizationId, string? userId, SaveAppointmentRequest request)
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
            return patient;

        if (string.IsNullOrWhiteSpace(patientName))
            return null;

        patient = new OrganizationClient
        {
            Id = Guid.NewGuid().ToString(),
            Organization = organizationId,
            ClientName = patientName,
            ClientPhone = request.Phone?.Trim(),
            ClientWa = request.UsePhoneAsWhatsApp ? request.Phone?.Trim() : null,
            ClientEmail = request.Email?.Trim(),
            ClientType = "fiz",
            ClientStatus = 1,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now,
            UserCreater = userId
        };

        _db.OrganizationClients.Add(patient);
        return patient;
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
        var query = _db.Appointments
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.AppointmentServices)
            .Where(x => x.OrganizationId == organizationId)
            .Where(x => string.IsNullOrWhiteSpace(doctorId) || x.DoctorId == doctorId)
            .OrderBy(x => x.StartsAt)
            .Select(x => new AppointmentCalendarEventViewModel
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
                PaymentType = x.PaymentType,
                IsActive = x.IsActive,
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
            });

        return await query.ToListAsync();
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
                Notes = "Первичный осмотр и сбор анамнеза."
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
                Notes = "Контроль после процедуры."
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
                Notes = "Слот доступен для записи."
            }
        };
    }
}
