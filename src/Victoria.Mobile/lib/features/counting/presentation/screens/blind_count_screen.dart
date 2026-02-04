import 'package:flutter/material.dart';
import '../../../../core/network/api_client.dart';

class BlindCountScreen extends StatefulWidget {
  const BlindCountScreen({super.key});

  @override
  State<BlindCountScreen> createState() => _BlindCountScreenState();
}

class _BlindCountScreenState extends State<BlindCountScreen> {
  final _lpnController = TextEditingController();
  final _qtyController = TextEditingController();
  bool _loading = false;
  final ApiClient _apiClient = ApiClient();

  Future<void> _submitCount() async {
    setState(() => _loading = true);
    try {
      final response = await _apiClient.dio.post('/inventory/count', data: {
        "lpnId": _lpnController.text,
        "countedQuantity": int.tryParse(_qtyController.text) ?? 0,
        "userId": "MOBILE-USER",
        "stationId": "PDA-WIFI"
      });

      if (response.statusCode == 200) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Conteo enviado correctamente'), backgroundColor: Colors.green),
        );
        Navigator.pop(context);
      }
    } catch (e) {
      // Manejado por interceptor
    } finally {
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('CONTEO CÍCLICO')),
      body: Padding(
        padding: const EdgeInsets.all(24.0),
        child: Column(
          children: [
            const Icon(Icons.visibility_off, size: 64, color: Colors.blueGrey),
            const SizedBox(height: 16),
            const Text('AUDITORÍA A CIEGAS', style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
            const Text('Ingrese la cantidad física real vista en el LPN.', style: TextStyle(color: Colors.grey)),
            const SizedBox(height: 48),
            TextField(
              controller: _lpnController,
              decoration: const InputDecoration(labelText: 'Escanear LPN', prefixIcon: Icon(Icons.qr_code)),
            ),
            const SizedBox(height: 24),
            TextField(
              controller: _qtyController,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'Cantidad Físicamente Contada', prefixIcon: Icon(Icons.numbers)),
              style: const TextStyle(fontSize: 32, fontWeight: FontWeight.bold),
            ),
            const Spacer(),
            if (_loading)
              const CircularProgressIndicator()
            else
              SizedBox(
                width: double.infinity,
                height: 60,
                child: ElevatedButton(
                  onPressed: _submitCount,
                  style: ElevatedButton.styleFrom(backgroundColor: Colors.purple, shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12))),
                  child: const Text('ENVIAR REPORTE', style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold)),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
