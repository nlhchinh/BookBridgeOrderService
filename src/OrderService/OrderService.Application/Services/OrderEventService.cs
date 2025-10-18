using System.Text.Json;
using OrderService.Domain.Entities;
using OrderService.Infracstructure.DBContext;
using OrderService.Infracstructure.Repositories;

public class OrderEventService
{
    private readonly OrderRepository _orderRepository;
    private readonly MessageRepository _messageRepository;
    private readonly OrderDbContext _context;

    public OrderEventService(
        OrderRepository orderRepository,
        MessageRepository messageRepository,
        OrderDbContext context)
    {
        _orderRepository = orderRepository;
        _messageRepository = messageRepository;
        _context = context;
    }

    public async Task<bool> CreateOrderWithEventAsync(Order order, string traceId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _orderRepository.CreateOrderAsync(order);

            var message = new Message
            {
                EventType = "OrderCreatedEvent",
                Payload = JsonSerializer.Serialize(order),
                TraceId = traceId,
                ServiceName = "OrderService"
            };

            await _messageRepository.AddAsync(message);
            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            return false;
        }
    }
}
