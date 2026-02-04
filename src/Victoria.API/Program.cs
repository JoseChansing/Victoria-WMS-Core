using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Victoria.Core.Infrastructure;
using Victoria.Infrastructure.Persistence;
using Victoria.Infrastructure.Redis;
using Victoria.Inventory.Application.Commands;

var builder = WebApplication.CreateBuilder(args);

// 1. Redis Configuration
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

// 2. Register Infrastructure Services (Fase 3)
builder.Services.AddScoped<ILockService, RedisLockService>();
builder.Services.AddScoped<IEventStore, PostgresEventStore>();

// 3. Register Application Services
builder.Services.AddScoped<ReceiveLpnHandler>();

builder.Services.AddControllers();
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
