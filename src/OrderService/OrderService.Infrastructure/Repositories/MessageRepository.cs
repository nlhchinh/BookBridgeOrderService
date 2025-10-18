using Common.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infracstructure.DBContext;

public class MessageRepository : BaseRepository<Message, long>
{
    public MessageRepository(OrderDbContext context) : base(context) { }

    public async Task<List<Message>> GetPendingMessagesAsync(int batchSize = 50)
    {
        return await _dbSet
            .Where(m => m.MessageStatus == MessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task<bool> MarkAsPublishedAsync(long messageId)
    {
        var message = await _dbSet.FindAsync(messageId);
        if (message == null) return false;

        message.MessageStatus = MessageStatus.Published;
        message.PublishedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkAsFailedAsync(long messageId, string? error = null)
    {
        var message = await _dbSet.FindAsync(messageId);
        if (message == null) return false;

        message.MessageStatus = MessageStatus.Failed;
        message.RetryCount++;
        message.LastError = error;
        await _context.SaveChangesAsync();
        return true;
    }
}
