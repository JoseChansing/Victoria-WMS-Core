using System;
using System.Collections.Generic;
using System.Linq;

namespace Victoria.Inventory.Domain.Aggregates
{
    public enum WaveStatus
    {
        Planned,
        Allocated,
        Released,
        Completed
    }

    public class Wave
    {
        public Guid Id { get; set; }
        public string WaveNumber { get; private set; }
        public WaveStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? ReleasedAt { get; private set; }
        
        private readonly List<Guid> _orderIds = new List<Guid>();
        public IReadOnlyCollection<Guid> OrderIds => _orderIds.AsReadOnly();

        public Wave(string waveNumber)
        {
            Id = Guid.NewGuid();
            WaveNumber = waveNumber;
            Status = WaveStatus.Planned;
            CreatedAt = DateTime.UtcNow;
        }

        public void AddOrder(Guid orderId)
        {
            if (Status != WaveStatus.Planned)
                throw new InvalidOperationException("Cannot add orders to a wave that is already processed.");
            
            if (!_orderIds.Contains(orderId))
            {
                _orderIds.Add(orderId);
            }
        }

        public void Allocate()
        {
            if (Status != WaveStatus.Planned) return;
            Status = WaveStatus.Allocated;
        }

        public void Release()
        {
            if (Status != WaveStatus.Allocated)
                throw new InvalidOperationException("Wave must be allocated before releasing.");
            
            Status = WaveStatus.Released;
            ReleasedAt = DateTime.UtcNow;
        }

        public void Complete()
        {
            Status = WaveStatus.Completed;
        }
    }
}
