# KyvoraPvp

Launcher de escritorio para Windows enfocado en Minecraft Java. Instala Forge para iniciar el HUD cliente de Kyvora en 1.8.9 y 1.16.5. El catálogo para instalar mods externos queda fuera por ahora. No incluye autenticación offline, cuentas prestadas ni archivos de juego redistribuidos.

## Requisitos

- Windows 10/11 de 64 bits
- .NET 8 SDK y JDK 8 x64 para compilar
- Una cuenta Microsoft que tenga una licencia de Minecraft: Java Edition
- Un Client ID de aplicación de escritorio registrado para el acceso Microsoft

## Configurar el acceso Microsoft

El launcher usa MSAL y el flujo oficial de Microsoft. No solicites ni pegues tu contraseña en KyvoraPvp: el inicio de sesión se realiza en la ventana de autenticación de Microsoft. El Client ID es un identificador público de la aplicación, no una contraseña ni un client secret.

1. Entra en [Microsoft Entra admin center](https://entra.microsoft.com/) y abre el registro de KyvoraPvp.
2. Configura los tipos de cuenta para incluir cuentas personales de Microsoft.
3. Ve a **Autenticación → Agregar una plataforma → Aplicaciones móviles y de escritorio** y agrega `http://localhost` como URI de redirección. No uses la plataforma **Web**.
4. En **Autenticación → Configuración avanzada**, habilita también **Permitir flujos de cliente público** y guarda los cambios. Esta opción por sí sola no registra la plataforma móvil/escritorio.
5. Copia el **Application (client) ID** en `appsettings.json`, en `MicrosoftClientId`.
6. No crees ni distribuyas un client secret para esta aplicación de escritorio.

El identificador se lee desde `appsettings.json` junto al ejecutable. El token y la sesión se gestionan por MSAL/CmlLib; KyvoraPvp no guarda contraseñas.

Iniciar sesión en el portal de Azure/Microsoft Entra solo configura la aplicación; no conecta tu perfil de juego. Para conectar Minecraft, abre KyvoraClient y pulsa **Vincular cuenta Microsoft**. El launcher muestra un código de un solo uso y abre la página de verificación oficial de Microsoft. Escribe el código ahí y deja el launcher abierto: esperará la respuesta y mostrará **Cuenta premium verificada** y tu nombre al terminar. No compartas el código de un solo uso con otras personas. El flujo de código de dispositivo no requiere URI de redirección; la próxima vez KyvoraClient intentará restaurar la sesión guardada y también puedes usar **Cambiar cuenta Microsoft**.

Si Microsoft presenta `AADSTS70002` con “The client application must be marked as 'mobile'”, agrega la plataforma **Aplicaciones móviles y de escritorio** y la URI `http://localhost` como se describe arriba. El interruptor de flujos de cliente público es una configuración aparte: verifica que también esté en **Sí**. Para cuentas Xbox personales, el tipo de cuenta registrada también debe incluir cuentas personales de Microsoft. Guarda los cambios, cierra la ventana de autenticación y vuelve a pulsar **Vincular cuenta Microsoft**. Este error sucede en Microsoft Entra antes de que Minecraft valide la cuenta, así que todavía no indica un problema con la licencia de Minecraft.

## Ejecutar

Desde esta carpeta:

```powershell
dotnet restore
dotnet run
```

Para publicar una versión autocontenida para Windows x64:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

Para generar el archivo que compartirás, ejecuta `Build-Installer.ps1`. Compila los HUD Kyvora para 1.8.9 y 1.16.5, publica el launcher, incluye un runtime Temurin Java 8 x64 y crea `dist/KyvoraInstaller.exe`, un instalador gráfico autocontenido. Al abrirlo, instala KyvoraClient en tu perfil de Windows, crea accesos directos en el menú Inicio y el escritorio, y ofrece abrir el launcher; no necesita permisos de administrador. Conserva el `appsettings.json` ya instalado si actualizas la aplicación.

El instalador empaqueta los archivos publicados dentro de un ejecutable, incluido Java 8 x64 y los HUD cliente de Kyvora. Al pulsar Jugar, Kyvora prepara Forge para la versión seleccionada; el HUD del cliente se activa en Minecraft 1.8.9 y 1.16.5. El catálogo para agregar mods de terceros no está incluido.

El launcher y el instalador usan el logo Kyvora Minecraft Java y el fondo de galaxia proporcionados para este proyecto. Las imágenes se empaquetan en la aplicación y no dependen de una conexión a Internet.

El Client ID se incluye en `appsettings.json` dentro del launcher. La primera partida necesita Internet para descargar los archivos oficiales de Minecraft y Forge.

## HUD de Kyvora

En el juego, pulsa **Shift derecho** para abrir el menú del HUD y activar o desactivar:

- Keystrokes (WASD, salto y botones del mouse)
- CPS izquierdo y derecho
- FPS
- Coordenadas
- Ping del servidor
- Insignia rosa K junto al nombre sobre tu personaje en tercera persona

Pulsa **Mover HUD** en el menú y arrastra los paneles de Keystrokes/CPS o estadísticas para colocarlos. Sus posiciones y las opciones visuales se guardan en `config/kyvora-hud.cfg`.

Para los hitboxes de depuración integrados en Minecraft, usa **F3+B**.

La memoria recomendada se ajusta a la versión de Minecraft y al RAM disponible: aproximadamente 1 GB para versiones antiguas, 1.5 GB para 1.13–1.14 y 2 GB para 1.15–1.16.5, con un límite para dejar recursos al sistema. El launcher incluye HUD Kyvora en 1.8.9 y 1.16.5; en las demás versiones inicia Forge sin el HUD. Revisa las reglas del servidor antes de usar modificaciones cliente, incluido cualquier HUD.

KyvoraPvp es un proyecto independiente y no está afiliado con Mojang Studios ni Microsoft.
