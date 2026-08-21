# Multi SMB Server

Servidor SMBv1 (NT LM 0.12) con interfaz gráfica en **.NET 8 / WPF**, pensado para servir juegos y recursos a consolas retro por red: **PlayStation 2** (Open PS2 Loader), **Wii** y **GameCube** (USB Loader GX, WiiFlow, Nintendont).

## Requisitos

- Windows (el acceso al disco usa las APIs nativas NT).
- .NET 8 Runtime (o usa la compilación autocontenida, ver [Generar el .exe](#generar-el-exe)).
- Las consolas (PS2, Wii, GameCube) en la misma red que la PC.

## Uso rápido

1. Ejecuta la aplicación.
2. En **Shares** agrega una o más carpetas compartidas:
   - **PS2**: la raíz con las carpetas de OPL (`DVD/`, `CD/`, `ART/`, `CFG/`, `VMC/`).
   - **Wii/GameCube** (opcional): la raíz con `wbfs/` (Wii) y/o `games/` (GameCube).
3. Ajusta el **Nombre** de cada share (p. ej. `PS2SMB`, `WII`), el **Puerto**, **Usuario** y **Contraseña**.
4. Pulsa **Iniciar servidor** y configura OPL tal como se indica abajo.

> El **Nombre del share** y el **Puerto** deben ser exactamente los mismos que configures en OPL (o en el loader de Wii).

La configuración se **guarda automáticamente** (al iniciar el servidor, al cerrar o con el botón **Guardar configuración**), así que la próxima vez se carga sola.

### Múltiples shares

Puedes exponer varias carpetas a la vez (PS2, Wii, GameCube...) con el botón **+ Agregar share**. Cada share tiene su propio nombre y carpeta. Todas las consolas se conectan al mismo servidor y puerto; el nombre del share es lo que las distingue.

## Bandeja del sistema y arranque silencioso

- **Minimizar** la ventana la oculta en la bandeja del sistema (sigue sirviendo en segundo plano).
- **Cerrar (X)** muestra un diálogo: salir de la aplicación, minimizar a la bandeja o cancelar.
- Doble clic en el icono de la bandeja (o menú **Abrir**) la restaura; **Salir** la cierra de verdad.
- Argumentos de línea de comandos:
  - `/START` — inicia el servidor automáticamente al abrir (usa la configuración guardada).
  - `/SILENT` (o `/HIDE`, `/MINIMIZED`) — arranca oculta en la bandeja.

Ejemplos:

```powershell
# Abrir y arrancar el servidor automáticamente
MultiSmbServer.exe /START

# Arrancar en segundo plano (bandeja) y con el servidor activo
MultiSmbServer.exe /START /SILENT
```

## Estructura de carpetas esperada

La carpeta del share de PS2 debe contener las subcarpetas estándar de OPL:

```
Carpeta PS2/
├── DVD/   -> juegos en formato .iso (juegos DVD)
├── CD/    -> juegos en formato .iso (juegos CD)
├── ART/   -> carátulas (descargadas por OPL)
├── CFG/   -> configuraciones por juego (las escribe OPL)
└── VMC/   -> Memory Cards virtuales (las escribe OPL)
```

Para Wii/GameCube (USB Loader GX, WiiFlow, Nintendont), la carpeta del share típicamente contiene:

```
Carpeta Wii/
├── wbfs/    -> juegos de Wii (.wbfs)
└── games/   -> juegos de GameCube (una subcarpeta por juego)
```

## Configuración en Open PS2 Loader (OPL)

Entra en OPL → **Settings** → **Network Settings** (configura antes la red con IP fija o DHCP) y rellena:

| Campo OPL | Valor |
|---|---|
| **SMB Server** | La IP de la PC donde corre el servidor (p. ej. `192.168.1.10`). |
| **SMB Share Name** | El mismo nombre del share (p. ej. `PS2SMB`). |
| **Share Port** | El mismo puerto que pusiste en la app (p. ej. `445` o `1445`). |
| **SMB Username** | El usuario configurado en la app (p. ej. `ps2`). |
| **SMB Password** | La contraseña configurada en la app (p. ej. `opl`), o déjala vacía. |

Pasos finales en OPL:

1. Vuelve al menú y selecciona el modo **Network** (SMB).
2. Pulsa **Refresh / Scan** para que OPL enumere los juegos de `DVD/` y `CD/`.
3. Inicia el juego: OPL lee la ISO por SMB en bloques de hasta ~60 KB.

### Notas de autenticación

- Si en OPL dejas la contraseña vacía, la sesión entra por **Guest**. En la app debe estar marcado **Permitir acceso Guest/Anónimo** (lo está por defecto).
- Si configuras usuario y contraseña, deben coincidir exactamente entre la app y OPL.
- OPL habla SMB1 clásico (no extended security) y usa NTLMv1 sobre el challenge del NEGOTIATE; el servidor lo maneja automáticamente.

## Generar el .exe

Desde la carpeta del proyecto (la que contiene `MultiSmbServer.csproj`):

```powershell
# Exe dependiente del framework (requiere .NET 8 Runtime en la PC de destino)
dotnet publish -c Release -r win-x64 --self-contained false -o publish

# Exe autocontenido en un solo archivo (no requiere .NET instalado, ~147 MB)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-standalone
```

El ejecutable queda en `publish\MultiSmbServer.exe` (dependiente del framework) o `publish-standalone\MultiSmbServer.exe` (autocontenido).

## Seguridad

SMBv1 es un protocolo antiguo y sin cifrado (es lo que exige OPL/PS2 y el homebrew de Wii, no se puede evitar). Para no exponerte como al servicio nativo de Windows, la app aplica estas medidas:

- **Solo LAN (activado por defecto)**: rechaza conexiones desde IPs públicas/Internet (solo acepta `192.168.x.x`, `10.x.x.x`, `172.16–31.x.x`, loopback e IPv6 link-local/ULA). Desmárcalo solo si sabes lo que haces.
- **Puerto no estándar**: usa un puerto alto (p. ej. `1445`) en lugar del `445` para reducir escaneos automáticos.
- **No hagas port-forwarding** del puerto en tu router: el servidor es solo para tu red local.
- **Credenciales**: define usuario/contraseña (o usa Guest). Recuerda que la autenticación es NTLMv1, débil por diseño de SMB1.

A diferencia del `LanmanServer` nativo de Windows, esta app no corre con privilegios del sistema ni escucha en `0.0.0.0` con el stack completo de Windows expuesto.

## Notas técnicas

- El servidor usa **SMBLibrary** (1.5.0) con SMB1 únicamente (`enableSMB1=true`), lo que requieren OPL y el homebrew de Wii.
- Soporta **múltiples shares** simultáneos (PS2, Wii/GameCube, etc.) en el mismo servidor y puerto.
- Puertos soportados: `445` (Direct TCP), `139` (NetBIOS over TCP) o **cualquier puerto personalizado** (en ese caso el servidor escucha Direct TCP en ese puerto).
- El acceso al sistema de archivos se hace con `NTDirectoryFileSystem` (SMBLibrary.Win32).
- Los logs (conexiones, autenticación, tree connect y lecturas) se muestran en la consola embebida con timestamp.
- Los logs de archivo se limitan para no saturar la UI durante la carga de juegos; las excepciones no controladas se escriben en `%APPDATA%\MultiSmbServer\crash.log`.

## Apoyar el proyecto

Si te resulta útil, podés colaborar con un cafecito:

- [Ko-fi](https://ko-fi.com/elanvzone)
- [Cafecito](https://cafecito.app/elanvzone)
