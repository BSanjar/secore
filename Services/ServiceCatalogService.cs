using Microsoft.EntityFrameworkCore;
using WebApplication1.Dtos;
using WebApplication1.Models.DBModels;
using WebApplication1.ViewModels.Services;

namespace WebApplication1.Services;

public interface IServiceCatalogService
{
    Task<IReadOnlyList<ServiceListItemViewModel>> GetServicesAsync(string organizationId, string? search);
    Task<ServiceUpsertDto?> GetServiceAsync(string organizationId, string id);
    Task<(bool Success, string Message)> CreateAsync(string organizationId, ServiceUpsertDto dto);
    Task<(bool Success, string Message)> UpdateAsync(string organizationId, string id, ServiceUpsertDto dto);
}

public class ServiceCatalogService : IServiceCatalogService
{
    private readonly AppDbContext _db;

    public ServiceCatalogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ServiceListItemViewModel>> GetServicesAsync(string organizationId, string? search)
    {
        var normalizedSearch = search?.Trim();
        var query = _db.OrganizationServices
            .AsNoTracking()
            .Where(x => x.Organization == organizationId && x.Isdeleted != 1);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
            query = query.Where(x => x.Name != null && x.Name.Contains(normalizedSearch));

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new ServiceListItemViewModel
            {
                Id = x.Id,
                Name = x.Name ?? "Без названия",
                IsFixed = x.FixedSum == 1,
                FixedAmount = x.ServiceSumm,
                MinAmount = x.MinSumm,
                MaxAmount = x.MaxSumm
            })
            .ToListAsync();
    }

    public async Task<ServiceUpsertDto?> GetServiceAsync(string organizationId, string id)
    {
        return await _db.OrganizationServices
            .AsNoTracking()
            .Where(x => x.Organization == organizationId && x.Id == id && x.Isdeleted != 1)
            .Select(x => new ServiceUpsertDto
            {
                Id = x.Id,
                Name = x.Name ?? string.Empty,
                IsFixed = x.FixedSum == 1,
                FixedAmount = x.ServiceSumm,
                MinAmount = x.MinSumm,
                MaxAmount = x.MaxSumm
            })
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Success, string Message)> CreateAsync(string organizationId, ServiceUpsertDto dto)
    {
        var validation = Validate(dto);
        if (!validation.Success)
            return validation;

        _db.OrganizationServices.Add(new OrganizationService
        {
            Id = Guid.NewGuid().ToString(),
            Organization = organizationId,
            Name = dto.Name.Trim(),
            FixedSum = dto.IsFixed ? 1 : 0,
            ServiceSumm = dto.IsFixed ? dto.FixedAmount : null,
            MinSumm = dto.IsFixed ? null : dto.MinAmount,
            MaxSumm = dto.IsFixed ? null : dto.MaxAmount,
            Isdeleted = 0
        });

        await _db.SaveChangesAsync();
        return (true, "Услуга создана.");
    }

    public async Task<(bool Success, string Message)> UpdateAsync(string organizationId, string id, ServiceUpsertDto dto)
    {
        var entity = await _db.OrganizationServices
            .FirstOrDefaultAsync(x => x.Organization == organizationId && x.Id == id && x.Isdeleted != 1);

        if (entity == null)
            return (false, "Услуга не найдена.");

        var validation = Validate(dto);
        if (!validation.Success)
            return validation;

        entity.Name = dto.Name.Trim();
        entity.FixedSum = dto.IsFixed ? 1 : 0;
        entity.ServiceSumm = dto.IsFixed ? dto.FixedAmount : null;
        entity.MinSumm = dto.IsFixed ? null : dto.MinAmount;
        entity.MaxSumm = dto.IsFixed ? null : dto.MaxAmount;

        await _db.SaveChangesAsync();
        return (true, "Изменения сохранены.");
    }

    private static (bool Success, string Message) Validate(ServiceUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, "Укажите название услуги.");

        if (dto.IsFixed)
        {
            if (!dto.FixedAmount.HasValue)
                return (false, "Укажите фиксированную сумму.");
        }
        else
        {
            if (!dto.MinAmount.HasValue && !dto.MaxAmount.HasValue)
                return (false, "Укажите минимальную или максимальную сумму.");

            if (dto.MinAmount.HasValue && dto.MaxAmount.HasValue && dto.MinAmount > dto.MaxAmount)
                return (false, "Минимальная сумма не может быть больше максимальной.");
        }

        return (true, string.Empty);
    }
}
