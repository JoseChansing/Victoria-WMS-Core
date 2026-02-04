import 'package:flutter/material.dart';
import 'package:vibration/vibration.dart';

class SecurityAlertOverlay extends StatelessWidget {
  final String message;
  final VoidCallback onClose;

  const SecurityAlertOverlay({
    super.key, 
    required this.message, 
    required this.onClose
  });

  static void show(BuildContext context, String message) {
    Vibration.vibrate(pattern: [0, 500, 100, 500]);
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => SecurityAlertOverlay(
        message: message,
        onClose: () => Navigator.pop(context),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Dialog.fullscreen(
      backgroundColor: Colors.red.withOpacity(0.95),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(Icons.security, size: 120, color: Colors.white),
          const SizedBox(height: 32),
          const Text(
            'ACCESO DENEGADO',
            style: TextStyle(color: Colors.white, fontSize: 32, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 16),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 32),
            child: Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white, fontSize: 18),
            ),
          ),
          const SizedBox(height: 64),
          ElevatedButton(
            onPressed: onClose,
            style: ElevatedButton.styleFrom(
              backgroundColor: Colors.white,
              foregroundColor: Colors.red,
              padding: const EdgeInsets.symmetric(horizontal: 48, vertical: 16),
              textStyle: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
            ),
            child: const Text('ENTENDIDO'),
          ),
        ],
      ),
    );
  }
}
