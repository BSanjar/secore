using System;

namespace WebApplication1.Dtos;

public class StatementRowDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = "";
    public string TypeBadgeClass { get; set; } = "";
    public string TypeText { get; set; } = "";
    public string Description { get; set; } = "";
    public string? DocumentId { get; set; }
    public decimal? AccrualAmount { get; set; }
    public decimal? PaymentAmount { get; set; }
    public decimal Commission { get; set; }
    public decimal BalanceAfter { get; set; }
}

