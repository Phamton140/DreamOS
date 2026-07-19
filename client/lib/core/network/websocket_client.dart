import 'package:signalr_netcore/signalr_client.dart';
import '../storage/secure_storage.dart';

class WebSocketClient {
  final SecureStorage _secureStorage = SecureStorage();
  HubConnection? _connection;

  Future<void> connect(String hubUrl, {required Function(Map<String, dynamic>) onProgressReceived}) async {
    await disconnect();

    final token = await _secureStorage.getToken();

    _connection = HubConnectionBuilder()
        .withUrl(
          hubUrl,
          options: HttpConnectionOptions(
            accessTokenFactory: () async => token ?? '',
          ),
        )
        .build();

    _connection!.on('OnBuildProgress', (arguments) {
      if (arguments != null && arguments.isNotEmpty) {
        final data = arguments[0] as Map<String, dynamic>;
        onProgressReceived(data);
      }
    });

    _connection!.onclose(({Exception? error}) {
      print('SignalR conexión cerrada: $error');
    });

    await _connection!.start();
  }

  Future<void> joinProjectGroup(String projectRoot) async {
    if (_connection != null && _connection!.state == HubConnectionState.Connected) {
      await _connection!.invoke('JoinProjectGroup', args: [projectRoot]);
    }
  }

  Future<void> leaveProjectGroup(String projectRoot) async {
    if (_connection != null && _connection!.state == HubConnectionState.Connected) {
      await _connection!.invoke('LeaveProjectGroup', args: [projectRoot]);
    }
  }

  Future<void> disconnect() async {
    if (_connection != null) {
      await _connection!.stop();
      _connection = null;
    }
  }
}
