using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Marten;
using Victoria.Core.Infrastructure;
using Victoria.Infrastructure.Persistence;
using Victoria.Infrastructure.Redis;
using Victoria.Inventory.Application.Commands;
using Victoria.Inventory.Domain.Services;

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
}).UseLightweightSessions();

// 3. Register Infrastructure Services (Wiring Interfaces with Implementations)
builder.Services.AddScoped<ILockService, RedisLockService>();
builder.Services.AddSingleton<IEpcParser, EpcParser>();
builder.Services.AddSingleton<IRfidDebouncer, RfidDebouncer>();
builder.Services.AddSingleton<Victoria.Core.ILpnFactory, Victoria.Core.LpnFactory>();

// 4. Register Application Services
builder.Services.AddScoped<Victoria.Inventory.Domain.Services.LabelService>();
builder.Services.AddScoped<Victoria.Infrastructure.Integration.DispatchEventHandler>();
builder.Services.AddScoped<Victoria.Inventory.Application.Services.CycleCountService>();
builder.Services.AddScoped<AllocationService>();
builder.Services.AddScoped<ReceiveLpnHandler>();
builder.Services.AddScoped<PutawayLpnHandler>();
builder.Services.AddScoped<AllocateOrderHandler>();
builder.Services.AddScoped<PickLpnHandler>();
builder.Services.AddScoped<PackingHandler>();
builder.Services.AddScoped<Victoria.Inventory.Application.Services.DispatchService>();

// 5. Odoo Integration Mix (ACL & RPC)
builder.Services.AddSingleton<Victoria.Core.Messaging.IMessageBus, Victoria.Infrastructure.Messaging.InMemoryMessageBus>();
builder.Services.AddHttpClient<Victoria.Infrastructure.Integration.Odoo.IOdooRpcClient, Victoria.Infrastructure.Integration.Odoo.OdooRpcClient>();
builder.Services.AddScoped<Victoria.Infrastructure.Integration.Odoo.ProductSyncService>();
builder.Services.AddScoped<Victoria.Infrastructure.Integration.Odoo.InboundOrderSyncService>();
builder.Services.AddScoped<Victoria.Infrastructure.Integration.Odoo.OdooFeedbackService>();
builder.Services.AddScoped<Victoria.Infrastructure.Integration.Odoo.OdooFeedbackConsumer>();
builder.Services.AddScoped<Victoria.Inventory.Application.Commands.ApproveReceiptOverageHandler>();

// Background Workers
if (builder.Configuration.GetValue<bool>("ENABLE_ODOO_POLLING"))
{
    builder.Services.AddHostedService<Victoria.Infrastructure.Integration.Odoo.OdooPollingService>();
}

builder.Services.AddControllers();

// 5. Habilitación de Swagger (Fase 4 Requirement)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
