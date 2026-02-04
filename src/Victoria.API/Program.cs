using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Victoria.Core.Infrastructure;
using Victoria.Infrastructure.Persistence;
using Victoria.Infrastructure.Redis;
using Victoria.Inventory.Application.Commands;
using Victoria.Inventory.Domain.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Connection Strings from Environment Variables (Fase 4 Requirement)
var redisConnectionString = builder.Configuration.GetValue<string>("REDIS_CONNECTION") ?? "localhost:6379";
var postgresConnectionString = builder.Configuration.GetValue<string>("POSTGRES_CONNECTION") ?? "Host=localhost;Database=victoria_wms;Username=vicky_admin;Password=vicky_password";

// 2. Redis Configuration
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

// 3. Register Infrastructure Services (Wiring Interfaces with Implementations)
builder.Services.AddScoped<ILockService, RedisLockService>();
builder.Services.AddSingleton<IEpcParser, EpcParser>();
builder.Services.AddSingleton<IRfidDebouncer, RfidDebouncer>();
builder.Services.AddScoped<IEventStore, PostgresEventStore>();

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
