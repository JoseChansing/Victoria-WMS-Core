using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Victoria.Core.Inventory;
using Victoria.Infrastructure.Services;
using System.Collections.Generic;
using TaskStatus = Victoria.Core.Inventory.TaskStatus;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/inventory/tasks")]
    public class InventoryTaskController : ControllerBase
    {
        private readonly InventoryTaskService _service;

        public InventoryTaskController(InventoryTaskService service)
        {
            _service = service;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateTask([FromBody] GenerateTaskRequest request)
        {
            var createdBy = User.Identity?.Name ?? "system"; 
            var targetIds = new List<string>();
            string targetType = "Product";
            
            if (!string.IsNullOrEmpty(request.LocationCode)) {
                targetIds.Add(request.LocationCode);
                targetType = "Location";
            } else if (!string.IsNullOrEmpty(request.ProductSku)) {
                targetIds.Add(request.ProductSku);
                targetType = "Product";
            }
            
            try
            {
                var (taskId, _) = await _service.CreateBatchTaskAsync(targetIds, targetType, request.Type, request.Priority, createdBy);
                return Ok(new { TaskId = taskId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("batch")]
        public async Task<IActionResult> GenerateBatchTasks([FromBody] CreateBatchTasksDto request)
        {
            var createdBy = User.Identity?.Name ?? "system";
            
            if (!Enum.TryParse<TaskType>(request.TaskType, out var taskTypeVal)) taskTypeVal = TaskType.CycleCount;
            if (!Enum.TryParse<TaskPriority>(request.Priority, out var priorityVal)) priorityVal = TaskPriority.Normal;

            try
            {
                var (taskId, warnings) = await _service.CreateBatchTaskAsync(request.TargetIds, request.TargetType, taskTypeVal, priorityVal, createdBy);
                return Ok(new { TaskId = taskId, Warnings = warnings });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("{id}/report")]
        public async Task<IActionResult> ReportCount(Guid id, [FromBody] ReportCountRequest request)
        {
            var userId = User.Identity?.Name ?? "operator";
            try
            {
                await _service.ReportLineCountAsync(id, request.LineId, request.CountedQuantity, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("{id}/approve")]
        public async Task<IActionResult> ApproveAdjustment(Guid id)
        {
            var supervisorId = User.Identity?.Name ?? "supervisor";
            try 
            {
                await _service.ApproveAdjustmentAsync(id, supervisorId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("{id}/reject")]
        public async Task<IActionResult> RejectAdjustment(Guid id, [FromBody] RejectAdjustmentRequest request)
        {
            try
            {
                await _service.RejectAdjustmentAsync(id, request.Reason);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPatch("{id}/priority")]
        public async Task<IActionResult> UpdatePriority(Guid id, [FromBody] UpdatePriorityRequest request)
        {
            try
            {
                await _service.UpdateTaskPriorityAsync(id, request.Priority);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("{id}/assign")]
        public async Task<IActionResult> AssignTask(Guid id, [FromBody] AssignTaskRequest request)
        {
            try
            {
                await _service.AssignTaskAsync(id, request.UserId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelTask(Guid id, [FromBody] CancelTaskRequest request)
        {
            try
            {
                await _service.CancelTaskAsync(id, request.Reason);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpDelete("{id}/lines/{lineId}")]
        public async Task<IActionResult> RemoveLine(Guid id, Guid lineId, [FromBody] RemoveLineRequest request)
        {
            var userId = User.Identity?.Name ?? "supervisor";
            try
            {
                await _service.RemoveLineFromTaskAsync(id, lineId, request.Reason ?? "Manual removal", userId);
                return Ok(new { Message = "Line removed successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ListTasks([FromQuery] TaskStatus? status, [FromQuery] TaskPriority? priority)
        {
            var tasks = await _service.ListTasksAsync(status, priority);
            return Ok(tasks);
        }
    }

    public class GenerateTaskRequest
    {
        public string LocationCode { get; set; } = string.Empty;
        public string ProductSku { get; set; } = string.Empty;
        public TaskType Type { get; set; }
        public TaskPriority Priority { get; set; }
    }

    public class ReportCountRequest
    {
        public Guid LineId { get; set; }
        public int CountedQuantity { get; set; }
    }

    public class RejectAdjustmentRequest
    {
         public string Reason { get; set; } = string.Empty;
    }

    public class UpdatePriorityRequest
    {
        public TaskPriority Priority { get; set; }
    }

    public class AssignTaskRequest
    {
        public string UserId { get; set; } = string.Empty;
    }

    public class CreateBatchTasksDto
    {
        public string TaskType { get; set; } = "CycleCount";
        public string Priority { get; set; } = "Normal";
        public string TargetType { get; set; } // "Location", "Product", "Lpn"
        public List<string> TargetIds { get; set; } = new();
    }

    public class CancelTaskRequest
    {
        public string Reason { get; set; } = "Cancelled by supervisor";
    }

    public class RemoveLineRequest
    {
        public string? Reason { get; set; }
    }
}
