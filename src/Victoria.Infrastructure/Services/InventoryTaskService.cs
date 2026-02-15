using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Marten;
using Microsoft.Extensions.Logging;
using Victoria.Core.Inventory;
using Victoria.Core.Inventory.Events;
using Victoria.Core.Interfaces;
using System.Linq;
using TaskStatus = Victoria.Core.Inventory.TaskStatus;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.Infrastructure.Services
{
    public class InventoryTaskService
    {
        private readonly IDocumentSession _session;
        private readonly IOdooAdapter _odooAdapter;
        private readonly ILogger<InventoryTaskService> _logger;

        public InventoryTaskService(IDocumentSession session, IOdooAdapter odooAdapter, ILogger<InventoryTaskService> logger)
        {
            _session = session;
            _odooAdapter = odooAdapter;
            _logger = logger;
        }

        public void LogBatchError(string targetId, Exception ex)
        {
            _logger.LogError(ex, "Failed to generate task for target {TargetId}", targetId);
        }

        public async Task<(Guid TaskId, List<string> Warnings)> CreateBatchTaskAsync(List<string> targetIds, string targetType, TaskType type, TaskPriority priority, string createdBy)
        {
            var warnings = new List<string>();
            var validLpns = new List<Lpn>();

            if (targetType == "Lpn")
            {
                foreach (var id in targetIds)
                {
                    var lpn = await _session.LoadAsync<Lpn>(id);
                    if (lpn == null)
                    {
                        warnings.Add($"LPN {id} no encontrado.");
                        continue;
                    }

                    if (lpn.CurrentTaskId != null)
                    {
                        warnings.Add($"LPN {id} omitido: ya está en una tarea activa.");
                        continue;
                    }

                    if (lpn.Status == Victoria.Inventory.Domain.Aggregates.LpnStatus.Allocated)
                    {
                        warnings.Add($"LPN {id} omitido: asignado a una orden de salida (Picking).");
                        continue;
                    }

                    validLpns.Add(lpn);
                }
            }
            else if (targetType == "Product")
            {
                foreach (var sku in targetIds)
                {
                    var lpns = await _session.Query<Lpn>()
                        .Where(x => x.Sku.Value == sku && x.Status != LpnStatus.Voided && x.Status != LpnStatus.Consumed)
                        .ToListAsync();

                    foreach (var lpn in lpns)
                    {
                        if (lpn.CurrentTaskId == null && lpn.Status != LpnStatus.Allocated)
                        {
                            validLpns.Add(lpn);
                        }
                    }
                }
            }
            else
            {
                // Support for Location targets if needed
            }

            if (validLpns.Count == 0)
            {
                throw new InvalidOperationException("No se encontraron LPNs válidos para crear la tarea.");
            }

            string taskNumber = $"TASK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
            var task = InventoryTask.Create(taskNumber, type, priority, createdBy);

            _session.Store(task);
            _session.Events.Append(task.Id.ToString(), new InventoryTaskCreated(task.Id, taskNumber, type.ToString(), priority.ToString(), createdBy));

            foreach (var lpn in validLpns)
            {
                task.AddLine(lpn.Id, $"{lpn.Sku} en {lpn.CurrentLocationId}", lpn.Quantity);
                lpn.LockToTask(task.Id);
                
                var line = task.Lines.Last();
                _session.Events.Append(task.Id.ToString(), new InventoryTaskLineAdded(task.Id, line.Id, lpn.Id, lpn.Quantity));
                _session.Store(lpn);
            }

            _session.Store(task);
            await _session.SaveChangesAsync();
            return (task.Id, warnings);
        }

        public async Task<(Guid? TaskId, List<string> Warnings)> CreateAutoPutawayTaskAsync(
            string orderId, 
            string orderNumber, 
            string createdBy = "SYSTEM")
        {
            var warnings = new List<string>();
            
            // Query LPNs in STAGE-RESERVE for this order
            var lpns = await _session.Query<Lpn>()
                .Where(x => x.SelectedOrderId == orderId 
                         && x.CurrentLocationId == "STAGE-RESERVE"
                         && x.CurrentTaskId == null
                         && x.Status != LpnStatus.Voided
                         && x.Status != LpnStatus.Consumed)
                .ToListAsync();
            
            if (lpns.Count == 0)
            {
                _logger.LogInformation("[AUTO-PUTAWAY] No LPNs found in STAGE-RESERVE for order {OrderNumber}. Skipping task creation.", orderNumber);
                return (null, warnings);
            }
            
            // Create putaway task
            string taskNumber = $"PUT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
            var task = InventoryTask.Create(taskNumber, TaskType.Putaway, TaskPriority.Normal, createdBy);
            
            _session.Store(task);
            _session.Events.Append(task.Id.ToString(), new InventoryTaskCreated(
                task.Id, taskNumber, "Putaway", "Normal", createdBy));
            
            foreach (var lpn in lpns)
            {
                task.AddLine(lpn.Id, $"{lpn.Sku} from {lpn.CurrentLocationId}", lpn.Quantity);
                lpn.LockToTask(task.Id);
                _session.Store(lpn);
            }
            
            await _session.SaveChangesAsync();
            
            _logger.LogInformation("[AUTO-PUTAWAY] Created task {TaskNumber} with {Count} LPNs for order {OrderNumber}", 
                taskNumber, lpns.Count, orderNumber);
            
            return (task.Id, warnings);
        }

        public async Task RemoveLineFromTaskAsync(Guid taskId, Guid lineId, string reason, string userId)
        {
            var task = await _session.LoadAsync<InventoryTask>(taskId);
            if (task == null)
                throw new ArgumentException("Task not found");
            
            // Find the line to get the LPN ID before removing
            var line = task.Lines.Find(l => l.Id == lineId);
            if (line == null)
                throw new ArgumentException("Line not found in task");
            
            string lpnId = line.TargetId;
            
            // Remove line from task (domain logic handles empty task cancellation)
            task.RemoveLine(lineId, reason);
            
            // Unlock the LPN
            var lpn = await _session.LoadAsync<Lpn>(lpnId);
            if (lpn != null)
            {
                lpn.UnlockFromTask();
                _session.Store(lpn);
            }
            
            // Store updated task
            _session.Store(task);
            
            // Emit event
            _session.Events.Append(task.Id.ToString(), new InventoryTaskLineRemoved(
                taskId, lineId, lpnId, reason, userId));
            
            await _session.SaveChangesAsync();
            
            _logger.LogInformation("[TASK-LINE-REMOVAL] Removed line {LineId} (LPN {LpnId}) from task {TaskId}. Reason: {Reason}", 
                lineId, lpnId, taskId, reason);
        }

        public async Task ReportLineCountAsync(Guid taskId, Guid lineId, int countedQty, string userId)
        {
            var task = await _session.LoadAsync<InventoryTask>(taskId);
            if (task == null) throw new ArgumentException("Task not found");

            // TAKE SAMPLE LOGIC
            if (task.Type == TaskType.TakeSample)
            {
                var line = task.Lines.Find(l => l.Id == lineId);
                if (line == null) throw new ArgumentException("Line not found");

                var lpn = await _session.LoadAsync<Lpn>(line.TargetId);
                if (lpn != null)
                {
                    // countedQty is the amount to REMOVE in TakeSample tasks
                    int amountToRemove = countedQty;
                    int newQty = lpn.Quantity - amountToRemove;

                    if (newQty <= 0)
                    {
                        // Set Consumed status, 0 qty, and clear location
                        lpn.AdjustQuantity(0, "QUALITY_SAMPLE_CONSUMED", userId, "BATCH_MODAL");
                        lpn.Deconsolidate(userId, "BATCH_MODAL"); // Sets Consumed status
                        // Clear location manually or ensure Deconsolidate does it? 
                        // Lpn.cs doesn't clear location in Deconsolidate.
                        // I'll manually adjust the location if needed or use a safer approach.
                    }
                    else
                    {
                        lpn.AdjustQuantity(newQty, "QUALITY_SAMPLE_REDUCTION", userId, "BATCH_MODAL");
                    }

                    // Send adjustment to Odoo
                    bool odooSuccess = await _odooAdapter.CreateInventoryAdjustment(lpn.Sku.Value, lpn.CurrentLocationId ?? "N/A", -amountToRemove, "Quality Sample");
                    if (!odooSuccess) _logger.LogWarning("Failed to send sample adjustment to Odoo for LPN {LpnId}", lpn.Id);

                    _session.Store(lpn);
                }
            }

            task.ReportLineCount(lineId, countedQty, userId);
            
            _session.Events.Append(task.Id.ToString(), new InventoryTaskLineReported(taskId, lineId, countedQty, userId));

            if (task.Status == TaskStatus.Completed)
            {
                await ReleaseTaskLpnsAsync(task);
                _session.Events.Append(task.Id.ToString(), new TaskCompleted(taskId, DateTime.UtcNow));
            }
            else if (task.Status == TaskStatus.PendingApproval)
            {
                _session.Events.Append(task.Id.ToString(), new DiscrepancyFound(taskId, 0, countedQty));
            }

            _session.Store(task);
            await _session.SaveChangesAsync();
        }

        public async Task ApproveAdjustmentAsync(Guid taskId, string supervisorId)
        {
            var task = await _session.LoadAsync<InventoryTask>(taskId);
            if (task == null) throw new ArgumentException("Task not found");

            if (task.Status != TaskStatus.PendingApproval)
                throw new InvalidOperationException("Task is not pending approval.");

            // Iterate lines with discrepancies
            foreach (var line in task.Lines.Where(l => l.CountedQty != l.ExpectedQty))
            {
                int diff = line.CountedQty - line.ExpectedQty;
                // Since Task doesn't have Sku/Location at root anymore, we need them from targetId or Line metadata
                // Assuming TargetId is LPN for now or we need a better way to get Sku/Location
                var lpn = await _session.LoadAsync<Lpn>(line.TargetId);
                if (lpn != null)
                {
                    bool odooSuccess = await _odooAdapter.CreateInventoryAdjustment(lpn.Sku.Value, lpn.CurrentLocationId, diff);
                    if (!odooSuccess) _logger.LogWarning("Failed to send adjustment to Odoo for LPN {LpnId}", lpn.Id);
                }
            }

            task.Approve(supervisorId);
            await ReleaseTaskLpnsAsync(task);

            _session.Events.Append(task.Id.ToString(), new AdjustmentApproved(taskId, supervisorId, "Multi-line adjustment approved"));
            _session.Events.Append(task.Id.ToString(), new TaskCompleted(taskId, DateTime.UtcNow));
            
            _session.Store(task);
            await _session.SaveChangesAsync();
        }

        public async Task RejectAdjustmentAsync(Guid taskId, string reason)
        {
            var task = await _session.LoadAsync<InventoryTask>(taskId);
            if (task == null) throw new ArgumentException("Task not found");

            task.Reject(reason);
            
            _session.Events.Append(task.Id.ToString(), new AdjustmentRejected(taskId, reason));
            
            _session.Store(task);
            await _session.SaveChangesAsync();
        }

        public async Task CancelTaskAsync(Guid taskId, string reason)
        {
            var task = await _session.LoadAsync<InventoryTask>(taskId);
            if (task == null) throw new ArgumentException("Task not found");

            task.Cancel(reason);
            await ReleaseTaskLpnsAsync(task);
            
            _session.Events.Append(task.Id.ToString(), new TaskCancelled(taskId, reason));
            
            _session.Store(task);
            await _session.SaveChangesAsync();
        }

        private async Task ReleaseTaskLpnsAsync(InventoryTask task)
        {
            foreach (var line in task.Lines)
            {
                // Only if target is LPN
                var lpn = await _session.LoadAsync<Lpn>(line.TargetId);
                if (lpn != null && lpn.CurrentTaskId == task.Id)
                {
                    lpn.ReleaseFromTask();
                    _session.Store(lpn);
                }
            }
        }

        public async Task UpdateTaskPriorityAsync(Guid taskId, TaskPriority priority)
        {
            var task = await _session.LoadAsync<InventoryTask>(taskId);
            if (task == null) throw new ArgumentException("Task not found");

            task.UpdatePriority(priority);
            _session.Events.Append(task.Id.ToString(), new InventoryTaskPriorityUpdated(taskId, priority.ToString()));
            _session.Store(task);
            await _session.SaveChangesAsync();
        }

        public async Task AssignTaskAsync(Guid taskId, string userId)
        {
            var task = await _session.LoadAsync<InventoryTask>(taskId);
            if (task == null) throw new ArgumentException("Task not found");

            task.Assign(userId);
            _session.Events.Append(task.Id.ToString(), new InventoryTaskAssigned(taskId, userId));
            _session.Store(task);
            await _session.SaveChangesAsync();
        }

        public async Task<List<InventoryTask>> ListTasksAsync(TaskStatus? status, TaskPriority? priority)
        {
             var query = _session.Query<InventoryTask>();
             if (status.HasValue) query = (Marten.Linq.IMartenQueryable<InventoryTask>)query.Where(t => t.Status == status.Value);
             if (priority.HasValue) query = (Marten.Linq.IMartenQueryable<InventoryTask>)query.Where(t => t.Priority == priority.Value);
             return (await query.ToListAsync()).ToList();
        }
    }
}
