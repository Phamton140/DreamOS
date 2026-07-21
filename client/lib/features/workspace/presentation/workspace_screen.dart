import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import '../../../core/di/injection.dart';
import '../../../core/storage/secure_storage.dart';
import '../../main/presentation/main_screen.dart';
import '../../pairing/presentation/pairing_screen.dart';

class WorkspaceScreen extends StatefulWidget {
  const WorkspaceScreen({super.key});

  @override
  State<WorkspaceScreen> createState() => _WorkspaceScreenState();
}

class _WorkspaceScreenState extends State<WorkspaceScreen> {
  final _secureStorage = SecureStorage();
  List<Map<String, dynamic>> _pcs = [];
  Map<String, dynamic>? _activePc;
  List<dynamic> _workspaces = [];
  bool _isLoading = true;
  bool _isConnected = false;

  @override
  void initState() {
    super.initState();
    _loadPcProfiles();
  }

  Future<void> _loadPcProfiles() async {
    setState(() => _isLoading = true);
    final pcsList = await _secureStorage.getLinkedPcs();
    final active = await _secureStorage.getActivePc();

    setState(() {
      _pcs = pcsList;
      _activePc = active ?? (pcsList.isNotEmpty ? pcsList.first : null);
    });

    if (_activePc != null) {
      await _connectToPc(_activePc!);
    } else {
      setState(() => _isLoading = false);
    }
  }

  Future<void> _connectToPc(Map<String, dynamic> pc) async {
    setState(() {
      _isLoading = true;
      _activePc = pc;
    });

    // Intentar inicializar conexión (LAN/Túnel)
    final success = await DI.apiClient.initializeConnection(pc);

    if (success) {
      // Conexión exitosa, cargar workspaces de la base de datos de la PC
      try {
        final response = await DI.apiClient.dio.get('/api/workspaces');
        setState(() {
          _workspaces = response.data as List;
          _isConnected = true;
        });
      } catch (e) {
        setState(() => _isConnected = false);
        _showError('No se pudieron recuperar las áreas de trabajo de la PC: $e');
      }
    } else {
      setState(() => _isConnected = false);
      _showError('No se pudo establecer conexión remota con el agente.');
    }

    setState(() => _isLoading = false);
  }

  Future<void> _createNewWorkspace() async {
    final nameController = TextEditingController();
    final pathController = TextEditingController();

    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('Nuevo Workspace', style: GoogleFonts.outfit(fontWeight: FontWeight.bold)),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: nameController,
              decoration: const InputDecoration(labelText: 'Nombre del Proyecto'),
            ),
            const SizedBox(height: 16),
            TextField(
              controller: pathController,
              decoration: const InputDecoration(
                labelText: 'Directorio en la PC (Ruta Absoluta)',
                hintText: 'Ej: C:/Proyectos/MiApp',
              ),
            ),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
          ElevatedButton(
            onPressed: () async {
              if (nameController.text.isNotEmpty && pathController.text.isNotEmpty) {
                Navigator.pop(context);
                setState(() => _isLoading = true);
                try {
                  await DI.apiClient.dio.post('/api/workspaces', data: {
                    'Name': nameController.text,
                    'ProjectRootPath': pathController.text
                  });
                  _loadPcProfiles();
                } catch (e) {
                  _showError('Fallo al crear workspace: $e');
                }
              }
            },
            child: const Text('Crear'),
          )
        ],
      ),
    );
  }

  void _showError(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: Colors.red),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('DreamOS Dev Dashboard'),
        actions: [
          IconButton(
            icon: const Icon(Icons.qr_code_scanner),
            onPressed: () {
              Navigator.push(
                context,
                MaterialPageRoute(builder: (context) => const PairingScreen()),
              ).then((_) => _loadPcProfiles());
            },
            tooltip: 'Vincular nueva PC',
          )
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : Padding(
              padding: const EdgeInsets.all(20.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // PC Profile Header
                  if (_activePc != null)
                    Card(
                      elevation: 4,
                      color: Theme.of(context).cardColor,
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
                      child: Padding(
                        padding: const EdgeInsets.all(16.0),
                        child: Row(
                          children: [
                            Icon(
                              Icons.computer,
                              color: _isConnected ? const Color(0xFF00CEC9) : Colors.red,
                              size: 40,
                            ),
                            const SizedBox(width: 16),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    _activePc!['Name'] ?? 'PC Remota',
                                    style: GoogleFonts.outfit(fontSize: 18, fontWeight: FontWeight.bold),
                                  ),
                                  const SizedBox(height: 4),
                                  Text(
                                    _isConnected 
                                        ? 'Conectado por ${_activePc!['ConnectionMode'] ?? 'Internet'}' 
                                        : 'Desconectado',
                                    style: GoogleFonts.outfit(
                                      fontSize: 14, 
                                      color: _isConnected ? const Color(0xFF00CEC9) : Colors.red
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            IconButton(
                              icon: const Icon(Icons.refresh),
                              onPressed: () => _connectToPc(_activePc!),
                            )
                          ],
                        ),
                      ),
                    )
                  else
                    Card(
                      elevation: 2,
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
                      child: Padding(
                        padding: const EdgeInsets.all(24.0),
                        child: Column(
                          children: [
                            const Icon(Icons.warning_amber_rounded, size: 48, color: Colors.orange),
                            const SizedBox(height: 12),
                            Text('No hay computadoras vinculadas', style: GoogleFonts.outfit(fontSize: 16)),
                            const SizedBox(height: 16),
                            ElevatedButton.icon(
                              onPressed: () {
                                Navigator.push(
                                  context,
                                  MaterialPageRoute(builder: (context) => const PairingScreen()),
                                ).then((_) => _loadPcProfiles());
                              },
                              icon: const Icon(Icons.add),
                              label: const Text('Vincular PC Ahora'),
                            )
                          ],
                        ),
                      ),
                    ),
                  const SizedBox(height: 24),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text('Mis Workspaces', style: Theme.of(context).textTheme.titleLarge),
                      if (_isConnected)
                        IconButton(
                          icon: const Icon(Icons.add_circle_outline, color: Color(0xFF00CEC9)),
                          onPressed: _createNewWorkspace,
                          tooltip: 'Nuevo Workspace',
                        )
                    ],
                  ),
                  const SizedBox(height: 12),
                  Expanded(
                    child: _workspaces.isEmpty
                        ? Center(
                            child: Text(
                              _isConnected 
                                  ? 'No hay workspaces creados aún. ¡Crea el primero!' 
                                  : 'Conéctate a una PC para ver tus áreas de trabajo.',
                              textAlign: TextAlign.center,
                            ),
                          )
                        : ListView.builder(
                            itemCount: _workspaces.length,
                            itemBuilder: (context, index) {
                               final ws = _workspaces[index] as Map<String, dynamic>;
                              final id = (ws['Id'] ?? ws['id'] ?? '') as String;
                              final name = (ws['Name'] ?? ws['name'] ?? 'Workspace sin nombre') as String;
                              final path = (ws['ProjectRootPath'] ?? ws['projectRootPath'] ?? '') as String;

                              return Card(
                                margin: const EdgeInsets.only(bottom: 12),
                                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                                child: ListTile(
                                  contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                                  title: Text(name, style: GoogleFonts.outfit(fontWeight: FontWeight.w600)),
                                  subtitle: Text(path, style: GoogleFonts.outfit(fontSize: 12)),
                                  trailing: Row(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      if (id.isNotEmpty)
                                        IconButton(
                                          icon: const Icon(Icons.delete_outline, color: Colors.redAccent, size: 20),
                                          onPressed: () {
                                            showDialog(
                                              context: context,
                                              builder: (context) => AlertDialog(
                                                title: const Text('Eliminar Workspace'),
                                                content: Text('¿Deseas desvincular "$name"? (Tus archivos en el PC no sufrirán ningún daño).'),
                                                actions: [
                                                  TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
                                                  TextButton(
                                                    onPressed: () async {
                                                      Navigator.pop(context);
                                                      try {
                                                        await DI.apiClient.dio.delete('/api/workspaces/$id');
                                                        _loadPcProfiles();
                                                      } catch (e) {
                                                        _showError('Fallo al eliminar workspace: $e');
                                                      }
                                                    },
                                                    child: const Text('Eliminar', style: TextStyle(color: Colors.red)),
                                                  )
                                                ],
                                              ),
                                            );
                                          },
                                          tooltip: 'Eliminar Workspace',
                                        ),
                                      const Icon(Icons.arrow_forward_ios, size: 16, color: Color(0xFF00CEC9)),
                                    ],
                                  ),
                                  onTap: () {
                                    Navigator.push(
                                      context,
                                      MaterialPageRoute(
                                        builder: (context) => MainScreen(
                                          workspace: {
                                            'Name': name,
                                            'ProjectRootPath': path
                                          },
                                        ),
                                      ),
                                    );
                                  },
                                ),
                              );
                            },
                          ),
                  )
                ],
              ),
            ),
    );
  }
}
