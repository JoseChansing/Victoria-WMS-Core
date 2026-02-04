using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Victoria.Core.Messaging;

namespace Victoria.Infrastructure.Messaging
{
    public class InMemoryMessageBus : IMessageBus
    {
        private readonly IServiceProvider _serviceProvider;

        public InMemoryMessageBus(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task PublishAsync<T>(T message) where T : class
        {
            Console.WriteLine($"[BUS] Message Published: {typeof(T).Name} at {DateTime.UtcNow}");
            
            // Simulación de ruteo asíncrono a consumidores registrados
            if (message is Victoria.Inventory.Domain.Events.DispatchConfirmed dispatchEvent)
            {
                using var scope = _serviceProvider.CreateScope();
                var consumer = scope.ServiceProvider.GetService<Victoria.Infrastructure.Integration.Odoo.OdooFeedbackConsumer>();
                if (consumer != null)
                {
                    await consumer.Handle(dispatchEvent);
                }
            }

            await Task.CompletedTask;
        }
    }
}
