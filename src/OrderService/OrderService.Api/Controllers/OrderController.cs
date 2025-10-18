using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Interface;
using OrderService.Application.Models;
using OrderService.Domain.Entities;
using System.Security.Claims;

namespace OrderService.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/orders")]
    public class OrderController : BaseApiController
    {
        private readonly IOrderServices _service;
        private readonly IPaymentService _paymentService;

        public OrderController(IOrderServices service, IPaymentService paymentService)
        {
            _service = service;
            _paymentService = paymentService;
        }

        // ==========================
        // 🔹 Helper methods
        // ==========================
        private Guid GetCustomerId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("nameid");
            return Guid.TryParse(id, out var guid) ? guid : Guid.Empty;
        }

        private string GetEmail() =>
            User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email")
            ?? string.Empty;

        // ==========================
        // 🔹 GET: Danh sách đơn hàng
        // ==========================
        [HttpGet("list")]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 10)
        {
            var result = await _service.GetAll(page, pageSize);
            return Ok(result);
        }

        // 🔹 GET: Chi tiết đơn hàng
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var order = await _service.GetById(id);
            return order is null ? NotFound() : Ok(order);
        }

        // 🔹 GET: Đơn hàng theo khách hàng
        [HttpGet("by-customer/{customerId:guid}")]
        public async Task<IActionResult> GetByCustomer(Guid customerId, int page = 1, int pageSize = 10)
        {
            var result = await _service.GetOrderByCustomer(customerId, page, pageSize);
            return Ok(result);
        }

        // ==========================
        // 🔹 POST: Tạo đơn hàng thủ công
        // ==========================
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] OrderCreateRequest request)
        {
            var order = await _service.Create(request);
            return Ok(order);
        }

        // Tạo đơn hàng qua giỏ hàng (checkout flow)
        [HttpPost("checkout/create")]
        public async Task<IActionResult> CreateFromCart([FromBody] OrderCreateRequest request)
        {
            Guid customerId;
            string customerEmail;

            try
            {
                // ✅ Lấy từ BaseApiController (đã kế thừa)
                customerId = GetCustomerId();
                customerEmail = GetCustomerEmail();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            var accessToken = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            if (string.IsNullOrEmpty(accessToken))
                return Unauthorized("Không tìm thấy Access Token.");

            try
            {
                // Gắn email & id vào request luôn (client không cần gửi)
                request.CustomerId = customerId;
                request.CustomerEmail = customerEmail;

                var orders = await _service.CreateFromCart(customerId, request, accessToken);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        // ==========================
        // 🔹 POST: Khởi tạo thanh toán
        // ==========================
        [HttpPost("{id:guid}/payment/initiate")]
        public async Task<IActionResult> InitiatePayment(Guid id)
        {
            var order = await _service.InitiatePayment(id);
            return Ok(new
            {
                order.Id,
                order.OrderNumber,
                order.TotalPrice,
                order.PaymentUrl,
                order.TransactionId,
                order.PaymentStatus
            });
        }

        // 🔹 POST: Callback từ nhà cung cấp thanh toán
        [AllowAnonymous]
        [HttpPost("payment/callback")]
        public async Task<IActionResult> PaymentCallback([FromForm] string transactionId)
        {
            var dict = Request.Form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

            var success = await _service.HandlePaymentCallback(transactionId, dict);
            if (!success)
                return BadRequest(new { message = "Xử lý callback thanh toán thất bại." });

            return Ok("Success");
        }

        // ==========================
        // 🔹 PUT: Xác nhận / Hủy đơn
        // ==========================
        [HttpPut("{id:guid}/confirm")]
        [Authorize(Roles = "Admin,Seller")]
        public async Task<IActionResult> Confirm(Guid id)
        {
            var order = await _service.GetById(id);
            if (order == null) return NotFound();

            return Ok(new { message = "implemented elsewhere" });
        }

        [HttpPut("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            return Ok(new { message = "cancel endpoint not implemented in sample" });
        }

        // ==========================
        // 🔹 POST: Kiểm tra trạng thái thanh toán
        // ==========================
        [HttpPost("{orderId:guid}/payment/check-status")]
        public async Task<IActionResult> CheckPaymentStatus(Guid orderId)
        {
            var order = await _service.GetById(orderId);
            if (order == null) return NotFound();

            if (order.PaymentStatus != PaymentStatus.Paid)
            {
                var isPaid = await _service.UpdatePaymentStatusAfterScan(orderId, order.TransactionId);
                if (isPaid)
                    return Ok(new { status = "Paid", order.TransactionId });
            }

            return Ok(new { status = order.PaymentStatus.ToString(), order.TransactionId });
        }
    }
}
