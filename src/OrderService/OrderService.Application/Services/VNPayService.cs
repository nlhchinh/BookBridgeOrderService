using OrderService.Application.Interface;
using OrderService.Application.Models;
using OrderService.Domain.Entities;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Web;
using System.Text;
using System.Net; // Cần dùng để encode URL

namespace OrderService.Application.Services.Payment
{
    public class VNPayService : IPaymentService
    {
        private readonly VNPayConfig _config;

        public VNPayService(IOptions<VNPayConfig> configOptions)
        {
            _config = configOptions.Value;
        }

        // 🎯 Phương thức 1: KHỞI TẠO THANH TOÁN (Lấy URL/QR)
        public Task<PaymentResult> InitiatePaymentAsync(PaymentTransaction transaction)
        {
            // 1. Chuẩn bị dữ liệu yêu cầu theo định dạng của VNPay
            var vnp_Params = new SortedList<string, string>();
            vnp_Params.Add("vnp_Version", "2.1.0");
            vnp_Params.Add("vnp_Command", "pay");
            vnp_Params.Add("vnp_TmnCode", _config.TmnCode);
            vnp_Params.Add("vnp_Amount", ((long)transaction.TotalAmount * 100).ToString()); // Số tiền * 100 (đơn vị VNPay là đồng)
            vnp_Params.Add("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnp_Params.Add("vnp_CurrCode", "VND");
            vnp_Params.Add("vnp_IpAddr", "127.0.0.1"); // IP của Server (hoặc Client nếu bạn truyền lên)
            vnp_Params.Add("vnp_Locale", "vn");
            vnp_Params.Add("vnp_OrderInfo", $"Thanh toan cho giao dich: {transaction.Id}");
            vnp_Params.Add("vnp_OrderType", "other");
            vnp_Params.Add("vnp_ReturnUrl", _config.ReturnUrl); // URL Callback của bạn
            vnp_Params.Add("vnp_TxnRef", transaction.Id.ToString()); // ID giao dịch nội bộ

            // *****************************************************************
            // 💡 QUAN TRỌNG: Thiết lập để nhận QR CODE.
            // Nếu bạn muốn QR Code thuần túy, VNPay sẽ tự động render nếu bạn không 
            // truyền các tham số ngân hàng.
            // Nếu bạn muốn trả về một chuỗi QR (payload) để tự gen ảnh QR:
            vnp_Params.Add("vnp_BankCode", "QRVNPAID"); // Chỉ định cho VNPay tạo URL chứa QR Code.
            // *****************************************************************

            // 2. Tạo URL và Mã hóa (Hashing)
            var paymentUrl = _config.BaseUrl;
            var data = new StringBuilder();

            foreach (var key in vnp_Params.Keys)
            {
                if (!string.IsNullOrEmpty(vnp_Params[key]))
                {
                    // Dùng WebUtility.UrlEncode thay cho HttpUtility.UrlEncode trong .NET Core
                    data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(vnp_Params[key]) + "&");
                }
            }

            var rawData = data.ToString().TrimEnd('&');
            var vnp_SecureHash = HmacSHA512(_config.HashSecret, rawData);

            paymentUrl += "?" + rawData + "&vnp_SecureHash=" + vnp_SecureHash;

            // 3. Trả về kết quả
            return Task.FromResult(new PaymentResult
            {
                Success = true,
                // PaymentUrl là URL mà VNPay sẽ redirect đến hoặc trả về trang chứa QR code
                PaymentUrl = paymentUrl,
                TransactionId = transaction.Id.ToString(), // Dùng TxnRef làm TransactionId tạm thời
                Message = "VNPay payment URL created. Frontend should redirect or show QR."
            });
        }

        // 🎯 Phương thức 2: XỬ LÝ CALLBACK/IPN
        public Task<PaymentResult> HandleCallbackAsync(string transactionId, IDictionary<string, string> payload)
        {
            // 1. Kiểm tra chữ ký (Secure Hash)
            if (!ValidateHash(payload))
            {
                return Task.FromResult(new PaymentResult { Success = false, Message = "Invalid hash signature." });
            }

            // 2. Vấn tin giao dịch (Để chắc chắn giao dịch là thật) - Nếu không dùng tính năng này, có thể bỏ qua
            // Hoặc chỉ dựa vào vnp_ResponseCode

            // 3. Kiểm tra mã phản hồi của VNPay
            var vnp_ResponseCode = payload.ContainsKey("vnp_ResponseCode") ? payload["vnp_ResponseCode"] : "";
            var success = vnp_ResponseCode == "00"; // 00 là thành công

            return Task.FromResult(new PaymentResult
            {
                Success = success,
                TransactionId = payload.ContainsKey("vnp_TransactionNo") ? payload["vnp_TransactionNo"] : transactionId,
                Message = success ? "Payment callback succeeded." : "Payment callback failed or is pending."
            });
        }

        // 🎯 Phương thức 3: VẤN TIN TRẠNG THÁI
        public async Task<bool> CheckTransactionStatusAsync(string transactionId)
        {
            // Gọi API vấn tin VNPay (Cần triển khai HTTP client call)
            // Đây là một bước phức tạp, thường được dùng để đối soát. 
            // VNPAY cung cấp API cho việc này.
            // ... (HTTP request logic to _config.QueryUrl)

            // Giả định: nếu logic vấn tin thành công trả về mã 00
            // Đối với mục đích của bạn, chúng ta có thể dựa vào callback hoặc triển khai chi tiết sau.

            // TẠM THỜI: luôn trả về true nếu transactionId có vẻ hợp lệ (giả lập)
            return await Task.FromResult(!string.IsNullOrEmpty(transactionId));
        }

        // --- Hỗ trợ VNPay Hash ---
        private string HmacSHA512(string key, string data)
        {
            // ... (Logic giữ nguyên)
            var hash = new StringBuilder();
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            using (var hmac = new HMACSHA512(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(dataBytes);
                foreach (var b in hashBytes)
                {
                    hash.Append(b.ToString("x2"));
                }
            }
            return hash.ToString();
        }

        private bool ValidateHash(IDictionary<string, string> payload)
        {
            // Logic kiểm tra hash của VNPay: tạo lại hash từ dữ liệu nhận được và so sánh
            // (Cần sắp xếp lại params, loại bỏ vnp_SecureHash, tạo hash và so sánh)
            return true; // Tạm thời chấp nhận
        }
    }
}