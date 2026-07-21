import 'package:flutter/material.dart';
import '../../../../core/network/api_client.dart';

class AiSettingsDialog extends StatefulWidget {
  final ApiClient apiClient;

  const AiSettingsDialog({super.key, required this.apiClient});

  @override
  State<AiSettingsDialog> createState() => _AiSettingsDialogState();
}

class _AiSettingsDialogState extends State<AiSettingsDialog> {
  bool _isLoading = true;
  bool _isSaving = false;
  bool _obscureKey = true;

  String _provider = 'Gemini'; // 'Gemini' or 'OpenAI'
  final TextEditingController _apiKeyController = TextEditingController();
  final TextEditingController _baseUrlController = TextEditingController();
  final TextEditingController _modelController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _loadSettings();
  }

  Future<void> _loadSettings() async {
    try {
      final response = await widget.apiClient.get('/api/ia/settings');
      if (response.data != null) {
        setState(() {
          _provider = response.data['provider'] ?? 'Gemini';
          _apiKeyController.text = response.data['apiKey'] ?? '';
          _baseUrlController.text = response.data['baseUrl'] ?? 'https://api.openai.com/v1';
          _modelController.text = response.data['model'] ?? 'gpt-4o-mini';
        });
      }
    } catch (e) {
      // Usar valores por defecto en caso de error inicial
    } finally {
      if (mounted) {
        setState(() {
          _isLoading = false;
        });
      }
    }
  }

  Future<void> _saveSettings() async {
    setState(() {
      _isSaving = true;
    });

    try {
      await widget.apiClient.post('/api/ia/settings', data: {
        'provider': _provider,
        'apiKey': _apiKeyController.text.trim(),
        'baseUrl': _baseUrlController.text.trim(),
        'model': _modelController.text.trim(),
      });

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('✨ Ajustes de IA guardados exitosamente'),
            backgroundColor: Color(0xFF10B981),
          ),
        );
        Navigator.of(context).pop(true);
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Error al guardar ajustes: $e'),
            backgroundColor: const Color(0xFFF43F5E),
          ),
        );
      }
    } finally {
      if (mounted) {
        setState(() {
          _isSaving = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Dialog(
      backgroundColor: const Color(0xFF1E1B2E),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      child: Padding(
        padding: const EdgeInsets.all(20.0),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: const Color(0xFF6C5CE7).withOpacity(0.2),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: const Icon(Icons.psychology_outlined, color: Color(0xFFA29BFE)),
                  ),
                  const SizedBox(width: 12),
                  const Expanded(
                    child: Text(
                      'Configuración de IA',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                        color: Colors.white,
                      ),
                    ),
                  ),
                  IconButton(
                    icon: const Icon(Icons.close, color: Colors.white54),
                    onPressed: () => Navigator.of(context).pop(),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              if (_isLoading)
                const Center(
                  child: Padding(
                    padding: EdgeInsets.all(20.0),
                    child: CircularProgressIndicator(color: Color(0xFF6C5CE7)),
                  ),
                )
              else ...[
                const Text(
                  'Proveedor de Inteligencia Artificial',
                  style: TextStyle(color: Colors.white70, fontSize: 13, fontWeight: FontWeight.w600),
                ),
                const SizedBox(height: 8),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  decoration: BoxDecoration(
                    color: Colors.white.withOpacity(0.05),
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(color: Colors.white.withOpacity(0.1)),
                  ),
                  child: DropdownButtonHideUnderline(
                    child: DropdownButton<String>(
                      value: _provider,
                      dropdownColor: const Color(0xFF252139),
                      isExpanded: true,
                      style: const TextStyle(color: Colors.white, fontSize: 14),
                      items: const [
                        DropdownMenuItem(
                          value: 'Gemini',
                          child: Text('Google Gemini (Integrado / Gratuito)'),
                        ),
                        DropdownMenuItem(
                          value: 'OpenAI',
                          child: Text('OpenAI / OpenGO / OpenRouter / DeepSeek'),
                        ),
                      ],
                      onChanged: (val) {
                        if (val != null) {
                          setState(() {
                            _provider = val;
                          });
                        }
                      },
                    ),
                  ),
                ),
                const SizedBox(height: 16),
                if (_provider == 'OpenAI') ...[
                  const Text(
                    'Plantilla Rápida de Servicio',
                    style: TextStyle(color: Colors.white70, fontSize: 13, fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(height: 6),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      ActionChip(
                        avatar: const Icon(Icons.bolt, size: 16, color: Colors.amber),
                        label: const Text('OpenCode / OpenGO', style: TextStyle(color: Colors.white, fontSize: 12)),
                        backgroundColor: const Color(0xFF6C5CE7).withOpacity(0.3),
                        side: const BorderSide(color: Color(0xFF6C5CE7)),
                        onPressed: () {
                          setState(() {
                            _baseUrlController.text = 'https://opencode.ai/zen/go/v1';
                            _modelController.text = 'deepseek-v4-pro';
                          });
                        },
                      ),
                      ActionChip(
                        avatar: const Icon(Icons.api, size: 16, color: Colors.greenAccent),
                        label: const Text('OpenAI Oficial', style: TextStyle(color: Colors.white, fontSize: 12)),
                        backgroundColor: Colors.green.withOpacity(0.2),
                        side: const BorderSide(color: Colors.green),
                        onPressed: () {
                          setState(() {
                            _baseUrlController.text = 'https://api.openai.com/v1';
                            _modelController.text = 'gpt-4o-mini';
                          });
                        },
                      ),
                    ],
                  ),
                  const SizedBox(height: 14),
                  const Text(
                    'API Key (OpenAI / OpenGO / OpenRouter)',
                    style: TextStyle(color: Colors.white70, fontSize: 13, fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(height: 6),
                  TextField(
                    controller: _apiKeyController,
                    obscureText: _obscureKey,
                    style: const TextStyle(color: Colors.white, fontSize: 14),
                    decoration: InputDecoration(
                      hintText: 'sk-...',
                      hintStyle: const TextStyle(color: Colors.white38),
                      filled: true,
                      fillColor: Colors.white.withOpacity(0.05),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: BorderSide(color: Colors.white.withOpacity(0.1)),
                      ),
                      enabledBorder: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: BorderSide(color: Colors.white.withOpacity(0.1)),
                      ),
                      suffixIcon: IconButton(
                        icon: Icon(
                          _obscureKey ? Icons.visibility : Icons.visibility_off,
                          color: Colors.white54,
                        ),
                        onPressed: () => setState(() => _obscureKey = !_obscureKey),
                      ),
                    ),
                  ),
                  const SizedBox(height: 14),
                  const Text(
                    'URL Base de la API (Base URL)',
                    style: TextStyle(color: Colors.white70, fontSize: 13, fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(height: 6),
                  TextField(
                    controller: _baseUrlController,
                    style: const TextStyle(color: Colors.white, fontSize: 14),
                    decoration: InputDecoration(
                      hintText: 'https://api.openai.com/v1',
                      hintStyle: const TextStyle(color: Colors.white38),
                      filled: true,
                      fillColor: Colors.white.withOpacity(0.05),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: BorderSide(color: Colors.white.withOpacity(0.1)),
                      ),
                      enabledBorder: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: BorderSide(color: Colors.white.withOpacity(0.1)),
                      ),
                    ),
                  ),
                  const SizedBox(height: 14),
                  const Text(
                    'Modelo de IA',
                    style: TextStyle(color: Colors.white70, fontSize: 13, fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(height: 6),
                  TextField(
                    controller: _modelController,
                    style: const TextStyle(color: Colors.white, fontSize: 14),
                    decoration: InputDecoration(
                      hintText: 'gpt-4o-mini, gpt-4o, deepseek-chat',
                      hintStyle: const TextStyle(color: Colors.white38),
                      filled: true,
                      fillColor: Colors.white.withOpacity(0.05),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: BorderSide(color: Colors.white.withOpacity(0.1)),
                      ),
                      enabledBorder: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: BorderSide(color: Colors.white.withOpacity(0.1)),
                      ),
                    ),
                  ),
                ] else ...[
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: Colors.blue.withOpacity(0.1),
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: Colors.blue.withOpacity(0.3)),
                    ),
                    child: const Row(
                      children: [
                        Icon(Icons.info_outline, color: Colors.blueAccent, size: 20),
                        SizedBox(width: 10),
                        Expanded(
                          child: Text(
                            'Usando el motor de Google Gemini por defecto. No requiere API Key propia.',
                            style: TextStyle(color: Colors.white70, fontSize: 12),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
                const SizedBox(height: 24),
                SizedBox(
                  width: double.infinity,
                  height: 48,
                  child: ElevatedButton(
                    onPressed: _isSaving ? null : _saveSettings,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF6C5CE7),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                    child: _isSaving
                        ? const CircularProgressIndicator(color: Colors.white, strokeWidth: 2)
                        : const Text(
                            'Guardar Ajustes de IA',
                            style: TextStyle(
                              color: Colors.white,
                              fontSize: 15,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
