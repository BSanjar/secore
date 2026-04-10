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
    Task<(bool Success, string Message)> CreateAsync(string organizationId, SpecializationUpsertDto dto);
    Task<(bool Success, string Message)> UpdateAsync(string organizationId, string id, SpecializationUpsertDto dto);
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
                DoctorsCount = x.UserSpecializations.Count()
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
                SortOrder = x.SortOrder
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

    public async Task<(bool Success, string Message)> CreateAsync(string organizationId, SpecializationUpsertDto dto)
    {
        var validation = await ValidateAsync(organizationId, dto, null);
        if (!validation.Success)
            return validation;

        var now = DateTime.Now;
        _db.Specializations.Add(new Specialization
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
        });
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
        await _db.SaveChangesAsync();
        return (true, "Изменения сохранены.");
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
}
