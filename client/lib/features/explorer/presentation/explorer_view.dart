import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import '../../../core/di/injection.dart';

class ExplorerView extends StatefulWidget {
  final String projectRoot;
  final Function(String path, String content) onFileSelected;

  const ExplorerView({
    super.key,
    required this.projectRoot,
    required this.onFileSelected,
  });

  @override
  State<ExplorerView> createState() => _ExplorerViewState();
}

class _ExplorerViewState extends State<ExplorerView> {
  late String _currentPath;
  List<dynamic> _items = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _currentPath = widget.projectRoot;
    _loadDirectoryContent();
  }

  Future<void> _loadDirectoryContent() async {
    setState(() => _isLoading = true);

    try {
      final response = await DI.apiClient.dio.get(
        '/api/files/explore',
        queryParameters: {'path': _currentPath},
      );
      setState(() {
        _items = response.data as List;
      });
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Error al cargar directorio: $e'), backgroundColor: Colors.red),
      );
    } finally {
      setState(() => _isLoading = false);
    }
  }

  Future<void> _handleItemTap(Map<String, dynamic> item) async {
    final path = (item['RelativePath'] ?? item['relativePath'] ?? '') as String;
    final isDir = (item['IsDirectory'] ?? item['isDirectory'] ?? false) as bool;

    if (isDir) {
      setState(() {
        _currentPath = path;
      });
      _loadDirectoryContent();
    } else {
      // Es un archivo, leer su contenido
      setState(() => _isLoading = true);
      try {
        final response = await DI.apiClient.dio.get(
          '/api/files/read',
          queryParameters: {'path': path},
        );
        final content = (response.data['Content'] ?? response.data['content'] ?? '') as String;
        widget.onFileSelected(path, content);
      } catch (e) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Error al abrir archivo: $e'), backgroundColor: Colors.red),
        );
      } finally {
        setState(() => _isLoading = false);
      }
    }
  }

  void _navigateUp() {
    if (_currentPath == widget.projectRoot) return;
    
    // Obtener directorio padre compatible con / y \
    final separator = _currentPath.contains('\\') ? '\\' : '/';
    final parts = _currentPath.split(RegExp(r'[/\\]'));
    if (parts.length > 1) {
      parts.removeLast();
      setState(() {
        _currentPath = parts.join(separator);
      });
      _loadDirectoryContent();
    }
  }

  Future<void> _deleteItem(String path) async {
    setState(() => _isLoading = true);
    try {
      await DI.apiClient.dio.post('/api/files/delete', data: {'Path': path});
      _loadDirectoryContent();
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Fallo al borrar: $e'), backgroundColor: Colors.red),
      );
      setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final isAtRoot = _currentPath == widget.projectRoot;
    final folderName = _currentPath.split(RegExp(r'[/\\]')).last;

    return Scaffold(
      appBar: AppBar(
        title: Text(folderName.isEmpty ? 'Explorador' : folderName),
        leading: isAtRoot
            ? null
            : IconButton(
                icon: const Icon(Icons.arrow_back),
                onPressed: _navigateUp,
              ),
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Breadcrumb path display
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                  color: const Color(0xFF151528),
                  child: Text(
                    _currentPath.replaceFirst(widget.projectRoot, 'Project'),
                    style: GoogleFonts.firaCode(fontSize: 12, color: const Color(0xFFA0A0C0)),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                Expanded(
                  child: _items.isEmpty
                      ? const Center(child: Text('Carpeta vacía'))
                      : ListView.builder(
                          itemCount: _items.length,
                          itemBuilder: (context, index) {
                            final item = _items[index] as Map<String, dynamic>;
                            final path = (item['RelativePath'] ?? item['relativePath'] ?? '') as String;
                            final name = path.split('/').last;
                            final isDir = (item['IsDirectory'] ?? item['isDirectory'] ?? false) as bool;
                            final fileType = (item['FileType'] ?? item['fileType'] ?? '') as String;

                            return ListTile(
                              leading: Icon(
                                isDir ? Icons.folder : Icons.insert_drive_file,
                                color: isDir ? const Color(0xFF6C5CE7) : const Color(0xFFA0A0C0),
                              ),
                              title: Text(
                                name,
                                style: GoogleFonts.outfit(fontWeight: isDir ? FontWeight.w600 : FontWeight.normal),
                              ),
                              trailing: IconButton(
                                icon: const Icon(Icons.delete_outline, color: Colors.redAccent, size: 20),
                                onPressed: () {
                                  showDialog(
                                    context: context,
                                    builder: (context) => AlertDialog(
                                      title: const Text('Confirmar Borrado'),
                                      content: Text('¿Estás seguro de eliminar "$name"?'),
                                      actions: [
                                        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
                                        TextButton(
                                          onPressed: () {
                                            Navigator.pop(context);
                                            _deleteItem(path);
                                          },
                                          child: const Text('Eliminar', style: TextStyle(color: Colors.red)),
                                        )
                                      ],
                                    ),
                                  );
                                },
                              ),
                              onTap: () => _handleItemTap(item),
                            );
                          },
                        ),
                ),
              ],
            ),
    );
  }
}
