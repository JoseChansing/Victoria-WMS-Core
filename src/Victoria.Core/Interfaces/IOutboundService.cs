using System.Threading.Tasks;
using System.Threading.Tasks;
using Victoria.Core.Interfaces;

namespace Victoria.Core.Interfaces
{
    public interface IOutboundService
    {
        Task<int> SyncAllAsync(IOdooRpcClient odooClient);
    }
}
