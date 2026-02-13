using System;
using System.Threading.Tasks;
using Marten;
using Victoria.Inventory.Domain.Aggregates;

namespace Victoria.Inventory.Application.Commands
{
    public record VoidLpnCommand(string LpnId, string Reason, string UserId, string StationId);

    public class VoidLpnHandler
    {
        private readonly IDocumentSession _session;

        public VoidLpnHandler(IDocumentSession session)
        {
            _session = session;
        }

        public async Task Handle(VoidLpnCommand command)
        {
            // 1. Load LPN
            var lpn = await _session.LoadAsync<Lpn>(command.LpnId);
            if (lpn == null) throw new InvalidOperationException($"LPN {command.LpnId} not found.");

            // 2. Load Inbound Order
            if (string.IsNullOrEmpty(lpn.SelectedOrderId))
                throw new InvalidOperationException("LPN is not associated with an Inbound Order.");

            var order = await _session.LoadAsync<InboundOrder>(lpn.SelectedOrderId);
            if (order == null) throw new InvalidOperationException($"Inbound Order {lpn.SelectedOrderId} not found.");

            // 3. Execute Domain Logic
            int qtyToRevert = lpn.Quantity;
            string sku = lpn.Sku.Value;

            lpn.Void(command.Reason, command.UserId, command.StationId);
            order.RevertReception(sku, qtyToRevert);

            // 4. Persistence
            _session.Store(lpn);
            _session.Store(order);
            await _session.SaveChangesAsync();
        }
    }
}
