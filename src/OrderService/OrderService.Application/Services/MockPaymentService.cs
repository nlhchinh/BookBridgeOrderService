using OrderService.Application.Interface;
using OrderService.Application.Models;
using OrderService.Domain.Entities;

namespace OrderService.Application.Services.Payment
{
    public class MockPaymentService : IPaymentService
    {
        // Mock provider: generate a fake URL representing QR or payment page
        public Task<PaymentResult> InitiatePaymentAsync(Order order)
        {
            // Create a transaction id
            var tx = $"TX-{Guid.NewGuid():N}";
            // Simulate a QR url or payment link
            var paymentUrl = $"https://pay.fake/{tx}?amount={order.TotalPrice}&order={order.OrderNumber}";
            return Task.FromResult(new PaymentResult
            {
                Success = true,
                PaymentUrl = paymentUrl,
                TransactionId = tx,
                Message = "Mock payment created"
            });
        }

        public Task<PaymentResult> HandleCallbackAsync(string transactionId, IDictionary<string, string> payload)
        {
            // In real: verify signature, check provider status -> here accept if payload contains status=success
            payload.TryGetValue("status", out var status);
            var success = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(new PaymentResult
            {
                Success = success,
                TransactionId = transactionId,
                PaymentUrl = null,
                Message = success ? "Payment succeeded (mock)" : "Payment failed (mock)"
            });
        }

        public Task<bool> CheckTransactionStatusAsync(string transactionId)
        {
            throw new NotImplementedException();
        }
    }
}
