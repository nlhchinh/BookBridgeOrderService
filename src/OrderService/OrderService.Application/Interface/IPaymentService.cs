using OrderService.Application.Models;
using OrderService.Domain.Entities;
using System.Threading.Tasks;

namespace OrderService.Application.Interface
{
    public interface IPaymentService
    {
        /// <summary>
        /// Initiate payment for order: return paymentUrl + txId
        /// </summary>
        Task<PaymentResult> InitiatePaymentAsync(Order order);

        /// <summary>
        /// Verify or handle provider callback; returns whether payment succeeded
        /// </summary>
        Task<PaymentResult> HandleCallbackAsync(string transactionId, IDictionary<string, string> payload);
        // THÊM: Kiểm tra trạng thái giao dịch
        /// <summary>
        /// Check transaction status directly with Payment Provider (e.g., polling after QR scan).
        /// </summary>
        Task<bool> CheckTransactionStatusAsync(string transactionId); // THAY ĐỔI
    }
}
