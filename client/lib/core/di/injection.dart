import '../network/api_client.dart';
import '../network/websocket_client.dart';

class DI {
  static final ApiClient apiClient = ApiClient();
  static final WebSocketClient wsClient = WebSocketClient();
}
