namespace WebApplication1.Dtos;

public class TierDto
{
    public decimal AmountFrom { get; set; }
    public decimal AmountTo { get; set; }
    public decimal Rate { get; set; }
}

public class TransactionCommissionResult
{
    public decimal LowerCommissionFromOrg { get; set; }
    public decimal UpperCommissionFromAgent { get; set; }
    public decimal LowerCommissionToAgent { get; set; }
}
