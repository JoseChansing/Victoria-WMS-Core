using System;
using Microsoft.Extensions.Configuration;

namespace Victoria.Core
{
    public interface ILpnFactory
    {
        string GenerateLpnId();
    }

    public class LpnFactory : ILpnFactory
    {
        private readonly string _prefix;
        private static int _sequence = 0; // In production this would be backed by DB/Redis

        public LpnFactory(IConfiguration config)
        {
            _prefix = config["App:TenantPrefix"] ?? "LPN";
        }

        public string GenerateLpnId()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var seq = System.Threading.Interlocked.Increment(ref _sequence);
            return $"{_prefix}-{timestamp}-{seq:D4}";
        }
    }
}
