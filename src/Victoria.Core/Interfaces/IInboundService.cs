using System.Threading.Tasks;
using Victoria.Core.Models;

namespace Victoria.Core.Interfaces
{
    public interface IInboundService
    {
        Task<int> SyncAllAsync(IOdooRpcClient odooClient);
        Task SyncPicking(OdooOrderDto odooPicking, string type);
        Task<int> PerformCleanupGuardian(IOdooRpcClient odooClient);
    }
}
