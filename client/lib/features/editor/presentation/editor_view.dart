import 'package:code_text_field/code_text_field.dart';
import 'package:flutter/material.dart';
import 'package:flutter_highlight/themes/monokai-sublime.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:highlight/languages/dart.dart';
import '../../../core/di/injection.dart';

class EditorView extends StatefulWidget {
  final String projectRoot;
  final String? filePath;
  final String content;
  final List<dynamic> proposedChanges;
  final VoidCallback onChangesApplied;
  final VoidCallback onCancelChanges;

  final VoidCallback? onBack;

  const EditorView({
    super.key,
    required this.projectRoot,
    required this.filePath,
    required this.content,
    required this.proposedChanges,
    required this.onChangesApplied,
    required this.onCancelChanges,
    this.onBack,
  });

  @override
  State<EditorView> createState() => _EditorViewState();
}

class _EditorViewState extends State<EditorView> {
  CodeController? _codeController;
  bool _isSaving = false;

  @override
  void initState() {
    super.initState();
    _initEditor();
  }

  @override
  void didUpdateWidget(covariant EditorView oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.content != widget.content || oldWidget.filePath != widget.filePath) {
      _initEditor();
    }
  }

  void _initEditor() {
    if (widget.filePath == null) return;
    
    _codeController = CodeController(
      text: widget.content,
      language: dart, // Podríamos mapear lenguajes según extensión en el futuro
    );
  }

  Future<void> _saveFile() async {
    if (widget.filePath == null || _codeController == null) return;
    setState(() => _isSaving = true);

    try {
      await DI.apiClient.dio.post(
        '/api/files/write',
        data: {
          'Path': widget.filePath,
          'Content': _codeController!.text,
        },
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Archivo guardado en la PC.'), backgroundColor: Colors.green),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Error al guardar: $e'), backgroundColor: Colors.red),
        );
      }
    } finally {
      setState(() => _isSaving = false);
    }
  }

  Future<void> _applyIaChanges() async {
    setState(() => _isSaving = true);
    try {
      final response = await DI.apiClient.dio.post(
        '/api/ia/apply',
        data: {
          'ProjectRoot': widget.projectRoot,
          'Changes': widget.proposedChanges,
        },
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('¡Cambios aplicados exitosamente!'), backgroundColor: Colors.green),
        );
        widget.onChangesApplied();
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Fallo al aplicar cambios: $e'), backgroundColor: Colors.red),
        );
      }
    } finally {
      setState(() => _isSaving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    // Si hay cambios propuestos por la IA, mostrar el visor de Diffs
    if (widget.proposedChanges.isNotEmpty) {
      return _buildDiffViewer();
    }

    if (widget.filePath == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Editor')),
        body: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.code, size: 64, color: Color(0xFF313244)),
              const SizedBox(height: 16),
              Text(
                'Selecciona un archivo del explorador para editar',
                style: GoogleFonts.outfit(color: const Color(0xFFA0A0C0)),
              )
            ],
          ),
        ),
      );
    }

    final filename = widget.filePath!.split('/').last;

    return Scaffold(
      appBar: AppBar(
        title: Text(filename),
        leading: widget.onBack != null
            ? IconButton(
                icon: const Icon(Icons.arrow_back),
                onPressed: widget.onBack,
              )
            : null,
        actions: [
          if (_isSaving)
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: 16.0),
              child: Center(child: SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))),
            )
          else
            IconButton(
              icon: const Icon(Icons.save_outlined, color: Color(0xFF00CEC9)),
              onPressed: _saveFile,
              tooltip: 'Guardar cambios en PC',
            )
        ],
      ),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
            color: const Color(0xFF151528),
            child: Text(
              widget.filePath!.replaceFirst(widget.projectRoot, 'Project'),
              style: GoogleFonts.firaCode(fontSize: 11, color: const Color(0xFF606080)),
              overflow: TextOverflow.ellipsis,
            ),
          ),
          Expanded(
            child: _codeController == null
                ? const Center(child: CircularProgressIndicator())
                : CodeTheme(
                      data: CodeThemeData(styles: monokaiSublimeTheme),
                      child: CodeField(
                        controller: _codeController!,
                        textStyle: GoogleFonts.firaCode(fontSize: 14),
                        lineNumberStyle: LineNumberStyle(
                          width: 40,
                          margin: 10,
                          textStyle: GoogleFonts.firaCode(fontSize: 12, color: const Color(0xFF606080)),
                        ),
                      ),
                    ),
          ),
        ],
      ),
    );
  }

  Widget _buildDiffViewer() {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Revisión de Cambios de IA'),
        backgroundColor: const Color(0xFF151528),
      ),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Banner de Advertencia
          Container(
            padding: const EdgeInsets.all(12),
            color: Colors.amber.withOpacity(0.12),
            child: Row(
              children: [
                const Icon(Icons.info_outline, color: Colors.amber),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    'La IA propone modificar ${widget.proposedChanges.length} archivo(s). Por favor revisa los cambios antes de aplicar.',
                    style: GoogleFonts.outfit(color: Colors.amber, fontSize: 13, fontWeight: FontWeight.w500),
                  ),
                ),
              ],
            ),
          ),
          Expanded(
            child: ListView.builder(
              itemCount: widget.proposedChanges.length,
              itemBuilder: (context, index) {
                final change = widget.proposedChanges[index] as Map<String, dynamic>;
                final filePath = (change['FilePath'] ?? change['filePath'] ?? '') as String;
                final action = (change['Action'] ?? change['action'] ?? '') as String;
                final description = (change['Description'] ?? change['description'] ?? '') as String;
                final newContent = (change['NewContent'] ?? change['newContent'] ?? '') as String;

                return Card(
                  color: const Color(0xFF1A1A2E),
                  margin: const EdgeInsets.all(12),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                    side: const BorderSide(color: Color(0xFF2C2C4E)),
                  ),
                  child: Padding(
                    padding: const EdgeInsets.all(16.0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Expanded(
                              child: Text(
                                filePath,
                                style: GoogleFonts.firaCode(fontWeight: FontWeight.bold, fontSize: 14, color: Colors.white),
                              ),
                            ),
                            Chip(
                              label: Text(
                                action.toUpperCase(),
                                style: GoogleFonts.outfit(
                                  fontWeight: FontWeight.bold,
                                  fontSize: 11,
                                  color: action == 'Create'
                                      ? const Color(0xFF00B894)
                                      : action == 'Delete'
                                          ? const Color(0xFFFF7675)
                                          : const Color(0xFF0984E3),
                                ),
                              ),
                              backgroundColor: action == 'Create'
                                  ? const Color(0xFF00B894).withOpacity(0.12)
                                  : action == 'Delete'
                                      ? const Color(0xFFFF7675).withOpacity(0.12)
                                      : const Color(0xFF0984E3).withOpacity(0.12),
                              side: BorderSide.none,
                              padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
                            )
                          ],
                        ),
                        const SizedBox(height: 8),
                        Text(
                          'Propósito: $description',
                          style: GoogleFonts.outfit(color: const Color(0xFFA0A0C0), fontSize: 13),
                        ),
                        const SizedBox(height: 12),
                        // Mini visor de código propuesto
                        Container(
                          width: double.infinity,
                          constraints: const BoxConstraints(maxHeight: 180),
                          padding: const EdgeInsets.all(10),
                          decoration: BoxDecoration(
                            color: const Color(0xFF0F0F1E),
                            borderRadius: BorderRadius.circular(8),
                            border: Border.all(color: const Color(0xFF2C2C4E)),
                          ),
                          child: SingleChildScrollView(
                            child: Text(
                              newContent.isEmpty ? '[Sin contenido o eliminado]' : newContent,
                              style: GoogleFonts.firaCode(fontSize: 12, color: const Color(0xFF55EFC4)),
                            ),
                          ),
                        )
                      ],
                    ),
                  ),
                );
              },
            ),
          ),
          // Botones de acción en la parte inferior
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 20),
            color: const Color(0xFF151528),
            child: Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: widget.onCancelChanges,
                    style: OutlinedButton.styleFrom(
                      foregroundColor: const Color(0xFFFF7675),
                      side: const BorderSide(color: Color(0xFFFF7675), width: 1.5),
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                    child: Text(
                      'Rechazar',
                      style: GoogleFonts.outfit(fontWeight: FontWeight.bold, fontSize: 14),
                    ),
                  ),
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: ElevatedButton(
                    onPressed: _isSaving ? null : _applyIaChanges,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF00B894),
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                      elevation: 0,
                    ),
                    child: _isSaving
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                          )
                        : Text(
                            'Aplicar Cambios',
                            style: GoogleFonts.outfit(fontWeight: FontWeight.bold, fontSize: 14),
                          ),
                  ),
                ),
              ],
            ),
          )
        ],
      ),
    );
  }
}
