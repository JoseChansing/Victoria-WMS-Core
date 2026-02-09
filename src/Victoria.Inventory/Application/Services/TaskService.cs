using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marten;
using Microsoft.Extensions.Logging;
using InventoryTask = Victoria.Inventory.Domain.Entities.Task;
using InventoryTaskStatus = Victoria.Inventory.Domain.Entities.TaskStatus;
using Victoria.Inventory.Domain.Entities;

namespace Victoria.Inventory.Application.Services
{
    public class TaskService
    {
        private readonly IDocumentSession _session;
        private readonly ILogger<TaskService> _logger;

        public TaskService(IDocumentSession session, ILogger<TaskService> logger)
        {
            _session = session;
            _logger = logger;
        }

        public async Task<IReadOnlyList<InventoryTask>> GetTasksAsync()
        {
            _logger.LogInformation("Getting all tasks...");
            try 
            {
                var result = await _session.Query<InventoryTask>()
                    .Where(t => t.Status != InventoryTaskStatus.Completed && t.Status != InventoryTaskStatus.Cancelled)
                    .ToListAsync();
                _logger.LogInformation($"Found {result.Count} tasks.");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tasks from DB");
                throw;
            }
        }

        public async Task<IReadOnlyList<InventoryTask>> GetTasksByWaveAsync(Guid waveId)
        {
            return await _session.Query<InventoryTask>()
                .Where(t => t.WaveId == waveId)
                .ToListAsync();
        }
    }
}
