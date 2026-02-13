using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Victoria.Core.Interfaces;

namespace Victoria.Infrastructure.Integration.Odoo
{
    public class OdooMoveDto
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public long LocationId { get; set; }
        public long LocationDestId { get; set; }
        public long ProductUom { get; set; }
    }

    public class OdooPickingDto
    {
        public long Id { get; set; }
        public string State { get; set; } = string.Empty;
    }

    public class OdooMoveLineDto
    {
        public long Id { get; set; }
        public double Quantity { get; set; }
    }

    public class OdooAdapter : IOdooAdapter
    {
        private readonly IOdooRpcClient _rpcClient;
        private readonly ILogger<OdooAdapter> _logger;

        public OdooAdapter(IOdooRpcClient rpcClient, ILogger<OdooAdapter> logger)
        {
            _rpcClient = rpcClient;
            _logger = logger;
        }

        public async Task<bool> ConfirmReceiptAsync(long pickingId, Dictionary<long, int> moveQuantities)
        {
            try
            {
                // 0. Validación de Entrada
                if (moveQuantities == null || moveQuantities.Count == 0)
                {
                    _logger.LogWarning("[ODOO-ADAPTER] No hay cantidades para sincronizar (Picking {Id}).", pickingId);
                    return false;
                }

                _logger.LogInformation("[ODOO-ADAPTER] INICIO Sincronización Strict-Mode (Iterativa) para Picking {Id}", pickingId);

                // PASO 1: Verificar Estado y LIMPIAR RESERVAS (UNRESERVE)
                // Es vital limpiar la "memoria" de Odoo sobre lo que esperaba recibir.
                var pickingFields = new string[] { "id", "state" };
                var pickings = await _rpcClient.SearchAndReadAsync<OdooPickingDto>("stock.picking", 
                    new object[][] { new object[] { "id", "=", (int)pickingId } }, pickingFields);

                if (pickings != null && pickings.Count > 0)
                {
                    var pick = pickings[0];
                    // Estados donde tiene sentido hacer unreserve
                    if (pick.State == "assigned" || pick.State == "confirmed" || pick.State == "partially_available")
                    {
                        _logger.LogInformation("[ODOO-ADAPTER] Ejecutando 'do_unreserve' para limpiar estado previo...");
                        await _rpcClient.ExecuteActionAsync("stock.picking", "do_unreserve", new object[] { new object[] { (int)pickingId } });
                    }
                }
                else 
                {
                    _logger.LogError("[ODOO-ADAPTER] Picking {Id} no encontrado en Odoo.", pickingId);
                    return false;
                }

                // PASO 2: Bucle de Procesamiento (Iteración Lineal)
                foreach (var mq in moveQuantities)
                {
                    int moveId = (int)mq.Key;
                    int qty = mq.Value;

                    if (moveId <= 0) continue;

                    try
                    {
                        // Leer datos frescos del movimiento apuntado
                        var moveFields = new string[] { "id", "product_id", "location_id", "location_dest_id", "product_uom" };
                        var moves = await _rpcClient.SearchAndReadAsync<OdooMoveDto>("stock.move", 
                            new object[][] { new object[] { "id", "=", moveId } }, moveFields);

                        if (moves == null || moves.Count == 0)
                        {
                            _logger.LogWarning("[ODOO-ADAPTER] Move {MoveId} no encontrado. Saltando.", moveId);
                            continue;
                        }

                        var move = moves[0];
                        _logger.LogInformation("📦 [ODOO-DEBUG] Move {MoveId} Data: ProductId={P}, Uom={U}", moveId, move.ProductId, move.ProductUom);

                        if (qty > 0)
                        {
                            // CASO 1: CANTIDAD POSITIVA -> Crear Línea de Detalle
                            // Usamos tipado estricto (int) para asegurar serialización correcta.
                            
                            var lineData = new Dictionary<string, object>
                            {
                                { "picking_id", (int)pickingId },
                                { "move_id", moveId },
                                { "product_id", (int)move.ProductId },
                                { "product_uom_id", (int)move.ProductUom },
                                { "location_id", (int)move.LocationId },
                                { "location_dest_id", (int)move.LocationDestId },
                                { "quantity", (double)qty },
                                { "picked", true } 
                            };

                            await _rpcClient.ExecuteActionAsync("stock.move.line", "create", new object[] { lineData });
                            _logger.LogInformation("📝 [ODOO-DEBUG] Created Line for Move {MoveId}, Qty {Qty}. Verifying...", moveId, qty);

                            // VERIFICATION: Read back the line to ensure Odoo accepted it
                            var createdLines = await _rpcClient.SearchAndReadAsync<OdooMoveLineDto>("stock.move.line", 
                                new object[][] { 
                                    new object[] { "move_id", "=", moveId },
                                    new object[] { "picking_id", "=", (int)pickingId }
                                }, new string[] { "id", "quantity" });
                            
                            if (createdLines != null && createdLines.Count > 0)
                            {
                                var cl = createdLines[0];
                                _logger.LogInformation("✅ [ODOO-VERIFY] Move {MoveId} validated in Odoo with Qty {QtyDone} (Expected {Qty})", moveId, cl.Quantity, qty);
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ [ODOO-VERIFY] Move {MoveId} seems to have NO lines in Odoo after creation attempt!", moveId);
                            }

                            // Actualizar Padre
                            var moveUpdate = new Dictionary<string, object> { { "picked", true } };
                            await _rpcClient.ExecuteActionAsync("stock.move", "write", new object[] { new object[] { moveId }, moveUpdate });
                        }
                        else
                        {
                            // CASO 2: CANTIDAD CERO -> Marcar explícitamente
                            // Esto le dice a Odoo: "Este movimiento NO se ha tocado o se recibió 0".
                            
                            var moveUpdateZero = new Dictionary<string, object>
                            {
                                { "quantity", 0 }, 
                                { "picked", false }
                            };
                            await _rpcClient.ExecuteActionAsync("stock.move", "write", new object[] { new object[] { moveId }, moveUpdateZero });
                            _logger.LogInformation("🚫 [ODOO-DEBUG] Move {MoveId} set to Qty=0, Picked=False", moveId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ODOO-ADAPTER] Error procesando Move {MoveId}", moveId);
                        throw; 
                    }
                }

                // PASO 3: Validación Final
                _logger.LogInformation("[ODOO-ADAPTER] Todos los movimientos procesados. Iniciando Validación Recursiva...");
                return await RecursiveValidateAsync(pickingId, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ODOO-ADAPTER] Error General en ConfirmReceiptAsync Picking {Id}", pickingId);
                return false;
            }
        }

        private async Task<bool> RecursiveValidateAsync(long pickingId, int depth)
        {
            try
            {
                if (depth > 3) 
                {
                    _logger.LogWarning("[ODOO-ADAPTER] Profundidad máxima de validación alcanzada para Picking {Id}.", pickingId);
                    return false;
                }

                _logger.LogInformation("[ODOO-ADAPTER] Intento de validación #{Depth} para Picking {Id}...", depth + 1, pickingId);

                var buttonContext = new Dictionary<string, object>
                {
                    { "context", new Dictionary<string, object>
                        {
                            { "skip_immediate", true },
                            { "button_validate_picking_ids", new object[] { pickingId } }
                        }
                    }
                };

                Console.WriteLine($"\n🔍 [ODOO-DEBUG] RecursiveValidateAsync depth {depth} for Picking {pickingId}...");
                var result = await _rpcClient.ExecuteActionAsync("stock.picking", "button_validate", new object[] { new object[] { pickingId } }, buttonContext);
                Console.WriteLine($"📡 [ODOO-DEBUG] button_validate Result Type: {result?.GetType().Name}");

                if (result == null || (result is bool b && b))
                {
                    _logger.LogInformation("[ODOO-ADAPTER] Validación directa exitosa.");
                    return true;
                }

                if (result is Dictionary<string, object?> action && action.ContainsKey("res_model"))
                {
                    var resModel = action["res_model"]?.ToString();
                    _logger.LogInformation("[ODOO-ADAPTER] Wizard Detectado: {Model}", resModel);

                    if (resModel == "stock.immediate.transfer")
                    {
                        _logger.LogInformation("[ODOO-ADAPTER] Procesando wizard de transferencia inmediata...");
                        int wizardId = action.ContainsKey("res_id") ? Convert.ToInt32(action["res_id"]) : 0;
                        if (wizardId > 0)
                        {
                            var procContext = new Dictionary<string, object>
                            {
                                { "context", new Dictionary<string, object>
                                    {
                                        { "active_id", pickingId },
                                        { "active_ids", new object[] { pickingId } },
                                        { "active_model", "stock.picking" }
                                    }
                                }
                            };
                            await _rpcClient.ExecuteActionAsync("stock.immediate.transfer", "process", new object[] { new object[] { wizardId } }, procContext);
                        }
                        
                        return await RecursiveValidateAsync(pickingId, depth + 1);
                    }
                    else if (resModel == "stock.backorder.confirmation")
                    {
                        _logger.LogInformation("[ODOO-ADAPTER] Procesando wizard de BACKORDER...");
                        int wizardId = action.ContainsKey("res_id") ? Convert.ToInt32(action["res_id"]) : 0;
                        if (wizardId > 0)
                        {
                            var procContext = new Dictionary<string, object>
                            {
                                { "context", new Dictionary<string, object>
                                    {
                                        { "active_id", pickingId },
                                        { "active_ids", new object[] { pickingId } },
                                        { "active_model", "stock.picking" }
                                    }
                                }
                            };
                            // En Backorder wizard, 'process' crea el backorder automáticamente.
                            await _rpcClient.ExecuteActionAsync("stock.backorder.confirmation", "process", new object[] { new object[] { wizardId } }, procContext);
                        }

                        return await RecursiveValidateAsync(pickingId, depth + 1);
                    }
                    else
                    {
                        _logger.LogInformation("[ODOO-ADAPTER] Wizard '{Model}' detectado. Siendo manejado por configuración de Odoo o ignorado si no es crítico.", resModel);
                    }
                }

                // Verificación final de estado si salimos sin confirmación explícita
                var finalVerify = await _rpcClient.SearchAndReadAsync<OdooPickingDto>("stock.picking", 
                    new object[][] { new object[] { "id", "=", pickingId } }, new string[] { "state" });
                
                if (finalVerify != null && finalVerify.Count > 0)
                {
                    _logger.LogInformation("[ODOO-ADAPTER] Estado final alcanzado: {State}", finalVerify[0].State);
                    return finalVerify[0].State == "done";
                }

                return false; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ODOO-ADAPTER] Error crítico sincronizando con Odoo picking {PickingId}", pickingId);
                throw;
            }
        }
        public async Task<int> CreatePackagingAsync(int productId, int templateId, string name, double qty, double weight, double length, double width, double height)
        {
            _logger.LogInformation("[ODOO-ADAPTER] Creando empaque para Producto {ProductId} (Template: {TemplateId})...", productId, templateId);
            try
            {
                // Victoria WMS specifically uses 'stock.move.bulk' for its custom logistics
                // This model is a custom many2many relation in their Odoo instance
                var values = new Dictionary<string, object>
                {
                    { "name", name },
                    { "qty_bulk", qty },
                    // Odoo custom stock.move.bulk expects M2M syntax on 'product_ids' and template IDs.
                    { "product_ids", new object[] { new object[] { 6, 0, new object[] { templateId } } } },
                    { "l_cm", length },
                    { "w_cm", width },
                    { "h_cm", height },
                    { "weight", weight }
                };

                var result = await _rpcClient.ExecuteActionAsync("stock.move.bulk", "create", new object[] { values });
                if (result == null) return 0;

                int odooId = int.TryParse(result.ToString(), out var id) ? id : 0;
                _logger.LogInformation("[ODOO-ADAPTER] Empaque (Bulk) creado exitosamente en Odoo. ID: {Id}", odooId);
                
                // BACKUP: Try to create a standard product.packaging as well if requested or as a fallback
                // User said "En Odoo no se ha creado", maybe they check standard packagings tab.
                try {
                    var stdValues = new Dictionary<string, object>
                    {
                        { "name", name },
                        { "qty", qty },
                        { "product_id", productId },
                        { "packaging_length", length },
                        { "packaging_width", width },
                        { "packaging_height", height },
                        { "max_weight", weight }
                    };
                    await _rpcClient.ExecuteActionAsync("product.packaging", "create", new object[] { stdValues });
                    _logger.LogInformation("[ODOO-ADAPTER] Standard product.packaging also created for product {ProductId}", productId);
                } catch(Exception ex) {
                   _logger.LogWarning("[ODOO-ADAPTER] Could not create standard product.packaging (optional): {Msg}", ex.Message);
                }

                return odooId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ODOO-ADAPTER] Error creando empaque para Producto {ProductId}", productId);
                throw;
            }
        }

        public async Task<bool> UpdatePackagingAsync(int packagingId, string name, double qty, double weight, double length, double width, double height)
        {
            _logger.LogInformation("[ODOO-ADAPTER] Actualizando empaque {Id}...", packagingId);
            try
            {
                var values = new Dictionary<string, object>
                {
                    { "name", name },
                    { "qty_bulk", qty },
                    { "l_cm", length },
                    { "w_cm", width },
                    { "h_cm", height },
                    { "weight", weight }
                };

                // stock.move.bulk write
                var success = await _rpcClient.ExecuteAsync("stock.move.bulk", "write", new object[] { new object[] { packagingId }, values });
                
                // Try to find if it corresponds to a standard packaging and update it too (heuristic: same name)
                // This is a bit risky but helps with the "not showing in odoo" issue.
                // For now, let's keep it simple and just return the bulk write status.
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ODOO-ADAPTER] Error actualizando empaque {Id}", packagingId);
                throw;
            }
        }
    }
}
