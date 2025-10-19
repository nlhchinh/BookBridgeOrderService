namespace OrderService.Application.Models
{
    public class VNPayConfig
    {
        public string TmnCode { get; set; }
        public string HashSecret { get; set; }
        public string BaseUrl { get; set; } // URL Khởi tạo thanh toán
        public string QueryUrl { get; set; } // URL Vấn tin giao dịch
        public string ReturnUrl { get; set; } // URL Callback (từ Controller của bạn)
    }
}