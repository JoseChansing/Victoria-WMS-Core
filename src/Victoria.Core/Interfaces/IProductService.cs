using System.Threading.Tasks;
using Victoria.Core.Models;

namespace Victoria.Core.Interfaces
{
    public interface IProductService
    {
        Task<int> SyncAllAsync(IOdooRpcClient odooClient);
        Task SyncProduct(OdooProductDto product);
        Task SyncSingleAsync(IOdooRpcClient odooClient, string sku);
    }
}
