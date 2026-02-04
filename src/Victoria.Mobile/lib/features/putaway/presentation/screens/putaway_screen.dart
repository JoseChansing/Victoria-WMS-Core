import 'package:flutter/material.dart';
import '../../../../core/network/api_client.dart';

class PutawayScreen extends StatefulWidget {
  const PutawayScreen({super.key});

  @override
  State<PutawayScreen> createState() => _PutawayScreenState();
}

class _PutawayScreenState extends State<PutawayScreen> {
  final _lpnController = TextEditingController();
  final _locController = TextEditingController();
  String? _suggestedLocation;
  bool _lpnScanned = false;
  bool _loading = false;

  final ApiClient _apiClient = ApiClient();

  Future<void> _handleLpnScan() async {
    setState(() => _loading = true);
    try {
      // Simulación de obtención de sugerencia del backend
      await Future.delayed(const Duration(milliseconds: 500));
      setState(() {
        _suggestedLocation = "LOC-A-01-05"; // Mock
        _lpnScanned = true;
      });
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _confirmPutaway() async {
    setState(() => _loading = true);
    try {
      final response = await _apiClient.dio.post('/inventory/putaway', data: {
        "lpnId": _lpnController.text,
        "locationCode": _locController.text,
        "userId": "MOBILE-USER",
        "stationId": "PDA-WIFI"
      });

      if (response.statusCode == 200) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Putaway completado con éxito'), backgroundColor: Colors.green),
        );
        Navigator.pop(context);
      }
    } catch (e) {
      // Los errores 403 son manejados por el interceptor sensorialmente,
      // pero aquí podríamos mostrar un diálogo específico si quisiéramos.
    } finally {
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('PROCESO: PUTAWAY')),
      body: Padding(
        padding: const EdgeInsets.all(24.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('PASO 1: Escanear LPN', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
            TextField(
              controller: _lpnController,
              decoration: InputDecoration(
                hintText: 'Escanear etiqueta LPN...',
                suffixIcon: IconButton(icon: const Icon(Icons.qr_code_scanner), onPressed: _handleLpnScan),
              ),
              onSubmitted: (_) => _handleLpnScan(),
            ),
            const SizedBox(height: 32),
            
            if (_lpnScanned) ...[
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(16),
                color: Colors.blue.withOpacity(0.1),
                child: Column(
                  children: [
                    const Text('UBICACIÓN SUGERIDA:'),
                    Text(_suggestedLocation ?? '-', style: const TextStyle(fontSize: 24, fontWeight: FontWeight.bold, color: Colors.blue)),
                  ],
                ),
              ),
              const SizedBox(height: 32),
              const Text('PASO 2: Confirmar Ubicación Actual', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
              TextField(
                controller: _locController,
                decoration: const InputDecoration(
                  hintText: 'Escanear etiqueta de Ubicación...',
                  suffixIcon: Icon(Icons.location_on),
                ),
              ),
              const SizedBox(height: 48),
              if (_loading) 
                const Center(child: CircularProgressIndicator())
              else
                SizedBox(
                  width: double.infinity,
                  height: 60,
                  child: ElevatedButton(
                    onPressed: _confirmPutaway,
                    style: ElevatedButton.styleFrom(backgroundColor: Colors.orange, shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12))),
                    child: const Text('CONFIRMAR GUARDADO', style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold)),
                  ),
                ),
            ],
          ],
        ),
      ),
    );
  }
}
