# 🚀 DreamOS Dev — Remote AI Coding & Workspace Controller

**DreamOS Dev** es un ecosistema de desarrollo remoto asistido por Inteligencia Artificial que te permite controlar, editar y ejecutar proyectos de tu PC desde un dispositivo móvil Android.

Conecta de forma segura tu celular a tu estación de trabajo mediante **Tráfico Local Directo (LAN)** o **Túneles de Cloudflare**, interactúa con un **Agente de IA Autónomo (Gemini 2.5 / Flash-latest)** que comprende la arquitectura completa de tu código, revisa **Diffs Visuales** antes de aplicar cambios y ejecuta comandos del sistema en tiempo real.

---

## 🏗️ Arquitectura del Proyecto

El sistema está dividido en dos componentes principales:

```
                  ┌─────────────────────────────────────────┐
                  │             DreamOS Client              │
                  │   (Flutter / Android / Cross-Platform)  │
                  └────────────────────┬────────────────────┘
                                       │
                         [ LAN / Cloudflare Tunnel ]
                                       │
                  ┌────────────────────▼────────────────────┐
                  │              DreamOS Agent              │
                  │    (C# .NET 8 Web API / LiteDB / IA)    │
                  └─────────────────────────────────────────┘
```

* **`agent/` (Servidor / PC)**: Web API en **.NET 8** integrada con **LiteDB** para almacenamiento embebido sin dependencias, **Cloudflare Quick Tunnels** para acceso remoto sin abrir puertos y el proveedor **Gemini API** para análisis de proyectos.
* **`client/` (Cliente / Móvil)**: Aplicación **Flutter 3.x** multiplataforma construida con diseño responsivo, temas oscuros dinámicos, editor de código con resaltado sintáctico y visualizador de Diffs estructurados.

---

## 📊 Estado de Desarrollo del Proyecto

### 🌟 Funcionalidades Desarrolladas (Completadas)

| Hito / Módulo | Estado | Descripción |
| :--- | :---: | :--- |
| **Hito 1: Conectividad Híbrida Inteligente** | ✅ Completado | Emparejamiento por QR o Token. Conexión automática por **LAN** en la misma red Wi-Fi o fallback transparente mediante **Cloudflare Tunnel** fuera de casa. |
| **Persistencia de Sesiones & Casing Normalizado** | ✅ Completado | Almacenamiento seguro en Android sin duplicados, soporte de perfiles por `DeviceId` y tolerancia a serialización PascalCase/camelCase. |
| **Hito 2: Explorador & Editor Remoto** | ✅ Completado | Exploración recursiva de workspaces en disco duro de la PC, visualización de código, edición multilínea y guardado remoto con copias de seguridad de respaldo (`.bak`) automáticas. |
| **Hito 3: Agente de IA & Diffs Visuales** | ✅ Completado | Integración con el modelo **Gemini 2.5 / Flash-latest**. Carga contextual inteligente del workspace, generación de planes de modificación estructurados en JSON y pantalla de revisión visual de cambios (Diffs en verde) previo a la aplicación. |
| **Preservación de Código Original por IA** | ✅ Completado | El backend inyecta automáticamente los archivos objetivos al prompt para que la IA edite manteniendo el código intacto. |

---

### 🔮 Funcionalidades por Desarrollar (Hoja de Ruta)

| Hito / Módulo | Estado | Descripción |
| :--- | :---: | :--- |
| **Hito 4: Consola en Tiempo Real & Compilación** | ⏳ Próximamente | Transmisión de logs de consola en tiempo real vía **SignalR**, activación de compilaciones (`flutter build apk`, `dotnet build`) y seguimiento con barra de progreso. |
| **Ejecución de Terminal Interactiva** | ⏳ Próximamente | Terminal móvil interactiva para ejecutar comandos en PowerShell/Bash directamente en la PC desde el celular. |
| **Módulo de Memoria y Aprendizaje del Agente** | ⏳ Próximamente | Persistencia de notas de arquitectura y contexto aprendidos por la IA a lo largo del tiempo en la base de datos LiteDB. |
| **Respaldo en la Nube (Google Drive API)** | ⏳ Próximamente | Copias de seguridad automáticas y sincronización de workspaces en la cuenta personal de Google Drive. |
| **Ecosistema de Plugins (Laravel, Flutter, Node)** | ⏳ Próximamente | Asistentes especializados por tecnología para autodetectar patrones (BLoC, Clean Architecture, MVC) y refactorizar código específico. |

---

## 🛠️ Tecnologías Utilizadas

### Agent (Backend / PC)
- **Framework**: C# .NET 8.0 Web API
- **Base de datos**: LiteDB (Embedded NoSQL)
- **Túneles de Red**: Cloudflare Tunnel CLI (`cloudflared`)
- **Seguridad**: Autenticación por JWT & Tokens de emparejamiento aleatorios
- **IA Provider**: Google Gemini API (`gemini-flash-latest`)
- **Realtime**: Microsoft SignalR Core

### Client (Frontend / Móvil)
- **Framework**: Flutter 3.x / Dart
- **UI & Layout**: Custom Dark Theme (Google Fonts: Outfit, Fira Code)
- **Editor de Código**: `code_text_field` con resaltado Monokai Sublime
- **Cliente HTTP**: Dio con interceptores JWT y reconexión dinámica
- **Seguridad**: `flutter_secure_storage` con limpiador automático de perfiles obsoletos

---

## 🚀 Guía de Inicio Rápido

### 1. Iniciar el Agente en la PC
```bash
cd agent
dotnet run --project DreamOS.Api
```
*Al iniciar, la consola mostrará la dirección LAN, la URL del Túnel de Cloudflare y el Token de Emparejamiento.*

### 2. Configurar la API Key de Gemini
Asegúrate de que la API Key de Google AI Studio (`AIzaSy...` o `AQ...`) esté configurada en las configuraciones del agente o en la base de datos `dreamos.db`.

### 3. Iniciar el Cliente en Android
```bash
cd client
flutter run
```
*Ingresa los datos de emparejamiento mostrados por el agente para conectar.*

---

## 📜 Licencia

Desarrollado como parte del proyecto **DreamOS**. Licencia privada para fines de desarrollo y prototipado.
