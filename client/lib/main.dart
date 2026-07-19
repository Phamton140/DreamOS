import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'core/theme/app_theme.dart';
import 'core/storage/secure_storage.dart';
import 'features/pairing/presentation/pairing_screen.dart';
import 'features/workspace/presentation/workspace_screen.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  
  // Habilitar dibujo de borde a borde (Edge-to-Edge) en Android
  SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
  
  // Verificar si hay perfiles de PC vinculados
  final secureStorage = SecureStorage();
  var pcs = await secureStorage.getLinkedPcs();
  
  // Limpieza automática de perfiles corruptos generados por el bug de casing anterior
  if (pcs.any((p) => p['DeviceId'] == null || p['DeviceId'] == '')) {
    await secureStorage.clearAll();
    pcs = [];
  }
  
  runApp(MyApp(hasLinkedPc: pcs.isNotEmpty));
}

class MyApp extends StatelessWidget {
  final bool hasLinkedPc;
  
  const MyApp({super.key, required this.hasLinkedPc});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'DreamOS Dev',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.darkTheme,
      home: hasLinkedPc ? const WorkspaceScreen() : const PairingScreen(),
    );
  }
}
