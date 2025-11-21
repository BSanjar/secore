using Microsoft.EntityFrameworkCore;
using WebApplication1.Areas.Simple.ViewModels;
using WebApplication1.Models.DBModels;
using WebApplication1.Modules.GenericModule.Models;
using WebApplication1.Modules.GenericModule.Services;

namespace WebApplication1.Areas.Simple.Services
{
    public class SimplePaymentsTableSource : ITableSource<PaymentListItemVm>
    {
        private readonly AppDbContext _db;

        public SimplePaymentsTableSource(AppDbContext db)
        {
            _db = db;
        }

        public async Task<PagedResult<PaymentListItemVm>> GetDataAsync(BaseFilterParams baseFilters)
        {
            var filters = NormalizeFilters(baseFilters);

            var query = _db.Transactions
                .AsNoTracking()
                .Include(t => t.InvoiceNavigation!)
                    .ThenInclude(i => i.ClientNavigation)
                .Where(t => t.TransactionType == "debit");

            if (!string.IsNullOrWhiteSpace(filters.Status))
            {
                var normalizedStatus = filters.Status.Trim();
                query = query.Where(t =>
                    EF.Functions.ILike(t.TransactionStatus ?? string.Empty, normalizedStatus));
            }

            if (filters.DateFrom.HasValue)
            {
                var from = filters.DateFrom.Value.Date;
                query = query.Where(t => t.TransactionDate >= from);
            }

            if (filters.DateTo.HasValue)
            {
                var to = filters.DateTo.Value.Date.AddDays(1);
                query = query.Where(t => t.TransactionDate < to);
            }

            if (!string.IsNullOrWhiteSpace(filters.SearchTerm))
            {
                query = ApplySearch(query, filters);
            }

            query = ApplySorting(query, filters);

            var totalItems = await query.CountAsync();
            var skip = (filters.PageNumber - 1) * filters.PageSize;

            var items = await query
                .Skip(skip)
                .Take(filters.PageSize)
                .Select(t => new PaymentListItemVm
                {
                    TransactionId = t.Id,
                    Date = t.TransactionDate,
                    ClientName = t.InvoiceNavigation!.ClientNavigation!.ClientName,
                    InvoiceName = t.InvoiceNavigation.NameInvoice,
                    AmountSom = (t.Summ ?? 0) / 100m,
                    Status = t.TransactionStatus
                })
                .ToListAsync();

            return new PagedResult<PaymentListItemVm>
            {
                Items = items,
                PageNumber = filters.PageNumber,
                PageSize = filters.PageSize,
                TotalItems = totalItems
            };
        }

        private static SimplePaymentsFilterParams NormalizeFilters(BaseFilterParams baseFilters)
        {
            var filters = baseFilters as SimplePaymentsFilterParams ?? new SimplePaymentsFilterParams
            {
                SearchTerm = baseFilters.SearchTerm,
                SearchColumn = baseFilters.SearchColumn,
                SortColumn = baseFilters.SortColumn,
                SortDirection = baseFilters.SortDirection,
                PageNumber = baseFilters.PageNumber,
                PageSize = baseFilters.PageSize
            };

            if (filters.PageNumber <= 0)
            {
                filters.PageNumber = 1;
            }

            if (filters.PageSize <= 0)
            {
                filters.PageSize = 10;
            }

            filters.SortDirection = string.IsNullOrWhiteSpace(filters.SortDirection)
                ? "DESC"
                : filters.SortDirection.ToUpper();

            return filters;
        }

        private static IQueryable<Transaction> ApplySearch(
            IQueryable<Transaction> query,
            SimplePaymentsFilterParams filters)
        {
            var term = filters.SearchTerm!.Trim();
            var likeTerm = $"%{term}%";

            return filters.SearchColumn?.ToLower() switch
            {
                "clientname" => query.Where(t =>
                    t.InvoiceNavigation != null &&
                    t.InvoiceNavigation.ClientNavigation != null &&
                    EF.Functions.ILike(t.InvoiceNavigation.ClientNavigation.ClientName ?? string.Empty, likeTerm)),

                "invoicename" => query.Where(t =>
                    t.InvoiceNavigation != null &&
                    EF.Functions.ILike(t.InvoiceNavigation.NameInvoice ?? string.Empty, likeTerm)),

                "status" => query.Where(t =>
                    EF.Functions.ILike(t.TransactionStatus ?? string.Empty, likeTerm)),

                _ => query.Where(t =>
                    (t.InvoiceNavigation != null &&
                     t.InvoiceNavigation.ClientNavigation != null &&
                     EF.Functions.ILike(t.InvoiceNavigation.ClientNavigation.ClientName ?? string.Empty, likeTerm))
                    || (t.InvoiceNavigation != null &&
                        EF.Functions.ILike(t.InvoiceNavigation.NameInvoice ?? string.Empty, likeTerm))
                    || EF.Functions.ILike(t.TransactionStatus ?? string.Empty, likeTerm))
            };
        }

        private static IQueryable<Transaction> ApplySorting(
            IQueryable<Transaction> query,
            SimplePaymentsFilterParams filters)
        {
            var sortColumn = filters.SortColumn?.ToLower();
            var desc = string.Equals(filters.SortDirection, "DESC", StringComparison.OrdinalIgnoreCase);

            return sortColumn switch
            {
                "clientname" => desc
                    ? query.OrderByDescending(t => t.InvoiceNavigation!.ClientNavigation!.ClientName)
                    : query.OrderBy(t => t.InvoiceNavigation!.ClientNavigation!.ClientName),

                "invoicename" => desc
                    ? query.OrderByDescending(t => t.InvoiceNavigation!.NameInvoice)
                    : query.OrderBy(t => t.InvoiceNavigation!.NameInvoice),

                "amountsom" => desc
                    ? query.OrderByDescending(t => t.Summ ?? 0)
                    : query.OrderBy(t => t.Summ ?? 0),

                "status" => desc
                    ? query.OrderByDescending(t => t.TransactionStatus)
                    : query.OrderBy(t => t.TransactionStatus),

                _ => desc
                    ? query.OrderByDescending(t => t.TransactionDate)
                    : query.OrderBy(t => t.TransactionDate)
            };
        }
    }
}

