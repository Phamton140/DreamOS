import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:speech_to_text/speech_to_text.dart' as stt;
import '../../../core/di/injection.dart';

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

  Future<void> _sendMessage() async {
    final text = _inputController.text.trim();
    if (text.isEmpty || _isLoading) return;

    _inputController.clear();
    setState(() {
      _messages.add({'sender': 'user', 'text': text});
      _isLoading = true;
    });
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

        final answer = (response.data['Answer'] ?? response.data['answer'] ?? '') as String;
        setState(() {
          _messages.add({'sender': 'ai', 'text': answer});
        });
      }
    } catch (e) {
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
            const Padding(
              padding: EdgeInsets.all(8.0),
              child: SizedBox(width: 24, height: 24, child: CircularProgressIndicator(strokeWidth: 2.5)),
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
