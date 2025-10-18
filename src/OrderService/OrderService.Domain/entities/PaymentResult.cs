namespace OrderService.Application.Models
{
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string PaymentUrl { get; set; } // url để frontend hiển thị QR / redirect
        public string TransactionId { get; set; }
        public string Message { get; set; }
    }
}
