import 'dart:async';
import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:speech_to_text/speech_to_text.dart' as stt;
import '../../../core/di/injection.dart';
import '../../../core/network/api_client.dart';
import 'ai_settings_dialog.dart';

class ChatView extends StatefulWidget {
  final String projectRoot;
  final Function(List<dynamic> changes) onProposedChangesReceived;

  const ChatView({
    super.key,
    required this.projectRoot,
    required this.onProposedChangesReceived,
  });

  @override
  State<ChatView> createState() => _ChatViewState();
}

class _ChatViewState extends State<ChatView> {
  final List<Map<String, dynamic>> _messages = [];
  final _inputController = TextEditingController();
  final ScrollController _scrollController = ScrollController();
  
  late stt.SpeechToText _speech;
  bool _isListening = false;
  bool _isLoading = false;
  bool _modifyMode = false; // Toggle para Preguntar vs Modificar Código

  // Variables de seguimiento de progreso
  Timer? _progressTimer;
  double _progressValue = 0.0;
  String _progressStatus = 'Iniciando análisis...';
  int _elapsedSeconds = 0;

  @override
  void initState() {
    super.initState();
    _speech = stt.SpeechToText();
    
    // Mensaje de bienvenida
    _messages.add({
      'sender': 'ai',
      'text': '¡Hola! Soy tu agente autónomo DreamOS. ¿En qué puedo ayudarte en este proyecto hoy?'
    });
  }

  @override
  void dispose() {
    _progressTimer?.cancel();
    _inputController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  void _startProgressTracking() {
    _progressValue = 0.05;
    _progressStatus = '🔍 Escaneando archivos y contexto del workspace...';
    _elapsedSeconds = 0;
    _progressTimer?.cancel();

    _progressTimer = Timer.periodic(const Duration(milliseconds: 500), (timer) {
      if (!mounted) return;
      setState(() {
        _elapsedSeconds = timer.tick ~/ 2;
        final sec = _elapsedSeconds;

        if (sec < 4) {
          _progressValue = 0.10 + (sec * 0.05);
          _progressStatus = '🔍 Escaneando estructura de archivos del proyecto...';
        } else if (sec < 12) {
          _progressValue = 0.30 + ((sec - 4) * 0.03);
          _progressStatus = '🧠 Procesando prompt con el modelo Gemini 2.5...';
        } else if (sec < 25) {
          _progressValue = 0.55 + ((sec - 12) * 0.02);
          _progressStatus = '✨ Generando modificaciones de código e inyectando parches...';
        } else if (sec < 45) {
          _progressValue = 0.80 + ((sec - 25) * 0.005);
          _progressStatus = '⚡ Validando sintaxis y preparando revisión visual de Diffs...';
        } else {
          _progressValue = 0.92;
          _progressStatus = '⏳ Finalizando procesamiento de cambios extensos...';
        }
      });
    });
  }

  void _stopProgressTracking({bool isSuccess = true}) {
    _progressTimer?.cancel();
    if (isSuccess) {
      setState(() {
        _progressValue = 1.0;
        _progressStatus = '✅ ¡Procesamiento completado con éxito!';
      });
    }
  }

  Future<void> _sendMessage() async {
    final text = _inputController.text.trim();
    if (text.isEmpty || _isLoading) return;

    _inputController.clear();
    setState(() {
      _messages.add({'sender': 'user', 'text': text});
      _isLoading = true;
    });
    _startProgressTracking();
    _scrollToBottom();

    try {
      if (_modifyMode) {
        // Enviar instrucción para planificar cambios en archivos
        final response = await DI.apiClient.dio.post(
          '/api/ia/modify-plan',
          data: {
            'Prompt': text,
            'ProjectRoot': widget.projectRoot,
            'TargetFiles': [] // Dejamos que la IA infiera o use toda la base
          },
        );

        _stopProgressTracking(isSuccess: true);
        final changesList = response.data as List;
        if (changesList.isNotEmpty) {
          setState(() {
            _messages.add({
              'sender': 'ai',
              'text': 'He planificado cambios en ${changesList.length} archivo(s). Por favor revísalos en la pestaña de Editor.'
            });
          });
          widget.onProposedChangesReceived(changesList);
        } else {
          setState(() {
            _messages.add({
              'sender': 'ai',
              'text': 'Analicé el proyecto pero no considero que sea necesario realizar modificaciones para esta instrucción.'
            });
          });
        }
      } else {
        // Modo consulta libre
        final response = await DI.apiClient.dio.post(
          '/api/ia/ask',
          data: {
            'Prompt': text,
            'ProjectRoot': widget.projectRoot,
          },
        );

        _stopProgressTracking(isSuccess: true);
        final answer = (response.data['Answer'] ?? response.data['answer'] ?? '') as String;
        setState(() {
          _messages.add({'sender': 'ai', 'text': answer});
        });
      }
    } catch (e) {
      _stopProgressTracking(isSuccess: false);
      setState(() {
        _messages.add({
          'sender': 'ai',
          'text': 'Lo siento, ocurrió un error al procesar tu solicitud: $e'
        });
      });
    } finally {
      setState(() => _isLoading = false);
      _scrollToBottom();
    }
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scrollController.hasClients) {
        _scrollController.animateTo(
          _scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 300),
          curve: Curves.easeOut,
        );
      }
    });
  }

  Future<void> _toggleListening() async {
    if (!_isListening) {
      bool available = await _speech.initialize(
        onStatus: (val) => print('Speech status: $val'),
        onError: (val) => print('Speech error: $val'),
      );
      if (available) {
        setState(() => _isListening = true);
        _speech.listen(
          onResult: (val) => setState(() {
            _inputController.text = val.recognizedWords;
          }),
        );
      }
    } else {
      setState(() => _isListening = false);
      _speech.stop();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Asistente de IA (Remoto)'),
        actions: [
          IconButton(
            icon: const Icon(Icons.psychology_outlined, color: Color(0xFFA29BFE)),
            tooltip: 'Configurar IA / Proveedores',
            onPressed: () {
              showDialog(
                context: context,
                builder: (context) => AiSettingsDialog(apiClient: DI.apiClient),
              );
            },
          ),
          // Selector de modo
          Row(
            children: [
              Text(
                _modifyMode ? 'Modificar Código' : 'Solo Preguntar',
                style: GoogleFonts.outfit(fontSize: 12, color: _modifyMode ? const Color(0xFF00CEC9) : Colors.white70),
              ),
              Switch(
                value: _modifyMode,
                activeColor: const Color(0xFF00CEC9),
                onChanged: (val) {
                  setState(() {
                    _modifyMode = val;
                  });
                },
              ),
            ],
          )
        ],
      ),
      body: Column(
        children: [
          Expanded(
            child: ListView.builder(
              controller: _scrollController,
              padding: const EdgeInsets.all(16),
              itemCount: _messages.length,
              itemBuilder: (context, index) {
                final msg = _messages[index];
                final isUser = msg['sender'] == 'user';

                return Align(
                  alignment: isUser ? Alignment.centerRight : Alignment.centerLeft,
                  child: Container(
                    margin: const EdgeInsets.only(bottom: 12),
                    padding: const EdgeInsets.all(14),
                    constraints: BoxConstraints(maxWidth: MediaQuery.of(context).size.width * 0.8),
                    decoration: BoxDecoration(
                      color: isUser ? const Color(0xFF6C5CE7) : const Color(0xFF1D1D30),
                      borderRadius: BorderRadius.only(
                        topLeft: const Radius.circular(12),
                        topRight: const Radius.circular(12),
                        bottomLeft: isUser ? const Radius.circular(12) : Radius.zero,
                        bottomRight: isUser ? Radius.zero : const Radius.circular(12),
                      ),
                    ),
                    child: Text(
                      msg['text'] ?? '',
                      style: GoogleFonts.outfit(fontSize: 15, color: Colors.white),
                    ),
                  ),
                );
              },
            ),
          ),
          if (_isLoading)
            Container(
              margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: const Color(0xFF151528),
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: const Color(0xFF00CEC9).withOpacity(0.3)),
                boxShadow: [
                  BoxShadow(
                    color: const Color(0xFF00CEC9).withOpacity(0.08),
                    blurRadius: 12,
                    offset: const Offset(0, 4),
                  )
                ]
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Expanded(
                        child: Text(
                          _progressStatus,
                          style: GoogleFonts.outfit(fontSize: 13, fontWeight: FontWeight.w600, color: const Color(0xFF00CEC9)),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(
                        '⏱️ ${_elapsedSeconds}s / máx 5m',
                        style: GoogleFonts.firaCode(fontSize: 11, color: const Color(0xFFA0A0C0)),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(6),
                    child: LinearProgressIndicator(
                      value: _progressValue,
                      minHeight: 8,
                      backgroundColor: const Color(0xFF1D1D30),
                      valueColor: const AlwaysStoppedAnimation<Color>(Color(0xFF00CEC9)),
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    '${(_progressValue * 100).toInt()}% completado',
                    style: GoogleFonts.outfit(fontSize: 11, color: const Color(0xFFA0A0C0)),
                  ),
                ],
              ),
            ),
          // Área de Entrada de texto
          Container(
            padding: const EdgeInsets.all(12),
            decoration: const BoxDecoration(
              color: Color(0xFF151528),
              border: Border(top: BorderSide(color: Color(0xFF1D1D30))),
            ),
            child: Row(
              children: [
                // Botón Dictado por voz
                IconButton(
                  icon: Icon(_isListening ? Icons.mic : Icons.mic_none, color: _isListening ? Colors.red : const Color(0xFF00CEC9)),
                  onPressed: _toggleListening,
                  tooltip: 'Dictar por voz',
                ),
                Expanded(
                  child: TextField(
                    controller: _inputController,
                    decoration: InputDecoration(
                      hintText: _modifyMode 
                          ? 'Escribe instrucción de cambio (ej: agrega botón login)' 
                          : 'Haz una pregunta sobre el proyecto...',
                      border: InputBorder.none,
                      filled: false,
                    ),
                    onSubmitted: (_) => _sendMessage(),
                  ),
                ),
                IconButton(
                  icon: const Icon(Icons.send, color: Color(0xFF00CEC9)),
                  onPressed: _sendMessage,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
