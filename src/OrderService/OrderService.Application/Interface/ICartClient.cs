using OrderService.Application.Models;
using System.Threading.Tasks;

namespace OrderService.Application.Interface
{
    public interface ICartClient
    {
        Task<CartDto?> GetCartAsync(string customerId, string accessToken);
        Task<bool> ClearCartAsync(string customerId, string accessToken);
        Task<bool> ClearCartStoreAsync(string customerId, int storeId, string accessToken);
    }
}
