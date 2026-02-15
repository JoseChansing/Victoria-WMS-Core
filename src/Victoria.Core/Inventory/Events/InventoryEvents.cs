using System;

namespace Victoria.Core.Inventory.Events
{
    public record InventoryTaskCreated(Guid TaskId, string TaskNumber, string Type, string Priority, string CreatedBy);
    public record InventoryTaskLineAdded(Guid TaskId, Guid LineId, string TargetId, int ExpectedQuantity);
    public record InventoryTaskLineReported(Guid TaskId, Guid LineId, int CountedQuantity, string UserId);
    public record InventoryTaskLineRemoved(Guid TaskId, Guid LineId, string LpnId, string Reason, string RemovedBy);
    public record InventoryTaskPriorityUpdated(Guid TaskId, string Priority);
    public record InventoryTaskAssigned(Guid TaskId, string UserId);
    public record CountReported(Guid TaskId, int CountedQuantity, string UserId);
    public record DiscrepancyFound(Guid TaskId, int Expected, int Counted);
    public record AdjustmentApproved(Guid TaskId, string SupervisorId, string OdooDetails);
    public record AdjustmentRejected(Guid TaskId, string RejectionReason);
    public record TaskCompleted(Guid TaskId, DateTime CompletedAt);
    public record TaskCancelled(Guid TaskId, string Reason);
}
