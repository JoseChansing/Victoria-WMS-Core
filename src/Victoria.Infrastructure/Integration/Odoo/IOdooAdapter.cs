using System.Collections.Generic;
using System.Threading.Tasks;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public interface IOdooAdapter
    {
        Task<bool> ConfirmReceiptAsync(long pickingId, Dictionary<long, int> moveQuantities);
        Task<int> CreatePackagingAsync(int productId, int templateId, string name, double qty, double weight, double length, double width, double height);
        Task<bool> UpdatePackagingAsync(int packagingId, string name, double qty, double weight, double length, double width, double height);
    }
}
