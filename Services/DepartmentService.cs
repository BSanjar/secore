using System.Data;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Controllers;
using WebApplication1.Dtos;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Services;

public interface IDepartmentService
{
    Task<bool> StorageReadyAsync();
    Task<IReadOnlyList<DepartmentsController.DepartmentIndexItem>> GetDepartmentsAsync(string organizationId, string? search);
    Task<DepartmentUpsertDto?> GetDepartmentAsync(string organizationId, string id);
    Task<IReadOnlyList<SelectListItem>> GetSpecializationOptionsAsync(string organizationId, IEnumerable<string>? selectedIds = null);
    Task<(bool Success, string Message)> CreateAsync(string organizationId, DepartmentUpsertDto dto);
    Task<(bool Success, string Message)> UpdateAsync(string organizationId, string id, DepartmentUpsertDto dto);
    Task<BulkImportResult> ImportAsync(string organizationId, Stream stream);
}

public class DepartmentService : IDepartmentService
{
    private readonly AppDbContext _db;

    public DepartmentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> StorageReadyAsync()
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select count(*)
                from information_schema.tables
                where table_schema = 'public'
                  and table_name in ('departments', 'specializations', 'user_departments', 'user_specializations', 'service_specializations')
                """;
            return Convert.ToInt32(await command.ExecuteScalarAsync()) >= 5;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    public async Task<IReadOnlyList<DepartmentsController.DepartmentIndexItem>> GetDepartmentsAsync(string organizationId, string? search)
    {
        var normalizedSearch = search?.Trim();
        var query = _db.Departments.AsNoTracking().Where(x => x.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(x =>
                x.Name.Contains(normalizedSearch) ||
                (x.Code != null && x.Code.Contains(normalizedSearch)) ||
                (x.Description != null && x.Description.Contains(normalizedSearch)));
        }

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new DepartmentsController.DepartmentIndexItem
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                Description = x.Description,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder,
                SpecializationsCount = x.Specializations.Count(s => s.IsActive),
                DoctorsCount = x.UserDepartments.Count()
            })
            .ToListAsync();
    }

    public async Task<DepartmentUpsertDto?> GetDepartmentAsync(string organizationId, string id)
    {
        return await _db.Departments
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.Id == id)
            .Select(x => new DepartmentUpsertDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                Description = x.Description,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder,
                SpecializationIds = x.Specializations.Select(s => s.Id).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<SelectListItem>> GetSpecializationOptionsAsync(string organizationId, IEnumerable<string>? selectedIds = null)
    {
        var selected = new HashSet<string>(selectedIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        return await _db.Specializations
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id,
                Text = x.Name,
                Selected = selected.Contains(x.Id)
            })
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> CreateAsync(string organizationId, DepartmentUpsertDto dto)
    {
        var validation = await ValidateAsync(organizationId, dto, null);
        if (!validation.Success)
            return validation;

        var now = DateTime.Now;
        var department = new Department
        {
            Id = Guid.NewGuid().ToString(),
            OrganizationId = organizationId,
            Name = dto.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            IsActive = dto.IsActive,
            SortOrder = dto.SortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Departments.Add(department);
        await AssignSpecializationsAsync(organizationId, department.Id, dto.SpecializationIds);
        await _db.SaveChangesAsync();
        return (true, "Отделение создано.");
    }

    public async Task<(bool Success, string Message)> UpdateAsync(string organizationId, string id, DepartmentUpsertDto dto)
    {
        var entity = await _db.Departments.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id);
        if (entity == null)
            return (false, "Отделение не найдено.");

        var validation = await ValidateAsync(organizationId, dto, id);
        if (!validation.Success)
            return validation;

        entity.Name = dto.Name.Trim();
        entity.Code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.IsActive = dto.IsActive;
        entity.SortOrder = dto.SortOrder;
        entity.UpdatedAt = DateTime.Now;
        await AssignSpecializationsAsync(organizationId, id, dto.SpecializationIds);
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

            var dto = new DepartmentUpsertDto
            {
                Name = name,
                Code = Cell(worksheet, row, 2),
                Description = Cell(worksheet, row, 3),
                SortOrder = ParseInt(Cell(worksheet, row, 4)),
                IsActive = ParseBool(Cell(worksheet, row, 5), true)
            };

            var existing = await _db.Departments.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Name == dto.Name.Trim());
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

    private async Task AssignSpecializationsAsync(string organizationId, string departmentId, IEnumerable<string>? specializationIds)
    {
        var selected = new HashSet<string>(
            specializationIds?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);

        var current = await _db.Specializations
            .Where(x => x.OrganizationId == organizationId && x.DepartmentId == departmentId)
            .ToListAsync();

        foreach (var specialization in current.Where(x => !selected.Contains(x.Id)))
            specialization.DepartmentId = null;

        var toAssign = await _db.Specializations
            .Where(x => x.OrganizationId == organizationId && selected.Contains(x.Id))
            .ToListAsync();

        foreach (var specialization in toAssign)
            specialization.DepartmentId = departmentId;
    }

    private async Task<(bool Success, string Message)> ValidateAsync(string organizationId, DepartmentUpsertDto dto, string? excludeId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, "Укажите название отделения.");

        var exists = await _db.Departments.AnyAsync(x =>
            x.OrganizationId == organizationId &&
            x.Name == dto.Name.Trim() &&
            x.Id != excludeId);

        return exists ? (false, "Отделение с таким названием уже существует.") : (true, string.Empty);
    }

    private static string? Cell(IXLWorksheet worksheet, int row, int column)
    {
        var value = worksheet.Cell(row, column).GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

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
