import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'presentation/screens/receipt_screen.dart';

void main() {
  runApp(const ProviderScope(child: VictoriaApp()));
}

class VictoriaApp extends StatelessWidget {
  const VictoriaApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Victoria WMS',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.blue),
        useMaterial3: true,
      ),
      home: const ReceiptScreen(),
    );
  }
}
