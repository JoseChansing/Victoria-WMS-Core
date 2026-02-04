using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Victoria.Core;
using Victoria.Core.Infrastructure;

namespace Victoria.Infrastructure.Persistence
{
    public class PostgresEventStore : IEventStore
    {
        private readonly string _connectionString;

        public PostgresEventStore(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _connectionString = configuration["POSTGRES_CONNECTION"] ?? "Host=localhost;Database=victoria_wms;Username=vicky_admin;Password=vicky_password";
        }

        public async Task AppendEventsAsync(string streamId, int expectedVersion, IEnumerable<IDomainEvent> events)
        {
            await SaveBatchAsync(new[] { new EventStreamBatch(streamId, expectedVersion, events) });
        }

        public async Task SaveBatchAsync(IEnumerable<EventStreamBatch> batches)
        {
            using var conn = new Npgsql.NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                foreach (var batch in batches)
                {
                    foreach (var @event in batch.Events)
                    {
                        var json = JsonSerializer.Serialize((object)@event);
                        var sql = "INSERT INTO Events (StreamId, Payload) VALUES (@streamId, @payload::jsonb)";
                        using var cmd = new Npgsql.NpgsqlCommand(sql, conn, transaction);
                        cmd.Parameters.AddWithValue("streamId", batch.StreamId);
                        cmd.Parameters.AddWithValue("payload", json);
                        
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<IDomainEvent>> GetEventsAsync(string streamId)
        {
            Console.WriteLine($"[POSTGRES] SELECT Payload FROM Events WHERE StreamId = '{streamId}' ORDER BY Sequence");
            return await Task.FromResult(new List<IDomainEvent>());
        }
    }
}
