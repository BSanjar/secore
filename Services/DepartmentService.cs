using System.Data;
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
    Task<(bool Success, string Message)> CreateAsync(string organizationId, DepartmentUpsertDto dto);
    Task<(bool Success, string Message)> UpdateAsync(string organizationId, string id, DepartmentUpsertDto dto);
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
                  and table_name in ('departments', 'specializations', 'user_departments', 'user_specializations')
                """;
            return Convert.ToInt32(await command.ExecuteScalarAsync()) >= 4;
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
                SortOrder = x.SortOrder
            })
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Success, string Message)> CreateAsync(string organizationId, DepartmentUpsertDto dto)
    {
        var validation = await ValidateAsync(organizationId, dto, null);
        if (!validation.Success)
            return validation;

        var now = DateTime.Now;
        _db.Departments.Add(new Department
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
        });
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
        await _db.SaveChangesAsync();
        return (true, "Изменения сохранены.");
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
}
