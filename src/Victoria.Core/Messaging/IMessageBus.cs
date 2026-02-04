using System;
using System.Threading.Tasks;

namespace Victoria.Core.Messaging
{
    public interface IMessageBus
    {
        Task PublishAsync<T>(T message) where T : class;
    }

    public class InMemoryMessageBus : IMessageBus
    {
        public Task PublishAsync<T>(T message) where T : class
        {
            // Simulación de envío a cola asíncrona
            Console.WriteLine($"[BUS] Message Published: {typeof(T).Name} at {DateTime.UtcNow}");
            return Task.CompletedTask;
        }
    }
}
