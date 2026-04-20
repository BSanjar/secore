using System;
using System.Collections.Generic;

namespace TempScaffold.Models;

/// <summary>
/// Справочник видов комиссии (верхняя/нижняя)
/// </summary>
public partial class Commission
{
    public string Id { get; set; } = null!;

    public string? Name { get; set; }

    /// <summary>
    /// percent | fixed | mixed | progressive
    /// </summary>
    public string CommissionKind { get; set; } = null!;

    public decimal? Rate { get; set; }

    public decimal? FixedAmount { get; set; }

    public decimal? MinFee { get; set; }

    public decimal? MaxFee { get; set; }

    public virtual ICollection<AgentCommission> AgentCommissionCommissions { get; set; } = new List<AgentCommission>();

    public virtual ICollection<AgentCommission> AgentCommissionLowerCommissions { get; set; } = new List<AgentCommission>();

    public virtual ICollection<CommissionTier> CommissionTiers { get; set; } = new List<CommissionTier>();

    public virtual ICollection<OrganizationSetting> OrganizationSettings { get; set; } = new List<OrganizationSetting>();
}
