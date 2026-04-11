using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Controllers;
using WebApplication1.Dtos;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Services;

public interface ISpecializationService
{
    Task<IReadOnlyList<SpecializationsController.SpecializationIndexItem>> GetSpecializationsAsync(string organizationId, string? search);
    Task<SpecializationUpsertDto?> GetSpecializationAsync(string organizationId, string id);
    Task<IReadOnlyList<SelectListItem>> GetDepartmentOptionsAsync(string organizationId, string? selectedDepartmentId = null);
    Task<IReadOnlyList<SelectListItem>> GetServiceOptionsAsync(string organizationId, IEnumerable<string>? selectedIds = null);
    Task<(bool Success, string Message)> CreateAsync(string organizationId, SpecializationUpsertDto dto);
    Task<(bool Success, string Message)> UpdateAsync(string organizationId, string id, SpecializationUpsertDto dto);
    Task<BulkImportResult> ImportAsync(string organizationId, Stream stream);
}

public class SpecializationService : ISpecializationService
{
    private readonly AppDbContext _db;

    public SpecializationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SpecializationsController.SpecializationIndexItem>> GetSpecializationsAsync(string organizationId, string? search)
    {
        var normalizedSearch = search?.Trim();
        var query = _db.Specializations
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Include(x => x.Department)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(x =>
                x.Name.Contains(normalizedSearch) ||
                (x.Description != null && x.Description.Contains(normalizedSearch)) ||
                (x.Department != null && x.Department.Name.Contains(normalizedSearch)));
        }

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new SpecializationsController.SpecializationIndexItem
            {
                Id = x.Id,
                Name = x.Name,
                DepartmentName = x.Department != null ? x.Department.Name : null,
                Description = x.Description,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder,
                DoctorsCount = x.UserSpecializations.Count(),
                ServicesCount = x.ServiceSpecializations.Count()
            })
            .ToListAsync();
    }

    public async Task<SpecializationUpsertDto?> GetSpecializationAsync(string organizationId, string id)
    {
        return await _db.Specializations
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.Id == id)
            .Select(x => new SpecializationUpsertDto
            {
                Id = x.Id,
                Name = x.Name,
                DepartmentId = x.DepartmentId,
                Description = x.Description,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder,
                ServiceIds = x.ServiceSpecializations.Select(s => s.OrganizationServiceId).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<SelectListItem>> GetDepartmentOptionsAsync(string organizationId, string? selectedDepartmentId = null)
    {
        var options = await _db.Departments
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id,
                Text = x.Name,
                Selected = x.Id == selectedDepartmentId
            })
            .ToListAsync();

        options.Insert(0, new SelectListItem
        {
            Value = string.Empty,
            Text = "Без отделения",
            Selected = string.IsNullOrWhiteSpace(selectedDepartmentId)
        });

        return options;
    }

    public async Task<IReadOnlyList<SelectListItem>> GetServiceOptionsAsync(string organizationId, IEnumerable<string>? selectedIds = null)
    {
        var selected = new HashSet<string>(selectedIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        return await _db.OrganizationServices
            .AsNoTracking()
            .Where(x => x.Organization == organizationId && x.Isdeleted != 1)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id,
                Text = x.Name ?? "Без названия",
                Selected = selected.Contains(x.Id)
            })
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> CreateAsync(string organizationId, SpecializationUpsertDto dto)
    {
        var validation = await ValidateAsync(organizationId, dto, null);
        if (!validation.Success)
            return validation;

        var now = DateTime.Now;
        var specialization = new Specialization
        {
            Id = Guid.NewGuid().ToString(),
            OrganizationId = organizationId,
            Name = dto.Name.Trim(),
            DepartmentId = string.IsNullOrWhiteSpace(dto.DepartmentId) ? null : dto.DepartmentId,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            IsActive = dto.IsActive,
            SortOrder = dto.SortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Specializations.Add(specialization);
        await SyncServicesAsync(organizationId, specialization.Id, dto.ServiceIds);
        await _db.SaveChangesAsync();
        return (true, "Специализация создана.");
    }

    public async Task<(bool Success, string Message)> UpdateAsync(string organizationId, string id, SpecializationUpsertDto dto)
    {
        var entity = await _db.Specializations.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id);
        if (entity == null)
            return (false, "Специализация не найдена.");

        var validation = await ValidateAsync(organizationId, dto, id);
        if (!validation.Success)
            return validation;

        entity.Name = dto.Name.Trim();
        entity.DepartmentId = string.IsNullOrWhiteSpace(dto.DepartmentId) ? null : dto.DepartmentId;
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.IsActive = dto.IsActive;
        entity.SortOrder = dto.SortOrder;
        entity.UpdatedAt = DateTime.Now;
        await SyncServicesAsync(organizationId, id, dto.ServiceIds);
        await _db.SaveChangesAsync();
        return (true, "Изменения сохранены.");
    }

    public async Task<BulkImportResult> ImportAsync(string organizationId, Stream stream)
    {
        var result = new BulkImportResult();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

        for (var row = 2; row <= lastRow; row++)
        {
            var name = Cell(worksheet, row, 1);
            if (string.IsNullOrWhiteSpace(name))
            {
                result.Skipped++;
                continue;
            }

            var departmentName = Cell(worksheet, row, 2);
            var departmentId = await ResolveDepartmentIdAsync(organizationId, departmentName);
            var serviceIds = await ResolveServiceIdsAsync(organizationId, Cell(worksheet, row, 6));
            var dto = new SpecializationUpsertDto
            {
                Name = name,
                DepartmentId = departmentId,
                Description = Cell(worksheet, row, 3),
                SortOrder = ParseInt(Cell(worksheet, row, 4)),
                IsActive = ParseBool(Cell(worksheet, row, 5), true),
                ServiceIds = serviceIds
            };

            var existing = await _db.Specializations.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Name == dto.Name.Trim());
            if (existing == null)
            {
                var create = await CreateAsync(organizationId, dto);
                if (create.Success) result.Created++;
                else result.Errors.Add($"Строка {row}: {create.Message}");
            }
            else
            {
                var update = await UpdateAsync(organizationId, existing.Id, dto);
                if (update.Success) result.Updated++;
                else result.Errors.Add($"Строка {row}: {update.Message}");
            }
        }

        return result;
    }

    private async Task SyncServicesAsync(string organizationId, string specializationId, IEnumerable<string>? serviceIds)
    {
        var selected = new HashSet<string>(
            serviceIds?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);

        var allowed = await _db.OrganizationServices
            .Where(x => x.Organization == organizationId && x.Isdeleted != 1 && selected.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        selected = new HashSet<string>(allowed, StringComparer.Ordinal);

        var current = await _db.ServiceSpecializations
            .Where(x => x.SpecializationId == specializationId)
            .ToListAsync();

        _db.ServiceSpecializations.RemoveRange(current.Where(x => !selected.Contains(x.OrganizationServiceId)));

        var existing = current.Select(x => x.OrganizationServiceId).ToHashSet(StringComparer.Ordinal);
        foreach (var serviceId in selected.Where(x => !existing.Contains(x)))
        {
            _db.ServiceSpecializations.Add(new ServiceSpecialization
            {
                Id = Guid.NewGuid().ToString(),
                OrganizationServiceId = serviceId,
                SpecializationId = specializationId,
                CreatedAt = DateTime.Now
            });
        }
    }

    private async Task<string?> ResolveDepartmentIdAsync(string organizationId, string? departmentName)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
            return null;

        return await _db.Departments
            .Where(x => x.OrganizationId == organizationId && x.Name == departmentName.Trim())
            .Select(x => x.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<List<string>> ResolveServiceIdsAsync(string organizationId, string? services)
    {
        var names = SplitNames(services);
        if (names.Count == 0)
            return new List<string>();

        return await _db.OrganizationServices
            .Where(x => x.Organization == organizationId && x.Isdeleted != 1 && x.Name != null && names.Contains(x.Name))
            .Select(x => x.Id)
            .ToListAsync();
    }

    private async Task<(bool Success, string Message)> ValidateAsync(string organizationId, SpecializationUpsertDto dto, string? excludeId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, "Укажите название специализации.");

        if (!string.IsNullOrWhiteSpace(dto.DepartmentId))
        {
            var departmentExists = await _db.Departments.AnyAsync(x => x.OrganizationId == organizationId && x.Id == dto.DepartmentId);
            if (!departmentExists)
                return (false, "Выбранное отделение не найдено.");
        }

        var exists = await _db.Specializations.AnyAsync(x =>
            x.OrganizationId == organizationId &&
            x.Name == dto.Name.Trim() &&
            x.Id != excludeId);

        return exists ? (false, "Специализация с таким названием уже существует.") : (true, string.Empty);
    }

    private static string? Cell(IXLWorksheet worksheet, int row, int column)
    {
        var value = worksheet.Cell(row, column).GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static List<string> SplitNames(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();

    private static int ParseInt(string? value) => int.TryParse(value, out var parsed) ? parsed : 0;

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "да" or "активно" => true,
            "0" or "false" or "no" or "нет" or "скрыто" => false,
            _ => defaultValue
        };
    }
}
