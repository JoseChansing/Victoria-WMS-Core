import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../features/auth/presentation/bloc/auth_bloc.dart';
import '../../features/putaway/presentation/screens/putaway_screen.dart';
import '../../features/picking/presentation/screens/picking_screen.dart';
import '../../features/counting/presentation/screens/blind_count_screen.dart';

class DashboardScreen extends StatelessWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final tenant = (context.watch<AuthBloc>().state as Authenticated).tenant;

    return Scaffold(
      appBar: AppBar(
        title: const Text('VICTORIA WMS', style: TextStyle(fontWeight: FontWeight.bold)),
        backgroundColor: Colors.blueAccent,
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            onPressed: () => context.read<AuthBloc>().add(LogoutRequested()),
          )
        ],
      ),
      body: Column(
        children: [
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(16),
            color: Colors.blueAccent.withOpacity(0.1),
            child: Row(
              children: [
                const Icon(Icons.business, color: Colors.blueAccent),
                const SizedBox(width: 8),
                Text('Compañía: ', style: TextStyle(fontWeight: FontWeight.bold, color: Colors.blueAccent)),
                Text(tenant, style: const TextStyle(fontWeight: FontWeight.bold)),
              ],
            ),
          ),
          Expanded(
            child: GridView.count(
              padding: const EdgeInsets.all(24),
              crossAxisCount: 2,
              crossAxisSpacing: 16,
              mainAxisSpacing: 16,
              children: [
                _buildMenuCard(context, 'Recepción', Icons.inventory, Colors.green, () {}),
                _buildMenuCard(context, 'Putaway', Icons.move_to_inbox, Colors.orange, () {
                  Navigator.push(context, MaterialPageRoute(builder: (_) => const PutawayScreen()));
                }),
                _buildMenuCard(context, 'Picking', Icons.shopping_basket, Colors.blue, () {
                  Navigator.push(context, MaterialPageRoute(builder: (_) => const PickingScreen()));
                }),
                _buildMenuCard(context, 'Conteos', Icons.checklist, Colors.purple, () {
                  Navigator.push(context, MaterialPageRoute(builder: (_) => const BlindCountScreen()));
                }),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildMenuCard(BuildContext context, String title, IconData icon, Color color, VoidCallback onTap) {
    return InkWell(
      onTap: onTap,
      child: Card(
        elevation: 4,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, size: 48, color: color),
            const SizedBox(height: 12),
            Text(title, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
          ],
        ),
      ),
    );
  }
}
