
using System.IO.Ports;
using Microsoft.Extensions.Hosting;

public class SerialBridgeWorker : BackgroundService
{
    private readonly GatewayStateStore _store;
    private readonly ILogger<SerialBridgeWorker> _logger;

    private readonly object _serialLock = new();

    private SerialPort? _serialPort;

    // เปลี่ยนได้ด้วย Environment Variable SERIAL_PORT
    private readonly string _portName =
        Environment.GetEnvironmentVariable("SERIAL_PORT")
        ?? "/dev/cu.usbserial-0001";

    private const int BaudRate = 115200;

    public SerialBridgeWorker(
        GatewayStateStore store,
        ILogger<SerialBridgeWorker> logger)
    {
        _store = store;
        _logger = logger;
    }

    // เปิด Serial Port และอ่านข้อมูลจาก ESP32
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                lock (_serialLock)
                {
                    if (_serialPort is null ||
                        !_serialPort.IsOpen)
                    {
                        _serialPort?.Dispose();

                        _serialPort = new SerialPort(
                            _portName,
                            BaudRate,
                            Parity.None,
                            8,
                            StopBits.One)
                        {
                            ReadTimeout = 500,
                            WriteTimeout = 1000,
                            NewLine = "\n"
                        };

                        _serialPort.Open();

                        _logger.LogInformation(
                            "Serial connected: {Port} at {BaudRate} bps",
                            _portName,
                            BaudRate);
                    }
                }

                string? line = null;

                // อ่านข้อมูลจาก Serial
                lock (_serialLock)
                {
                    if (_serialPort?.IsOpen == true &&
                        _serialPort.BytesToRead > 0)
                    {
                        try
                        {
                            line = _serialPort.ReadLine().Trim();
                        }
                        catch (TimeoutException)
                        {
                            // ไม่มีข้อมูลใหม่ในช่วงเวลาที่กำหนด
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    _store.Update(line);

                    _logger.LogInformation(
                        "[Serial Telemetry] {Data}",
                        line);
                }

                await Task.Delay(20, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Serial connection error: {Message}",
                    ex.Message);

                lock (_serialLock)
                {
                    _serialPort?.Dispose();
                    _serialPort = null;
                }

                await Task.Delay(3000, stoppingToken);
            }
        }
    }

    // ส่งคำสั่งไปยัง ESP32
    public bool SendSerialCommand(string command)
    {
        lock (_serialLock)
        {
            try
            {
                if (_serialPort?.IsOpen != true)
                {
                    _logger.LogWarning(
                        "Cannot send command: Serial port is not connected.");

                    return false;
                }

                _serialPort.WriteLine(command);

                _logger.LogInformation(
                    "[Serial Command] {Command}",
                    command);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send Serial command.");

                return false;
            }
        }
    }

    // ปิด Serial Port เมื่อหยุดแอปพลิเคชัน
    public override void Dispose()
    {
        lock (_serialLock)
        {
            _serialPort?.Dispose();
            _serialPort = null;
        }

        base.Dispose();
    }
}
