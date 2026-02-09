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
                // 0. Input Validation
                if (moveQuantities == null || moveQuantities.Count == 0)
                {
                    _logger.LogWarning("[ODOO-ADAPTER] No se recibieron cantidades para sincronizar con el Picking {PickingId}", pickingId);
                    return false;
                }

                _logger.LogInformation("[ODOO-ADAPTER] Iniciando sincronización ROBUSTA para Picking {PickingId}", pickingId);

                // PASO 0: Verificar Estado y Forzar Reserva (assigned/Ready)
                var pickingDomain = new object[][] { new object[] { "id", "=", pickingId } };
                var pickingFields = new string[] { "id", "state" };
                var pickings = await _rpcClient.SearchAndReadAsync<OdooPickingDto>("stock.picking", pickingDomain, pickingFields);

                if (pickings != null && pickings.Count > 0)
                {
                    var pick = pickings[0];
                    _logger.LogInformation("[ODOO-ADAPTER] Estado actual del Picking {Id}: {State}", pickingId, pick.State);

                    _logger.LogInformation("[ODOO-ADAPTER] Iniciando purga de líneas para picking {Id}...", pickingId);
                    
                    // A. Buscar por Picking ID
                    var linesByPick = await _rpcClient.SearchAndReadAsync<OdooMoveLineDto>("stock.move.line", 
                        new object[][] { new object[] { "picking_id", "=", pickingId } }, new string[] { "id" });
                    
                    _logger.LogInformation("[ODOO-ADAPTER] Purga: {Count} líneas encontradas por picking_id", linesByPick?.Count ?? 0);
                    
                    if (linesByPick != null && linesByPick.Count > 0)
                    {
                        var ids = linesByPick.Select(l => (object)l.Id).ToArray();
                        var unlinkResult = await _rpcClient.ExecuteActionAsync("stock.move.line", "unlink", new object[] { ids });
                        _logger.LogInformation("[ODOO-ADAPTER] unlink por picking_id result: {Result}", unlinkResult);
                    }

                    // B. Buscar por Move IDs (Nuclear pass)
                    var moveIdsArray = moveQuantities.Keys.Select(k => (object)k).ToArray();
                    foreach (var mId in moveIdsArray)
                    {
                        var linesByMove = await _rpcClient.SearchAndReadAsync<OdooMoveLineDto>("stock.move.line", 
                            new object[][] { new object[] { "move_id", "=", mId } }, new string[] { "id" });
                        
                        if (linesByMove != null && linesByMove.Count > 0)
                        {
                            _logger.LogInformation("[ODOO-ADAPTER] Purga: {Count} líneas encontradas para Move {Id}", linesByMove.Count, mId);
                            var ids = linesByMove.Select(l => (object)l.Id).ToArray();
                            await _rpcClient.ExecuteActionAsync("stock.move.line", "unlink", new object[] { ids });
                        }
                    }

                    // PASO 1.1: Unreserve (Solo si el terreno no está limpio)
                    if (pick.State == "assigned")
                    {
                        _logger.LogInformation("[ODOO-ADAPTER] Ejecutando do_unreserve final...");
                        await _rpcClient.ExecuteActionAsync("stock.picking", "do_unreserve", new object[] { new object[] { pickingId } });
                    }
                }

                // PASO 2: Escritura en Detalle (Deep Dive 3.0 - Creación Explícita)
                foreach (var mq in moveQuantities)
                {
                    long moveId = mq.Key;
                    int qty = mq.Value;

                    if (moveId <= 0) continue;

                    try 
                    {
                        // A. Leer detalles del movimiento para reconstruir la línea
                        var moveFields = new string[] { "id", "product_id", "location_id", "location_dest_id", "product_uom" };
                        var moves = await _rpcClient.SearchAndReadAsync<OdooMoveDto>("stock.move", 
                            new object[][] { new object[] { "id", "=", moveId } }, moveFields);

                        if (moves == null || moves.Count == 0)
                        {
                            _logger.LogWarning("[ODOO-ADAPTER] Move {MoveId} not found. Skipping.", moveId);
                            continue;
                        }

                        var move = moves[0];

                        // B. Crear la línea de detalle (stock.move.line) - SOLO SI HAY CANTIDAD
                        if (qty <= 0)
                        {
                            _logger.LogInformation("[ODOO-ADAPTER] Saltando Move {MoveId} (Qty 0).", moveId);
                            continue;
                        }

                        _logger.LogInformation("[ODOO-ADAPTER] Creando stock.move.line para Move {MoveId} -> Qty {Qty}", moveId, qty);
                        
                        var lineData = new Dictionary<string, object>
                        {
                            { "picking_id", pickingId },
                            { "move_id", moveId },
                            { "product_id", move.ProductId },
                            { "location_id", move.LocationId },
                            { "location_dest_id", move.LocationDestId },
                            { "product_uom_id", move.ProductUom },
                            { "quantity", (double)qty },
                            { "picked", true } 
                        };

                        var lineResult = await _rpcClient.ExecuteActionAsync("stock.move.line", "create", new object[] { lineData });
                        
                        if (lineResult != null)
                        {
                            long newLineId = Convert.ToInt64(lineResult is List<object> l ? l[0] : lineResult);
                            _logger.LogInformation("[ODOO-ADAPTER] Línea creada exitosamente (ID: {LineId})", newLineId);

                            // C. Sincronizar el Move Principal (Odoo 17 CRÍTICO)
                            // En Odoo 17, el Move debe estar marcado como 'picked' y tener 'quantity' (done) 
                            // para que button_validate lo procese sin entrar en bucles de reserva.
                            var moveUpdate = new Dictionary<string, object>
                            {
                                { "quantity", (double)qty },
                                { "picked", true }
                            };
                            await _rpcClient.ExecuteActionAsync("stock.move", "write", new object[] { new object[] { moveId }, moveUpdate });
                            _logger.LogInformation("[ODOO-ADAPTER] Move {MoveId} actualizado con picked=true, qty={Qty}", moveId, qty);

                            // VERIFICACIÓN: Read-Back
                            var verifyLines = await _rpcClient.SearchAndReadAsync<OdooMoveLineDto>("stock.move.line", 
                                new object[][] { new object[] { "id", "=", newLineId } }, new string[] { "quantity" });
                            
                            if (verifyLines != null && verifyLines.Count > 0)
                            {
                                double odooQty = verifyLines[0].Quantity;
                                _logger.LogInformation("[VERIFY] Move {MoveId} -> Line {NewLineId} -> Odoo tiene: {OdooQty} (Esperado: {ExpectedQty})", moveId, newLineId, odooQty, qty);
                                
                                if (Math.Abs(odooQty - qty) > 0.01)
                                {
                                    throw new Exception($"Fallo de Integridad Crítico: Odoo no persistió la cantidad recreada {qty} en la línea {newLineId} para el movimiento {moveId}. Odoo reporta {odooQty}.");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ODOO-ADAPTER] Error en fase de reconstrucción de línea para Move {MoveId}", moveId);
                        throw;
                    }
                }

                // PASO 2.5: Consolidación (Action Assign) - CRÍTICO PARA ODOO 17
                _logger.LogInformation("[ODOO-ADAPTER] Ejecutando action_assign para consolidar líneas en Picking {Id}...", pickingId);
                await _rpcClient.ExecuteActionAsync("stock.picking", "action_assign", new object[] { new object[] { pickingId } });

                // PASO 3: Validación Recursiva (Odoo 17 puede ser multi-paso)
                return await RecursiveValidateAsync(pickingId, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico en ConfirmReceiptAsync para Picking {Id}", pickingId);
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
                            { "skip_backorder", true },
                            { "button_validate_picking_ids", new object[] { pickingId } }
                        }
                    }
                };

                var result = await _rpcClient.ExecuteActionAsync("stock.picking", "button_validate", new object[] { new object[] { pickingId } }, buttonContext);

                if (result == null || (result is bool b && b))
                {
                    _logger.LogInformation("[ODOO-ADAPTER] Validación directa exitosa.");
                    return true;
                }

                if (result is Dictionary<string, object?> action && action.ContainsKey("res_model"))
                {
                    var resModel = action["res_model"]?.ToString();
                    _logger.LogInformation("[ODOO-ADAPTER] Wizard Detectado: {Model}", resModel);

                    if (resModel == "stock.backorder.confirmation")
                    {
                        int wizardId = 0;
                        if (action.ContainsKey("res_id") && action["res_id"] != null)
                            wizardId = Convert.ToInt32(action["res_id"]);

                        if (wizardId == 0)
                    {
                        _logger.LogInformation("[ODOO-ADAPTER] Creando wizard de backorder manualmente...");
                        var wizardData = new Dictionary<string, object>
                        {
                            { "pick_ids", new object[] { new object[] { 4, pickingId, 0 } } } // M2M Link
                        };
                        var createResult = await _rpcClient.ExecuteActionAsync("stock.backorder.confirmation", "create", 
                            new object[] { wizardData },
                            new Dictionary<string, object> { { "context", new Dictionary<string, object> { { "active_id", pickingId } } } });
                        
                        if (createResult != null) wizardId = Convert.ToInt32(createResult);
                    }

                        if (wizardId > 0)
                        {
                            _logger.LogInformation("[ODOO-ADAPTER] Procesando wizard {WizardId}...", wizardId);
                            
                            // Refuerzo de contexto: pasar picking_id
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

                            await _rpcClient.ExecuteActionAsync("stock.backorder.confirmation", "process", 
                                new object[] { new object[] { wizardId } }, procContext);
                            
                            // Recurse para confirmar estado final
                            return await RecursiveValidateAsync(pickingId, depth + 1);
                        }
                    }
                    else if (resModel == "stock.immediate.transfer")
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
                    else
                    {
                        // CASO C: Desconocido (Bloqueo para análisis)
                        _logger.LogError("[ODOO-ADAPTER] Wizard desconocido detectado: {ResModel}. Bloqueando validación preventiva.", resModel);
                        throw new Exception($"Wizard inesperado de Odoo detectado ({resModel}). Revise los logs del 'Wizard Spy' para más detalles.");
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
    }
}
