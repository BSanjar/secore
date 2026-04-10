using System.Data;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Dtos;
using WebApplication1.Models.DBModels;
using WebApplication1.ViewModels.Appointments;
using WebApplication1.ViewModels.Doctors;

namespace WebApplication1.Services;

public interface IDoctorDirectoryService
{
    Task<IReadOnlyList<DoctorListItemViewModel>> GetDoctorsAsync(string organizationId, string? search);
    Task<DoctorAssignmentsEditViewModel?> GetDoctorAssignmentsAsync(string organizationId, string doctorId);
    Task<(bool Success, string Message)> UpdateDoctorAssignmentsAsync(string organizationId, DoctorAssignmentsUpsertDto dto);
    Task<bool> DoctorSchedulesStorageReadyAsync();
    Task<bool> DoctorScheduleOverridesStorageReadyAsync();
    Task<(bool Success, string Message)> UpdateDoctorScheduleAsync(string organizationId, SaveUserWorkScheduleRequest dto);
    Task<(bool Success, string Message)> UpdateDoctorScheduleOverrideAsync(string organizationId, SaveUserWorkScheduleOverrideRequest dto);
}

public class DoctorDirectoryService : IDoctorDirectoryService
{
    private readonly AppDbContext _db;

    public DoctorDirectoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DoctorListItemViewModel>> GetDoctorsAsync(string organizationId, string? search)
    {
        var normalizedSearch = search?.Trim();

        var query = _db.Users
            .AsNoTracking()
            .Where(x => x.Organization == organizationId && (x.Isdeleted == null || x.Isdeleted == 0))
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.RoleNavigation)
            .Include(x => x.UserDepartments)
                .ThenInclude(x => x.Department)
            .Include(x => x.UserSpecializations)
                .ThenInclude(x => x.Specialization)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(x =>
                (x.Name != null && x.Name.Contains(normalizedSearch)) ||
                (x.Email != null && x.Email.Contains(normalizedSearch)) ||
                (x.Phone != null && x.Phone.Contains(normalizedSearch)));
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new DoctorListItemViewModel
            {
                Id = x.Id,
                Name = x.Name ?? "Без имени",
                Email = x.Email,
                Phone = x.Phone,
                Roles = x.UserRoles
                    .Where(ur => ur.Isdeleted == null || ur.Isdeleted == 0)
                    .Select(ur => ur.RoleNavigation != null ? ur.RoleNavigation.Name : null)
                    .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
                    .Select(roleName => roleName!)
                    .ToList(),
                Departments = x.UserDepartments
                    .OrderByDescending(ud => ud.IsPrimary)
                    .Select(ud => ud.Department.Name)
                    .ToList(),
                Specializations = x.UserSpecializations
                    .OrderByDescending(us => us.IsPrimary)
                    .Select(us => us.Specialization.Name)
                    .ToList()
            })
            .ToListAsync();
    }

    public async Task<DoctorAssignmentsEditViewModel?> GetDoctorAssignmentsAsync(string organizationId, string doctorId)
    {
        var doctor = await _db.Users
            .AsNoTracking()
            .Where(x => x.Organization == organizationId && (x.Isdeleted == null || x.Isdeleted == 0) && x.Id == doctorId)
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.RoleNavigation)
            .Include(x => x.UserDepartments)
            .Include(x => x.UserSpecializations)
            .FirstOrDefaultAsync();

        if (doctor == null)
            return null;

        var departments = await _db.Departments
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new DoctorOptionViewModel
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync();

        var specializations = await _db.Specializations
            .AsNoTracking()
            .Include(x => x.Department)
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new DoctorOptionViewModel
            {
                Id = x.Id,
                Name = x.Name,
                SecondaryText = x.Department != null ? x.Department.Name : null
            })
            .ToListAsync();

        var doctorSchedulesReady = await DoctorSchedulesStorageReadyAsync();
        var doctorScheduleOverridesReady = await DoctorScheduleOverridesStorageReadyAsync();

        var schedules = doctorSchedulesReady
            ? await _db.UserWorkSchedules
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.UserId == doctorId && x.IsActive)
                .OrderBy(x => x.DayOfWeek)
                .Select(x => new DoctorScheduleViewModel
                {
                    DoctorId = x.UserId,
                    DayOfWeek = x.DayOfWeek,
                    StartTime = x.StartTime.ToString(@"hh\:mm"),
                    EndTime = x.EndTime.ToString(@"hh\:mm")
                })
                .ToListAsync()
            : new List<DoctorScheduleViewModel>();

        var scheduleOverrides = doctorScheduleOverridesReady
            ? await _db.UserWorkScheduleOverrides
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && x.UserId == doctorId)
                .OrderBy(x => x.WorkDate)
                .Select(x => new DoctorScheduleOverrideViewModel
                {
                    UserId = x.UserId,
                    WorkDate = x.WorkDate.ToString("yyyy-MM-dd"),
                    IsWorking = x.IsWorking,
                    StartTime = x.StartTime.HasValue ? x.StartTime.Value.ToString(@"hh\:mm") : null,
                    EndTime = x.EndTime.HasValue ? x.EndTime.Value.ToString(@"hh\:mm") : null,
                    Comment = x.Comment
                })
                .ToListAsync()
            : new List<DoctorScheduleOverrideViewModel>();

        var appointments = await _db.Appointments
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.AppointmentServices)
            .Where(x => x.OrganizationId == organizationId && x.DoctorId == doctorId)
            .OrderBy(x => x.StartsAt)
            .Take(100)
            .Select(x => new AppointmentCalendarEventViewModel
            {
                Id = x.Id,
                Title = x.Title ?? "Прием",
                DoctorId = x.DoctorId,
                Doctor = doctor.Name ?? "Без имени",
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
            })
            .ToListAsync();

        var primarySchedule = schedules.FirstOrDefault();

        return new DoctorAssignmentsEditViewModel
        {
            Form = new DoctorAssignmentsUpsertDto
            {
                Id = doctor.Id,
                SelectedDepartmentIds = doctor.UserDepartments.Select(x => x.DepartmentId).ToList(),
                SelectedSpecializationIds = doctor.UserSpecializations.Select(x => x.SpecializationId).ToList(),
                PrimaryDepartmentId = doctor.UserDepartments.FirstOrDefault(x => x.IsPrimary)?.DepartmentId,
                PrimarySpecializationId = doctor.UserSpecializations.FirstOrDefault(x => x.IsPrimary)?.SpecializationId
            },
            ScheduleForm = new SaveUserWorkScheduleRequest
            {
                UserId = doctor.Id,
                DaysOfWeek = schedules.Select(x => x.DayOfWeek).Distinct().OrderBy(x => x).ToList(),
                StartTime = primarySchedule?.StartTime ?? "08:00",
                EndTime = primarySchedule?.EndTime ?? "18:00"
            },
            OverrideForm = new SaveUserWorkScheduleOverrideRequest
            {
                UserId = doctor.Id,
                WorkDate = DateTime.Today,
                Mode = "default"
            },
            DoctorId = doctor.Id,
            DoctorName = doctor.Name ?? "Без имени",
            Email = doctor.Email,
            Phone = doctor.Phone,
            Roles = doctor.UserRoles
                .Where(ur => ur.Isdeleted == null || ur.Isdeleted == 0)
                .Select(ur => ur.RoleNavigation != null ? ur.RoleNavigation.Name : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList(),
            Departments = departments,
            Specializations = specializations,
            DoctorSchedules = schedules,
            DoctorScheduleOverrides = scheduleOverrides,
            Events = appointments,
            DoctorSchedulesReady = doctorSchedulesReady,
            DoctorSchedulesMessage = doctorSchedulesReady
                ? null
                : "Таблица базовых смен пользователей еще не создана. Сначала примените SQL-скрипт appointments.",
            DoctorScheduleOverridesReady = doctorScheduleOverridesReady,
            DoctorScheduleOverridesMessage = doctorScheduleOverridesReady
                ? null
                : "Таблица индивидуальных смен по датам еще не создана. Чтобы настраивать отдельные дни, примените SQL-скрипт overrides."
        };
    }

    public async Task<(bool Success, string Message)> UpdateDoctorAssignmentsAsync(string organizationId, DoctorAssignmentsUpsertDto dto)
    {
        var doctor = await _db.Users
            .Where(x => x.Organization == organizationId && (x.Isdeleted == null || x.Isdeleted == 0) && x.Id == dto.Id)
            .Include(x => x.UserDepartments)
            .Include(x => x.UserSpecializations)
            .FirstOrDefaultAsync();

        if (doctor == null)
            return (false, "Врач не найден.");

        var departmentIds = dto.SelectedDepartmentIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var specializationIds = dto.SelectedSpecializationIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var validDepartments = await _db.Departments
            .Where(x => x.OrganizationId == organizationId && departmentIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        var validSpecializations = await _db.Specializations
            .Where(x => x.OrganizationId == organizationId && specializationIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        if (validDepartments.Count != departmentIds.Count)
            return (false, "Часть выбранных отделений не найдена.");

        if (validSpecializations.Count != specializationIds.Count)
            return (false, "Часть выбранных специализаций не найдена.");

        if (!string.IsNullOrWhiteSpace(dto.PrimaryDepartmentId) && !departmentIds.Contains(dto.PrimaryDepartmentId))
            return (false, "Основное отделение должно входить в выбранный список.");

        if (!string.IsNullOrWhiteSpace(dto.PrimarySpecializationId) && !specializationIds.Contains(dto.PrimarySpecializationId))
            return (false, "Основная специализация должна входить в выбранный список.");

        var now = DateTime.Now;

        var existingDepartments = doctor.UserDepartments.ToList();
        foreach (var relation in existingDepartments.Where(x => !departmentIds.Contains(x.DepartmentId)))
            _db.UserDepartments.Remove(relation);

        foreach (var departmentId in departmentIds)
        {
            var relation = existingDepartments.FirstOrDefault(x => x.DepartmentId == departmentId);
            if (relation == null)
            {
                doctor.UserDepartments.Add(new UserDepartment
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = doctor.Id,
                    DepartmentId = departmentId,
                    IsPrimary = false,
                    CreatedAt = now
                });
            }
        }

        foreach (var relation in doctor.UserDepartments)
            relation.IsPrimary = relation.DepartmentId == dto.PrimaryDepartmentId;

        var existingSpecializations = doctor.UserSpecializations.ToList();
        foreach (var relation in existingSpecializations.Where(x => !specializationIds.Contains(x.SpecializationId)))
            _db.UserSpecializations.Remove(relation);

        foreach (var specializationId in specializationIds)
        {
            var relation = existingSpecializations.FirstOrDefault(x => x.SpecializationId == specializationId);
            if (relation == null)
            {
                doctor.UserSpecializations.Add(new UserSpecialization
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = doctor.Id,
                    SpecializationId = specializationId,
                    IsPrimary = false,
                    CreatedAt = now
                });
            }
        }

        foreach (var relation in doctor.UserSpecializations)
            relation.IsPrimary = relation.SpecializationId == dto.PrimarySpecializationId;

        await _db.SaveChangesAsync();
        return (true, "Привязки врача обновлены.");
    }

    public async Task<bool> DoctorSchedulesStorageReadyAsync()
    {
        return await TableExistsAsync("user_work_schedules");
    }

    public async Task<bool> DoctorScheduleOverridesStorageReadyAsync()
    {
        return await TableExistsAsync("user_work_schedule_overrides");
    }

    public async Task<(bool Success, string Message)> UpdateDoctorScheduleAsync(string organizationId, SaveUserWorkScheduleRequest dto)
    {
        if (!await DoctorSchedulesStorageReadyAsync())
            return (false, "Таблица базовых смен пользователей еще не создана. Сначала примените SQL-скрипт appointments.");

        if (string.IsNullOrWhiteSpace(dto.UserId))
            return (false, "Выберите врача.");

        if (!TimeSpan.TryParse(dto.StartTime, out var startTime))
            return (false, "Укажите корректное время начала смены.");

        if (!TimeSpan.TryParse(dto.EndTime, out var endTime))
            return (false, "Укажите корректное время окончания смены.");

        if (endTime <= startTime)
            return (false, "Время окончания смены должно быть позже времени начала.");

        var days = dto.DaysOfWeek
            .Where(x => x is >= 1 and <= 7)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (days.Count == 0)
            return (false, "Выберите хотя бы один рабочий день.");

        var userExists = await _db.Users.AnyAsync(x =>
            x.Id == dto.UserId &&
            x.Organization == organizationId &&
            (x.Isdeleted == null || x.Isdeleted == 0));

        if (!userExists)
            return (false, "Пользователь не найден в текущей организации.");

        var existing = await _db.UserWorkSchedules
            .Where(x => x.OrganizationId == organizationId && x.UserId == dto.UserId)
            .ToListAsync();

        _db.UserWorkSchedules.RemoveRange(existing);

        var now = DateTime.Now;
        foreach (var day in days)
        {
            _db.UserWorkSchedules.Add(new UserWorkSchedule
            {
                Id = Guid.NewGuid().ToString(),
                OrganizationId = organizationId,
                UserId = dto.UserId!,
                DayOfWeek = day,
                StartTime = startTime,
                EndTime = endTime,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _db.SaveChangesAsync();
        return (true, "Базовая смена врача сохранена.");
    }

    public async Task<(bool Success, string Message)> UpdateDoctorScheduleOverrideAsync(string organizationId, SaveUserWorkScheduleOverrideRequest dto)
    {
        if (!await DoctorScheduleOverridesStorageReadyAsync())
            return (false, "Таблица индивидуальных смен пользователей еще не создана. Сначала примените SQL-скрипт overrides.");

        if (string.IsNullOrWhiteSpace(dto.UserId))
            return (false, "Выберите врача.");

        if (dto.WorkDate == null)
            return (false, "Укажите дату.");

        var userExists = await _db.Users.AnyAsync(x =>
            x.Id == dto.UserId &&
            x.Organization == organizationId &&
            (x.Isdeleted == null || x.Isdeleted == 0));

        if (!userExists)
            return (false, "Пользователь не найден в текущей организации.");

        var workDate = dto.WorkDate.Value.Date;
        var mode = (dto.Mode ?? "default").Trim().ToLowerInvariant();
        var existing = await _db.UserWorkScheduleOverrides.FirstOrDefaultAsync(x =>
            x.OrganizationId == organizationId &&
            x.UserId == dto.UserId &&
            x.WorkDate == workDate);

        if (mode == "default")
        {
            if (existing != null)
            {
                _db.UserWorkScheduleOverrides.Remove(existing);
                await _db.SaveChangesAsync();
            }

            return (true, "Для выбранной даты восстановлен базовый график.");
        }

        TimeSpan? startTime = null;
        TimeSpan? endTime = null;
        var isWorking = mode == "custom";

        if (mode != "off" && mode != "custom")
            return (false, "Укажите корректный режим для выбранной даты.");

        if (isWorking)
        {
            if (!TimeSpan.TryParse(dto.StartTime, out var parsedStart))
                return (false, "Укажите корректное время начала смены.");

            if (!TimeSpan.TryParse(dto.EndTime, out var parsedEnd))
                return (false, "Укажите корректное время окончания смены.");

            if (parsedEnd <= parsedStart)
                return (false, "Время окончания смены должно быть позже времени начала.");

            startTime = parsedStart;
            endTime = parsedEnd;
        }

        var now = DateTime.Now;
        if (existing == null)
        {
            existing = new UserWorkScheduleOverride
            {
                Id = Guid.NewGuid().ToString(),
                OrganizationId = organizationId,
                UserId = dto.UserId!,
                WorkDate = workDate,
                CreatedAt = now
            };
            _db.UserWorkScheduleOverrides.Add(existing);
        }

        existing.IsWorking = isWorking;
        existing.StartTime = startTime;
        existing.EndTime = endTime;
        existing.Comment = dto.Comment?.Trim();
        existing.UpdatedAt = now;

        await _db.SaveChangesAsync();
        return (true, isWorking
            ? "Индивидуальная смена на выбранную дату сохранена."
            : "Для выбранной даты сохранен выходной.");
    }

    private async Task<bool> TableExistsAsync(string tableName)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                select count(*)
                from information_schema.tables
                where table_schema = 'public'
                  and table_name = '{tableName}'
                """;
            return Convert.ToInt32(await command.ExecuteScalarAsync()) >= 1;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}
