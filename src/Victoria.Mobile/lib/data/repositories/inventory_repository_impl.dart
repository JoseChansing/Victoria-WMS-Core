import 'package:dio/dio.dart';
import '../../domain/repositories/inventory_repository.dart';

class InventoryRepositoryImpl implements InventoryRepository {
  final Dio _dio;

  // CAMBIO TEMPORAL: localhost para Web/Windows (Android Emulator usa 10.0.2.2)
  static const String _baseUrl = 'http://localhost:5000/api/v1';

  InventoryRepositoryImpl([Dio? dio]) 
      : _dio = dio ?? Dio(BaseOptions(baseUrl: _baseUrl));

  @override
  Future<void> receiveLpn(String lpnId, String orderId, String userId, String stationId) async {
    try {
      await _dio.post('/inventory/receipt', data: {
        'lpnId': lpnId,
        'orderId': orderId,
        'userId': userId,
        'stationId': stationId,
      });
    } on DioException catch (e) {
      if (e.response?.statusCode == 409) {
        throw Exception('Conflict: ${e.response?.data['error']}');
      }
      if (e.response?.statusCode == 400) {
        throw Exception('Bad Request: ${e.response?.data['error']}');
      }
      rethrow;
    }
  }
}
