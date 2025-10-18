using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OrderService.Application.Interface;
using OrderService.Application.Models;

namespace OrderService.Application.Services.External
{
    public class CartClient : ICartClient
    {
        private readonly HttpClient _http;

        public CartClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<CartDto?> GetCartAsync(string customerId, string accessToken) // THAY ĐỔI
        {
            // THÊM: Thiết lập Authorization Header
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // GET api/cart/{customerId}
            var resp = await _http.GetAsync($"/api/cart/{customerId}");

            if (!resp.IsSuccessStatusCode)
            {
                // Bạn có thể log response body/status code để debug
                return null;
            }

            // Đảm bảo CartDto có thể deserialize đúng
            var content = await resp.Content.ReadAsStringAsync();
            var cart = JsonSerializer.Deserialize<CartDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return cart;
        }

        public async Task<bool> ClearCartAsync(string customerId, string accessToken) // THAY ĐỔI
        {
            // THÊM: Thiết lập Authorization Header
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // DELETE api/cart/{customerId}
            var resp = await _http.DeleteAsync($"/api/cart/{customerId}");
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> ClearCartStoreAsync(string customerId, int storeId, string accessToken)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            // optional: delete for a store
            var resp = await _http.DeleteAsync($"/api/cart/{customerId}/store/{storeId}");
            return resp.IsSuccessStatusCode;
        }
    }
}
