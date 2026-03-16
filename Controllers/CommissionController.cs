using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Controllers;

[RequireAuth]
[RequirePermission("settings.tariff")]
public class CommissionController : Controller
{
    private readonly AppDbContext _db;

    public CommissionController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var list = await _db.Commissions
            .Include(c => c.Tiers.OrderBy(t => t.SortOrder))
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View(list);
    }

    public IActionResult Create()
    {
        return View(new Commission());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string? Name,
        string CommissionKind,
        decimal? Rate,
        decimal? FixedAmount,
        decimal? MinFee,
        decimal? MaxFee,
        string? TiersJson)
    {
        if (string.IsNullOrWhiteSpace(CommissionKind))
        {
            ModelState.AddModelError("CommissionKind", "Укажите вид комиссии.");
            return View(new Commission { Name = Name, CommissionKind = CommissionKind ?? "percent", Rate = Rate, FixedAmount = FixedAmount, MinFee = MinFee, MaxFee = MaxFee });
        }

        var rate = ParseDecimalFromForm(Request.Form["Rate"]);
        var fixedAmount = ParseDecimalFromForm(Request.Form["FixedAmount"]);
        var minFee = ParseDecimalFromForm(Request.Form["MinFee"]);
        var maxFee = ParseDecimalFromForm(Request.Form["MaxFee"]);

        var id = Guid.NewGuid().ToString();
        var commission = new Commission
        {
            Id = id,
            Name = Name,
            CommissionKind = CommissionKind,
            Rate = rate ?? Rate,
            FixedAmount = fixedAmount ?? FixedAmount,
            MinFee = minFee ?? MinFee,
            MaxFee = maxFee ?? MaxFee
        };

        _db.Commissions.Add(commission);

        if ((CommissionKind == "single_tier" || CommissionKind == "progressive") && !string.IsNullOrWhiteSpace(TiersJson))
        {
            try
            {
                var tiers = System.Text.Json.JsonSerializer.Deserialize<List<TierDto>>(TiersJson);
                if (tiers != null && tiers.Count > 0)
                {
                    int order = 0;
                    foreach (var t in tiers.OrderBy(x => x.AmountFrom))
                    {
                        _db.CommissionTiers.Add(new CommissionTier
                        {
                            Id = Guid.NewGuid().ToString(),
                            CommissionId = id,
                            AmountFrom = t.AmountFrom,
                            AmountTo = t.AmountTo,
                            Rate = t.Rate,
                            SortOrder = ++order
                        });
                    }
                }
            }
            catch { /* ignore invalid json */ }
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Комиссия создана.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var c = await _db.Commissions.Include(x => x.Tiers.OrderBy(t => t.SortOrder)).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        return View(c);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string Id,
        string? Name,
        string CommissionKind,
        decimal? Rate,
        decimal? FixedAmount,
        decimal? MinFee,
        decimal? MaxFee,
        string? TiersJson)
    {
        var commission = await _db.Commissions.Include(x => x.Tiers).FirstOrDefaultAsync(x => x.Id == Id);
        if (commission == null) return NotFound();

        var rate = ParseDecimalFromForm(Request.Form["Rate"]);
        var fixedAmount = ParseDecimalFromForm(Request.Form["FixedAmount"]);
        var minFee = ParseDecimalFromForm(Request.Form["MinFee"]);
        var maxFee = ParseDecimalFromForm(Request.Form["MaxFee"]);

        commission.Name = Name;
        commission.CommissionKind = CommissionKind ?? commission.CommissionKind;
        commission.Rate = rate ?? Rate;
        commission.FixedAmount = fixedAmount ?? FixedAmount;
        commission.MinFee = minFee ?? MinFee;
        commission.MaxFee = maxFee ?? MaxFee;

        var existingTiers = commission.Tiers.ToList();
        foreach (var t in existingTiers)
            _db.CommissionTiers.Remove(t);

        if ((CommissionKind == "single_tier" || CommissionKind == "progressive") && !string.IsNullOrWhiteSpace(TiersJson))
        {
            try
            {
                var tiers = System.Text.Json.JsonSerializer.Deserialize<List<TierDto>>(TiersJson);
                if (tiers != null && tiers.Count > 0)
                {
                    int order = 0;
                    foreach (var t in tiers.OrderBy(x => x.AmountFrom))
                    {
                        _db.CommissionTiers.Add(new CommissionTier
                        {
                            Id = Guid.NewGuid().ToString(),
                            CommissionId = commission.Id,
                            AmountFrom = t.AmountFrom,
                            AmountTo = t.AmountTo,
                            Rate = t.Rate,
                            SortOrder = ++order
                        });
                    }
                }
            }
            catch { }
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Комиссия сохранена.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var c = await _db.Commissions.FindAsync(id);
        if (c == null) return NotFound();
        _db.Commissions.Remove(c);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Комиссия удалена.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Парсит decimal из формы (поддержка и точки, и запятой).</summary>
    private static decimal? ParseDecimalFromForm(Microsoft.Extensions.Primitives.StringValues value)
    {
        var s = value.ToString();
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim().Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private class TierDto
    {
        public decimal AmountFrom { get; set; }
        public decimal AmountTo { get; set; }
        public decimal Rate { get; set; }
    }
}
