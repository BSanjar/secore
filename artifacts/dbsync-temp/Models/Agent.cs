using System;
using System.Collections.Generic;

namespace TempScaffold.Models;

public partial class Agent
{
    public string Id { get; set; } = null!;

    public string? Name { get; set; }

    public string? ApiLogin { get; set; }

    public string? ApiPsw { get; set; }

    public string? Allowlistip { get; set; }

    public virtual ICollection<AgentCommission> AgentCommissions { get; set; } = new List<AgentCommission>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
