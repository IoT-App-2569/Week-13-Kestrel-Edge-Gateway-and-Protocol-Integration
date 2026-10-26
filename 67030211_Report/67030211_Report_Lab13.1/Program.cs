using MQTTnet.AspNetCore;
using MQTTnet.Server;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(opt => {
    opt.ListenAnyIP(5000);
    opt.ListenAnyIP(1883, l => l.UseMqtt());
});
builder.Services.AddMqttServer(opt => opt.WithDefaultEndpoint());
var app = builder.Build();
app.UseMqttServer(server => {
    server.InterceptingPublishAsync += args => {
        Console.WriteLine($"[MQTTnet Inbound] Topic: {args.ApplicationMessage.Topic}");
        return Task.CompletedTask;
    };
});
app.MapGet("/", () => "Embedded MQTTnet Broker Active!");
app.Run();
