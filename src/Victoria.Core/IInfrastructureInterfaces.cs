using System;
using System.Threading.Tasks;
using Victoria.Core;

namespace Victoria.Core.Infrastructure
{
    public record EventStreamBatch(string StreamId, int ExpectedVersion, IEnumerable<IDomainEvent> Events);

    public interface IEventStore
    {
        Task AppendEventsAsync(string streamId, int expectedVersion, IEnumerable<IDomainEvent> events);
        Task SaveBatchAsync(IEnumerable<EventStreamBatch> batches);
        Task<IEnumerable<IDomainEvent>> GetEventsAsync(string streamId);
    }

    public interface ILockService
    {
        Task<bool> AcquireLockAsync(string key, TimeSpan ttl);
        Task ReleaseLockAsync(string key);
    }
}
