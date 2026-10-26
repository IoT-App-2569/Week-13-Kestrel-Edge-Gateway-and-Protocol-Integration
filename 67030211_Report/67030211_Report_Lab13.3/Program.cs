var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
var app = builder.Build();
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapHub<GatewayHub>("/hubs/gateway");
app.Run();
