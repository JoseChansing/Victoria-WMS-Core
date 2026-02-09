using System;
using System.Threading.Tasks;
using Victoria.Inventory.Domain.Services;
using Victoria.Inventory.Domain.ValueObjects;
using Victoria.Core;

namespace Victoria.Inventory.Application.Commands
{
    public class AllocateOrderCommand
    {
        public string TenantId { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
    }

    public class AllocateOrderHandler
    {
        private readonly AllocationService _allocationService;

        public AllocateOrderHandler(AllocationService allocationService)
        {
            _allocationService = allocationService;
        }

        public async Task Handle(AllocateOrderCommand command)
        {
            // Delegar al Servicio de Dominio (Cerebro)
            await _allocationService.AllocateStockForOrder(
                command.TenantId,
                command.OrderId, 
                Sku.Create(command.Sku), 
                command.Quantity, 
                command.UserId, 
                command.StationId);
        }
    }
}
