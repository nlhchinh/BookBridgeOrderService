using OrderService.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.Models
{
    public class OrderCreateRequest
    {
        public Guid CustomerId { get; set; }

        public string? CustomerEmail { get; set; }

        [Required(ErrorMessage = "Cửa hàng là bắt buộc.")]
        public int BookstoreId { get; set; }

        [Required, Phone]
        public string CustomerPhoneNumber { get; set; }

        [Required, MaxLength(255)]
        public string DeliveryAddress { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        public PaymentProvider? PaymentProvider { get; set; }

        [Required, MinLength(1)]
        public List<CartItemDto> OrderItems { get; set; } = new();
    }
}
