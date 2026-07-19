import 'package:flutter/material.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../core/di/injection.dart';

class ConsoleView extends StatefulWidget {
  final String projectRoot;

  const ConsoleView({
    super.key,
    required this.projectRoot,
  });

  @override
  State<ConsoleView> createState() => _ConsoleViewState();
}

class _ConsoleViewState extends State<ConsoleView> {
  final List<String> _consoleLogs = [];
  final ScrollController _scrollController = ScrollController();
  final FlutterLocalNotificationsPlugin _localNotifications = FlutterLocalNotificationsPlugin();

  bool _isBuilding = false;
  int _buildPercentage = 0;
  String _buildPhase = 'Inactivo';
  String? _downloadUrl;

  @override
  void initState() {
    super.initState();
    _initNotifications();
    _connectSignalRLogs();
  }

  void _initNotifications() {
    const androidSettings = AndroidInitializationSettings('@mipmap/ic_launcher');
    const initSettings = InitializationSettings(android: androidSettings);
    _localNotifications.initialize(initSettings);
  }

  void _connectSignalRLogs() {
    // Escuchar actualizaciones directamente desde el websocket cliente
    // Para simplificar, le decimos al cliente websocket que cuando reciba progreso, actualice este estado
    final activePcUrl = DI.apiClient.dio.options.baseUrl;
    final hubUrl = '$activePcUrl/hubs/console';
    
    DI.wsClient.connect(hubUrl, onProgressReceived: (data) {
      if (!mounted) return;
      
      final percentage = data['Percentage'] as int;
      final phase = data['Phase'] as String;
      final logLine = data['LogLine'] as String;

      setState(() {
        _isBuilding = percentage < 100 && phase != 'Fallido';
        _buildPercentage = percentage;
        _buildPhase = phase;
        _consoleLogs.add(logLine);
      });

      _scrollToBottom();

      // Si terminó de compilar con éxito
      if (percentage == 100 && (phase == 'Completado' || phase == 'Completado con advertencias')) {
        // Encontrar enlace de descarga si viene en el log
        if (logLine.contains('WebViewLink:') || logLine.contains('Enlace de descarga:')) {
          final parts = logLine.split(': ');
          if (parts.length > 1) {
            setState(() {
              _downloadUrl = parts.sublist(1).join(': ').trim();
            });
          }
        }
        _showBuildNotification(true, 'Compilación Exitosa', 'El APK está listo y subido a Google Drive.');
      } else if (phase == 'Fallido' || phase == 'Error') {
        _showBuildNotification(false, 'Compilación Fallida', 'Se detectaron errores en el compilador.');
      }
    }).then((_) {
      // Unirse al grupo
      DI.wsClient.joinProjectGroup(widget.projectRoot);
    });
  }

  Future<void> _showBuildNotification(bool success, String title, String body) async {
    const androidDetails = AndroidNotificationDetails(
      'dreamos_build_channel',
      'Compilaciones DreamOS',
      channelDescription: 'Notificaciones de compilaciones de APK',
      importance: Importance.max,
      priority: Priority.high,
    );
    const details = NotificationDetails(android: androidDetails);
    await _localNotifications.show(0, title, body, details);
  }

  Future<void> _startBuild() async {
    setState(() {
      _consoleLogs.clear();
      _consoleLogs.add('>>> Iniciando solicitud de compilación remota...');
      _isBuilding = true;
      _buildPercentage = 0;
      _buildPhase = 'Solicitando...';
      _downloadUrl = null;
    });

    try {
      final response = await DI.apiClient.dio.post(
        '/api/build/run-build',
        data: {'ProjectPath': widget.projectRoot},
      );
      final msg = response.data['Message'] as String;
      setState(() {
        _consoleLogs.add('>>> $msg');
      });
    } catch (e) {
      setState(() {
        _isBuilding = false;
        _buildPhase = 'Fallo';
        _consoleLogs.add('>>> Error de conexión al lanzar compilación: $e');
      });
    }
  }

  Future<void> _runPubGet() async {
    setState(() {
      _consoleLogs.add('>>> Solicitando flutter pub get en PC...');
      _isBuilding = true;
    });

    try {
      final response = await DI.apiClient.dio.post(
        '/api/terminal/run',
        data: {
          'Command': 'flutter pub get',
          'WorkingDirectory': widget.projectRoot
        },
      );
      final output = response.data['Output'] as String;
      setState(() {
        _consoleLogs.add(output);
        _isBuilding = false;
      });
    } catch (e) {
      setState(() {
        _isBuilding = false;
        _consoleLogs.add('>>> Error al ejecutar comando: $e');
      });
    }
  }

  Future<void> _runGitStatus() async {
    setState(() {
      _consoleLogs.add('>>> Consultando git status...');
    });
    try {
      final response = await DI.apiClient.dio.post(
        '/api/terminal/run',
        data: {
          'Command': 'git status',
          'WorkingDirectory': widget.projectRoot
        },
      );
      final output = response.data['Output'] as String;
      setState(() {
        _consoleLogs.add(output);
      });
    } catch (e) {
      setState(() {
        _consoleLogs.add('>>> Error al ejecutar git: $e');
      });
    }
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scrollController.hasClients) {
        _scrollController.animateTo(
          _scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 200),
          curve: Curves.easeOut,
        );
      }
    });
  }

  Future<void> _openDownloadLink() async {
    if (_downloadUrl == null) return;
    final uri = Uri.parse(_downloadUrl!);
    if (await canLaunchUrl(uri)) {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('No se pudo abrir el enlace: $_downloadUrl')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Consola y Compilación'),
      ),
      body: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Status Card
            Card(
              elevation: 4,
              color: const Color(0xFF1D1D30),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
              child: Padding(
                padding: const EdgeInsets.all(16.0),
                child: Row(
                  children: [
                    // Circular Progress
                    SizedBox(
                      width: 60,
                      height: 60,
                      child: Stack(
                        alignment: Alignment.center,
                        children: [
                          CircularProgressIndicator(
                            value: _isBuilding ? _buildPercentage / 100 : 0.0,
                            backgroundColor: Colors.black26,
                            color: const Color(0xFF00CEC9),
                            strokeWidth: 5,
                          ),
                          Text(
                            '$_buildPercentage%',
                            style: GoogleFonts.firaCode(fontSize: 12, fontWeight: FontWeight.bold),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 16),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Estado del Proceso', style: GoogleFonts.outfit(color: const Color(0xFFA0A0C0), fontSize: 13)),
                          const SizedBox(height: 4),
                          Text(
                            _buildPhase,
                            style: GoogleFonts.outfit(fontSize: 18, fontWeight: FontWeight.bold),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 12),
            // Quick Commands Row
            Row(
              children: [
                Expanded(
                  child: ElevatedButton.icon(
                    onPressed: _isBuilding ? null : _startBuild,
                    icon: const Icon(Icons.build_circle),
                    label: const Text('Compilar APK'),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: ElevatedButton.icon(
                    onPressed: _isBuilding ? null : _runPubGet,
                    icon: const Icon(Icons.download),
                    label: const Text('Pub Get'),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton(
                  icon: const Icon(Icons.history_toggle_off, color: Color(0xFF00CEC9)),
                  onPressed: _runGitStatus,
                  tooltip: 'Git Status',
                ),
              ],
            ),
            const SizedBox(height: 16),
            // Download Link Card
            if (_downloadUrl != null)
              Card(
                color: Colors.green.withOpacity(0.15),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                child: ListTile(
                  leading: const Icon(Icons.cloud_done, color: Colors.greenAccent),
                  title: Text('APK Compilado Disponible', style: GoogleFonts.outfit(fontWeight: FontWeight.bold)),
                  subtitle: Text('Subido a tu Google Drive', style: GoogleFonts.outfit(fontSize: 12)),
                  trailing: ElevatedButton(
                    onPressed: _openDownloadLink,
                    style: ElevatedButton.styleFrom(backgroundColor: Colors.green, padding: const EdgeInsets.symmetric(horizontal: 16)),
                    child: const Text('Abrir Drive'),
                  ),
                ),
              ),
            const SizedBox(height: 12),
            // Logs Terminal Area
            Expanded(
              child: Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: const Color(0xFF07070F),
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: const Color(0xFF1D1D30)),
                ),
                child: ListView.builder(
                  controller: _scrollController,
                  itemCount: _consoleLogs.length,
                  itemBuilder: (context, index) {
                    return Text(
                      _consoleLogs[index],
                      style: GoogleFonts.firaCode(fontSize: 12, color: const Color(0xFFC0C0D0)),
                    );
                  },
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
