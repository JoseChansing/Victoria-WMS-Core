using System.Collections.Generic;
using System.Threading.Tasks;

namespace Victoria.Core.Interfaces
{
    public interface IOdooRpcClient
    {
        Task<int> AuthenticateAsync();
        Task<List<T>> SearchAndReadAsync<T>(string model, object[][] domain, string[] fields) where T : new();
        Task<bool> ExecuteAsync(string model, string method, object[] ids, Dictionary<string, object>? values = null);
        Task<object?> ExecuteActionAsync(string model, string method, object[] ids, Dictionary<string, object>? values = null);
    }
}
