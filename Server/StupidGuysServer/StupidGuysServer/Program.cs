using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StupidGuysServer.Configuration;
using StupidGuysServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddSingleton<LobbiesManager>();
builder.Services.AddSingleton(GameServerSettings.FromEnvironment());
builder.Services.AddSingleton(MatchmakingSettings.FromEnvironment());
builder.Services.AddSingleton(provider =>
{
    var settings = provider.GetRequiredService<MatchmakingSettings>();
    return new GameServerAllocator(settings.PortRangeStart, settings.PortRangeEnd);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHub<MatchmakingHub>("/matchmaking");

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
var url = $"http://0.0.0.0:{port}";

Console.WriteLine($"SignalR Server starting at {url}/matchmaking");

app.Run(url);   
