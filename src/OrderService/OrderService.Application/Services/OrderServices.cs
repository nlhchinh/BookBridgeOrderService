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

        public async Task<IEnumerable<Order>> CreateFromCart(
            Guid customerId,
            OrderCreateRequest checkoutRequest,
            string accessToken)
        {
            // 1. Kiểm tra Request Data (Dùng DTO mới: checkoutRequest.Stores)
            if (checkoutRequest.Stores == null || !checkoutRequest.Stores.Any() || checkoutRequest.Stores.All(s => !s.OrderItems.Any()))
                throw new Exception("Yêu cầu thanh toán không chứa mặt hàng nào hoặc cửa hàng hợp lệ.");

            // 2. Kiểm tra thanh toán (Áp dụng chung)
            if (checkoutRequest.PaymentMethod == PaymentMethod.COD && checkoutRequest.PaymentProvider == null)
                throw new ValidationException("Cần chọn nhà cung cấp thanh toán khi chọn thanh toán online.");

            var createdOrders = new List<Order>();

            // 3. Lặp qua danh sách Stores trong Request DTO để tạo từng Order
            foreach (var store in checkoutRequest.Stores)
            {
                if (!store.OrderItems.Any()) continue;

                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    BookstoreId = store.BookstoreId, // Lấy BookstoreId từ StoreCheckoutDto
                    OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 6)}",

                    // Lấy thông tin chung từ Request chính
                    CustomerPhoneNumber = checkoutRequest.CustomerPhoneNumber,
                    DeliveryAddress = checkoutRequest.DeliveryAddress,
                    PaymentMethod = checkoutRequest.PaymentMethod,

                    // Thiết lập PaymentProvider mặc định nếu cần
                    PaymentProvider = checkoutRequest.PaymentProvider ??
                                      (checkoutRequest.PaymentMethod == PaymentMethod.VietQR ? PaymentProvider.VNPay : null),

                    OrderDate = DateTime.UtcNow,
                    OrderStatus = OrderStatus.Created,
                    PaymentStatus = PaymentStatus.Pending
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

                // Thao tác trực tiếp: Chỉ thêm vào DbContext, chưa lưu
                _orderDbContext.Orders.Add(order);
                createdOrders.Add(order);
            }

            // 4. LƯU TẤT CẢ (Unit of Work)
            if (createdOrders.Any())
            {
                var result = await _orderDbContext.SaveChangesAsync(); // <-- GỌI 1 LẦN

                if (result == 0)
                {
                    throw new Exception("Không thể tạo bất kỳ đơn hàng nào. Vui lòng kiểm tra lại dữ liệu.");
                }

                // 5. Xóa giỏ hàng (giả sử: xóa toàn bộ giỏ hàng cũ sau khi checkout thành công)
                var isCartCleared = await _cartClient.ClearCartAsync(customerId.ToString(), accessToken);
                if (!isCartCleared)
                {
                    // Log lỗi nếu không xóa được cart
                }
            }

            return createdOrders;
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

        public async Task<Order> InitiatePayment(Guid orderId)
        {
            var order = await GetById(orderId); // Dùng lại GetById đã sửa
            // ... (Logic kiểm tra như cũ)
            if (order == null) throw new Exception("Order not found");
            if (order.PaymentMethod != PaymentMethod.VietQR && order.PaymentMethod != PaymentMethod.EWallet)
                throw new Exception("Phương thức thanh toán không hỗ trợ thanh toán online.");

            // Gọi Payment Service để tạo QR Code / Payment URL
            var result = await _paymentService.InitiatePaymentAsync(order);
            if (result.Success)
            {
                // Cần lấy lại entity có tracking để update
                var orderToUpdate = await _orderDbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
                if (orderToUpdate == null) throw new Exception("Order not found for update");

                orderToUpdate.PaymentUrl = result.PaymentUrl;
                orderToUpdate.TransactionId = result.TransactionId;
                orderToUpdate.PaymentStatus = PaymentStatus.Pending; // Chờ thanh toán

                // THAY THẾ: await _repo.UpdateOrderAsync(order);
                await _orderDbContext.SaveChangesAsync();

                return orderToUpdate;
            }
            else
            {
                throw new Exception($"Không thể khởi tạo thanh toán: {result.Message}");
            }
        }

        public async Task<bool> HandlePaymentCallback(string transactionId, IDictionary<string, string> payload)
        {
            // Lấy danh sách Order (bỏ AsNoTracking() để có thể update sau này)
            var orders = await _orderDbContext.Orders
                .Where(o => o.TransactionId == transactionId)
                .ToListAsync();

            if (!orders.Any()) return false;

            var result = await _paymentService.HandleCallbackAsync(transactionId, payload);

            // Cần thêm logic xác thực callback (chữ ký, mã bảo mật,...) trong _paymentService

            foreach (var order in orders)
            {
                if (result.Success)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.OrderStatus = OrderStatus.Confirmed;
                    order.PaidDate = DateTime.UtcNow;
                    // Entity đã được tracking, chỉ cần thay đổi
                }
                else
                {
                    order.PaymentStatus = PaymentStatus.Failed;
                    // Entity đã được tracking, chỉ cần thay đổi
                }
            }

            // THAY THẾ: await _repo.UpdateOrderAsync(order) cho từng order
            await _orderDbContext.SaveChangesAsync(); // <-- Lưu tất cả thay đổi trong một lần

            return true;
        }

        public async Task<bool> UpdatePaymentStatusAfterScan(Guid orderId, string transactionId)
        {
            var order = await _orderDbContext.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId); // Bỏ AsNoTracking()

            if (order == null) return false;

            if (order.PaymentStatus == PaymentStatus.Paid) return true;

            // Gọi Payment Service để kiểm tra trạng thái giao dịch
            var isPaid = await _paymentService.CheckTransactionStatusAsync(transactionId);

            if (isPaid)
            {
                order.PaymentStatus = PaymentStatus.Paid;
                order.OrderStatus = OrderStatus.Confirmed;
                order.PaidDate = DateTime.UtcNow;

                // THAY THẾ: await _repo.UpdateOrderAsync(order);
                await _orderDbContext.SaveChangesAsync();

                return true;
            }
            return false;
        }

        // Cần thêm lại method này để tránh lỗi compile nếu bạn dùng nó ở đâu đó:
        public async Task<IEnumerable<Order>> SearchByCustomerEmail(string email)
        {
            throw new NotImplementedException("You must call UserService to resolve email->userId");
        }
    }
}