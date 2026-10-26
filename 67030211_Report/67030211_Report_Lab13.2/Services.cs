using System.IO.Ports;
public class SerialBridgeWorker : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken token) {
        using var p = new SerialPort("COM24", 115200);
        p.Open();
        while (!token.IsCancellationRequested) { string line = p.ReadLine(); }
    }
}
public class GatewayStateStore { public object Get(string k) => new { voltage = 1.65, raw = 2048 }; }
