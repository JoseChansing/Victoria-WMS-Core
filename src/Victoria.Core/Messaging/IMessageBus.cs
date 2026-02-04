using System.Threading.Tasks;

namespace Victoria.Core.Messaging
{
    public interface IMessageBus
    {
        Task PublishAsync<T>(T message) where T : class;
    }
}
