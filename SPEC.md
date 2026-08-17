# CineSched .NET: especificación funcional

## 1. Propósito

Este documento es la fuente normativa del comportamiento observable de CineSched durante y después de la migración. La secuencia de implementación está en [MIGRATION-PLAN.md](MIGRATION-PLAN.md) y los límites técnicos en [ARCHITECTURE.md](ARCHITECTURE.md).

Los escenarios utilizan keywords Gherkin en inglés para facilitar su traslado posterior a archivos `.feature`; los nombres y descripciones permanecen en español.

## 2. Convenciones

### Etiquetas

| Etiqueta | Significado |
|---|---|
| `@core` | Regla pura o caso de uso cubierto por pruebas unitarias de Core |
| `@compatibility` | Contrato de archivo compartido entre Swift y .NET |
| `@integration` | Pipeline que combina varias piezas reales; solo se definen cuatro clases dentro del mismo proyecto de tests |
| `@ui` | Comportamiento de presentación Uno |
| `@manual` | Requiere comprobación visual o interacción real de escritorio |
| `@pdf` | Regla de un reporte PDF |

### Términos

- **Boneyard**: colección de escenas todavía no programadas.
- **Shoot day**: día del calendario de producción, con escenas, banners/eventos y call sheet.
- **Escena estructural**: escena, banner, comida o evento cuyo movimiento puede deshacerse.
- **Página**: ocho octavos; internamente `duration` siempre se guarda en octavos enteros.
- **Tiempo estimado**: minutos enteros.
- **Proyecto actual**: estado mantenido por `ProjectService`, incluyendo documento, dirty state y undo/redo; el archivo asociado pertenece a App.
- **Evento de calendario**: item visual asociado a un día que no cuenta como escena ni entra al Boneyard.

### Invariantes globales

1. Cada escena y shoot day tiene un UUID estable y único.
2. Una escena normal aparece exactamente una vez: en Boneyard o en un shoot day.
3. Mover datos nunca crea copias implícitas ni cambia UUID.
4. Eventos de calendario, banners y comidas no alteran conteos de escenas o páginas salvo que la especificación indique duración temporal.
5. Las fechas persistidas representan el mismo instante al cruzar Swift y .NET.
6. Un error de lectura, importación o exportación no modifica el estado actual.
7. Las operaciones UI invocan casos de uso Core; la UI no replica reglas de negocio.

## 3. Ciclo de vida del proyecto

```gherkin
@core
Feature: Crear y reiniciar un proyecto
  Scenario: Crear el proyecto inicial
    Given no existe un proyecto abierto
    When se crea un proyecto nuevo
    Then el título es "Untitled Movie"
    And no hay escenas en el Boneyard
    And los shoot days del rango inicial no contienen escenas ni call sheets
    And ProductionInfo contiene sus valores por defecto
    And la fecha de creación corresponde al momento de creación

  Scenario: Confirmar un nuevo proyecto sobre contenido existente
    Given el proyecto actual contiene escenas o información de producción
    When el usuario confirma New Project
    Then escenas, call sheets, información de producción y schedule lock se eliminan
    And el archivo manual asociado deja de ser el destino de Save
    And la operación no se incorpora al historial estructural de undo

  Scenario: Cancelar un nuevo proyecto
    Given el proyecto actual contiene cambios
    When el usuario cancela la confirmación de New Project
    Then el documento y su archivo asociado permanecen sin cambios
```

```gherkin
@core
Feature: Abrir y guardar proyectos
  Scenario: Save usa el archivo asociado
    Given el proyecto fue abierto o guardado en una ruta accesible
    And el proyecto contiene cambios
    When se ejecuta Save
    Then se serializa un snapshot consistente en el mismo archivo
    And el proyecto deja de estar dirty después de completarse la escritura
    And el archivo se registra como reciente

  Scenario: Save sin archivo asociado solicita destino
    Given el proyecto nunca fue guardado manualmente
    When se ejecuta Save
    Then la aplicación solicita un destino igual que Save As

  Scenario: Save As no cambia el destino al cancelar
    Given existe un archivo asociado
    When se ejecuta Save As y se cancela el picker
    Then no se escribe ningún archivo
    And el destino anterior continúa asociado

  Scenario: Un error de escritura conserva dirty state
    Given el proyecto contiene cambios
    When la escritura falla por permisos o I/O
    Then el proyecto continúa dirty
    And se presenta un error recuperable
    And el archivo anterior no queda parcialmente reemplazado
```

```gherkin
@core
Feature: Autosave y archivos recientes
  Scenario: Autosave espera dos segundos desde el último cambio
    Given el proyecto cambia repetidamente en menos de dos segundos
    When transcurren dos segundos desde el cambio más reciente
    Then se guarda un único snapshot en app data

  Scenario: Un cambio nuevo cancela el autosave pendiente
    Given existe un autosave pendiente
    When cambia otra propiedad persistida
    Then se cancela el delay anterior
    And comienza un nuevo delay de dos segundos

  Scenario: Guardar manualmente no borra el autosave recuperable
    Given existe un snapshot de autosave válido
    When se guarda manualmente el proyecto
    Then el documento manual y el autosave representan el mismo estado confirmado

  Scenario: Recientes conserva diez entradas únicas
    Given existen diez archivos recientes diferentes
    When se abre nuevamente uno de ellos
    Then ese archivo pasa a la primera posición
    And no aparece duplicado
    And la lista conserva como máximo diez entradas

  Scenario: Una entrada reciente inaccesible se descarta de forma segura
    Given un archivo reciente fue movido o perdió permisos
    When se intenta abrir
    Then se informa que ya no es accesible
    And no cambia el proyecto actual
    And la entrada stale puede eliminarse de recientes
```

## 4. Contrato JSON Swift/.NET

```gherkin
@core @compatibility
Feature: Decodificar proyectos Swift
  Scenario: Abrir un proyecto v4.5.0 completo
    Given un fixture producido con el esquema Codable v4.5.0
    When se decodifica con ProjectService
    Then se conservan todos los UUID, fechas, escenas, shoot days y datos de producción
    And se conservan banners, comidas, eventos, breakdown y schedule lock

  Scenario: Abrir el formato legacy de nivel superior
    Given un JSON que solo contiene allScenes y shootDays
    When se decodifica
    Then el título usa el default de proyecto importado
    And ProductionInfo usa sus defaults
    And no se pierden escenas ni días

  Scenario: Abrir cast heredado como string
    Given una escena cuyo campo cast es "ANA, LUIS, EXTRA"
    When se decodifica
    Then cast contiene "ANA", "LUIS" y "EXTRA"
    And no contiene valores vacíos o espacios exteriores

  Scenario: Aplicar defaults de campos ausentes
    Given un documento antiguo sin sceneNumber, breakdown, banners, blackout ni locationRoster
    When se decodifica
    Then strings ausentes son vacíos
    And arrays ausentes son vacíos
    And flags ausentes son false
    And defaultLunchTime es "01:30 PM"

  Scenario Outline: Leer formatos de fecha aceptados por Swift
    Given una propiedad Date codificada como <representation>
    When se decodifica
    Then representa el instante esperado sin depender de la cultura del sistema

    Examples:
      | representation                 |
      | "2026-08-16T20:30:00-0400"    |
      | "2026-08-17T00:30:00+0000"    |
      | segundos desde 2001-01-01 UTC  |
```

```gherkin
@core @compatibility
Feature: Codificar proyectos para Swift
  Scenario: Escribir el wire format compartido
    Given un ProjectDocument completo
    When se codifica
    Then los nombres de propiedad usan camelCase exacto
    And DayNightType, BannerType y MealKind usan sus raw values Swift exactos
    And las fechas usan yyyy-MM-dd'T'HH:mm:ssZ
    And el JSON es pretty printed y UTF-8
    And todos los campos requeridos por Swift están presentes

  Scenario: No persistir preferencias de UI
    Given el usuario cambió idioma, tema y estado del sidebar
    When se guarda el proyecto
    Then esas preferencias no aparecen en ProjectDocument
```

```gherkin
@integration @compatibility
Feature: Pipeline de compatibilidad de proyecto
  Scenario: Abrir, modificar, guardar y recargar un fixture Swift
    Given fixtures Swift actual, legacy y cast-string
    When cada fixture se abre, se modifica mediante un caso de uso y se vuelve a guardar
    Then el resultado se puede decodificar nuevamente con el contrato Swift
    And al recargarlo en .NET conserva IDs, fechas, relaciones y valores modificados
    And no pierde campos que el caso de uso no modificó
```

## 5. Escenas y parsing

```gherkin
@core
Feature: Administrar escenas
  Scenario: Crear una escena normal
    Given datos válidos de título, páginas, tiempo, tipo, cast y resumen
    When se crea la escena
    Then recibe un UUID único
    And aparece al final del Boneyard
    And sus páginas se guardan en octavos y su tiempo en minutos

  Scenario: Editar una escena conserva identidad y posición
    Given una escena programada
    When se editan sus datos descriptivos
    Then conserva UUID, día y posición
    And los totales del día se recalculan

  Scenario: Duplicar crea una identidad nueva
    Given una escena existente
    When se duplica
    Then la copia conserva los datos editables
    And recibe un UUID diferente
    And se inserta en el mismo contexto indicado por el caso de uso

  Scenario: Eliminar una escena programada
    Given una escena dentro de un shoot day
    When se confirma su eliminación
    Then ya no aparece en el shoot day ni en Boneyard
    And la operación entra al historial de undo
```

```gherkin
@core
Feature: Interpretar duración y tiempo
  Scenario Outline: Convertir páginas a octavos
    Given la entrada <input>
    When se interpreta como duración
    Then el resultado es <eighths> octavos

    Examples:
      | input   | eighths |
      | "15"    | 15      |
      | "8"     | 8       |
      | "1 7/8" | 15      |
      | "7/8"   | 7       |
      | "2.5"   | 20      |

  Scenario Outline: Convertir tiempo estimado a minutos
    Given la entrada <input>
    When se interpreta como tiempo
    Then el resultado es <minutes> minutos

    Examples:
      | input  | minutes |
      | "4"    | 240     |
      | "10"   | 600     |
      | "15"   | 15      |
      | "2:30" | 150     |

  Scenario: Rechazar una duración inválida
    Given una entrada que no representa páginas ni tiempo
    When se valida el formulario
    Then no se modifica la escena
    And se devuelve un error asociado al campo
```

```gherkin
@core
Feature: Breakdown y número de escena
  Scenario: Extraer número desde el slugline
    Given una escena sin sceneNumber y título "12A. INT. CASA - DAY"
    When se normaliza
    Then sceneNumber es "12A"
    And title es "INT. CASA - DAY"

  Scenario: Ordenar escenas en script order
    Given escenas 2, 12, 12A, 12B y 20 sin importar su schedule
    When se consultan para Breakdown Browser
    Then aparecen en orden 2, 12, 12A, 12B y 20

  Scenario: Conservar categorías independientes
    Given una escena con Props, Wardrobe, SFX y VFX
    When se edita una categoría
    Then las demás categorías permanecen sin cambios
    And SFX y VFX nunca se fusionan
```

## 6. Boneyard, búsqueda y selección

```gherkin
@core
Feature: Ordenar y buscar escenas
  Scenario Outline: Ordenar Boneyard sin cambiar persistencia
    Given un Boneyard con escenas variadas
    When se selecciona el orden <sort>
    Then la consulta devuelve el orden esperado
    And el orden almacenado de allScenes no cambia

    Examples:
      | sort      |
      | Default   |
      | Location  |
      | INT/EXT   |
      | Cast      |
      | Day/Night |

  Scenario: Buscar en todo el schedule
    Given escenas programadas y no programadas
    When se busca texto presente en título, cast o resumen ignorando mayúsculas
    Then se devuelven todas las coincidencias con su día opcional

  Scenario: Selección Shift se limita al mismo contexto
    Given dos escenas de un mismo día y otra de un día diferente
    When se hace Shift-select entre las dos primeras
    Then se selecciona su rango dentro del día
    And no se extiende el rango al otro día
```

## 7. Calendario y scheduling

```gherkin
@core
Feature: Generar calendario de producción
  Scenario: Alinear semanas de lunes a domingo
    Given un rango que comienza en miércoles y termina en martes
    When se genera el calendario
    Then cada fila contiene siete columnas de lunes a domingo
    And los slots fuera del rango no se convierten en shoot days editables

  Scenario: Actualizar rango sin shift
    Given escenas programadas dentro del rango actual
    And Shift Schedule está desactivado
    When cambia la fecha inicial
    Then las escenas conservan sus fechas absolutas si siguen en el rango

  Scenario: Actualizar rango con shift
    Given Shift Schedule está activado
    When la fecha inicial se mueve tres días
    Then todos los shoot days y su contenido se desplazan tres días
    And las fechas relativas entre escenas se conservan
```

```gherkin
@core
Feature: Mover escenas
  Scenario: Mover una escena desde Boneyard a un día
    Given una escena no programada
    When se coloca en una posición de un shoot day
    Then desaparece del Boneyard
    And aparece exactamente una vez en la posición solicitada

  Scenario: Mover un grupo conserva su orden relativo
    Given varias escenas seleccionadas en orden conocido
    When se mueven juntas a un día
    Then se insertan contiguamente
    And conservan su orden relativo

  Scenario: Reordenar dentro de un día
    Given un día con varias escenas
    When una escena se mueve entre otras dos
    Then solo cambia el orden de ese día
    And UUID y contenido permanecen iguales

  Scenario: Send to Day usa la misma regla que drag-and-drop
    Given escenas seleccionadas
    When se elige una fecha mediante Send to Day
    Then el resultado de dominio equivale a moverlas al final de ese día

  Scenario: Remove from Day devuelve escenas normales al Boneyard
    Given escenas normales programadas
    When se ejecuta Remove from Day
    Then vuelven al Boneyard
    And conservan identidad y datos
```

```gherkin
@core
Feature: Mover días completos
  Scenario: Mover a un día vacío
    Given un origen con escenas y call sheet
    And un destino vacío
    When se mueve el día completo
    Then escenas y call sheet pasan al destino
    And el origen queda vacío

  Scenario: Intercambiar con un día ocupado
    Given origen y destino contienen datos
    When se arrastra el día origen al destino
    Then se intercambian escenas y call sheets
    And las fechas de los contenedores no se intercambian
```

```gherkin
@core
Feature: Blackout days
  Scenario: Programar en blackout informa pero no bloquea
    Given un shoot day marcado blackout
    When se programa una escena en ese día
    Then la operación se completa
    And el día y la escena se reportan como conflicto visual de blackout

  Scenario: Marcar todos los weekdays equivalentes
    Given un rango de calendario
    When se marcan todos los sábados como unavailable
    Then cada sábado editable queda blackout
    And otros weekdays no cambian
```

```gherkin
@core
Feature: Undo y redo estructural
  Scenario: Deshacer una operación estructural
    Given se movieron escenas entre días
    When se ejecuta Undo
    Then escenas y días vuelven al snapshot anterior
    And se recalculan selección, conflictos y schedule lock

  Scenario: Redo restaura la operación deshecha
    Given una operación acaba de deshacerse
    When se ejecuta Redo
    Then se restaura el snapshot posterior

  Scenario: Una edición nueva limpia redo
    Given existe al menos un snapshot de redo
    When se ejecuta una nueva operación estructural
    Then redo queda vacío

  Scenario: El historial conserva como máximo treinta snapshots
    Given se ejecutan más de treinta operaciones estructurales
    When se inspecciona el historial
    Then solo las treinta más recientes pueden deshacerse
```

## 8. Stripboard, banners y timeline

```gherkin
@core
Feature: Calcular la cascada temporal
  Scenario: Cascada automática desde general call
    Given un día con general call 07:30 AM y escenas con duración estimada
    When se calcula el timeline
    Then cada item comienza al terminar el anterior
    And wrap coincide con el final del último item

  Scenario: Un ancla fija reinicia la cascada posterior
    Given una escena con customStartTime 11:00 AM
    When se calcula el timeline
    Then esa escena comienza a las 11:00 AM
    And los items posteriores continúan desde su final

  Scenario: Una ancla anterior al final previo no produce duración negativa
    Given un item anterior que termina después del ancla solicitada
    When se calcula el timeline
    Then se aplica la regla de validación definida por el editor
    And ningún rango termina antes de comenzar

  Scenario: Editar duración actualiza todos los items posteriores
    Given un timeline automático
    When aumenta la duración de un item
    Then su final y los rangos posteriores se desplazan por la diferencia
```

```gherkin
@core
Feature: Banners, comidas y eventos
  Scenario: Crear un banner
    Given tipo, título, nota, duración y color válidos
    When se agrega al stripboard
    Then participa en la cascada temporal
    And no cuenta como escena ni páginas

  Scenario: Crear una comida automática
    Given un MealKind y hora
    When se crea la comida
    Then Lunch, Dinner y Wrap usan 60 minutos por defecto
    And Snack usa 15 minutos por defecto
    And se identifica como banner y auto meal

  Scenario: Crear un evento de calendario
    Given título, hora y color
    When se agrega al día
    Then se muestra en calendario
    And no entra al Boneyard, conteo de escenas o breakdown
```

## 9. Production Setup, conflictos y schedule lock

```gherkin
@core
Feature: Administrar información de producción
  Scenario: Actualizar cast conserva referencias
    Given un CastMember utilizado en escenas y call sheets
    When se edita actor o character
    Then las consultas de call sheet reflejan el valor actualizado

  Scenario: Crew daily aparece por defecto en nuevos call sheets
    Given miembros crew marcados daily y no-daily
    When se inicializa un call sheet
    Then los daily aparecen seleccionados
    And los no-daily permanecen disponibles sin seleccionar

  Scenario: Location roster evita duplicados
    Given locations con el mismo nombre y dirección ignorando mayúsculas y espacios
    When se normaliza el roster
    Then se conserva una única location lógica
```

```gherkin
@core
Feature: Detectar conflictos de disponibilidad
  Scenario: Actor unavailable en un día programado
    Given un CastMember asociado a un personaje
    And una fecha unavailable que contiene una escena del personaje
    When se escanean conflictos
    Then se reportan actor, personaje, escena y fecha

  Scenario: Personaje no registrado no genera conflicto de actor
    Given una escena con un personaje sin CastMember asociado
    When se escanean conflictos
    Then no se inventa un actor ni una disponibilidad

  Scenario: Números de escena duplicados se detectan globalmente
    Given escenas programadas o no programadas con el mismo sceneNumber no vacío
    When se escanean duplicados
    Then todos sus UUID aparecen en el resultado
```

```gherkin
@core
Feature: Schedule lock
  Scenario: Lock captura los días actuales de cada personaje
    Given un schedule con cast programado
    When se bloquea el schedule
    Then se guarda lockedAt
    And workingDays contiene las fechas ordenadas por personaje
    And el schedule permanece editable

  Scenario: Detectar días agregados y removidos
    Given un schedule lock existente
    When una escena cambia los working days de un personaje
    Then el reporte contiene addedDays y removedDays exactos

  Scenario: Unlock elimina el baseline
    Given un schedule lock existente
    When se desbloquea
    Then scheduleLock queda null
    And no se reportan cambios hasta crear otro lock
```

## 10. Call sheets

```gherkin
@core
Feature: Editar call sheets
  Scenario: Inicializar cast desde escenas del día
    Given un shoot day con personajes en sus escenas
    When se abre su call sheet
    Then aparecen entradas de cast únicas
    And actor se resuelve desde ProductionInfo cuando existe

  Scenario: Inicializar locations sin duplicados
    Given escenas del día con real locations repetidas
    When se abre su call sheet
    Then cada location lógica aparece una vez

  Scenario: Crew seleccionado conserva referencia estable
    Given un crew roster y un miembro seleccionado en el call sheet
    When se cambia su nombre o rol en Production Setup
    Then el call sheet refleja el cambio sin perder selección

  Scenario: Detectar que un día tiene datos de call sheet
    Given un call sheet con cualquier campo relevante no vacío
    When se consulta hasCallSheetData
    Then el resultado es true
```

## 11. Importación de guiones

```gherkin
@core
Feature: Importar Final Draft
  Scenario: Extraer escenas desde FDX o XML
    Given un documento válido con Scene Headings, números y paragraphs
    When se importa
    Then se extraen sceneNumber, slugline, INT/EXT, time of day y cast
    And los títulos se normalizan a mayúsculas
    And las escenas se agregan al Boneyard

  Scenario: Aplicar defaults cuando FDX no ofrece paginación suficiente
    Given una escena válida sin duración calculable
    When se importa
    Then duration es al menos un octavo
    And estimatedTime es al menos el default compatible
```

```gherkin
@core
Feature: Importar Fountain y Highland
  Scenario Outline: Reconocer una extensión soportada
    Given un archivo con extensión <extension>
    When se selecciona Import Script
    Then se enruta al importer Fountain/Highland

    Examples:
      | extension |
      | .fountain |
      | .md       |
      | .spmd     |
      | .highland |

  Scenario: Leer el contenido de Highland
    Given un archivo Highland ZIP válido
    When se importa
    Then se usa la primera entrada disponible entre text.fountain, text.markdown y text.txt

  Scenario: Highland sin entrada de texto falla sin mutar
    Given un archivo Highland sin entradas reconocidas
    When se importa
    Then se devuelve un error descriptivo
    And el proyecto no cambia

  Scenario: Confirmar importación sobre un proyecto con contenido
    Given un proyecto con escenas, schedule o título modificado
    When termina el parsing Fountain/Highland
    Then el resultado queda pendiente hasta confirmación
    And al confirmar solo agrega escenas al Boneyard
```

```gherkin
@integration
Feature: Pipeline de importación de guiones
  Scenario: Normalizar FDX, Fountain y Highland de referencia
    Given muestras equivalentes en FDX, Fountain y Highland
    When cada formato atraviesa selección, parsing, paginación y commit
    Then produce escenas con números, títulos, tipos, cast y duraciones esperados
    And los errores no dejan commits parciales
```

## 12. Reportes PDF

```gherkin
@core @pdf
Feature: Exportar el calendario
  Scenario: Generar calendario landscape US Letter
    Given un rango con escenas y días vacíos
    When se genera Schedule PDF
    Then las páginas son US Letter landscape
    And contiene título, fechas, escenas y totales diarios
    And pagina sin dibujar fuera del área útil
```

```gherkin
@core @pdf
Feature: Exportar stripboard y One-Line
  Scenario: Generar Strip Schedule
    Given shoot days con escenas, banners y tipos variados
    When se genera Stripboard PDF
    Then conserva el orden y colores semánticos de las tiras
    And muestra información de producción relevante

  Scenario: Generar One-Line Shooting Schedule
    Given un timeline calculado
    When se genera One-Line PDF
    Then cada fila contiene horario, escena/slugline, página y octavos
    And el header muestra Crew Call, Set Call y Lunch
    And el final muestra wrap, páginas y tiempo acumulado
```

```gherkin
@core @pdf
Feature: Exportar DOOD
  Scenario: Calcular códigos estándar
    Given un actor que trabaja uno o varios días
    When se genera DOOD
    Then se asignan SW, W, H, WF o SWF según su secuencia
    And unavailable dates se muestran como X
    And blackout days se distinguen en el header

  Scenario: Excluir hold days
    Given Include Hold está desactivado
    When se genera DOOD
    Then las celdas H quedan vacías
    And la columna HLD no se imprime

  Scenario: Paginar actores y días
    Given más actores y días de los que caben en una página
    When se genera DOOD
    Then pagina filas y columnas
    And repite headers y leyenda en cada página
```

```gherkin
@core @pdf
Feature: Exportar breakdown y call sheet
  Scenario: Breakdown produce una página por escena normal
    Given escenas con categorías de breakdown
    When se genera Breakdown PDF
    Then cada escena normal tiene una hoja en script order
    And cada categoría aparece en su celda sin fusionarse con otras

  Scenario: Call sheet usa datos sincronizados
    Given un shoot day, ProductionInfo y CallSheetData completos
    When se genera Call Sheet PDF
    Then es US Letter portrait
    And contiene general call, contactos, milestones, clima, hospital, escenas, cast, crew y notas
    And el idioma corresponde a la preferencia activa
```

```gherkin
@integration @pdf
Feature: Pipeline de generación de reportes
  Scenario: Generar todos los reportes desde un proyecto representativo
    Given un fixture con schedule, timeline, cast, crew, conflictos, call sheets y breakdown
    When se generan los seis tipos de reporte
    Then cada resultado comienza con una firma PDF válida
    And puede abrirse nuevamente con PDFsharp
    And tiene orientación, cantidad de páginas y textos clave esperados
    And usa fuentes embebidas sin depender del sistema operativo
```

## 13. Localización, temas y desktop UX

```gherkin
@core
Feature: Localizar contenido
  Scenario Outline: Cambiar idioma
    Given el idioma activo es <language>
    When se consulta un recurso soportado
    Then UI, fechas y reportes usan <language>

    Examples:
      | language |
      | English  |
      | Español  |

  Scenario: Una clave faltante tiene fallback visible
    Given una clave no traducida
    When se solicita en español
    Then se usa el texto inglés o la propia clave
    And la aplicación no falla
```

```gherkin
@ui @manual
Feature: Experiencia Fluent de escritorio
  Scenario: Cambiar entre Calendar y Stripboard
    Given la ventana principal está abierta
    When se selecciona otro modo
    Then el sidebar y proyecto permanecen
    And el contenido central cambia sin perder selección válida

  Scenario: Aplicar tema y modo de color
    Given un tema system, blue, green o yellow
    When se alterna light o dark
    Then canvas, paneles, accent y estados mantienen contraste legible
    And la preferencia se restaura al reiniciar

  Scenario: Usar modificadores propios de la plataforma
    Given CineSched se ejecuta en Linux, Windows o macOS
    When se usa selección múltiple o un shortcut
    Then Linux y Windows usan Ctrl
    And macOS usa Cmd

  Scenario: Animar sin alterar el layout
    Given cambia selección, hover, panel o drag state
    When se ejecuta la transición
    Then usa opacity o transform de corta duración
    And no cambia la posición final calculada por Core

  Scenario: Usar pickers y clipboard
    Given la plataforma ofrece integración de escritorio
    When se abre, guarda o copia información
    Then se utiliza un servicio concreto de plataforma dentro de App
    And Core no recibe tipos Uno ni handles de ventana
```

## 14. Workflow completo

```gherkin
@integration
Feature: Pipeline funcional de un proyecto
  Scenario: Importar, programar, bloquear, cambiar, deshacer y persistir
    Given un proyecto nuevo y un guion Fountain de referencia
    When se importa el guion
    And se programan escenas en varios días
    And se configura cast con disponibilidad
    And se bloquea el schedule
    And se mueve una escena causando un cambio de working day
    Then el conflicto y el schedule lock report reflejan el cambio
    When se deshace el movimiento
    Then conflicto y working days vuelven al estado anterior
    When se guarda y recarga el proyecto
    Then el estado final y sus IDs se conservan
```

## 15. Mapeo de pruebas a implementar

### `CineSched.Tests/Unit`

Las pruebas unitarias se agruparán por slice y utilizarán nombres de comportamiento:

- `Projects`: defaults, dirty state, autosave scheduling, recent list y codec/converters.
- `Scenes`: fraction/time parser, scene number, decorado, INT/EXT, CRUD, búsqueda y sort.
- `Scheduling`: calendar range, shift, move/reorder, group order, day swap, blackout y undo/redo.
- `Stripboard`: cascade, fixed anchors, banner/meal/event factories y totals.
- `Production`: roster normalization, stable references y call sheet projections.
- `Conflicts`: availability, duplicate scene numbers y blackout projection.
- `ScheduleLock`: baseline, added/removed days, lock y unlock.
- `ScriptImport`: FDX extraction, Fountain parsing/pagination y Highland archive validation.
- `Reports`: DOOD codes, page layout calculations, language y report preconditions.

Cada escenario `@core` debe tener al menos una prueba directamente rastreable por nombre. Los Scenario Outline se implementarán como teorías parametrizadas.

### `CineSched.Tests/Integration`

El mismo proyecto de tests contendrá únicamente estas cuatro clases de integración:

1. `ProjectCompatibilityPipelineTests`.
2. `ScriptImportPipelineTests`.
3. `ProjectWorkflowPipelineTests`.
4. `ReportGenerationPipelineTests`.

No se agregarán integraciones para repetir validaciones ya cubiertas por los métodos de cada service. Una nueva prueba de integración requerirá justificar qué frontera real no puede validarse con una prueba Core.

### Aceptación manual UI

Una checklist por Linux, Windows y macOS cubrirá:

- Inicio, tamaño de ventana, menús y CommandBar.
- Sidebar, Calendar/Stripboard y dialogs.
- Click, double-click, context menu, hover y scroll.
- Ctrl/Cmd/Shift selection y shortcuts.
- Drag de escenas, grupos y días completos.
- Pickers, cancelaciones, recientes y permisos.
- Español/English, temas y light/dark.
- Animaciones, contraste y proyectos grandes.
