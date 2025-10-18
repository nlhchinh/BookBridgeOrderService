using Common.Infrastructure.Repositories;
using OrderService.Domain.Entities;
using OrderService.Infracstructure.DBContext;

namespace OrderService.Infracstructure.Repositories
{
    public class OrderItemRepository : BaseRepository<OrderItem, Guid>
    {
        public OrderItemRepository(OrderDbContext context) : base(context) { }
        public async Task<List<OrderItem>> CreateRangeAsync(List<OrderItem> oL)
        {
            if (oL == null || oL.Count == 0) return new List<OrderItem>();
            await _dbSet.AddRangeAsync(oL);
            await _context.SaveChangesAsync();
            return oL;
        }
    }
}
