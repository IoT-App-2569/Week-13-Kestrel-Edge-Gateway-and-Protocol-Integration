// All-in-One Converged Multi-Protocol IoT Integration
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(opt => { opt.ListenAnyIP(5000); opt.ListenAnyIP(1883, l => l.UseMqtt()); });
builder.Services.AddSignalR();
builder.Services.AddMqttServer(opt => opt.WithDefaultEndpoint());
builder.Services.AddHostedService<SerialBridgeWorker>();
var app = builder.Build();
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapHub<GatewayHub>("/hubs/gateway");
app.Run();
