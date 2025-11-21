using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Table("transactions")]
public partial class Transaction
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("transaction_date", TypeName = "timestamp without time zone")]
    public DateTime? TransactionDate { get; set; }

    /// <summary>
    /// success
    /// error
    /// </summary>
    [Column("transaction_status", TypeName = "character varying")]
    public string? TransactionStatus { get; set; }

    /// <summary>
    /// сумма в тыйынах
    /// </summary>
    [Column("summ")]
    public decimal? Summ { get; set; }

    /// <summary>
    /// сумма транзакции в тыйынах вмесе с комиссией
    /// </summary>
    [Column("transaction_summ")]
    public decimal? TransactionSumm { get; set; }

    [Column("invoice", TypeName = "character varying")]
    public string? Invoice { get; set; }

    /// <summary>
    /// debit - приход
    /// credit - расход
    /// </summary>
    [Column("transaction_type", TypeName = "character varying")]
    public string? TransactionType { get; set; }

    [ForeignKey("Invoice")]
    [InverseProperty("Transactions")]
    public virtual Invoice? InvoiceNavigation { get; set; }
}
