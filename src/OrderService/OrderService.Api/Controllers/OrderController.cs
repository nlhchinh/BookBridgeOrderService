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
    [Route("api/orders")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderServices _service;
        private readonly IPaymentService _paymentService;

        public OrderController(IOrderServices service, IPaymentService paymentService)
        {
            _service = service;
            _paymentService = paymentService;

        }

        [HttpGet]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 10)
        {
            var result = await _service.GetAll(page, pageSize);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var order = await _service.GetById(id);
            if (order == null) return NotFound();
            return Ok(order);
        }

        [HttpGet("customer/{customerId:guid}")]
        public async Task<IActionResult> GetByCustomer(Guid customerId, int page = 1, int pageSize = 10)
        {
            var result = await _service.GetOrderByCustomer(customerId, page, pageSize);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] OrderCreateRequest request)
        {
            var order = await _service.Create(request);
            return Ok(order);
        }

        [HttpPost("checkout")] // THAY ĐỔI URL cho luồng checkout
        public async Task<IActionResult> CreateFromCart([FromBody] OrderCreateRequest request)
        {
            // 1. Lấy CustomerId từ Token
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(customerIdClaim) || !Guid.TryParse(customerIdClaim, out var customerId))
            {
                return Unauthorized("Customer ID không hợp lệ.");
            }

            // 2. Lấy Access Token từ Header
            // Lưu ý: Tên header là "Authorization: Bearer <token>"
            var accessToken = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized("Không tìm thấy Access Token.");
            }

            try
            {
                // 3. Gọi service với checkoutRequest và accessToken
                var orders = await _service.CreateFromCart(customerId, request, accessToken);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id:guid}/initiate-payment")]
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

        // Provider callback/webhook endpoint - provider calls this (public endpoint)
        // Endpoint cho Provider callback/webhook
        [HttpPost("payment/callback")]
        [AllowAnonymous] // Callback từ Payment Provider không cần Token
        public async Task<IActionResult> PaymentCallback([FromForm] string transactionId)
        {
            // Lấy payload từ Request.Form
            var dict = new Dictionary<string, string>();
            foreach (var kv in Request.Form)
            {
                dict[kv.Key] = kv.Value;
            }

            var success = await _service.HandlePaymentCallback(transactionId, dict);
            if (!success)
                return BadRequest(new { message = "Xử lý callback thanh toán thất bại." });

            // Cổng thanh toán thường yêu cầu phản hồi "OK" hoặc "Success"
            return Ok("Success");
        }


        [HttpPut("{id:guid}/confirm")]
        public async Task<IActionResult> Confirm(Guid id)
        {
            // implement as you want (simplified)
            var order = await _service.GetById(id);
            if (order == null) return NotFound();
            // call repo update if needed...
            return Ok(new { message = "implemented elsewhere" });
        }

        [HttpPut("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            // simplified
            return Ok(new { message = "cancel endpoint not implemented in sample" });
        }

        [HttpGet("{orderId:guid}/payment-status")]
        public async Task<IActionResult> CheckPaymentStatus(Guid orderId)
        {
            var order = await _service.GetById(orderId);
            if (order == null) return NotFound();

            // Front-end sẽ dùng transactionId/orderId để kiểm tra
            if (order.PaymentStatus != PaymentStatus.Paid)
            {
                // Gọi service để kiểm tra lại với Payment Provider (nếu cần)
                var isPaid = await _service.UpdatePaymentStatusAfterScan(orderId, order.TransactionId);
                if (isPaid)
                    return Ok(new { status = "Paid", order.TransactionId });
            }

            return Ok(new { status = order.PaymentStatus.ToString(), order.TransactionId });
        }
    }
}
