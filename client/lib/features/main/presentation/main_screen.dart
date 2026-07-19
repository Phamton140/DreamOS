import 'package:flutter/material.dart';
import '../../../core/di/injection.dart';
import '../../chat/presentation/chat_view.dart';
import '../../editor/presentation/editor_view.dart';
import '../../explorer/presentation/explorer_view.dart';
import '../../terminal/presentation/console_view.dart';

class MainScreen extends StatefulWidget {
  final Map<String, dynamic> workspace;
  const MainScreen({super.key, required this.workspace});

  @override
  State<MainScreen> createState() => _MainScreenState();
}

class _MainScreenState extends State<MainScreen> with SingleTickerProviderStateMixin {
  late TabController _tabController;
  
  String? _activeFilePath;
  String _activeFileContent = '';
  List<dynamic> _proposedChanges = []; // Lista de cambios propuestos por la IA (Diffs)

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 4, vsync: this);
    
    // Conectar SignalR ConsoleHub para recibir logs de compilación
    _connectSignalR();
  }

  Future<void> _connectSignalR() async {
    try {
      final activePc = DI.apiClient.dio.options.baseUrl;
      final hubUrl = '$activePc/hubs/console';
      
      // Iniciar WebSocket Hub
      await DI.wsClient.connect(hubUrl, onProgressReceived: (progress) {
        // Enviar evento de progreso al bus global o que el ConsoleView escuche.
        // SignalR se encarga de retransmitir al grupo específico de este proyecto.
      });

      // Unirse al grupo específico de este proyecto
      await DI.wsClient.joinProjectGroup(widget.workspace['ProjectRootPath']);
    } catch (_) {
      // Fallo de conexión de sockets, se reintentará
    }
  }

  @override
  void dispose() {
    DI.wsClient.leaveProjectGroup(widget.workspace['ProjectRootPath']);
    DI.wsClient.disconnect();
    _tabController.dispose();
    super.dispose();
  }

  void _openFile(String path, String content) {
    setState(() {
      _activeFilePath = path;
      _activeFileContent = content;
    });
    // Cambiar a la pestaña de Editor (índice 1)
    _tabController.animateTo(1);
  }

  void _setProposedChanges(List<dynamic> changes) {
    setState(() {
      _proposedChanges = changes;
    });
    // Cambiar a la pestaña de Editor para ver el Diff (índice 1)
    _tabController.animateTo(1);
  }

  void _clearProposedChanges() {
    setState(() {
      _proposedChanges = [];
    });
  }

  @override
  Widget build(BuildContext context) {
    final projectRoot = widget.workspace['ProjectRootPath'] as String;

    return Scaffold(
      body: SafeArea(
        child: TabBarView(
          controller: _tabController,
          physics: const NeverScrollableScrollPhysics(), // Evita deslizamientos accidentales
          children: [
            ExplorerView(
              projectRoot: projectRoot,
              onFileSelected: _openFile,
            ),
            EditorView(
              projectRoot: projectRoot,
              filePath: _activeFilePath,
              content: _activeFileContent,
              proposedChanges: _proposedChanges,
              onChangesApplied: () {
                _clearProposedChanges();
                // Opcional: recargar el archivo activo
              },
              onCancelChanges: _clearProposedChanges,
              onBack: () => _tabController.animateTo(0),
            ),
            ChatView(
              projectRoot: projectRoot,
              onProposedChangesReceived: _setProposedChanges,
            ),
            ConsoleView(
              projectRoot: projectRoot,
            ),
          ],
        ),
      ),
      bottomNavigationBar: SafeArea(
        top: false,
        child: Container(
          decoration: const BoxDecoration(
            color: Color(0xFF0F0F1E),
            border: Border(top: BorderSide(color: Color(0xFF1D1D30), width: 1.5)),
          ),
          child: TabBar(
            controller: _tabController,
            indicatorColor: const Color(0xFF00CEC9),
            labelColor: const Color(0xFF00CEC9),
            unselectedLabelColor: const Color(0xFFA0A0C0),
            tabs: const [
              Tab(icon: Icon(Icons.folder_open), text: 'Archivos'),
              Tab(icon: Icon(Icons.code), text: 'Editor'),
              Tab(icon: Icon(Icons.forum_outlined), text: 'Chat IA'),
              Tab(icon: Icon(Icons.terminal), text: 'Consola'),
            ],
          ),
        ),
      ),
    );
  }
}
