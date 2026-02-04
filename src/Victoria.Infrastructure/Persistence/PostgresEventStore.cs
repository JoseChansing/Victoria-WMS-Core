using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using Victoria.Core;
using Victoria.Core.Infrastructure;

namespace Victoria.Infrastructure.Persistence
{
    public class PostgresEventStore : IEventStore
    {
        // En un entorno real se usaría Marten o Npgsql con JSONB
        // Aquí simulamos el comportamiento para el Walking Skeleton
        
        public async Task AppendEventsAsync(string streamId, int expectedVersion, IEnumerable<IDomainEvent> events)
        {
            Console.WriteLine($"[POSTGRES] Opening transaction for stream: {streamId}");
            
            foreach (var @event in events)
            {
                // REQUISITO: Serialización a JSONB
                var json = JsonSerializer.Serialize((object)@event);
                Console.WriteLine($"[POSTGRES] INSERT INTO Events (StreamId, Payload) VALUES ('{streamId}', '{json}'::jsonb)");
            }

            Console.WriteLine("[POSTGRES] Committing transaction");
            await Task.CompletedTask;
        }

        public async Task<IEnumerable<IDomainEvent>> GetEventsAsync(string streamId)
        {
            Console.WriteLine($"[POSTGRES] SELECT Payload FROM Events WHERE StreamId = '{streamId}' ORDER BY Sequence");
            return await Task.FromResult(new List<IDomainEvent>());
        }
    }
}
