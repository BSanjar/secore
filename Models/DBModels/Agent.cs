using System;
using System.Collections.Generic;

namespace WebApplication1.Models.DBModels;

public partial class Agent
{
    public string Id { get; set; } = null!;

    public string? ApiLogin { get; set; }

    public string? ApiPassword { get; set; }

    public string? Name { get; set; }

    /// <summary>
    /// Список разрешённых IP для API (через запятую/точку с запятой). Пусто/NULL = разрешить все.
    /// </summary>
    public string? AllowlistIp { get; set; }

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public virtual ICollection<AgentCommission> AgentCommissions { get; set; } = new List<AgentCommission>();
}
