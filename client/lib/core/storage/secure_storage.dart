import 'dart:convert';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class SecureStorage {
  final _storage = const FlutterSecureStorage();

  static const _tokenKey = 'auth_token';
  static const _activePcKey = 'active_pc_profile';
  static const _pcsListKey = 'linked_pcs_list';

  Future<void> saveToken(String token) async {
    await _storage.write(key: _tokenKey, value: token);
  }

  Future<String?> getToken() async {
    return await _storage.read(key: _tokenKey);
  }

  Future<void> clearToken() async {
    await _storage.delete(key: _tokenKey);
  }

  Future<void> saveActivePc(Map<String, dynamic> pc) async {
    await _storage.write(key: _activePcKey, value: jsonEncode(pc));
  }

  Future<Map<String, dynamic>?> getActivePc() async {
    final data = await _storage.read(key: _activePcKey);
    if (data == null) return null;
    return jsonDecode(data) as Map<String, dynamic>;
  }

  Future<void> clearActivePc() async {
    await _storage.delete(key: _activePcKey);
  }

  Future<List<Map<String, dynamic>>> getLinkedPcs() async {
    final data = await _storage.read(key: _pcsListKey);
    if (data == null) return [];
    final list = jsonDecode(data) as List;
    return list.map((e) => e as Map<String, dynamic>).toList();
  }

  Future<void> addLinkedPc(Map<String, dynamic> pc) async {
    final list = await getLinkedPcs();
    // Eliminar si ya existe un perfil con el mismo DeviceId o Nombre para evitar duplicados obsoletos
    list.removeWhere((item) => item['DeviceId'] == pc['DeviceId'] || item['Name'] == pc['Name']);
    list.add(pc);
    await _storage.write(key: _pcsListKey, value: jsonEncode(list));
  }

  Future<void> removeLinkedPc(String deviceId) async {
    final list = await getLinkedPcs();
    list.removeWhere((item) => item['DeviceId'] == deviceId);
    await _storage.write(key: _pcsListKey, value: jsonEncode(list));
  }

  Future<void> clearAll() async {
    await _storage.delete(key: _tokenKey);
    await _storage.delete(key: _activePcKey);
    await _storage.delete(key: _pcsListKey);
  }
}
