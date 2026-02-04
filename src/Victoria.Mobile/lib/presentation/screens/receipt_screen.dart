import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../data/repositories/inventory_repository_impl.dart';

// Simple Provider for the repository
final inventoryRepositoryProvider = Provider((ref) => InventoryRepositoryImpl());

// State Provider for status
final receiptStatusProvider = StateProvider<String?>((ref) => null);

class ReceiptScreen extends ConsumerStatefulWidget {
  const ReceiptScreen({super.key});

  @override
  ConsumerState<ReceiptScreen> createState() => _ReceiptScreenState();
}

class _ReceiptScreenState extends ConsumerState<ReceiptScreen> {
  final _lpnController = TextEditingController();
  bool _isLoading = false;

  Future<void> _handleReceipt() async {
    final lpn = _lpnController.text.trim();
    if (lpn.isEmpty) return;

    setState(() => _isLoading = true);
    ref.read(receiptStatusProvider.notifier).state = null;

    try {
      final repo = ref.read(inventoryRepositoryProvider);
      // Hardcoded dummy data for demo purposes as per phase requirements
      await repo.receiveLpn(lpn, "ORD-001", "USER-MOBILE", "STATION-MOBILE");
      
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('LPN Received Successfully (200 OK)'),
          backgroundColor: Colors.green,
        ),
      );
      _lpnController.clear();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Error: $e'),
          backgroundColor: Colors.red,
        ),
      );
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Victoria WMS - Receipt')),
      body: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          children: [
            TextField(
              controller: _lpnController,
              decoration: const InputDecoration(
                labelText: 'Scan/Enter LPN ID',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 20),
            SizedBox(
              width: double.infinity,
              height: 50,
              child: ElevatedButton(
                onPressed: _isLoading ? null : _handleReceipt,
                child: _isLoading 
                    ? const CircularProgressIndicator() 
                    : const Text('RECIBIR'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
