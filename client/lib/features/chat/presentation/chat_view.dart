import 'dart:async';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
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

  // Variables de seguimiento de tiempo real y cancelación
  CancelToken? _cancelToken;
  Timer? _progressTimer;
  int _elapsedSeconds = 0;

  // Variables de modelo activo
  String _activeModelName = 'Gemini 2.5';

  String _formatSeconds(int sec) {
    final m = (sec ~/ 60).toString().padLeft(2, '0');
    final s = (sec % 60).toString().padLeft(2, '0');
    return '$m:$s';
  }

  @override
  void initState() {
    super.initState();
    _speech = stt.SpeechToText();
    _loadActiveModelName();
    
    // Mensaje de bienvenida
    _messages.add({
      'sender': 'ai',
      'text': '¡Hola! Soy tu agente autónomo DreamOS. ¿En qué puedo ayudarte en este proyecto hoy?'
    });
  }

  Future<void> _loadActiveModelName() async {
    try {
      final response = await DI.apiClient.get('/api/ia/settings');
      if (response.data != null) {
        final provider = response.data['provider'] ?? 'Gemini';
        final model = response.data['model'] ?? '';
        setState(() {
          if (provider == 'Gemini') {
            _activeModelName = 'Gemini 2.5';
          } else {
            _activeModelName = model.isNotEmpty ? model : 'OpenAI';
          }
        });
      }
    } catch (_) {}
  }

  @override
  void dispose() {
    _progressTimer?.cancel();
    _inputController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  void _startTimer() {
    _elapsedSeconds = 0;
    _progressTimer?.cancel();
    _progressTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) return;
      setState(() {
        _elapsedSeconds++;
      });
    });
  }

  void _stopTimer() {
    _progressTimer?.cancel();
  }

  Future<void> _sendMessage() async {
    final text = _inputController.text.trim();
    if (text.isEmpty || _isLoading) return;

    _inputController.clear();
    _cancelToken = CancelToken();

    setState(() {
      _messages.add({'sender': 'user', 'text': text});
      _isLoading = true;
    });

    _startTimer();
    _scrollToBottom();

    try {
      if (_modifyMode) {
        final response = await DI.apiClient.dio.post(
          '/api/ia/modify-plan',
          data: {
            'Prompt': text,
            'ProjectRoot': widget.projectRoot,
            'TargetFiles': []
          },
          cancelToken: _cancelToken,
        );

        _stopTimer();
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
        final response = await DI.apiClient.dio.post(
          '/api/ia/ask',
          data: {
            'Prompt': text,
            'ProjectRoot': widget.projectRoot,
          },
          cancelToken: _cancelToken,
        );

        _stopTimer();
        final answer = (response.data['Answer'] ?? response.data['answer'] ?? '') as String;
        setState(() {
          _messages.add({'sender': 'ai', 'text': answer});
        });
      }
    } catch (e) {
      _stopTimer();
      if (CancelToken.isCancel(e as DioException)) {
        setState(() {
          _messages.add({
            'sender': 'ai',
            'text': '⏹ Petición cancelada por el usuario.'
          });
        });
      } else {
        String errorMsg = e.toString();
        if (e is DioException && e.response?.data != null) {
          if (e.response?.data is Map && e.response?.data['message'] != null) {
            errorMsg = e.response!.data['message'].toString();
          }
        }
        setState(() {
          _messages.add({
            'sender': 'ai',
            'text': 'Lo siento, ocurrió un error al procesar tu solicitud: $errorMsg'
          });
        });
      }
    } finally {
      _stopTimer();
      if (mounted) {
        setState(() => _isLoading = false);
      }
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
        title: Text('Asistente IA ($_activeModelName)'),
        actions: [
          IconButton(
            icon: const Icon(Icons.psychology_outlined, color: Color(0xFFA29BFE)),
            tooltip: 'Configurar IA / Proveedores',
            onPressed: () async {
              final updated = await showDialog<bool>(
                context: context,
                builder: (context) => AiSettingsDialog(apiClient: DI.apiClient),
              );
              if (updated == true) {
                _loadActiveModelName();
              }
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
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        SelectableText(
                          msg['text'] ?? '',
                          style: GoogleFonts.outfit(fontSize: 15, color: Colors.white),
                        ),
                        const SizedBox(height: 4),
                        Align(
                          alignment: Alignment.centerRight,
                          child: InkWell(
                            onTap: () {
                              final textToCopy = msg['text'] ?? '';
                              Clipboard.setData(ClipboardData(text: textToCopy));
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(
                                  content: Text('📋 Mensaje copiado al portapapeles'),
                                  duration: Duration(seconds: 1),
                                  backgroundColor: Color(0xFF6C5CE7),
                                ),
                              );
                            },
                            child: const Padding(
                              padding: EdgeInsets.only(top: 2, left: 4),
                              child: Icon(Icons.copy_rounded, size: 14, color: Colors.white54),
                            ),
                          ),
                        ),
                      ],
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
                border: Border.all(color: const Color(0xFF6C5CE7).withOpacity(0.4)),
                boxShadow: [
                  BoxShadow(
                    color: const Color(0xFF6C5CE7).withOpacity(0.12),
                    blurRadius: 12,
                    offset: const Offset(0, 4),
                  )
                ],
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Row(
                        children: [
                          const SizedBox(
                            width: 14,
                            height: 14,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              valueColor: AlwaysStoppedAnimation<Color>(Color(0xFF00CEC9)),
                            ),
                          ),
                          const SizedBox(width: 10),
                          Text(
                            '🧠 Modelo: $_activeModelName',
                            style: GoogleFonts.outfit(
                              fontSize: 13,
                              fontWeight: FontWeight.w600,
                              color: const Color(0xFF00CEC9),
                            ),
                          ),
                        ],
                      ),
                      Text(
                        '⏱️ ${_formatSeconds(_elapsedSeconds)}',
                        style: GoogleFonts.firaCode(
                          fontSize: 12,
                          fontWeight: FontWeight.bold,
                          color: const Color(0xFFA0A0C0),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Expanded(
                        child: Text(
                          _modifyMode
                              ? '⚡ Generando parches y estructura de código en PC...'
                              : '💭 Procesando respuesta sin límite de tiempo...',
                          style: GoogleFonts.outfit(fontSize: 11, color: Colors.white70),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Material(
                        color: Colors.transparent,
                        child: InkWell(
                          radius: 20,
                          borderRadius: BorderRadius.circular(20),
                          onTap: () {
                            _cancelToken?.cancel('Cancelado por el usuario');
                          },
                          child: Container(
                            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                            decoration: BoxDecoration(
                              color: Colors.redAccent.withOpacity(0.2),
                              borderRadius: BorderRadius.circular(20),
                              border: Border.all(color: Colors.redAccent),
                            ),
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                const Icon(Icons.stop_circle_rounded, size: 14, color: Colors.redAccent),
                                const SizedBox(width: 4),
                                Text(
                                  'Detener Petición',
                                  style: GoogleFonts.outfit(
                                    fontSize: 11,
                                    fontWeight: FontWeight.bold,
                                    color: Colors.redAccent,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                      ),
                    ],
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
