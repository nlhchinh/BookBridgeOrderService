using OrderService.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.Models
{
    public class OrderCreateRequest
    {
        // CustomerId được lấy từ Token/Path, nên không cần [Required] trong Body DTO

        public Guid CustomerId { get; set; }
        public string? CustomerEmail { get; set; }

        // Thông tin chung cho tất cả đơn hàng
        [Required, Phone]
        public string CustomerPhoneNumber { get; set; }

        [Required, MaxLength(255)]
        public string DeliveryAddress { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        public PaymentProvider? PaymentProvider { get; set; }

        // Danh sách các cửa hàng (Multi-Store)
        [Required(ErrorMessage = "Phải có ít nhất một cửa hàng trong checkout.")]
        [MinLength(1)]
        public List<StoreCheckoutDto> Stores { get; set; } = new();
    }
}