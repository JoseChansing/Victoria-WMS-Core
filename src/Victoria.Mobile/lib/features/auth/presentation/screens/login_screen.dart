import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../bloc/auth_bloc.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _userController = TextEditingController();
  final _passwordController = TextEditingController();
  String _selectedTenant = 'PERFECTPTY';
  final List<String> _tenants = ['PERFECTPTY', 'NATSUKI', 'PDM', 'FILTROS'];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF1E1E2C),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(32.0),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.warehouse, size: 80, color: Colors.blueAccent),
              const SizedBox(height: 16),
              const Text(
                'VICTORIA WMS',
                style: TextStyle(
                  color: Colors.white,
                  fontSize: 28,
                  fontWeight: FontWeight.bold,
                  letterSpacing: 2,
                ),
              ),
              const Text(
                'Mobile Core 2.0',
                style: TextStyle(color: Colors.blueGrey, fontSize: 16),
              ),
              const SizedBox(height: 48),
              
              DropdownButtonFormField<String>(
                value: _selectedTenant,
                dropdownColor: const Color(0xFF2D2D44),
                style: const TextStyle(color: Colors.white),
                decoration: InputDecoration(
                  labelText: 'Compañía (Tenant)',
                  labelStyle: const TextStyle(color: Colors.blueAccent),
                  enabledBorder: OutlineInputBorder(
                    borderSide: const BorderSide(color: Colors.blueGrey),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderSide: const BorderSide(color: Colors.blueAccent, width: 2),
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
                items: _tenants.map((t) => DropdownMenuItem(value: t, child: Text(t))).toList(),
                onChanged: (val) => setState(() => _selectedTenant = val!),
              ),
              const SizedBox(height: 16),
              
              TextField(
                controller: _userController,
                style: const TextStyle(color: Colors.white),
                decoration: InputDecoration(
                  labelText: 'Usuario',
                  labelStyle: const TextStyle(color: Colors.blueAccent),
                  prefixIcon: const Icon(Icons.person, color: Colors.blueAccent),
                  enabledBorder: OutlineInputBorder(
                    borderSide: const BorderSide(color: Colors.blueGrey),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderSide: const BorderSide(color: Colors.blueAccent, width: 2),
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
              ),
              const SizedBox(height: 16),
              
              TextField(
                controller: _passwordController,
                obscureText: true,
                style: const TextStyle(color: Colors.white),
                decoration: InputDecoration(
                  labelText: 'Contraseña',
                  labelStyle: const TextStyle(color: Colors.blueAccent),
                  prefixIcon: const Icon(Icons.lock, color: Colors.blueAccent),
                  enabledBorder: OutlineInputBorder(
                    borderSide: const BorderSide(color: Colors.blueGrey),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderSide: const BorderSide(color: Colors.blueAccent, width: 2),
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
              ),
              const SizedBox(height: 32),
              
              BlocConsumer<AuthBloc, AuthState>(
                listener: (context, state) {
                  if (state is AuthError) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(content: Text(state.message), backgroundColor: Colors.redAccent),
                    );
                  }
                },
                builder: (context, state) {
                  if (state is AuthLoading) {
                    return const CircularProgressIndicator();
                  }
                  return SizedBox(
                    width: double.infinity,
                    height: 56,
                    child: ElevatedButton(
                      onPressed: () {
                        context.read<AuthBloc>().add(
                          LoginRequested(_userController.text, _passwordController.text, _selectedTenant),
                        );
                      },
                      style: ElevatedButton.styleFrom(
                        backgroundColor: Colors.blueAccent,
                        shape: RoundedRectanglePlatform.borderRadius(BorderRadius.circular(12)),
                      ),
                      child: const Text('INICIAR SESIÓN', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Colors.white)),
                    ),
                  );
                },
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// Helper para compatibilidad de bordes
class RoundedRectanglePlatform {
  static RoundedRectangleBorder borderRadius(BorderRadius radius) => RoundedRectangleBorder(borderRadius: radius);
}
