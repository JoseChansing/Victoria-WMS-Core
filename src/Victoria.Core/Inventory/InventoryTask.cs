using System;
using System.Collections.Generic;
using Victoria.Core.Inventory.Events;

namespace Victoria.Core.Inventory
{
    public enum TaskType
    {
        Putaway,
        CycleCount,
        Replenishment,
        Investigation,
        TakeSample
    }

    public enum TaskPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    public enum TaskStatus
    {
        Pending,
        Assigned,
        InProgress,
        PendingApproval,
        Syncing,
        Completed,
        Cancelled
    }

    public enum LineStatus
    {
        Pending,
        Counted,
        Verified
    }

    public class InventoryTaskLine
    {
        public Guid Id { get; set; }
        public string TargetId { get; set; } = string.Empty; // LPN/SKU/Loc
        public string TargetDescription { get; set; } = string.Empty;
        public int ExpectedQty { get; set; }
        public int CountedQty { get; set; }
        public LineStatus Status { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class InventoryTask
    {
        public Guid Id { get; set; }
        public string TaskNumber { get; set; } = string.Empty;
        public TaskType Type { get; set; }
        public TaskPriority Priority { get; set; }
        public TaskStatus Status { get; set; }

        public List<InventoryTaskLine> Lines { get; set; } = new();

        public string? AssignedUserId { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectionReason { get; set; }

        // Marten events
        public List<object> _pendingEvents = new();

        public InventoryTask() { }

        public static InventoryTask Create(string taskNumber, TaskType type, TaskPriority priority, string createdBy)
        {
            var task = new InventoryTask
            {
                Id = Guid.NewGuid(),
                TaskNumber = taskNumber,
                Type = type,
                Priority = priority,
                Status = TaskStatus.Pending,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };
            
            return task;
        }

        public void AddLine(string targetId, string description, int expectedQty)
        {
            Lines.Add(new InventoryTaskLine
            {
                Id = Guid.NewGuid(),
                TargetId = targetId,
                TargetDescription = description,
                ExpectedQty = expectedQty,
                Status = LineStatus.Pending
            });
        }

        public void RemoveLine(Guid lineId, string reason)
        {
            if (Status == TaskStatus.Completed || Status == TaskStatus.Cancelled)
                throw new InvalidOperationException("Cannot remove lines from a closed task.");
            
            var line = Lines.Find(l => l.Id == lineId);
            if (line == null)
                throw new ArgumentException($"Line {lineId} not found in task.");
            
            Lines.Remove(line);
            
            // If task becomes empty, auto-cancel it
            if (Lines.Count == 0)
            {
                Status = TaskStatus.Cancelled;
                RejectionReason = $"Auto-cancelled: All lines removed. Reason: {reason}";
            }
        }

        public void Assign(string userId)
        {
            if (Status == TaskStatus.Completed || Status == TaskStatus.Cancelled) 
                throw new InvalidOperationException("Cannot assign completed or cancelled task.");
            
            Status = TaskStatus.Assigned;
            AssignedUserId = userId;
        }

        public void UpdatePriority(TaskPriority priority)
        {
            if (Status == TaskStatus.Completed || Status == TaskStatus.Cancelled)
                throw new InvalidOperationException("Cannot update priority of a closed task.");
            
            Priority = priority;
        }

        public void ReportLineCount(Guid lineId, int qty, string userId)
        {
            if (Status == TaskStatus.Completed || Status == TaskStatus.Cancelled) 
                throw new InvalidOperationException("Task is closed.");

            var line = Lines.Find(l => l.Id == lineId);
            if (line == null) throw new ArgumentException("Line not found");

            line.CountedQty = qty;
            line.Status = LineStatus.Counted;
            line.CompletedAt = DateTime.UtcNow;

            Status = TaskStatus.InProgress;

            // Simple logic: if all lines counted, move task forward
            if (Lines.TrueForAll(l => l.Status == LineStatus.Counted))
            {
                if (Lines.TrueForAll(l => l.CountedQty == l.ExpectedQty))
                {
                    Status = TaskStatus.Completed;
                }
                else
                {
                    Status = TaskStatus.PendingApproval;
                }
            }
        }

        public void Approve(string supervisorId)
        {
            if (Status != TaskStatus.PendingApproval)
                throw new InvalidOperationException("Task is not pending approval.");

            Status = TaskStatus.Completed;
            ApprovedBy = supervisorId;
            ApprovedAt = DateTime.UtcNow;

            foreach (var line in Lines)
            {
                line.Status = LineStatus.Verified;
            }
        }

        public void Reject(string reason)
        {
             if (Status != TaskStatus.PendingApproval)
                throw new InvalidOperationException("Task is not pending approval.");

            Status = TaskStatus.InProgress; // Send back to operator
            RejectionReason = reason;

            foreach (var line in Lines)
            {
                if (line.CountedQty != line.ExpectedQty)
                {
                    line.Status = LineStatus.Pending; // Mark for re-count
                }
            }
        }

        public void Cancel(string reason)
        {
            if (Status == TaskStatus.Completed || Status == TaskStatus.Cancelled)
                throw new InvalidOperationException("Cannot cancel a closed task.");
            
            Status = TaskStatus.Cancelled;
            RejectionReason = reason; 
        }
    }
}
