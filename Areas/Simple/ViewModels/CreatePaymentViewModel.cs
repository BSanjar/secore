namespace WebApplication1.Areas.Simple.ViewModels
{
    public class CreatePaymentViewModel
    {
        public string? ClientId { get; set; }          
        public decimal AmountSom { get; set; } 
        public string? InvoiceName { get; set; }   
        public string? PayCode { get; set; }
    }
    public class PaymentListItemVm
    {
        public string TransactionId { get; set; } = null!;
        public DateTime? Date { get; set; }
        public string? ClientName { get; set; }
        public string? InvoiceName { get; set; }
        public decimal AmountSom { get; set; }
        public string? Status { get; set; }
    }
}
