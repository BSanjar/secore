using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.ViewModels.Services;

namespace WebApplication1.Services;

public interface IServiceCatalogService
{
    Task<IReadOnlyList<ServiceListItemViewModel>> GetServicesAsync(string organizationId, string? search);
    Task<ServiceUpsertDto?> GetServiceAsync(string organizationId, string id);
    Task<IReadOnlyList<SelectListItem>> GetSpecializationOptionsAsync(string organizationId, IEnumerable<string>? selectedIds = null);
    Task<(bool Success, string Message)> CreateAsync(string organizationId, ServiceUpsertDto dto);
    Task<(bool Success, string Message)> UpdateAsync(string organizationId, string id, ServiceUpsertDto dto);
    Task<BulkImportResult> ImportAsync(string organizationId, Stream stream);
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
                FixedAmount = ParsersHelper.TyiynToSom(x.ServiceSumm),
                MinAmount = ParsersHelper.TyiynToSom(x.MinSumm),
                MaxAmount = ParsersHelper.TyiynToSom(x.MaxSumm),
                SpecializationNames = x.ServiceSpecializations
                    .Select(s => s.Specialization.Name)
                    .OrderBy(name => name)
                    .ToList()
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
                FixedAmount = ParsersHelper.TyiynToSom(x.ServiceSumm),
                MinAmount = ParsersHelper.TyiynToSom(x.MinSumm),
                MaxAmount = ParsersHelper.TyiynToSom(x.MaxSumm),
                SpecializationIds = x.ServiceSpecializations.Select(s => s.SpecializationId).ToList()
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
                Text = x.Department != null ? x.Department.Name + " / " + x.Name : x.Name,
                Selected = selected.Contains(x.Id)
            })
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> CreateAsync(string organizationId, ServiceUpsertDto dto)
    {
        var validation = Validate(dto);
        if (!validation.Success)
            return validation;

        var service = new OrganizationService
        {
            Id = Guid.NewGuid().ToString(),
            Organization = organizationId,
            Name = dto.Name.Trim(),
            FixedSum = dto.IsFixed ? 1 : 0,
            ServiceSumm = dto.IsFixed ? ParsersHelper.SomToTyiyn(dto.FixedAmount) : null,
            MinSumm = dto.IsFixed ? null : ParsersHelper.SomToTyiyn(dto.MinAmount),
            MaxSumm = dto.IsFixed ? null : ParsersHelper.SomToTyiyn(dto.MaxAmount),
            Isdeleted = 0
        };

        _db.OrganizationServices.Add(service);
        await SyncSpecializationsAsync(organizationId, service.Id, dto.SpecializationIds);
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
        entity.ServiceSumm = dto.IsFixed ? ParsersHelper.SomToTyiyn(dto.FixedAmount) : null;
        entity.MinSumm = dto.IsFixed ? null : ParsersHelper.SomToTyiyn(dto.MinAmount);
        entity.MaxSumm = dto.IsFixed ? null : ParsersHelper.SomToTyiyn(dto.MaxAmount);
        await SyncSpecializationsAsync(organizationId, id, dto.SpecializationIds);

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

            var isFixed = ParseBool(Cell(worksheet, row, 2), true);
            var dto = new ServiceUpsertDto
            {
                Name = name,
                IsFixed = isFixed,
                FixedAmount = ParseDecimal(Cell(worksheet, row, 3)),
                MinAmount = ParseDecimal(Cell(worksheet, row, 4)),
                MaxAmount = ParseDecimal(Cell(worksheet, row, 5)),
                SpecializationIds = await ResolveSpecializationIdsAsync(organizationId, Cell(worksheet, row, 6))
            };

            var existing = await _db.OrganizationServices.FirstOrDefaultAsync(x =>
                x.Organization == organizationId &&
                x.Name == dto.Name.Trim() &&
                x.Isdeleted != 1);

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

    private async Task SyncSpecializationsAsync(string organizationId, string serviceId, IEnumerable<string>? specializationIds)
    {
        var selected = new HashSet<string>(
            specializationIds?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);

        var allowed = await _db.Specializations
            .Where(x => x.OrganizationId == organizationId && selected.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        selected = new HashSet<string>(allowed, StringComparer.Ordinal);

        var current = await _db.ServiceSpecializations
            .Where(x => x.OrganizationServiceId == serviceId)
            .ToListAsync();

        _db.ServiceSpecializations.RemoveRange(current.Where(x => !selected.Contains(x.SpecializationId)));

        var existing = current.Select(x => x.SpecializationId).ToHashSet(StringComparer.Ordinal);
        foreach (var specializationId in selected.Where(x => !existing.Contains(x)))
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

    private async Task<List<string>> ResolveSpecializationIdsAsync(string organizationId, string? specializations)
    {
        var names = SplitNames(specializations);
        if (names.Count == 0)
            return new List<string>();

        return await _db.Specializations
            .Where(x => x.OrganizationId == organizationId && names.Contains(x.Name))
            .Select(x => x.Id)
            .ToListAsync();
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

    private static string? Cell(IXLWorksheet worksheet, int row, int column)
    {
        var value = worksheet.Cell(row, column).GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static List<string> SplitNames(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();

    private static decimal? ParseDecimal(string? value) => decimal.TryParse(value, out var parsed) ? parsed : null;

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "да" or "фикс" or "фиксированная" => true,
            "0" or "false" or "no" or "нет" or "диапазон" => false,
            _ => defaultValue
        };
    }
}
