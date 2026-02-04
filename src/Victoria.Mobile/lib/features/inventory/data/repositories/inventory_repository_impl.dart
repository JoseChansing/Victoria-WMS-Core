import 'package:dio/dio.dart';
import '../../../../core/network/api_client.dart';
import '../../domain/repositories/inventory_repository.dart';

class InventoryRepositoryImpl implements InventoryRepository {
  final ApiClient _apiClient;

  InventoryRepositoryImpl(this._apiClient);

  @override
  Future<void> receiveLpn(String lpnId, String orderId, String userId, String stationId) async {
    try {
      await _apiClient.dio.post('/inventory/receipt', data: {
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
