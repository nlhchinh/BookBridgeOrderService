namespace OrderService.Application.Models
{
    public class PaymentResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? PaymentUrl { get; set; }
        public string? TransactionId { get; set; }
    }
}