var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(opt => { opt.ListenAnyIP(5000); opt.ListenAnyIP(1883, l => l.UseMqtt()); });
builder.Services.AddSingleton<GatewayStateStore>();
builder.Services.AddHostedService<SerialBridgeWorker>();
var app = builder.Build();
app.MapGet("/api/telemetry/latest", (GatewayStateStore store) => Results.Ok(store.Get("latest_telemetry")));
app.Run();
