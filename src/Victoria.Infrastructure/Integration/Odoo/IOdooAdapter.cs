using System.Collections.Generic;
using System.Threading.Tasks;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public interface IOdooAdapter
    {
        Task<bool> ConfirmReceiptAsync(long pickingId, Dictionary<long, int> moveQuantities);
    }
}
