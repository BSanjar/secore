namespace WebApplication1.Dtos;

public class SaveAppointmentRequest
{
    public string? AppointmentId { get; set; }
    public string? PatientId { get; set; }
    public string? PatientName { get; set; }
    public string? DoctorId { get; set; }
    public string? Title { get; set; }
    public string? Phone { get; set; }
    public bool UsePhoneAsWhatsApp { get; set; }
    public string? Email { get; set; }
    public string? Comment { get; set; }
    public string? PatientGender { get; set; }
    public string? PatientBirthDate { get; set; }
    public string? ReferralSource { get; set; }
    public string? PaymentType { get; set; }
    public string? AppointmentStatus { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? AppointmentDate { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public List<AppointmentServiceItemRequest> Services { get; set; } = new();
}

public class AppointmentServiceItemRequest
{
    public string? OrganizationServiceId { get; set; }
    public string? ServiceName { get; set; }
    public decimal? PriceTyiyn { get; set; }
    public int Quantity { get; set; } = 1;
}

public class GenerateAppointmentInvoiceRequest
{
    public string? AppointmentId { get; set; }
}

public class MarkAppointmentPaidRequest
{
    public string? AppointmentId { get; set; }
}

public class CancelAppointmentRequest
{
    public string? AppointmentId { get; set; }

    /// <summary>
    /// При отмене оплаченной записи — создать возвратные транзакции и снять оплату со счёта.
    /// </summary>
    public bool RefundPayment { get; set; }
}

public class SendAppointmentInvoiceWhatsAppRequest
{
    public string? AppointmentId { get; set; }
    public string? InvoiceId { get; set; }
}
