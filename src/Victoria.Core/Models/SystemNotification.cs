using System;

namespace Victoria.Core.Models
{
    /// <summary>
    /// Agregado ligero para notificaciones y alertas del sistema (Guardian, Errores, etc)
    /// </summary>
    public class SystemNotification
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Critical, Warning, Info
        /// </summary>
        public string Severity { get; set; } = "Info";
        
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Opcional: Referencia a una entidad (ej: el OrderNumber de la orden huérfana)
        /// </summary>
        public string? ReferenceId { get; set; }
    }
}
