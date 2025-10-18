using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.Models
{
    public class StoreCheckoutDto
    {
        [Required(ErrorMessage = "Cửa hàng là bắt buộc.")]
        public int BookstoreId { get; set; } // Giả sử BookstoreId là Guid

        [Required(ErrorMessage = "Phải có ít nhất một mặt hàng.")]
        [MinLength(1)]
        public List<CartItemDto> OrderItems { get; set; } = new();
    }
}