using System;
using System.Threading.Tasks;
using StackExchange.Redis;
using Victoria.Core.Infrastructure;

namespace Victoria.Infrastructure.Redis
{
    public class RedisLockService : ILockService
    {
        private readonly IDatabase _database;

        public RedisLockService(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase();
        }

        public async Task<bool> AcquireLockAsync(string key, TimeSpan ttl)
        {
            // REQUISITO CRÍTICO: Uso de When.NotExists (NX) para atomicidad
            return await _database.StringSetAsync(
                key, 
                "locked", 
                ttl, 
                when: When.NotExists);
        }

        public async Task ReleaseLockAsync(string key)
        {
            await _database.KeyDeleteAsync(key);
        }
    }
}
