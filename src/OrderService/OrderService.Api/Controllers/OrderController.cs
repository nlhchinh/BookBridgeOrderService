using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Interface;
using OrderService.Application.Models;
using OrderService.Domain.Entities;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OrderService.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/orders")] // 👉 rõ nghĩa hơn thay vì chỉ "api/orders"
    public class OrderController : ControllerBase
    {
        private readonly IOrderServices _service;
        private readonly IPaymentService _paymentService;

        public OrderController(IOrderServices service, IPaymentService paymentService)
        {
            _service = service;
            _paymentService = paymentService;
        }

        // 🔹 Lấy danh sách tất cả đơn
        [HttpGet("list")]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 10)
        {
            var result = await _service.GetAll(page, pageSize);
            return Ok(result);
        }

        // 🔹 Lấy chi tiết đơn hàng theo ID
        [HttpGet("details/{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var order = await _service.GetById(id);
            if (order == null) return NotFound();
            return Ok(order);
        }

        // 🔹 Lấy danh sách đơn hàng theo khách hàng
        [HttpGet("customer/{customerId:guid}/orders")]
        public async Task<IActionResult> GetByCustomer(Guid customerId, int page = 1, int pageSize = 10)
        {
            var result = await _service.GetOrderByCustomer(customerId, page, pageSize);
            return Ok(result);
        }

        // 🔹 Tạo đơn hàng thủ công (không qua giỏ hàng)
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] OrderCreateRequest request)
        {
            var order = await _service.Create(request);
            return Ok(order);
        }

        // 🔹 Tạo đơn hàng qua giỏ hàng (checkout flow)
        [HttpPost("checkout/create")]
        public async Task<IActionResult> CreateFromCart([FromBody] OrderCreateRequest request)
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(customerIdClaim) || !Guid.TryParse(customerIdClaim, out var customerId))
            {
                return Unauthorized("Customer ID không hợp lệ.");
            }

            var accessToken = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized("Không tìm thấy Access Token.");
            }

            try
            {
                var orders = await _service.CreateFromCart(customerId, request, accessToken);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // 🔹 Khởi tạo thanh toán
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

        // 🔹 Webhook / callback từ nhà cung cấp thanh toán
        [HttpPost("payment/provider-callback")]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentCallback([FromForm] string transactionId)
        {
            var dict = new Dictionary<string, string>();
            foreach (var kv in Request.Form)
            {
                dict[kv.Key] = kv.Value;
            }

            var success = await _service.HandlePaymentCallback(transactionId, dict);
            if (!success)
                return BadRequest(new { message = "Xử lý callback thanh toán thất bại." });

            return Ok("Success");
        }

        // 🔹 Xác nhận đơn hàng (admin / seller)
        [HttpPut("{id:guid}/confirm-order")]
        public async Task<IActionResult> Confirm(Guid id)
        {
            var order = await _service.GetById(id);
            if (order == null) return NotFound();
            return Ok(new { message = "implemented elsewhere" });
        }

        // 🔹 Hủy đơn hàng
        [HttpPut("{id:guid}/cancel-order")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            return Ok(new { message = "cancel endpoint not implemented in sample" });
        }

        // 🔹 Kiểm tra trạng thái thanh toán
        [HttpGet("{orderId:guid}/payment/status")]
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
