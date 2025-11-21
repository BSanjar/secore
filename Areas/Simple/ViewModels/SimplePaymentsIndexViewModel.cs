using System;
using WebApplication1.Modules.GenericModule.Models;

namespace WebApplication1.Areas.Simple.ViewModels
{
    public class SimplePaymentsIndexViewModel
    {
        public GenericTableViewModel<object> Table { get; set; } = new();
        public string CreatePaymentUrl { get; set; } = string.Empty;
        public string? FlashMessage { get; set; }
    }

    public class SimplePaymentsFilterParams : BaseFilterParams
    {
        public string? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }
}

