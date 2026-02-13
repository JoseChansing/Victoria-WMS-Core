using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Marten;
using Victoria.Core.Models;
using System.Linq;

namespace Victoria.API.Controllers
{
    [ApiController]
    [Route("api/v1/notifications")]
    public class NotificationsController : ControllerBase
    {
        private readonly IDocumentSession _session;

        public NotificationsController(IDocumentSession session)
        {
            _session = session;
        }

        [HttpGet("unread")]
        public async Task<IActionResult> GetUnread()
        {
            var notifications = await _session.Query<SystemNotification>()
                .Where(x => !x.IsRead)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Ok(notifications);
        }

        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var notification = await _session.LoadAsync<SystemNotification>(id);
            if (notification == null) return NotFound();

            notification.IsRead = true;
            _session.Store(notification);
            await _session.SaveChangesAsync();

            return Ok(new { Success = true });
        }
    }
}
