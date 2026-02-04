using System;
using System.Collections.Concurrent;

namespace Victoria.Inventory.Domain.Services
{
    public interface IRfidDebouncer
    {
        bool ShouldProcess(string epc, int windowSeconds = 5);
        void Reset(string epc);
    }

    public class RfidDebouncer : IRfidDebouncer
    {
        // Key: EPC, Value: Last processed timestamp
        private readonly ConcurrentDictionary<string, DateTime> _readCache = new ConcurrentDictionary<string, DateTime>();

        public bool ShouldProcess(string epc, int windowSeconds = 5)
        {
            var now = DateTime.UtcNow;
            
            // Try to get the last read time.
            if (_readCache.TryGetValue(epc, out var lastRead))
            {
                if ((now - lastRead).TotalSeconds < windowSeconds)
                {
                    // Too soon, ignore
                    return false;
                }
            }

            // Update or add the new timestamp
            _readCache[epc] = now;
            return true;
        }

        public void Reset(string epc)
        {
            _readCache.TryRemove(epc, out _);
        }
    }
}
