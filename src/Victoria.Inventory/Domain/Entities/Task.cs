using System;

namespace Victoria.Inventory.Domain.Entities
{
    public enum TaskType
    {
        PickToTote,
        FullPalletMove,
        CycleCount
    }

    public enum TaskStatus
    {
        Pending,
        InProgress,
        Completed,
        Short,   // For when a picker cannot find the item
        Cancelled
    }

    public class Task
    {
        public Guid Id { get; set; }
        public Guid WaveId { get; private set; }
        public Guid OutboundOrderId { get; private set; } // Nullable if generic task? For now let's keep it linked or null for CycleCount
        public string LineId { get; private set; } // Reference to OutboundLine (if picking)

        public TaskType Type { get; private set; }
        public TaskStatus Status { get; private set; }

        // Details
        public string SourceLocation { get; private set; }
        public string TargetLocation { get; private set; } // e.g. Tote ID or Dock
        public string LpnId { get; private set; }
        public string ProductId { get; private set; } // Odoo Product ID or SKU
        public double Quantity { get; private set; }
        public double PickedQuantity { get; private set; }
        
        public DateTime CreatedAt { get; private set; }
        public string AssociatedToteId { get; private set; }

        public Task(Guid waveId, Guid orderId, string lineId, TaskType type, string sourceLocation, string targetLocation, string lpnId, string productId, double quantity)
        {
            Id = Guid.NewGuid();
            WaveId = waveId;
            OutboundOrderId = orderId;
            LineId = lineId;
            Type = type;
            Status = TaskStatus.Pending;
            SourceLocation = sourceLocation;
            TargetLocation = targetLocation;
            LpnId = lpnId;
            ProductId = productId;
            Quantity = quantity;
            CreatedAt = DateTime.UtcNow;
        }
        
        // Factory for CycleCount
        public static Task CreateCycleCount(Guid waveId, string locationId, string expectedProductId)
        {
             return new Task(waveId, Guid.Empty, string.Empty, TaskType.CycleCount, locationId, "SYSTEM", string.Empty, expectedProductId, 0);
        }

        public static Task CreateFullPalletMove(Guid waveId, string sourceLocation, double quantity, string productId, string lpnId)
        {
            return new Task(waveId, Guid.Empty, string.Empty, TaskType.FullPalletMove, sourceLocation, "DOCK-OUT", lpnId, productId, quantity);
        }

        public static Task CreatePickToTote(Guid waveId, Guid orderId, string lineId, string sourceLocation, string targetTote, string lpnId, string productId, double quantity)
        {
            return new Task(waveId, orderId, lineId, TaskType.PickToTote, sourceLocation, targetTote, lpnId, productId, quantity);
        }

        public void Complete(double pickedQty, string? toteId = null)
        {
            PickedQuantity = pickedQty;
            AssociatedToteId = toteId ?? string.Empty;
            Status = (pickedQty < Quantity) ? TaskStatus.Short : TaskStatus.Completed;
        }
    }
}
