using AutoMapper;
using Common.Paging;
using OrderService.Application.Interface;
using OrderService.Application.Models;
using OrderService.Domain.Entities;
using OrderService.Infracstructure.Repositories;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace OrderService.Application.Services
{
    public class OrderServices : IOrderServices
    {
        private readonly OrderRepository _repo;
        private readonly OrderItemRepository _itemRepo;
        private readonly IMapper _mapper;
        private readonly ICartClient _cartClient;
        private readonly IPaymentService _paymentService;

        public OrderServices(
            OrderRepository repo,
            OrderItemRepository itemRepo,
            IMapper mapper,
            ICartClient cartClient,
            IPaymentService paymentService)
        {
            _repo = repo;
            _itemRepo = itemRepo;
            _mapper = mapper;
            _cartClient = cartClient;
            _paymentService = paymentService;
        }

        public async Task<PagedResult<Order>> GetAll(int page, int pageSize)
        {
            var list = (await _repo.GetAllAsync()).ToList();
            return PagedResult<Order>.Create(list, page, pageSize);
        }

        public async Task<Order> GetById(Guid id) => await _repo.GetByIdAsync(id);

        public async Task<PagedResult<Order>> GetOrderByCustomer(Guid customerId, int pageNo, int pageSize)
        {
            var list = (await _repo.GetOrdersByCustomerAsync(customerId)).ToList();
            return PagedResult<Order>.Create(list, pageNo, pageSize);
        }

        public async Task<Order> Create(OrderCreateRequest request)
        {
            if (request.PaymentMethod == PaymentMethod.COD && request.PaymentProvider == null)
                throw new ValidationException("Cần chọn nhà cung cấp thanh toán khi chọn thanh toán online.");

            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = request.CustomerId,
                BookstoreId = request.BookstoreId,
                OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6]}",
                CustomerPhoneNumber = request.CustomerPhoneNumber,
                DeliveryAddress = request.DeliveryAddress,
                OrderDate = DateTime.UtcNow,
                OrderStatus = OrderStatus.Created,
                PaymentMethod = request.PaymentMethod,
                PaymentProvider = request.PaymentProvider,
                PaymentStatus = PaymentStatus.Pending,
            };

            order.OrderItems = request.OrderItems.Select(i => new OrderItem
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

            var ok = await _repo.CreateOrderAsync(order);
            if (!ok) throw new Exception("Không thể tạo đơn hàng");

            return order;
        }

        // Create orders from cart - one order per store
        public async Task<IEnumerable<Order>> CreateFromCart(
            Guid customerId,
            OrderCreateRequest checkoutRequest, // DTO chứa thông tin giao hàng
            string accessToken) // Access Token để gọi Cart Service
        {
            // 1. Lấy Cart từ Cart Service
            var cart = await _cartClient.GetCartAsync(customerId.ToString(), accessToken);

            if (cart == null || cart.Stores == null || !cart.Stores.Any() || cart.Stores.All(s => !s.Items.Any()))
                throw new Exception("Giỏ hàng rỗng hoặc không tồn tại."); // Ném lỗi thay vì trả về rỗng

            var createdOrders = new List<Order>();

            // 2. Lặp qua các cửa hàng để tạo Order
            foreach (var store in cart.Stores)
            {
                if (!store.Items.Any()) continue;

                // Lấy thông tin từ OrderCreateRequest
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    BookstoreId = store.StoreId,
                    OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 6)}",

                    // LẤY TỪ checkoutRequest
                    CustomerPhoneNumber = checkoutRequest.CustomerPhoneNumber,
                    DeliveryAddress = checkoutRequest.DeliveryAddress,
                    PaymentMethod = checkoutRequest.PaymentMethod, // THÊM PaymentMethod
                    PaymentProvider = checkoutRequest.PaymentMethod == PaymentMethod.VietQR ? PaymentProvider.VNPay : null, // Giả định

                    OrderDate = DateTime.UtcNow,
                    OrderStatus = OrderStatus.Created,
                    PaymentStatus = PaymentStatus.Pending
                };

                order.OrderItems = store.Items.Select(i => new OrderItem
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

                var created = await _repo.CreateOrderAsync(order);
                if (!created) continue;
                createdOrders.Add(order);
            }

            // 3. Xóa toàn bộ giỏ hàng nếu tạo Order thành công
            if (createdOrders.Any())
            {
                // Truyền Access Token vào để xóa Cart
                var isCartCleared = await _cartClient.ClearCartAsync(customerId.ToString(), accessToken);
                if (!isCartCleared)
                {
                    // Ghi log lỗi: Không xóa được cart, nhưng Order đã được tạo.
                    // Cần cơ chế xử lý lỗi này (ví dụ: thông báo cho user, hoặc thử lại)
                }
            }

            return createdOrders;
        }

        public async Task<Order> Update(Guid id, OrderUpdateRequest request)
        {
            // Logic Update (giữ nguyên)
            var exist = await _repo.GetByIdAsync(id);
            if (exist == null) throw new Exception("Order not found");

            exist.CustomerPhoneNumber = request.CustomerPhoneNumber ?? exist.CustomerPhoneNumber;
            exist.DeliveryAddress = request.DeliveryAddress ?? exist.DeliveryAddress;
            exist.PaymentMethod = request.PaymentMethod ?? exist.PaymentMethod;
            exist.PaymentProvider = request.PaymentProvider ?? exist.PaymentProvider;

            var ok = await _repo.UpdateOrderAsync(exist);
            if (!ok) throw new Exception("Update failed");
            return exist;
        }

        public async Task Delete(Guid id)
        {
            // Logic Delete (giữ nguyên)
            var exist = await _repo.GetByIdAsync(id);
            if (exist == null) return;
            exist.IsDeleted = true;
            await _repo.UpdateOrderAsync(exist);
        }

        public async Task<IEnumerable<Order>> SearchByCustomerEmail(string email)
        {
            throw new NotImplementedException("You must call UserService to resolve email->userId");
        }

        public async Task<Order> InitiatePayment(Guid orderId)
        {
            // Logic InitiatePayment (giữ nguyên và rất tốt)
            var order = await _repo.GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");
            if (order.PaymentMethod != PaymentMethod.VietQR && order.PaymentMethod != PaymentMethod.EWallet)
                throw new Exception("Phương thức thanh toán không hỗ trợ thanh toán online.");

            // Gọi Payment Service để tạo QR Code / Payment URL
            var result = await _paymentService.InitiatePaymentAsync(order);
            if (result.Success)
            {
                order.PaymentUrl = result.PaymentUrl;
                order.TransactionId = result.TransactionId;
                order.PaymentStatus = PaymentStatus.Pending; // Chờ thanh toán
                await _repo.UpdateOrderAsync(order);
            }
            else
            {
                throw new Exception($"Không thể khởi tạo thanh toán: {result.Message}");
            }

            return order;
        }

        // Called by webhook/async callback when provider notifies payment status
        public async Task<bool> HandlePaymentCallback(string transactionId, IDictionary<string, string> payload)
        {
            // Logic HandlePaymentCallback (giữ nguyên và rất tốt)
            var orders = (await _repo.GetAllAsync()).Where(o => o.TransactionId == transactionId).ToList();
            if (!orders.Any()) return false;

            var result = await _paymentService.HandleCallbackAsync(transactionId, payload);

            // Cần thêm logic xác thực callback (chữ ký, mã bảo mật,...) trong _paymentService

            foreach (var order in orders)
            {
                if (result.Success)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.OrderStatus = OrderStatus.Confirmed; // Chuyển sang Confirmed sau khi Paid
                    order.PaidDate = DateTime.UtcNow; // Ghi nhận thời gian Paid
                    await _repo.UpdateOrderAsync(order);
                }
                else
                {
                    order.PaymentStatus = PaymentStatus.Failed;
                    await _repo.UpdateOrderAsync(order);
                }
            }
            return true;
        }

        // THÊM: Method để Front-end kiểm tra lại trạng thái sau khi quét QR/Redirect
        public async Task<bool> UpdatePaymentStatusAfterScan(Guid orderId, string transactionId)
        {
            var order = await _repo.GetByIdAsync(orderId);
            if (order == null) return false;

            if (order.PaymentStatus == PaymentStatus.Paid) return true; // Đã Paid rồi, không cần làm gì

            // Gọi Payment Service để kiểm tra trạng thái giao dịch
            var isPaid = await _paymentService.CheckTransactionStatusAsync(transactionId);

            if (isPaid)
            {
                order.PaymentStatus = PaymentStatus.Paid;
                order.OrderStatus = OrderStatus.Confirmed;
                order.PaidDate = DateTime.UtcNow;
                await _repo.UpdateOrderAsync(order);
                return true;
            }
            return false;
        }
    }
}