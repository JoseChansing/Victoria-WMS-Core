using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
// No change needed here, just verifying.
using Marten;
using Victoria.Core.Infrastructure;
using Victoria.Infrastructure.Persistence;
using Victoria.Infrastructure.Redis;
using Victoria.Inventory.Application.Commands;
using Victoria.Inventory.Domain.Services;
using Victoria.Infrastructure.Projections;
using Victoria.Core.Models;
using Weasel.Core;

var builder = WebApplication.CreateBuilder(args);

// 1. Connection Strings from Environment Variables
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? builder.Configuration.GetValue<string>("REDIS_CONNECTION") ?? "localhost:6379";
var postgresConnectionString = builder.Configuration.GetConnectionString("Marten") ?? builder.Configuration.GetValue<string>("POSTGRES_CONNECTION") ?? "Host=localhost;Database=victoria_wms;Username=vicky_admin;Password=vicky_password";

// 2. Redis Configuration (With resilience for staggered startup)
var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
redisOptions.AbortOnConnectFail = false;
redisOptions.ConnectTimeout = 10000; // 10s
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisOptions));

// Marten Configuration (Identidad Aislada por Instancia)
builder.Services.AddMarten(opts =>
{
    opts.Connection(postgresConnectionString);
    // Aseguramos que NO haya particionamiento por tenant lógico
    opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Single; 
    opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;

    opts.Schema.For<Victoria.Inventory.Domain.Aggregates.Product>().Duplicate(x => x.Brand, pgType: "varchar", configure: idx => idx.Name = "idx_product_brand");
    opts.Schema.For<Victoria.Inventory.Domain.Aggregates.Product>().Duplicate(x => x.Category, pgType: "varchar", configure: idx => idx.Name = "idx_product_category");

    opts.AutoCreateSchemaObjects = AutoCreate.All; 

    // Explicit registration to avoid discovery issues in background tasks
    opts.RegisterDocumentType<Victoria.Inventory.Domain.Aggregates.Location>();
    opts.RegisterDocumentType<Victoria.Inventory.Domain.Aggregates.Product>();
    opts.RegisterDocumentType<Victoria.Inventory.Domain.Aggregates.InboundOrder>();
    opts.RegisterDocumentType<Victoria.Inventory.Domain.Aggregates.Lpn>();
    opts.RegisterDocumentType<Victoria.Inventory.Domain.Entities.Task>();
    opts.RegisterDocumentType<Victoria.Core.Inventory.InventoryTask>();
    
    // Projections (Phase 3)
    opts.Projections.Add<InventoryByItemProjection>(Marten.Events.Projections.ProjectionLifecycle.Inline);
    opts.Projections.Add<InventoryByLocationProjection>(Marten.Events.Projections.ProjectionLifecycle.Inline);
    opts.Projections.Add<LpnDetailProjection>(Marten.Events.Projections.ProjectionLifecycle.Inline);
    opts.Projections.Add<LpnHistoryProjection>(Marten.Events.Projections.ProjectionLifecycle.Inline);
    opts.Projections.Snapshot<Victoria.Inventory.Domain.Aggregates.Lpn>(Marten.Events.Projections.SnapshotLifecycle.Inline);

}).UseLightweightSessions();

// 3. Register Infrastructure Services (Wiring Interfaces with Implementations)
builder.Services.AddScoped<Victoria.Core.Infrastructure.IEventStore, Victoria.Infrastructure.Persistence.PostgresEventStore>();
builder.Services.AddScoped<Victoria.Core.Infrastructure.ILockService, Victoria.Infrastructure.Redis.RedisLockService>();
builder.Services.AddSingleton<Victoria.Inventory.Domain.Services.IEpcParser, Victoria.Inventory.Domain.Services.EpcParser>();
builder.Services.AddSingleton<Victoria.Inventory.Domain.Services.IRfidDebouncer, Victoria.Inventory.Domain.Services.RfidDebouncer>();
builder.Services.AddSingleton<Victoria.Inventory.Domain.Services.ILpnFactory, Victoria.Inventory.Domain.Services.LpnFactory>();
builder.Services.AddSingleton<Victoria.Inventory.Domain.Services.IScanClassifier, Victoria.Inventory.Domain.Services.ScanClassifier>();

// 4. Register Application Services
builder.Services.AddScoped<Victoria.Inventory.Domain.Services.LabelService>();
builder.Services.AddScoped<Victoria.Infrastructure.Integration.DispatchEventHandler>();
builder.Services.AddScoped<Victoria.Inventory.Application.Services.CycleCountService>();
builder.Services.AddScoped<AllocationService>();
builder.Services.AddScoped<Victoria.Inventory.Application.Services.PackingRuleValidator>();
// Phase 4 Outbound Services
builder.Services.AddScoped<Victoria.Inventory.Application.Services.OutboundOrderSyncService>();
builder.Services.AddScoped<Victoria.Inventory.Application.Services.WaveService>();
builder.Services.AddScoped<Victoria.Inventory.Application.Services.TaskService>();
builder.Services.AddScoped<ReceiveLpnHandler>();
builder.Services.AddScoped<VoidLpnHandler>();
builder.Services.AddScoped<PutawayLpnHandler>();
builder.Services.AddScoped<AllocateOrderHandler>();
builder.Services.AddScoped<PickLpnHandler>();
builder.Services.AddScoped<PackingHandler>();
builder.Services.AddScoped<Victoria.Inventory.Application.Services.DispatchService>();
builder.Services.AddScoped<Victoria.Inventory.Application.Services.InventorySyncService>();

// 5. Odoo Integration Mix (DI Stabilization)
builder.Services.AddSingleton<Victoria.Core.Messaging.IMessageBus, Victoria.Infrastructure.Messaging.InMemoryMessageBus>();

// Typed Client Registration (Senior Best Practice)
builder.Services.AddHttpClient<Victoria.Core.Interfaces.IOdooRpcClient, Victoria.Infrastructure.Integration.Odoo.OdooRpcClient>()
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(300));

// Explicit Scoped Registrations for Persistence/Sync
builder.Services.AddScoped<Victoria.Core.Interfaces.IProductService, Victoria.Infrastructure.Integration.Odoo.ProductSyncService>();
builder.Services.AddScoped<Victoria.Core.Interfaces.IInboundService, Victoria.Infrastructure.Integration.Odoo.InboundOrderSyncService>();
builder.Services.AddScoped<Victoria.Infrastructure.Integration.Odoo.OdooFeedbackService>();
builder.Services.AddScoped<Victoria.Infrastructure.Integration.Odoo.OdooFeedbackConsumer>();
builder.Services.AddScoped<Victoria.Core.Interfaces.IOdooAdapter, Victoria.Infrastructure.Integration.Odoo.OdooAdapter>();
builder.Services.AddScoped<Victoria.Infrastructure.Services.InventoryTaskService>();
builder.Services.AddScoped<Victoria.Inventory.Application.Commands.ApproveReceiptOverageHandler>();

// 6. RFID & Printing Services
var gs1Prefix = builder.Configuration["GS1Settings:CompanyPrefix"] ?? "750123456";
var filterVal = int.Parse(builder.Configuration["GS1Settings:FilterValue"] ?? "1");
builder.Services.AddSingleton(new Victoria.Infrastructure.Services.EpcEncoderService(gs1Prefix, filterVal));

// Background Workers
// 62: Background Workers
// builder.Services.AddHostedService<Victoria.Infrastructure.Integration.Odoo.OdooPollingService>();

// Servicio de Carga Inicial (Auto-Sync al inicio)
builder.Services.AddHostedService<Victoria.Infrastructure.Services.InitialDataLoader>();
// Worker Recurrente (Cada 5 min)
builder.Services.AddHostedService<Victoria.Infrastructure.Services.RecurringSyncWorker>();

// 5a. CORS Policy (System Stabilization - Allow everything for local dev)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
        // Note: Cannot use AllowAnyOrigin with AllowCredentials.
        // If credentials are needed, use SetIsOriginAllowed(_ => true)
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options => 
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase; 
    });

// 5. Habilitación de Swagger (Fase 4 Requirement)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

Console.WriteLine("-----------------------------------------------");
Console.WriteLine("VICTORIA WMS - LOCAL DEVELOPMENT MODE - DELTA SYNC BUILD");
Console.WriteLine($"Odoo Target: {builder.Configuration["Odoo:Url"]}");
Console.WriteLine("-----------------------------------------------");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("AllowFrontend");

app.UseAuthentication(); // Required for Bearer token validation even if not strictly enforced yet
app.UseAuthorization();
app.MapControllers();

app.Run();
