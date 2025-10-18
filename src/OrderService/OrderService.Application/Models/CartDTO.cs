namespace OrderService.Application.Models
{
    public class CartItemDto
    {
        public int BookId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class CartStoreDto
    {
        public int StoreId { get; set; }
        public List<CartItemDto> Items { get; set; } = new();
    }

    public class CartDto
    {
        public string CustomerId { get; set; }
        public string? CustomerPhoneNumber { get; set; }
        public string? DeliveryAddress { get; set; }
        public List<CartStoreDto> Stores { get; set; } = new();
    }
}
