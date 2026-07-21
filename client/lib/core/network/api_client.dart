import 'package:dio/dio.dart';
import '../storage/secure_storage.dart';

class ApiClient {
  final Dio _dio = Dio();
  final SecureStorage _secureStorage = SecureStorage();
  String _baseUrl = '';

  ApiClient() {
    _dio.options.connectTimeout = const Duration(seconds: 30);
    _dio.options.receiveTimeout = null; // Sin límite de tiempo (Infinito)
    _dio.options.sendTimeout = null; // Sin límite de tiempo (Infinito)
    
    // Interceptor para agregar token JWT a todas las peticiones
    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await _secureStorage.getToken();
        if (token != null) {
          options.headers['Authorization'] = 'Bearer $token';
        }
        return handler.next(options);
      },
      onError: (e, handler) {
        // Log de errores de red o reintento automático
        return handler.next(e);
      },
    ));
  }

  String get baseUrl => _baseUrl;
  Dio get dio => _dio;

  Future<Response<T>> get<T>(String path, {Map<String, dynamic>? queryParameters, Options? options}) {
    return _dio.get<T>(path, queryParameters: queryParameters, options: options);
  }

  Future<Response<T>> post<T>(String path, {dynamic data, Map<String, dynamic>? queryParameters, Options? options}) {
    return _dio.post<T>(path, data: data, queryParameters: queryParameters, options: options);
  }

  /// Inicializa la base url probando primero la LAN y luego haciendo fallback al Túnel
  Future<bool> initializeConnection(Map<String, dynamic> pcProfile) async {
    final lanUrl = pcProfile['LanUrl'] as String;
    final tunnelUrl = pcProfile['TunnelUrl'] as String;

    // 1. Probar LAN (3.5s timeout para detección confiable en Wi-Fi)
    try {
      final tempDio = Dio(BaseOptions(
        connectTimeout: const Duration(milliseconds: 3500),
        receiveTimeout: const Duration(milliseconds: 3500),
      ));
      
      // Intentamos un endpoint libre de la API (ej: el endpoint de QR/Pairing no bloqueado)
      final response = await tempDio.get('${lanIpEndpoint(lanUrl)}/api/pairing/qr');
      if (response.statusCode == 200) {
        _baseUrl = lanUrl;
        _dio.options.baseUrl = _baseUrl;
        await _secureStorage.saveActivePc({
          ...pcProfile,
          'LastConnectedUrl': _baseUrl,
          'ConnectionMode': 'LAN'
        });
        return true;
      }
    } catch (e) {
      print('DEBUG initializeConnection LAN error: $e');
      // Fallo de LAN, procedemos a probar el túnel
    }

    // 2. Probar Tunnel de Cloudflare
    try {
      final tempDio = Dio(BaseOptions(
        connectTimeout: const Duration(milliseconds: 5000),
        receiveTimeout: const Duration(milliseconds: 5000),
      ));
      final response = await tempDio.get('${lanIpEndpoint(tunnelUrl)}/api/pairing/qr');
      if (response.statusCode == 200) {
        _baseUrl = tunnelUrl;
        _dio.options.baseUrl = _baseUrl;
        await _secureStorage.saveActivePc({
          ...pcProfile,
          'LastConnectedUrl': _baseUrl,
          'ConnectionMode': 'CloudflareTunnel'
        });
        return true;
      }
    } catch (e) {
      print('DEBUG initializeConnection Tunnel error: $e');
      // Ambos fallaron
    }

    // Si fallan pings directos, usar la última URL activa como último recurso
    _baseUrl = tunnelUrl;
    _dio.options.baseUrl = _baseUrl;
    return false;
  }

  String lanIpEndpoint(String rawUrl) {
    return rawUrl.endsWith('/') ? rawUrl.substring(0, rawUrl.length - 1) : rawUrl;
  }
}
