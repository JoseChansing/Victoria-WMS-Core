import 'package:flutter/material.dart';

class PickingScreen extends StatefulWidget {
  const PickingScreen({super.key});

  @override
  State<PickingScreen> createState() => _PickingScreenState();
}

class _PickingScreenState extends State<PickingScreen> {
  int _currentStep = 0;
  final List<Map<String, String>> _tasks = [
    {"loc": "ZONE-A-01", "lpn": "LPN-NATS-001", "sku": "SKU-MONITOR", "qty": "5"},
    {"loc": "ZONE-B-05", "lpn": "LPN-PERF-202", "sku": "SKU-CABLE", "qty": "10"},
  ];

  @override
  Widget build(BuildContext context) {
    final task = _tasks[_currentStep];

    return Scaffold(
      appBar: AppBar(title: const Text('PREPARACIÓN: PICKING')),
      body: Padding(
        padding: const EdgeInsets.all(24.0),
        child: Column(
          children: [
            LinearProgressIndicator(value: (_currentStep + 1) / _tasks.length),
            const SizedBox(height: 32),
            const Text('SIGUIENTE UBICACIÓN:', style: TextStyle(color: Colors.grey)),
            Text(task['loc']!, style: const TextStyle(fontSize: 48, fontWeight: FontWeight.bold, color: Colors.blueAccent)),
            const Divider(height: 64),
            Row(
              children: [
                const Icon(Icons.qr_code, size: 48),
                const SizedBox(width: 16),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('ESCANEAR LPN:', style: TextStyle(color: Colors.grey)),
                    Text(task['lpn']!, style: const TextStyle(fontSize: 24, fontWeight: FontWeight.bold)),
                  ],
                )
              ],
            ),
            const SizedBox(height: 24),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text('SKU: ${task['sku']}', style: const TextStyle(fontSize: 18)),
                Text('CANT: ${task['qty']}', style: const TextStyle(fontSize: 24, fontWeight: FontWeight.bold, color: Colors.green)),
              ],
            ),
            const Spacer(),
            SizedBox(
              width: double.infinity,
              height: 70,
              child: ElevatedButton.icon(
                onPressed: () {
                  if (_currentStep < _tasks.length - 1) {
                    setState(() => _currentStep++);
                  } else {
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text('Picking de orden completado'), backgroundColor: Colors.green),
                    );
                    Navigator.pop(context);
                  }
                },
                icon: const Icon(Icons.check_circle, color: Colors.white, size: 32),
                label: const Text('CONFIRMAR ESCANEO', style: TextStyle(color: Colors.white, fontSize: 20, fontWeight: FontWeight.bold)),
                style: ElevatedButton.styleFrom(backgroundColor: Colors.blueAccent),
              ),
            )
          ],
        ),
      ),
    );
  }
}
