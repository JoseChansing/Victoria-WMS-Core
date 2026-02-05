import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:vibration/vibration.dart';
import 'package:audioplayers/audioplayers.dart';
import '../../main.dart';
import '../../features/common/presentation/widgets/security_alert_overlay.dart';
import 'package:flutter/material.dart';

class ApiClient {
  late Dio _dio;
  final FlutterSecureStorage _storage = const FlutterSecureStorage();
  final AudioPlayer _audioPlayer = AudioPlayer();

  ApiClient() {
    _dio = Dio(BaseOptions(
      baseUrl: 'https://api.victoriawms.dev/api/v1',
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 10),
    ));

    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await _storage.read(key: 'vicky_token');
        final tenant = await _storage.read(key: 'vicky_tenant');

        if (token != null) {
          options.headers['Authorization'] = 'Bearer $token';
        }

        if (tenant != null) {
          options.headers['X-Tenant-Id'] = tenant;
          // Si el backend lo espera en el body (como vimos en la Fase 9),
          // podemos inyectarlo aquí si el data es un Map.
          if (options.data is Map) {
            options.data['tenantId'] = tenant;
          }
        }

        return handler.next(options);
      },
      onError: (DioException e, handler) async {
        if (e.response?.statusCode == 403) {
          // FEEDBACK SENSORIAL: Alerta roja masiva (será manejada en la UI)
          // pero aquí disparamos vibración y sonido.
          if (await Vibration.hasVibrator() ?? false) {
             Vibration.vibrate(pattern: [0, 500, 100, 500], intensities: [0, 255, 0, 255]);
          }
          await _audioPlayer.play(AssetSource('sounds/error_buzzer.mp3'));
          
          final context = navigatorKey.currentContext;
          if (context != null) {
            SecurityAlertOverlay.show(context, "El Tenant actual no tiene permiso para procesar este recurso.");
          }
        }
        return handler.next(e);
      },
    ));
  }

  Dio get dio => _dio;
}
