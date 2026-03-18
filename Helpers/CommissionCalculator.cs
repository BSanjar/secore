using WebApplication1.Models.DBModels;

namespace WebApplication1.Helpers;

/// <summary>
/// Расчёт комиссии по правилам из справочника Commission.
/// Параметры комиссии (FixedAmount, MinFee, MaxFee, ступени) хранятся в сомах.
/// Вход: сумма в тыйынах. Выход: комиссия в тыйынах (для полей LowerCommissionFromOrg / UpperCommissionFromAgent / LowerCommissionToAgent в Transaction).
/// </summary>
public static class CommissionCalculator
{
    /// <summary>
    /// Вычисляет сумму комиссии в тыйынах для записи в поля комиссий Transaction.
    /// Внутри расчёт ведётся в сомах (1 сом = 100 тыйын).
    /// </summary>
    /// <param name="commission">Комиссия из справочника (с подгруженными Tiers при single_tier/progressive).</param>
    /// <param name="amountTyiyn">База: сумма в тыйынах (из Invoice/Transaction).</param>
    /// <returns>Сумма комиссии в тыйынах.</returns>
    public static decimal CalculateFee(Commission commission, decimal amountTyiyn)
    {
        if (commission == null) return 0;

        decimal amountSom = amountTyiyn / 100m;

        decimal feeSom = commission.CommissionKind switch
        {
            "percent" => amountSom * (commission.Rate ?? 0),
            "fixed" => commission.FixedAmount ?? 0,
            "mixed" => amountSom * (commission.Rate ?? 0) + (commission.FixedAmount ?? 0),
            "single_tier" => CalculateSingleTier(commission, amountSom),
            "progressive" => CalculateProgressive(commission, amountSom),
            _ => 0
        };

        if (commission.MinFee.HasValue && feeSom < commission.MinFee.Value)
            feeSom = commission.MinFee.Value;
        if (commission.MaxFee.HasValue && feeSom > commission.MaxFee.Value)
            feeSom = commission.MaxFee.Value;

        return Math.Round(feeSom * 100m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Одна ставка по диапазону: ступени в сомах, ставка применяется ко всей сумме (в сомах).
    /// </summary>
    private static decimal CalculateSingleTier(Commission commission, decimal amountSom)
    {
        var tiers = commission.Tiers?.OrderBy(t => t.SortOrder).ToList() ?? new List<CommissionTier>();
        var tier = tiers.FirstOrDefault(t => amountSom >= t.AmountFrom && amountSom <= t.AmountTo);
        if (tier == null)
            return commission.Rate.HasValue ? amountSom * commission.Rate.Value : 0;
        return amountSom * tier.Rate;
    }

    /// <summary>
    /// Прогрессивная: ступени в сомах, каждая ступень применяется к своей части суммы (в сомах).
    /// </summary>
    private static decimal CalculateProgressive(Commission commission, decimal amountSom)
    {
        var tiers = commission.Tiers?.OrderBy(t => t.SortOrder).ToList() ?? new List<CommissionTier>();
        decimal totalFee = 0;
        foreach (var t in tiers)
        {
            if (amountSom <= t.AmountFrom) continue;
            decimal portion = Math.Min(t.AmountTo, amountSom) - t.AmountFrom;
            if (portion <= 0) continue;
            totalFee += portion * t.Rate;
        }
        return totalFee;
    }
}
