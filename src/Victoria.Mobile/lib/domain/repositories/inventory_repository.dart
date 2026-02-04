abstract class InventoryRepository {
  Future<void> receiveLpn(String lpnId, String orderId, String userId, String stationId);
}
