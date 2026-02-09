using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Victoria.Inventory.Application.Services;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/outbound")]
    public class OutboundController : ControllerBase
    {
        private readonly OutboundOrderSyncService _syncService;
        private readonly WaveService _waveService;
        private readonly TaskService _taskService;

        public OutboundController(
            OutboundOrderSyncService syncService,
            WaveService waveService,
            TaskService taskService)
        {
            _syncService = syncService;
            _waveService = waveService;
            _taskService = taskService;
        }

        [HttpPost("sync")]
        public async Task<IActionResult> SyncOrders()
        {
            try
            {
                var count = await _syncService.SyncOrdersAsync();
                return Ok(new { message = $"Synced {count} outbound orders from Odoo." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("wave")]
        public async Task<IActionResult> AllocateWave([FromBody] Guid[] orderIds)
        {
            try
            {
                var waveId = await _waveService.AllocateWaveAsync(orderIds);
                return Ok(new { waveId, message = "Wave allocated successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("tasks")]
        public async Task<IActionResult> GetTasks([FromQuery] Guid? waveId)
        {
            try
            {
                var tasks = waveId.HasValue 
                    ? await _taskService.GetTasksByWaveAsync(waveId.Value)
                    : await _taskService.GetTasksAsync();
                
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetTasks: {ex.Message} \n {ex.StackTrace}");
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }
    }
}
