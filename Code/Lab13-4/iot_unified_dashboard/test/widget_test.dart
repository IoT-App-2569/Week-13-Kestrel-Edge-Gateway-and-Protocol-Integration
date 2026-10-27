import 'package:flutter_test/flutter_test.dart';

import 'package:iot_unified_dashboard/gateway_config.dart';

void main() {
  test('gateway config has safe defaults when no dart-define is given', () {
    expect(GatewayConfig.host, '127.0.0.1');
    expect(GatewayConfig.mqttPort, 1883);
    // ไม่มีรหัสผ่านฝังอยู่ในซอร์สโค้ด
    expect(GatewayConfig.hasCredentials, isFalse);
  });
}
