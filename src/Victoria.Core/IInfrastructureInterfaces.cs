using System;
using System.Threading.Tasks;
using Victoria.Core;

namespace Victoria.Core.Infrastructure
{
    public interface IEventStore
    {
        Task AppendEventsAsync(string streamId, int expectedVersion, IEnumerable<IDomainEvent> events);
        Task<IEnumerable<IDomainEvent>> GetEventsAsync(string streamId);
    }

    public interface ILockService
    {
        Task<bool> AcquireLockAsync(string key, TimeSpan ttl);
        Task ReleaseLockAsync(string key);
    }
}
