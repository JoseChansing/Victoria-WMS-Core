using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Victoria.Infrastructure.Integration.Odoo;
using Victoria.Inventory.Domain.Aggregates;
using Victoria.Core.Interfaces;
using Victoria.Core.Models;
using Marten;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace Victoria.Infrastructure.Services
{
    public class InitialDataLoader : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<InitialDataLoader> _logger;

        public InitialDataLoader(IServiceProvider serviceProvider, ILogger<InitialDataLoader> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // 1. Esperar 10 segundos tras el arranque
                await Task.Delay(10000, stoppingToken);

                // 1.1 Ensure EventStore Table Exists (CRITICAL FIX)
                using (var scope = _serviceProvider.CreateScope())
                {
                    await EnsureEventStoreSchema(scope.ServiceProvider);
                }

                // 2. Log obligatorio
                Console.WriteLine("🚀 [AUTO-START] Forzando descarga de datos de Odoo para el Frontend...");

                // --- DIAGNÓSTICO DE ZONA HORARIA (USER REQUEST) ---
                try {
                    using (var diagScope = _serviceProvider.CreateScope())
                    {
                        Console.WriteLine("----------------------------------------------------------------");
                        Console.WriteLine($"[RELOJ] System Local: {DateTime.Now}");
                        Console.WriteLine($"[RELOJ] System UTC:   {DateTime.UtcNow}");

                        var diagClient = diagScope.ServiceProvider.GetRequiredService<IOdooRpcClient>();
                        
                        // --- FIELD DISCOVERY (TEMP - RE-ADDED) ---
                        try 
                        {
                            Console.WriteLine("[FIELD-DISCOVERY] Fetching product.product fields...");
                            // execute_kw(db, uid, password, 'product.product', 'fields_get', [], {'attributes': ['string', 'help', 'type']})
                            var allFields = await diagClient.ExecuteKwAsync<Dictionary<string, object>>("product.product", "fields_get", new object[] { }, new Dictionary<string, object> { { "attributes", new string[] { "string", "type" } } });
                            
                            var potentialFields = allFields.Keys
                                .Where(k => k.Contains("marca", StringComparison.OrdinalIgnoreCase) || 
                                            k.Contains("brand", StringComparison.OrdinalIgnoreCase) || 
                                            k.Contains("lados", StringComparison.OrdinalIgnoreCase) || 
                                            k.Contains("side", StringComparison.OrdinalIgnoreCase) ||
                                            k.StartsWith("x_", StringComparison.OrdinalIgnoreCase))
                                .ToList();

                            Console.WriteLine($"[FIELD-DISCOVERY] Found {potentialFields.Count} potential fields:");
                            await System.IO.File.WriteAllTextAsync("ODOO_FIELDS_DUMP.txt", string.Join(Environment.NewLine, potentialFields));
                            foreach(var f in potentialFields) Console.WriteLine($"   -> {f}");
                        }
                        catch(Exception exField)
                        {
                            Console.WriteLine($"[FIELD-DISCOVERY] ERROR: {exField.Message}");
                        }
                        // ------------------------------

                        // Traer 1 producto cualquiera para ver su fecha
                        var diagFields = new string[] { "write_date", "display_name" };
                        // Empty domain
                        var diagProducts = await diagClient.SearchAndReadAsync<OdooProductDto>("product.product", new object[][] { }, diagFields);

                        if (diagProducts != null && diagProducts.Count > 0)
                        {
                            var p = diagProducts[0];
                            Console.WriteLine($"[RELOJ] Producto ref: {p.Display_Name}");
                            Console.WriteLine($"[RELOJ] Odoo Write_Date (Raw): {p.Write_Date}");

                            if (DateTime.TryParse(p.Write_Date, out var odooDate))
                            {
                                // Asumimos que Odoo manda fecha en lo que él cree que es UTC (o Local)
                                // Comparar con UTC real del sistema
                                var diff = odooDate - DateTime.UtcNow;
                                var logMsg = $"[RELOJ] Odoo Write_Date: {p.Write_Date} (parsed: {odooDate})\n[RELOJ] Diferencia: {diff.TotalMinutes:F2} min";
                                Console.WriteLine(logMsg);
                                await System.IO.File.WriteAllTextAsync("CLOCK_DIAG.txt", logMsg);

                                if (Math.Abs(diff.TotalMinutes) > 60) // 1 hora
                                {
                                    Console.WriteLine("⚠️⚠️⚠️ CRITICAL CLOCK SKEW DETECTED ⚠️⚠️⚠️");
                                    Console.WriteLine("El reloj de Odoo parece estar en LOCAL TIME, no en UTC.");
                                    await System.IO.File.AppendAllTextAsync("CLOCK_DIAG.txt", "\nSKEW: CRITICAL (>60min)");
                                }
                                else if (Math.Abs(diff.TotalMinutes) > 2)
                                {
                                    Console.WriteLine("⚠️ WARN: Pequeño desajuste de reloj.");
                                    await System.IO.File.AppendAllTextAsync("CLOCK_DIAG.txt", "\nSKEW: WARN (>2min)");
                                }
                                else
                                {
                                    Console.WriteLine("✅ Sincronización de Tiempo: CORRECTA (UTC).");
                                    await System.IO.File.AppendAllTextAsync("CLOCK_DIAG.txt", "\nSKEW: OK");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("[RELOJ] No se pudieron leer productos para diagnóstico.");
                        }
                        Console.WriteLine("----------------------------------------------------------------");
                    }
                } catch (Exception exDiag) {
                    Console.WriteLine($"[RELOJ] Error en diagnóstico: {exDiag.Message}");
                }
                // --- FIN DIAGNÓSTICO ---

                using (var scope = _serviceProvider.CreateScope())
                {
                    var session = scope.ServiceProvider.GetRequiredService<Marten.IDocumentSession>(); // Inject Session
                    var odooClient = scope.ServiceProvider.GetRequiredService<IOdooRpcClient>();
                    var inboundService = scope.ServiceProvider.GetRequiredService<IInboundService>();
                    var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

                    /* 
                    // 2.1 RESET STATE (FORCE FULL SYNC - TOTAL REACTIVATION)
                    _logger.LogWarning("⚠️ [AUTO-START] EJECUTANDO PURGA TOTAL DE BASE DE DATOS (FACTORY RESET)...");
                    
                    // PURGE EVERYTHING (TOTAL REACTIVATION)
                    session.DeleteWhere<Product>(x => true);
                    session.DeleteWhere<SyncState>(x => true);
                    session.DeleteWhere<InboundOrder>(x => true);
                    session.DeleteWhere<Lpn>(x => true); // CRITICAL: REVISIÓN PROFUNDA - Consolidar Loose Stock
                    session.DeleteWhere<Location>(x => true);
                    
                    await session.SaveChangesAsync();
                    Console.WriteLine("🧹 [RESET] Purga completa: Productos, Estados, Ordenes, LPNs y Ubicaciones eliminados.");
                    _logger.LogInformation("Purga completa ejecutada exitosamente.");
                    */
                    
                    // 4. SEED LOCATIONS (MOVE TO START FOR BETTER VISIBILITY)
                    await SeedLocationsAsync(session);

                    // 5. Invocar sincronización (usando los métodos reales SyncAllAsync)
                    _logger.LogInformation("Inicio de sincronización automática de productos...");
                    int products = await productService.SyncAllAsync(odooClient);
                    _logger.LogInformation($"Productos sincronizados: {products}");

                    _logger.LogInformation("Inicio de sincronización automática de órdenes...");
                    int orders = await inboundService.SyncAllAsync(odooClient);
                    _logger.LogInformation($"Órdenes sincronizadas: {orders}");
                }

                Console.WriteLine("✅ [AUTO-START] Carga inicial completada.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [AUTO-START] Error en carga inicial: {ex.Message}");
                _logger.LogError(ex, "Error crítico en InitialDataLoader");
            }
        }

        private async Task EnsureEventStoreSchema(IServiceProvider sp)
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var connectionString = config["POSTGRES_CONNECTION"] ?? "Host=localhost;Database=victoria_wms;Username=vicky_admin;Password=vicky_password";

            try
            {
                using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                var sql = @"
                    CREATE TABLE IF NOT EXISTS Events (
                        Sequence SERIAL PRIMARY KEY,
                        StreamId VARCHAR(255) NOT NULL,
                        Payload JSONB NOT NULL,
                        Timestamp TIMESTAMP DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS idx_streamid ON Events(StreamId);
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine("✅ [SCHEMA] Tabla 'Events' verificada/creada correctamente.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [SCHEMA] Error creando tabla Events: {ex.Message}");
            }
        }

        private async Task SeedLocationsAsync(Marten.IDocumentSession session)
        {
            _logger.LogInformation("Seeding master locations...");
            
            var locations = new[]
            {
                new { Code = "DOCK-LPN", Profile = LocationProfile.Reserve, IsPickable = false },
                new { Code = "DOCK-UNITS", Profile = LocationProfile.Picking, IsPickable = false },
                new { Code = "STAGE-RESERVE", Profile = LocationProfile.Reserve, IsPickable = true },
                new { Code = "STAGE-PICKING", Profile = LocationProfile.Picking, IsPickable = true },
                new { Code = "PHOTO-STATION", Profile = LocationProfile.Picking, IsPickable = false }
            };

            var existingLocations = await session.Query<Location>().ToListAsync();
            
            foreach (var locData in locations)
            {
                var existing = existingLocations.FirstOrDefault(x => x.Code.Value == locData.Code);

                if (existing == null)
                {
                    var loc = Location.Create(Victoria.Inventory.Domain.ValueObjects.LocationCode.Create(locData.Code), locData.Profile, locData.IsPickable);
                    session.Store(loc);
                    Console.WriteLine($"   [SEED] Creando ubicación: {locData.Code} ({locData.Profile})");
                }
                else
                {
                    Console.WriteLine($"   [SEED] Ubicación ya existe: {locData.Code}");
                }
            }

            await session.SaveChangesAsync();
            Console.WriteLine("✅ Infraestructura de Ubicaciones: VERIFICADA");
        }
    }
}
