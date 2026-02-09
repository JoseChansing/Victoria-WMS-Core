using System;
using System.Threading.Tasks;

namespace Victoria.Inventory.Domain.Services
{
    public interface ILpnFactory
    {
        Task<string> CreateNextAsync();
    }

    public class LpnFactory : ILpnFactory
    {
        public LpnFactory()
        {
        }

        public Task<string> CreateNextAsync()
        {
            // Robust timestamp-based generation for atomic-like uniqueness
            // Format: PTC + 10 numeric digits from UtcNow Ticks
            var timestampPart = (DateTime.UtcNow.Ticks % 10000000000).ToString("D10");
            return Task.FromResult($"PTC{timestampPart}");
        }
    }
}
