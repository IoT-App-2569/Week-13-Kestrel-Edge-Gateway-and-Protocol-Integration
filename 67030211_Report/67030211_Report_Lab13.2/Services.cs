using System.IO.Ports;
public class SerialBridgeWorker : BackgroundService {
    private SerialPort _serial;
    protected override async Task ExecuteAsync(CancellationToken token) {
        _serial = new SerialPort("COM24", 115200) { NewLine = "\n" };
        _serial.Open();
        while (!token.IsCancellationRequested) {
            string line = _serial.ReadLine();
            // Process and bridge to MQTT
        }
    }
}
