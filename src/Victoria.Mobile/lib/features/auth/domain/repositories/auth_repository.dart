import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../../../../core/network/api_client.dart';

class AuthRepository {
  final ApiClient _apiClient;
  final FlutterSecureStorage _storage = const FlutterSecureStorage();

  AuthRepository(this._apiClient);

  Future<bool> login(String user, String password, String tenant) async {
    try {
      // Simulación de login contra el backend .NET 8
      // En producción: final response = await _apiClient.dio.post('/auth/login', data: {...});
      
      // Simulemos éxito con un token ficticio
      final mockToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.s2v_"; 
      
      await _storage.write(key: 'vicky_token', value: mockToken);
      await _storage.write(key: 'vicky_tenant', value: tenant);
      await _storage.write(key: 'vicky_user', value: user);
      
      return true;
    } catch (e) {
      return false;
    }
  }

  Future<void> logout() async {
    await _storage.deleteAll();
  }

  Future<String?> getSelectedTenant() async {
    return await _storage.read(key: 'vicky_tenant');
  }

  Future<bool> isAuthenticated() async {
    final token = await _storage.read(key: 'vicky_token');
    return token != null;
  }
}
