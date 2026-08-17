# CineSched: plan de migración a Uno Platform y .NET 10

## 1. Propósito y autoridad de los documentos

Este documento define el orden de ejecución de la migración de CineSched D.G.D v4.5.0 desde SwiftUI/AppKit a una aplicación de escritorio multiplataforma basada en Uno Platform y .NET 10.

Los tres documentos de la migración se complementan y tienen esta autoridad:

1. [SPEC.md](SPEC.md) define el comportamiento observable que debe conservarse.
2. [ARCHITECTURE.md](ARCHITECTURE.md) define los límites, dependencias y contratos técnicos.
3. Este documento define la secuencia de entrega, sus gates y la definición de terminado.

Si una implementación contradice `SPEC.md`, la implementación es incorrecta. Si una solución satisface la especificación pero rompe las reglas de dependencia de `ARCHITECTURE.md`, debe refactorizarse antes de completar la fase.

## 2. Estado inicial

### Aplicación existente

- Versión de referencia: CineSched D.G.D v4.5.0, commit etiquetado `v4.5.0`.
- Plataforma: macOS 13 o posterior.
- Tecnologías: Swift 5, SwiftUI, AppKit, CoreText, PDFKit y APIs de sandbox de macOS.
- Persistencia: documentos JSON manuales y autosave serializado en `UserDefaults`.
- Arquitectura actual: estado principal concentrado en `ContentView`, extensiones de operaciones y archivos Swift separados por vista, parser o exportador.
- Pruebas automatizadas: no existen en el repositorio actual.
- Proyecto Xcode: el README lo menciona, pero no está incluido en el repositorio; el código Swift y la aplicación compilada son la referencia disponible.

### Restricciones relevantes

- WinUI 3 y Windows App SDK solo se ejecutan en Windows. En Linux se utilizará la proyección de APIs WinUI ofrecida por Uno Platform.
- Linux y macOS utilizarán el renderer Skia. Para mantener la misma experiencia, Windows también usará el target Skia Desktop en esta migración.
- La interfaz será Fluent y conservará flujos e información, pero no intentará reproducir SwiftUI píxel por píxel.
- El código Swift permanecerá en el repositorio como referencia y alternativa funcional durante toda la migración.

## 3. Objetivos

1. Ejecutar una única base C#/XAML en Linux, Windows y macOS, en x64 y ARM64.
2. Conservar todas las funciones documentadas en README y CHANGELOG.
3. Abrir documentos creados por Swift y producir documentos que Swift pueda volver a abrir sin perder datos.
4. Separar por completo la lógica de negocio de la interfaz mediante `CineSched.Core` y `CineSched.App`.
5. Organizar Core por vertical slices, con casos de uso aislados y pruebas.
6. Producir los seis reportes PDF de forma vectorial y determinista en las tres plataformas.
7. Generar artefactos autocontenidos mediante CI; AppImage y `tar.gz` serán las entregas Linux principales.

## 4. Fuera de alcance

- Firma de código, notarización de Apple o publicación en Microsoft Store, App Store o Flathub.
- Sincronización en nube, colaboración multiusuario, autenticación o telemetría.
- Base de datos, servidor web o cambio del modelo local de documentos.
- Soporte móvil, WebAssembly o interfaz táctil especializada.
- Conversión del código Swift en una librería compartida.
- Nuevas funciones de producción que no estén en la versión v4.5.0.

## 5. Stack fijado

| Componente | Decisión |
|---|---|
| Runtime y SDK | .NET SDK 10.0.400, fijado en `global.json` |
| UI | Uno SDK 6.6.42, XAML compatible con WinUI y Skia Desktop |
| Patrón de presentación | MVVM con CommunityToolkit.Mvvm 8.4.2 |
| PDF | PDFsharp Core 6.2.4 con fuentes Noto Sans embebidas |
| JSON | `System.Text.Json` con convertidores propios de compatibilidad Swift |
| XML/FDX | `System.Xml`/`System.Xml.Linq`, sin dependencia de Final Draft |
| Highland | `System.IO.Compression.ZipArchive` |
| Pruebas | xUnit sobre .NET 10 |
| Solución | `CineSched.slnx` con dos proyectos de producción |
| CI | GitHub Actions en Windows, Ubuntu y macOS |

Las versiones se administrarán centralmente en `Directory.Packages.props`. Una actualización posterior requerirá un cambio explícito y la ejecución completa de pruebas.

## 6. Matriz de migración funcional

| Capacidad actual | Referencia Swift principal | Vertical slice destino |
|---|---|---|
| Nuevo, abrir, guardar, Save As, autosave y recientes | `ProjectStore.swift`, `RecentFilesStore.swift` | Projects |
| Esquema JSON y modelos | `Models.swift` | Projects / Domain |
| Escenas, parsing y breakdown | `Models.swift`, `Parsers.swift`, vistas de escena | Scenes |
| Calendario, rango y drag-and-drop | `CalendarView.swift`, `ContentView.swift` | Scheduling |
| Boneyard, búsqueda y ordenamiento | `ContentView.swift` | Scenes / Scheduling |
| Stripboard y cascada temporal | `StripboardView.swift` | Stripboard |
| Banners, comidas y eventos | Vistas de input y `Models.swift` | Stripboard |
| Cast, crew, locations y disponibilidad | `ProductionSetupSheet.swift` | Production |
| Conflictos y blackout days | `ConflictScanner.swift`, `CalendarView.swift` | Conflicts / Scheduling |
| Schedule lock | `ScheduleLockScanner.swift` | ScheduleLock |
| Call sheets | `CallSheetEditor.swift` | CallSheets |
| FDX/XML | `FinalDraftParser.swift` | ScriptImport |
| Fountain/Markdown/Highland | Parsers e importadores Fountain/Highland | ScriptImport |
| Seis reportes PDF | Exportadores PDF Swift | Reports |
| Español/inglés y temas | `Localization.swift`, `ThemeManager.swift` | Settings / App Resources |
| Menús, atajos y ventanas | `CineSchedApp.swift` | App / Shell |

## 7. Estrategia de branch y commits

- Branch de trabajo: `feature/uno-dotnet10-cross-platform`, creado desde `main` en v4.5.0.
- No se moverán ni eliminarán los archivos Swift.
- `.codegraph/`, outputs, paquetes restaurados y artefactos locales permanecerán fuera de Git.
- Cada fase terminará en uno o más commits autocontenidos que compilen y superen su gate.
- No se mezclará una actualización funcional del producto Swift con la migración.

## 8. Fases y gates

### Fase 0: contrato y fixtures de referencia

**Trabajo**

- Mantener estos tres documentos como contrato de implementación.
- Inventariar todas las propiedades Codable, valores raw de enums, defaults y reglas legacy.
- Crear fixtures versionados: proyecto vacío, proyecto completo v4.5.0, formato legacy, cast como string y datos con campos opcionales ausentes.
- Añadir muestras mínimas FDX, Fountain y Highland que no contengan material con copyright.
- Definir proyectos representativos para DOOD, conflictos, lock, call sheets y PDFs.

**Gate**

- Cada feature del README y CHANGELOG aparece en `SPEC.md`.
- Los fixtures cubren todos los tipos persistidos y pueden inspeccionarse manualmente como JSON válido.
- No se ha alterado el comportamiento Swift.

### Fase 1: solución, dependencias y CI inicial

**Trabajo**

- Crear `CineSched.slnx`, `global.json`, configuración común y administración central de paquetes.
- Crear `CineSched.Core`, `CineSched.App`, `CineSched.Core.Tests` y `CineSched.IntegrationTests`.
- Generar una shell Uno mínima con Skia Desktop y composición de dependencias.
- Configurar restore, build y test en GitHub Actions para los tres sistemas operativos.

**Gate**

- La solución restaura y compila sin warnings en los runners soportados.
- Core no contiene referencias a Uno, WinUI ni APIs de plataforma.
- La aplicación muestra una ventana vacía en Linux, Windows y macOS.

### Fase 2: dominio, parsers y compatibilidad JSON

**Trabajo**

- Portar entidades, enums, valores por defecto y propiedades derivadas.
- Implementar parsing de octavos, duración, números de escena, INT/EXT y decorado.
- Implementar `SwiftCompatibleProjectCodec` y convertidores de fechas, enums y campos legacy.
- Crear `ProjectSession`, dirty state y undo/redo de 30 snapshots.
- Implementar New, Open, Save y Save As sobre streams.

**Gate**

- Todas las pruebas `@compatibility` y `@core` de Projects y Scenes pasan.
- Los fixtures Swift actuales y legacy se cargan sin pérdida.
- El JSON generado conserva nombres, enum values, UUID y fecha compatible con el decoder Swift.

### Fase 3: escenas, calendario y scheduling

**Trabajo**

- Implementar CRUD, duplicado, búsqueda, Boneyard y los cinco ordenamientos.
- Implementar calendario lunes-domingo, rango, shift schedule y estadísticas.
- Implementar selección individual, Cmd/Ctrl, Shift, movimiento, reordenamiento, Send to Day, Remove from Day y swap de días.
- Implementar blackout days, días recurrentes y undo/redo estructural.

**Gate**

- Todas las reglas de Scheduling son reproducibles sin UI mediante pruebas Core.
- No se pueden duplicar ni perder IDs durante movimientos o undo/redo.
- Los eventos de calendario no entran en Boneyard ni afectan estadísticas de escenas.

### Fase 4: stripboard y producción

**Trabajo**

- Implementar stripboard, banners, comidas, eventos y cascada temporal.
- Conservar anclas de hora fija y editores de duración.
- Implementar Production Setup, cast, crew, defaults diarios, locations y disponibilidad.
- Implementar conflictos, blackout flags, schedule lock y su reporte de diferencias.

**Gate**

- La cascada produce tiempos deterministas para escenas, banners, comidas y wrap.
- Los conflictos se recalculan al modificar schedule o disponibilidad.
- Lock no bloquea edición; solo informa días agregados o removidos.

### Fase 5: call sheets e importadores

**Trabajo**

- Implementar todos los campos de call sheet, sincronización de escenas, cast, crew y locations.
- Portar FDX/XML, Fountain, Markdown, Screenplain Markdown y Highland.
- Mantener reglas de extracción de escenas, paginación, personajes, títulos y defaults.
- Conservar la confirmación antes de importar Fountain/Highland en un proyecto con contenido.

**Gate**

- Los tres pipelines de importación producen resultados normalizados equivalentes.
- Los cambios de actor, crew o location se reflejan en call sheets existentes mediante IDs/referencias estables.
- Archivos inválidos producen errores presentables sin modificar el proyecto.

### Fase 6: reportes PDF

**Trabajo**

- Crear un font resolver con Noto Sans Regular, Bold e Italic y registrar sus licencias.
- Portar calendario, strip schedule, One-Line Shooting Schedule, DOOD, breakdown y call sheet.
- Conservar orientación US Letter, márgenes, paginación, códigos y localización.
- Separar cálculo/layout de la escritura PDF para permitir pruebas deterministas.

**Gate**

- `ReportGenerationPipelineTests` genera los seis documentos desde el fixture completo.
- Cada documento abre correctamente, tiene las páginas/orientación esperadas y contiene sus textos clave.
- No depende de fuentes instaladas en el sistema ni de AppKit/PDFKit.

### Fase 7: interfaz Uno Fluent

**Trabajo**

- Construir shell, MenuBar, CommandBar, sidebar, Boneyard y selector Calendar/Stripboard.
- Conectar view models delgados a handlers Core.
- Implementar diálogos de escena, producción, call sheet, conflictos, lock y breakdown.
- Implementar drag-and-drop, tooltips, búsqueda, multi-selección y scrolling.
- Portar traducciones, temas, modo claro/oscuro, atajos y animaciones de estado.
- Usar pickers Uno y adaptadores App específicos solo cuando una plataforma lo requiera.

**Gate**

- Los flujos `@ui` pasan el checklist en las tres plataformas.
- Ctrl se usa en Linux/Windows y Cmd en macOS sin cambiar la semántica.
- La UI no contiene reglas de scheduling, importación, serialización o reportes.

### Fase 8: persistencia de aplicación, empaquetado y estabilización

**Trabajo**

- Implementar autosave cancelable con debounce de dos segundos y restauración segura.
- Guardar tema, idioma, estado de sidebar y últimos diez documentos fuera del JSON del proyecto.
- Publicar aplicaciones autocontenidas para seis runtime identifiers.
- Empaquetar Linux como AppImage y `tar.gz`; Windows como ZIP; macOS como `.app` comprimida.
- Ejecutar regresión completa, pruebas de rendimiento con proyectos grandes y revisión de licencias.

**Gate**

- CI produce todos los artefactos desde un checkout limpio.
- Una instalación sin .NET puede iniciar, abrir un fixture, guardarlo y generar reportes.
- No quedan diferencias funcionales sin documentar respecto de `SPEC.md`.

## 9. Estrategia de pruebas

- Core tendrá la mayoría de la cobertura: reglas de negocio, parsers, scheduling, timeline, conflictos, lock, importadores y layout de reportes.
- Habrá solo cuatro suites de integración de alto valor, descritas en `SPEC.md`: compatibilidad de proyecto, importación, workflow completo y reportes.
- Los detalles visuales específicos de Skia se validarán mediante checklist manual y screenshots de referencia, no con una gran suite frágil de UI tests.
- Cada bug de compatibilidad o lógica descubierto durante la migración deberá reproducirse primero con una prueba Core o de integración.

## 10. Compatibilidad y rollback

- El formato escrito seguirá siendo el esquema v4.5.0; no se agregará un campo requerido de versión.
- Propiedades nuevas de UI o preferencias se almacenarán en app data, nunca en el documento compartido.
- Antes de sobrescribir un archivo existente se escribirá a un temporal en el mismo directorio y se reemplazará de forma atómica cuando la plataforma lo permita.
- El usuario podrá seguir abriendo el mismo documento con la aplicación Swift.
- La implementación Swift no se retirará hasta después de una versión estable multiplataforma aceptada.

## 11. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Fechas Swift usan formatter y fallback numérico propio | `DateTimeOffset`, convertidor dual y fixtures de diferentes zonas horarias |
| Diferencias de controles o drag-and-drop entre targets Uno | Reglas en Core, payload por UUID y checklist por plataforma |
| PDF cambia por fuentes del host | Noto Sans embebida y font resolver único |
| Highland no es un ZIP válido o cambia su estructura | Validación defensiva y búsqueda limitada a entradas conocidas |
| Paths recientes pierden permisos | Adaptadores de plataforma, descarte de entradas stale y error recuperable |
| Proyecto grande degrada calendario | Colecciones virtualizadas, recomputación incremental y pruebas de rendimiento |
| Autosave compite con edición/guardado manual | CancellationToken, snapshot consistente y serialización exclusiva |
| API Uno no cubre una integración del sistema | Adapter en `CineSched.App/Platform`; nunca contaminar Core |

## 12. Definition of Done

La migración está terminada cuando:

- Todos los escenarios Core y de integración definidos en `SPEC.md` pasan en CI.
- Los escenarios UI/manual pasan en Linux, Windows y macOS.
- Los documentos Swift actuales y legacy abren correctamente y el JSON .NET puede volver a abrirse en Swift.
- Las seis exportaciones PDF contienen los mismos datos y reglas industriales.
- FDX, Fountain y Highland importan correctamente y manejan errores sin mutar el proyecto.
- Autosave, recientes, idioma, temas, atajos, undo/redo y drag-and-drop funcionan en los tres sistemas.
- `CineSched.Core` permanece libre de dependencias de UI o plataforma.
- CI publica los artefactos autocontenidos acordados.
- README, CHANGELOG y licencias se actualizan como parte del release final, no antes de alcanzar paridad.
