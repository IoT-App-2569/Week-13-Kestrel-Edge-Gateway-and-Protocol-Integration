using System.Security.Cryptography;
using System.Text;

// ตัวช่วยด้านความปลอดภัยของเกตเวย์: อ่านความลับจาก user-secrets / environment variable เท่านั้น
public static class GatewaySecurity
{
    public const string ApiKeyHeader = "X-Api-Key";

    // ตรวจว่าตั้งค่าความลับครบก่อนเปิดเซิร์ฟเวอร์ ถ้าไม่ครบให้หยุดทำงานพร้อมบอกวิธีตั้งค่า
    public static bool ValidateSecrets(IConfiguration config, bool requireApiKey)
    {
        var problems = new List<string>();
        if (config.GetValue("Mqtt:RequireAuthentication", true))
        {
            if (string.IsNullOrWhiteSpace(config["Mqtt:Username"])) problems.Add("Mqtt:Username");
            if ((config["Mqtt:Password"] ?? "").Length < 8) problems.Add("Mqtt:Password (>= 8 ตัวอักษร)");
        }
        if (requireApiKey && (config["Gateway:ApiKey"] ?? "").Length < 16)
        {
            problems.Add("Gateway:ApiKey (>= 16 ตัวอักษร)");
        }
        if (problems.Count == 0) return true;

        Console.Error.WriteLine($"""
            [Security] ยังไม่ได้ตั้งค่าความลับ: {string.Join(", ", problems)}
            ตั้งค่าในโฟลเดอร์โปรเจกต์ด้วย (ค่าถูกเก็บนอกโฟลเดอร์งาน ไม่ติดไปกับ git):
              dotnet user-secrets set "Mqtt:Username" "student"
              dotnet user-secrets set "Mqtt:Password" "<your-strong-password>"
              dotnet user-secrets set "Gateway:ApiKey" "<random-string-16+-chars>"
            """);
        return false;
    }

    // เปรียบเทียบแบบใช้เวลาคงที่ ป้องกัน timing attack
    public static bool FixedEquals(string? a, string? b) =>
        a is not null && b is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    // HTTP security headers ของหน้าแดชบอร์ด: ห้ามโหลดสคริปต์จากที่อื่น, ห้ามฝังใน iframe
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            var h = ctx.Response.Headers;
            h.ContentSecurityPolicy =
                "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
                "connect-src 'self' ws: wss:; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
            h.XContentTypeOptions = "nosniff";
            h.XFrameOptions = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            await next();
        });

    // Endpoint filter: endpoint ที่สั่งงานฮาร์ดแวร์ต้องส่ง header X-Api-Key ที่ถูกต้อง
    public static RouteHandlerBuilder RequireApiKey(this RouteHandlerBuilder endpoint) =>
        endpoint.AddEndpointFilter(async (ctx, next) =>
        {
            var expected = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Gateway:ApiKey"];
            var provided = ctx.HttpContext.Request.Headers[ApiKeyHeader].ToString();
            return FixedEquals(provided, expected) ? await next(ctx) : Results.Unauthorized();
        });
}
