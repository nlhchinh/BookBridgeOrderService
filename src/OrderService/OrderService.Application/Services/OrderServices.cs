using AutoMapper;
using Common.Paging;
using OrderService.Application.Interface;
using OrderService.Application.Models;
using OrderService.Domain.Entities;
using OrderService.Infracstructure.DBContext;
// Đã loại bỏ OrderService.Infracstructure.Repositories
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.EntityFrameworkCore; // Cần dùng để có các phương thức EF Core

namespace OrderService.Application.Services
{
    public class OrderServices : IOrderServices
    {
        // Loại bỏ các Repository không cần thiết
        // private readonly OrderRepository _repo;
        // private readonly OrderItemRepository _itemRepo;
        private readonly IMapper _mapper;
        private readonly ICartClient _cartClient;
        private readonly IPaymentService _paymentService;
        private readonly OrderDbContext _orderDbContext; // Giữ lại DbContext để thao tác trực tiếp

        public OrderServices(
            // Loại bỏ OrderRepository và OrderItemRepository khỏi tham số
            // OrderRepository repo,
            // OrderItemRepository itemRepo,
            IMapper mapper,
            ICartClient cartClient,
            IPaymentService paymentService,
            OrderDbContext orderDbContext)
        {
            // _repo = repo; // Loại bỏ
            // _itemRepo = itemRepo; // Loại bỏ
            _mapper = mapper;
            _cartClient = cartClient;
            _paymentService = paymentService;
            _orderDbContext = orderDbContext; // Giữ lại DbContext
        }

        // Sửa các phương thức sử dụng DbContext trực tiếp:

        public async Task<PagedResult<Order>> GetAll(int page, int pageSize)
        {
            // Thay thế _repo.GetAllAsync()
            var list = await _orderDbContext.Orders
                .Include(o => o.OrderItems)
                .AsNoTracking()
                .ToListAsync();
            return PagedResult<Order>.Create(list, page, pageSize);
        }

        public async Task<Order> GetById(Guid id)
        {
            // Thay thế _repo.GetByIdAsync(id)
            return await _orderDbContext.Orders
                .Include(o => o.OrderItems)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<PagedResult<Order>> GetOrderByCustomer(Guid customerId, int pageNo, int pageSize)
        {
            // Thay thế _repo.GetOrdersByCustomerAsync(customerId)
            var list = await _orderDbContext.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.CustomerId == customerId)
                .AsNoTracking()
                .ToListAsync();
            return PagedResult<Order>.Create(list, pageNo, pageSize);
        }

        public async Task<Order> Create(OrderCreateRequest request)
        {
            // Kiểm tra: Đảm bảo chỉ có MỘT Store trong request khi dùng phương thức Create
            if (request.Stores == null || request.Stores.Count != 1)
                throw new Exception("Phương thức Create chỉ hỗ trợ tạo đơn hàng đơn lẻ, vui lòng đảm bảo Request chỉ chứa 1 Store.");

            // Lấy thông tin Store đầu tiên (đơn lẻ)
            var storeRequest = request.Stores.First();

            if (request.PaymentMethod == PaymentMethod.COD && request.PaymentProvider == null)
                throw new ValidationException("Cần chọn nhà cung cấp thanh toán khi chọn thanh toán online.");

            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = request.CustomerId,

                // ❌ SỬA LỖI 1: Lấy BookstoreId từ Store đầu tiên
                BookstoreId = storeRequest.BookstoreId,

                OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6]}",
                CustomerPhoneNumber = request.CustomerPhoneNumber,
                DeliveryAddress = request.DeliveryAddress,
                OrderDate = DateTime.UtcNow,
                OrderStatus = OrderStatus.Created,
                PaymentMethod = request.PaymentMethod,
                PaymentProvider = request.PaymentProvider,
                PaymentStatus = PaymentStatus.Pending,
            };

            // ❌ SỬA LỖI 2: Lấy OrderItems từ Store đầu tiên
            order.OrderItems = storeRequest.OrderItems.Select(i => new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                BookId = i.BookId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.Quantity * i.UnitPrice
            }).ToList();

            order.TotalQuantity = order.OrderItems.Sum(x => x.Quantity);
            order.TotalPrice = order.OrderItems.Sum(x => x.TotalPrice);

            _orderDbContext.Orders.Add(order);
            var result = await _orderDbContext.SaveChangesAsync();

            if (result == 0) throw new Exception("Không thể tạo đơn hàng");

            return order;
        }

        public async Task<PaymentTransaction> CreateFromCart( // <- ĐÃ THAY ĐỔI KIỂU TRẢ VỀ
    Guid customerId,
    OrderCreateRequest checkoutRequest,
    string accessToken)
        {
            // 1. Kiểm tra Request Data
            if (checkoutRequest.Stores == null || !checkoutRequest.Stores.Any() || checkoutRequest.Stores.All(s => !s.OrderItems.Any()))
                throw new ArgumentException("Yêu cầu thanh toán không chứa mặt hàng nào hoặc cửa hàng hợp lệ.");

            // 2. Kiểm tra thanh toán
            if (checkoutRequest.PaymentMethod != PaymentMethod.COD && checkoutRequest.PaymentProvider == null)
                throw new ArgumentException("Cần chọn nhà cung cấp thanh toán khi chọn thanh toán online.");

            var createdOrders = new List<Order>();

            // 3. Lặp qua danh sách Stores trong Request DTO để tạo từng Order
            foreach (var store in checkoutRequest.Stores)
            {
                if (!store.OrderItems.Any()) continue;

                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    BookstoreId = store.BookstoreId,
                    OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 6)}",
                    CustomerPhoneNumber = checkoutRequest.CustomerPhoneNumber,
                    DeliveryAddress = checkoutRequest.DeliveryAddress,
                    PaymentMethod = checkoutRequest.PaymentMethod,
                    PaymentProvider = checkoutRequest.PaymentProvider,

                    OrderDate = DateTime.UtcNow,
                    OrderStatus = OrderStatus.Created,
                    PaymentStatus = PaymentStatus.Pending // Order status ban đầu
                };

                // Tạo OrderItems
                order.OrderItems = store.OrderItems.Select(i => new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    BookId = i.BookId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.UnitPrice * i.Quantity
                }).ToList();

                order.TotalQuantity = order.OrderItems.Sum(x => x.Quantity);
                order.TotalPrice = order.OrderItems.Sum(x => x.TotalPrice);

                _orderDbContext.Orders.Add(order);
                createdOrders.Add(order);
            }

            if (!createdOrders.Any())
            {
                throw new ArgumentException("Không có đơn hàng nào được tạo.");
            }

            // --- Bắt đầu Unit of Work/Transaction ---
            // 4. LƯU TẤT CẢ ORDERS vào DB (Lần 1)
            try
            {
                var rowsAffected = await _orderDbContext.SaveChangesAsync();
                if (rowsAffected == 0)
                {
                    throw new Exception("Lưu đơn hàng vào cơ sở dữ liệu thất bại (Lần 1).");
                }
            }
            catch (DbUpdateException ex)
            {
                throw new Exception("Lỗi DB khi lưu Orders: " + ex.InnerException?.Message ?? ex.Message);
            }


            // 5. TẠO GIAO DỊCH THANH TOÁN GỘP (Payment Transaction)
            var totalAmount = createdOrders.Sum(o => o.TotalPrice);

            var paymentTx = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                TotalAmount = totalAmount,
                PaymentStatus = PaymentStatus.Pending,
                // PaymentUrl sẽ được cập nhật sau. Ta tạm thời set một giá trị để thỏa mãn [Required] của DBContext nếu cần.
                PaymentUrl = "Initializing",
            };

            // Liên kết tất cả Orders vừa tạo với PaymentTransaction này
            foreach (var order in createdOrders)
            {
                order.PaymentTransactionId = paymentTx.Id;
            }

            _orderDbContext.PaymentTransactions.Add(paymentTx);

            // 6. LƯU PaymentTransaction & Cập nhật Order.PaymentTransactionId (Lần 2)
            try
            {
                await _orderDbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new Exception("Lỗi DB khi lưu PaymentTransaction và liên kết Orders (Lần 2): " + ex.InnerException?.Message ?? ex.Message);
            }


            // 7. Xử lý COD: Trả về PaymentTransaction với trạng thái Paid
            if (checkoutRequest.PaymentMethod == PaymentMethod.COD)
            {
                paymentTx.PaymentStatus = PaymentStatus.Paid; // Hoặc Confirmed/Delivered tùy nghiệp vụ COD
                paymentTx.PaidDate = DateTime.UtcNow;
                paymentTx.PaymentUrl = "COD_SUCCESS";
                paymentTx.TransactionId = $"COD_TX_{paymentTx.Id}";

                // Cập nhật Orders thành Confirmed/Paid
                foreach (var order in createdOrders)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.OrderStatus = OrderStatus.Confirmed;
                }

                try
                {
                    await _orderDbContext.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    throw new Exception("Lỗi DB khi cập nhật trạng thái COD: " + ex.InnerException?.Message ?? ex.Message);
                }
            }
            else // 8. KHỞI TẠO THANH TOÁN ONLINE (VietQR, EWallet)
            {
                PaymentResult paymentResult;
                try
                {
                    // Gọi Payment Service để tạo QR Code / Payment URL cho giao dịch gộp
                    paymentResult = await _paymentService.InitiatePaymentAsync(paymentTx);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Lỗi gọi Payment Service: {ex.Message}");
                }

                if (!paymentResult.Success)
                {
                    // Vẫn giữ Orders/PaymentTransaction trong DB, chỉ báo lỗi khởi tạo thanh toán
                    throw new Exception($"Không thể khởi tạo thanh toán online: {paymentResult.Message}");
                }

                // 9. CẬP NHẬT PaymentTransaction với kết quả (URL và TX ID)
                paymentTx.PaymentUrl = paymentResult.PaymentUrl;
                paymentTx.TransactionId = paymentResult.TransactionId;

                // Lưu PaymentUrl và TransactionId vào DbContext (Lần 3)
                try
                {
                    await _orderDbContext.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    throw new Exception("Lỗi DB khi cập nhật PaymentTransaction URL/ID (Lần 3): " + ex.InnerException?.Message ?? ex.Message);
                }
            }

            // 10. Xóa giỏ hàng (Không nằm trong Transaction chính, chỉ log lỗi nếu thất bại)
            try
            {
                await _cartClient.ClearCartAsync(customerId.ToString(), accessToken);
            }
            catch (Exception ex)
            {
                // Có thể log lỗi ở đây nhưng không cần dừng luồng thanh toán chính
                Console.WriteLine($"Warning: Failed to clear cart for customer {customerId}: {ex.Message}");
            }

            // 11. Trả về PaymentTransaction để frontend hiển thị QR/redirect
            return paymentTx;
        }

        public async Task<Order> Update(Guid id, OrderUpdateRequest request)
        {
            // Thay thế _repo.GetByIdAsync(id);
            var exist = await _orderDbContext.Orders
                .FirstOrDefaultAsync(o => o.Id == id); // Bỏ AsNoTracking() để Entity Framework theo dõi thay đổi

            if (exist == null) throw new Exception("Order not found");

            exist.CustomerPhoneNumber = request.CustomerPhoneNumber ?? exist.CustomerPhoneNumber;
            exist.DeliveryAddress = request.DeliveryAddress ?? exist.DeliveryAddress;
            exist.PaymentMethod = request.PaymentMethod ?? exist.PaymentMethod;
            exist.PaymentProvider = request.PaymentProvider ?? exist.PaymentProvider;

            // THAY THẾ: await _repo.UpdateOrderAsync(exist);
            // Không cần gọi _orderDbContext.Orders.Update(exist) vì entity đã được theo dõi (tracked)
            var result = await _orderDbContext.SaveChangesAsync();

            if (result == 0) throw new Exception("Update failed");
            return exist;
        }

        public async Task Delete(Guid id)
        {
            // Thay thế _repo.GetByIdAsync(id);
            var exist = await _orderDbContext.Orders
                .FirstOrDefaultAsync(o => o.Id == id);

            if (exist == null) return;

            exist.IsDeleted = true;
            // THAY THẾ: await _repo.UpdateOrderAsync(exist);
            await _orderDbContext.SaveChangesAsync();
        }

        // ... các phương thức khác (SearchByCustomerEmail, InitiatePayment, HandlePaymentCallback, UpdatePaymentStatusAfterScan) được sửa tương tự
        // VÌ CÁC PHƯƠNG THỨC SAU SỬ DỤNG _repo RẤT NHIỀU, TÔI SẼ CHỈ SỬA NHỮNG CHỖ CẦN THIẾT

        public async Task<PaymentTransaction> InitiatePayment(Guid orderId)
        {
            // Cần lấy order có tracking để update
            var order = await _orderDbContext.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new Exception("Order not found");
            if (order.PaymentTransactionId.HasValue) throw new InvalidOperationException("Order đã được gán với giao dịch thanh toán."); // Ngăn chặn tạo lại giao dịch
            if (order.PaymentStatus == PaymentStatus.Paid) throw new InvalidOperationException("Order đã được thanh toán.");
            if (order.PaymentMethod == PaymentMethod.COD)
                throw new InvalidOperationException("Phương thức thanh toán COD không cần khởi tạo.");
            if (order.PaymentMethod != PaymentMethod.VietQR && order.PaymentMethod != PaymentMethod.EWallet)
                throw new InvalidOperationException("Phương thức thanh toán không hỗ trợ thanh toán online.");

            // 1. TẠO GIAO DỊCH THANH TOÁN ĐƠN LẺ
            var paymentTx = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                TotalAmount = order.TotalPrice,
                PaymentStatus = PaymentStatus.Pending,
                PaymentUrl = "Initializing" // Tạm thời
            };

            // 2. LIÊN KẾT: Gán PaymentTransactionId mới cho Order
            order.PaymentTransactionId = paymentTx.Id;
            order.PaymentStatus = PaymentStatus.Pending;

            _orderDbContext.PaymentTransactions.Add(paymentTx);

            // Lưu Transaction vào DB
            try
            {
                await _orderDbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new Exception("Lỗi DB khi lưu PaymentTransaction và liên kết Order: " + ex.InnerException?.Message ?? ex.Message);
            }


            // 3. KHỞI TẠO THANH TOÁN
            PaymentResult result;
            try
            {
                result = await _paymentService.InitiatePaymentAsync(paymentTx);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi gọi Payment Service: {ex.Message}");
            }


            if (result.Success)
            {
                // 4. Cập nhật PaymentTransaction với kết quả
                paymentTx.PaymentUrl = result.PaymentUrl;
                paymentTx.TransactionId = result.TransactionId;

                // Lưu lại kết quả
                try
                {
                    await _orderDbContext.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    throw new Exception("Lỗi DB khi cập nhật PaymentTransaction URL/ID: " + ex.InnerException?.Message ?? ex.Message);
                }

                return paymentTx;
            }
            else
            {
                throw new Exception($"Lỗi khởi tạo thanh toán: {result.Message}");
            }
        }

        public async Task<bool> HandlePaymentCallback(string transactionId, IDictionary<string, string> payload)
        {
            // 1. Tìm PaymentTransaction dựa trên TransactionId
            var paymentTx = await _orderDbContext.PaymentTransactions
                // Cần Include các Order liên quan để cập nhật trạng thái của chúng
                .Include(pt => pt.Orders)
                .FirstOrDefaultAsync(pt => pt.TransactionId == transactionId);

            if (paymentTx == null)
            {
                // Log lỗi: Không tìm thấy giao dịch
                return false;
            }

            // 2. Gọi Payment Service để xác thực callback
            var result = await _paymentService.HandleCallbackAsync(transactionId, payload);

            // 3. Cập nhật trạng thái của PaymentTransaction
            if (result.Success)
            {
                paymentTx.PaymentStatus = PaymentStatus.Paid;
                paymentTx.PaidDate = DateTime.UtcNow;

                // 4. Cập nhật tất cả các Orders thuộc về giao dịch này
                foreach (var order in paymentTx.Orders)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.OrderStatus = OrderStatus.Confirmed;
                }
            }
            else
            {
                paymentTx.PaymentStatus = PaymentStatus.Failed;

                // Cập nhật Orders sang Failed
                foreach (var order in paymentTx.Orders)
                {
                    order.PaymentStatus = PaymentStatus.Failed;
                    order.OrderStatus = OrderStatus.Canceled; // Hoặc giữ nguyên Created/Pending tùy nghiệp vụ
                }
            }

            // 5. Lưu tất cả thay đổi
            await _orderDbContext.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UpdatePaymentStatusAfterScan(Guid orderId)
        {
            // 1. Tìm Order để lấy ra PaymentTransactionId
            // Sử dụng AsNoTracking() vì ta sẽ dùng PaymentTransaction để tracking
            var order = await _orderDbContext.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return false;

            // 2. Kiểm tra nếu đã thanh toán rồi thì trả về true
            if (order.PaymentStatus == PaymentStatus.Paid) return true;

            // Đảm bảo Order đang được liên kết với một giao dịch
            if (order.PaymentTransactionId == null)
            {
                // Log lỗi: Đơn hàng không liên kết với giao dịch thanh toán nào (có thể là COD)
                throw new InvalidOperationException($"Order {orderId} không có TransactionId. Có thể là COD.");
            }

            // 3. Tìm PaymentTransaction dựa trên PaymentTransactionId
            var paymentTx = await _orderDbContext.PaymentTransactions
                .Include(pt => pt.Orders) // Rất quan trọng: Bao gồm tất cả các Order liên quan
                .FirstOrDefaultAsync(pt => pt.Id == order.PaymentTransactionId.Value);

            if (paymentTx == null) return false;

            // 4. Nếu PaymentTransaction đã Paid thì cập nhật Orders và trả về
            if (paymentTx.PaymentStatus == PaymentStatus.Paid)
            {
                // Cập nhật tất cả các Orders thuộc về giao dịch này
                foreach (var relatedOrder in paymentTx.Orders)
                {
                    if (relatedOrder.PaymentStatus != PaymentStatus.Paid)
                    {
                        relatedOrder.PaymentStatus = PaymentStatus.Paid;
                        relatedOrder.OrderStatus = OrderStatus.Confirmed;
                    }
                }
                await _orderDbContext.SaveChangesAsync();
                return true;
            }

            // 5. Gọi Payment Service để kiểm tra trạng thái giao dịch
            // Dùng TransactionId của PaymentTransaction, không phải Order (Order.TransactionId hiện không dùng)
            var isPaid = await _paymentService.CheckTransactionStatusAsync(paymentTx.TransactionId);

            if (isPaid)
            {
                // 6. Cập nhật trạng thái của PaymentTransaction
                paymentTx.PaymentStatus = PaymentStatus.Paid;

                // 7. Cập nhật tất cả các Orders thuộc về giao dịch này
                foreach (var relatedOrder in paymentTx.Orders)
                {
                    relatedOrder.PaymentStatus = PaymentStatus.Paid;
                    relatedOrder.OrderStatus = OrderStatus.Confirmed;
                    // PaidDate sẽ được DbContext tự động thêm vào trong SaveChangesAsync (trong OrderDbContext.SaveChangesAsync)
                }

                await _orderDbContext.SaveChangesAsync();
                return true;
            }

            // Nếu không thanh toán thành công
            return false;
        }

        // Cần thêm lại method này để tránh lỗi compile nếu bạn dùng nó ở đâu đó:
        public async Task<IEnumerable<Order>> SearchByCustomerEmail(string email)
        {
            throw new NotImplementedException("You must call UserService to resolve email->userId");
        }
    }
}