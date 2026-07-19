import 'dart:convert';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import '../../../core/storage/secure_storage.dart';
import '../../workspace/presentation/workspace_screen.dart';

class PairingScreen extends StatefulWidget {
  const PairingScreen({super.key});

  @override
  State<PairingScreen> createState() => _PairingScreenState();
}

class _PairingScreenState extends State<PairingScreen> {
  final _secureStorage = SecureStorage();
  final _manualController = TextEditingController();
  bool _isProcessing = false;

  Future<void> _processPairingData(String rawJson) async {
    if (_isProcessing) return;
    setState(() => _isProcessing = true);

    try {
      final data = jsonDecode(rawJson) as Map<String, dynamic>;
      final lanUrl = (data['LanUrl'] ?? data['lanUrl'] ?? '') as String;
      final tunnelUrl = (data['TunnelUrl'] ?? data['tunnelUrl'] ?? '') as String;
      final pairingToken = (data['PairingToken'] ?? data['pairingToken'] ?? '') as String;

      if (lanUrl.isEmpty || tunnelUrl.isEmpty || pairingToken.isEmpty) {
        throw Exception('El JSON no contiene todos los campos requeridos (lanUrl, tunnelUrl, pairingToken).');
      }

      // Intentar vincular contra el endpoint de confirmación.
      // Probamos con la LAN primero, si falla con el túnel.
      String targetUrl = lanUrl;
      final dio = Dio(BaseOptions(connectTimeout: const Duration(seconds: 3)));
      
      Response? response;
      try {
        response = await dio.post(
          '$lanUrl/api/pairing/confirm',
          data: {
            'PairingToken': pairingToken,
            'DeviceName': 'Xiaomi Mi 11 (Android)',
            'DeviceOs': 'Android 13'
          },
        );
      } catch (_) {
        // Fallback al túnel público
        response = await dio.post(
          '$tunnelUrl/api/pairing/confirm',
          data: {
            'PairingToken': pairingToken,
            'DeviceName': 'Xiaomi Mi 11 (Android) - Remoto',
            'DeviceOs': 'Android 13'
          },
        );
        targetUrl = tunnelUrl;
      }

      if (response.statusCode == 200) {
        final respData = response.data as Map<String, dynamic>;
        final token = (respData['Token'] ?? respData['token'] ?? '') as String;
        final deviceId = (respData['DeviceId'] ?? respData['deviceId'] ?? '') as String;

        // Guardar token y perfil del PC
        await _secureStorage.saveToken(token);
        await _secureStorage.addLinkedPc({
          'Name': 'Computadora Principal',
          'LanUrl': lanUrl,
          'TunnelUrl': tunnelUrl,
          'PairingToken': pairingToken,
          'DeviceId': deviceId,
          'LastConnectedUrl': targetUrl
        });

        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('¡PC Vinculada Exitosamente!'), backgroundColor: Colors.green),
          );
          Navigator.pushReplacement(
            context,
            MaterialPageRoute(builder: (context) => const WorkspaceScreen()),
          );
        }
      } else {
        throw Exception('El servidor rechazó el emparejamiento.');
      }
    } catch (e) {
      if (mounted) {
        showDialog(
          context: context,
          builder: (context) => AlertDialog(
            title: const Text('Error de Emparejamiento'),
            content: Text('No se pudo establecer conexión con el agente: $e'),
            actions: [
              TextButton(onPressed: () => Navigator.pop(context), child: const Text('Entendido')),
            ],
          ),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _isProcessing = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Vincular Agente'),
      ),
      body: SingleChildScrollView(
        child: Padding(
          padding: const EdgeInsets.all(24.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                'Escanea el código QR de DreamOS en tu PC para comenzar',
                style: GoogleFonts.outfit(fontSize: 16, color: const Color(0xFFA0A0C0)),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 20),
              Container(
                height: 280,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(16),
                  color: Colors.black12,
                  border: Border.all(color: const Color(0xFF313244), width: 2),
                ),
                child: ClipRRect(
                  borderRadius: BorderRadius.circular(14),
                  child: _isProcessing
                      ? const Center(child: CircularProgressIndicator())
                      : MobileScanner(
                          onDetect: (capture) {
                            final List<Barcode> barcodes = capture.barcodes;
                            for (final barcode in barcodes) {
                              if (barcode.rawValue != null) {
                                _processPairingData(barcode.rawValue!);
                                break;
                              }
                            }
                          },
                        ),
                ),
              ),
              const SizedBox(height: 24),
              Row(
                children: [
                  const Expanded(child: Divider(color: Color(0xFF313244))),
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    child: Text('O INGRESA MANUALMENTE', style: GoogleFonts.outfit(fontSize: 12, color: const Color(0xFF606080))),
                  ),
                  const Expanded(child: Divider(color: Color(0xFF313244))),
                ],
              ),
              const SizedBox(height: 20),
              TextField(
                controller: _manualController,
                maxLines: 4,
                decoration: const InputDecoration(
                  hintText: 'Pega el JSON de emparejamiento generado por el agente aquí...',
                ),
              ),
              const SizedBox(height: 16),
              ElevatedButton(
                onPressed: _isProcessing
                    ? null
                    : () {
                        if (_manualController.text.isNotEmpty) {
                          _processPairingData(_manualController.text);
                        }
                      },
                child: const Text('Vincular Manualmente'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
