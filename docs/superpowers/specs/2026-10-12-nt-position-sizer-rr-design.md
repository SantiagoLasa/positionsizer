# Position Sizer RR — Indicador NinjaTrader 8

**Fecha:** 2026-10-12
**Estado:** Spec aprobada en diseño; pendiente review del usuario antes de build.
**Autor:** santi + Claude
**Metodología:** Liviana (spec → build con test de lógica + checklist manual en NT8).

---

## 1. Objetivo

Herramienta de *position sizing* para **operar manualmente** en NinjaTrader 8, inspirada en
el EA "Position Sizer" de MT5. El usuario define cuánto arriesgar en dólares, arrastra dos
líneas (entrada y stop) sobre el gráfico, y el indicador calcula automáticamente **cuántos
contratos** operar y dibuja los **objetivos en R-múltiplos (1R, 2R, 3R)**.

**No coloca órdenes.** Solo calcula y dibuja. El usuario ejecuta la orden a mano en NT8.

## 2. Alcance

### Dentro de alcance
- Indicador NT8 (`PositionSizerRR`), overlay sobre el panel de precio.
- Dos líneas horizontales **arrastrables**: Entrada y Stop.
- Riesgo definido como **monto fijo en USD** (parámetro de configuración del indicador).
- Cálculo de contratos con **redondeo hacia abajo** (nunca excede el riesgo).
- **Dirección automática**: stop debajo de entrada = compra (TPs arriba); stop arriba = venta (TPs abajo).
- Tres niveles de TP fijos: **1R, 2R, 3R**.
- Funciona con **cualquier futuro** (lee `PointValue` y `TickSize` del instrumento).
- Muestra en pantalla: **# de contratos**, **riesgo real ($ / ticks / puntos)**, **ganancia +$ por cada TP**.

### Fuera de alcance (no-goals)
- Colocación/gestión de órdenes (ATM, bracket, etc.).
- Comisiones/fees en el cálculo del riesgo.
- Riesgo como % de cuenta (solo monto fijo en $).
- Instrumentos no-futuros (forex, acciones).
- Scaling in/out o lógica de salidas parciales (solo se dibujan los niveles).
- Precio numérico exacto de cada línea en pantalla (decisión del usuario: no mostrarlo).

### Posibles extensiones futuras (no ahora)
- Colocar la orden con el tamaño calculado.
- Riesgo como % de cuenta.
- Cantidad de TPs configurable / on-off por nivel.
- Caja editable sobre el chart para el riesgo.

## 3. Cómo se usa (flujo del usuario)

1. Compilar el indicador en NT8 (F5) una vez.
2. Agregar `PositionSizerRR` a un gráfico de un futuro.
3. En la config del indicador, poner **Riesgo ($)** (ej. 500).
4. Aparecen dos líneas arrastrables: **Entrada** y **Stop** (cerca del precio actual).
5. Arrastrar Stop (y/o Entrada) para definir la distancia de riesgo.
6. El indicador actualiza **en vivo** mientras se arrastra: contratos, riesgo real, y las
   3 líneas de TP con su ganancia en $.
7. El usuario coloca la orden manualmente en NT8 con el # de contratos indicado.

## 4. Lógica de cálculo (contrato/unidad testeable)

Toda la aritmética vive en una clase estática **sin dependencias de NinjaTrader**
(`PositionSizerMath`), para poder testearla fuera de NT8.

Definiciones (precios en puntos del instrumento):

```
pointValue        = Instrument.MasterInstrument.PointValue     // $ por 1 punto completo
tickSize          = Instrument.MasterInstrument.TickSize
distancia (pts)   = |entrada − stop|
distancia (ticks) = distancia / tickSize
riesgoPorContrato = distancia × pointValue                     // $ que se pierde con 1 contrato si toca el stop
contratos         = floor( riesgoDeseado / riesgoPorContrato ) // redondeo hacia abajo
riesgoReal        = contratos × riesgoPorContrato
dirección         = stop < entrada → LONG ; stop > entrada → SHORT ; stop == entrada → FLAT
precioTPn         = LONG:  entrada + n × distancia
                    SHORT: entrada − n × distancia      (n = 1,2,3)
gananciaTPn ($)   = n × riesgoReal                             // 1R = riesgoReal, 2R = 2×, 3R = 3×
```

### Reglas de borde
- `riesgoPorContrato <= 0` (entrada == stop) → contratos = 0, dirección = FLAT, no se dibujan TPs,
  se muestra aviso: **"Ajustá el stop (entrada = stop)"**.
- `riesgoDeseado <= 0` → contratos = 0, aviso: **"Poné un riesgo > 0"**.
- `contratos == 0` con riesgo y distancia válidos (el riesgo no alcanza ni para 1 contrato) →
  aviso: **"Riesgo por contrato ($X) > riesgo deseado. Achicá el stop o subí el riesgo."**
- Precios de TP se redondean al tick con `RoundToTick(price, tickSize)` para mostrar.

### Firma de los métodos puros (`PositionSizerMath`, namespace `PositionSizerTool`, sin refs NT)

```csharp
public enum Direction { Long, Short, Flat }

double RiskPerContract(double entry, double stop, double pointValue)   // |e-s| * pv
int    Contracts(double riskDollars, double riskPerContract)           // floor, nunca < 0
double ActualRisk(int contracts, double riskPerContract)
Direction Dir(double entry, double stop)
double TargetPrice(double entry, double stop, int n)                   // según dirección
double TargetProfit(int contracts, double riskPerContract, int n)      // n * actualRisk
double DistanceTicks(double entry, double stop, double tickSize)
double RoundToTick(double price, double tickSize)
```

## 5. Arquitectura / componentes

Dos archivos `.cs`, ambos se despliegan en la carpeta `Custom\Indicators` de NT8 (compilan
juntos en `NinjaTrader.Custom.dll`):

| Archivo | Namespace | Responsabilidad | Depende de NT? |
|---|---|---|---|
| `PositionSizerMath.cs` | `PositionSizerTool` | Toda la aritmética pura (sección 4). | **No** (testeable aislado) |
| `PositionSizerRR.cs` | `NinjaTrader.NinjaScript.Indicators` | Indicador: líneas arrastrables, lectura de anclas, render de TPs/etiquetas, config. | Sí |

El indicador hace `using PositionSizerTool;` y delega todo el cálculo en `PositionSizerMath`.

### Parámetros de configuración (propiedades NinjaScript)
| Propiedad | Tipo | Default | Display |
|---|---|---|---|
| `RiskDollars` | double | 500 | "Riesgo ($)" |
| `DefaultStopTicks` | int | 40 | "Distancia inicial del stop (ticks)" |
| `EntryColor` | Brush | Goldenrod | "Color entrada" |
| `StopColor` | Brush | Red | "Color stop" |
| `TargetColor` | Brush | LimeGreen | "Color objetivos" |
| `TextColor` | Brush | White | "Color texto" |
| `FontSize` | float | 12 | "Tamaño texto" |

`TargetCount = 3` queda como constante (decisión: TPs fijos 1R/2R/3R).

### Ciclo de vida (NinjaScript States)
- **SetDefaults**: `Name`, `IsOverlay = true`, `IsChartOnly = true`, `DrawOnPricePanel = true`,
  valores default de parámetros.
- **Primer `OnBarUpdate` válido** (con `CurrentBar >= 0` y `Close` disponible): si las líneas
  Entrada/Stop no existen en `DrawObjects`, se crean con `Draw.HorizontalLine`:
  - Entrada = `Close[0]` redondeado a tick.
  - Stop = Entrada − `DefaultStopTicks × tickSize` (default long).
  - Tags fijos: `"PSRR_Entry"`, `"PSRR_Stop"`. Si el usuario borra una, se recrea.
- **`OnRender(chartControl, chartScale)`**:
  1. `base.OnRender(...)`.
  2. Leer precios: `((HorizontalLine)DrawObjects["PSRR_Entry"]).StartAnchor.Price` y análogo para stop.
     Si falta alguna → no renderizar derivados (se recrean en el próximo `OnBarUpdate`).
  3. Calcular con `PositionSizerMath` usando `PointValue`/`TickSize` del instrumento.
  4. Dibujar con `RenderTarget` (SharpDX):
     - 3 líneas de TP (horizontales, `TargetColor`) a lo ancho del panel, Y = `chartScale.GetYByValue(precioTPn)`.
     - Etiqueta a la derecha de cada TP: `"1R +$___"`, etc.
     - Etiquetas chicas "ENTRADA" / "STOP" junto a sus líneas.
     - Caja resumen (rectángulo semitransparente + texto) arriba-izquierda:
       `Contratos: N` / `Riesgo: $X (Y ticks / Z pts)` / `Dir: LONG|SHORT`.
     - Si hay condición de borde → mostrar el aviso correspondiente en la caja.
  5. Liberar recursos SharpDX creados por frame (`ToDxBrush` + `TextFormat` → `Dispose`).

### Notas técnicas NT8 (verificadas en docs/foro oficial)
- `OnRender` se dispara en cada repintado, incluido **mientras se arrastra** una línea → update en vivo.
  (`chartScale.GetYByValue(precio)` → pixel Y.)
- Las `HorizontalLine` creadas con `Draw.HorizontalLine` son **arrastrables por el usuario** y su
  `StartAnchor.Price` **se actualiza solo** al arrastrarlas.
- `$ por tick = PointValue × TickSize`. `PointValue` = valor en moneda de 1 punto completo.
- Requiere `using NinjaTrader.NinjaScript.DrawingTools;` para el tipo `HorizontalLine`.
- No modificar la colección `DrawObjects` dentro de `OnRender` (se altera solo en `OnBarUpdate`).

## 6. Verificación / testing

NinjaScript solo compila y corre dentro de NT8; la parte visual no se puede ejecutar fuera.
Por eso el testing se parte en dos:

### 6.1 Test automático de la lógica (`PositionSizerMath`)
- Proyecto de test aparte que **incluye el mismo `PositionSizerMath.cs`** (sin NinjaTrader.dll).
- Runner concreto (xUnit vía `dotnet`, o consola auto-aserción) se decide al empezar el build
  según lo que haya instalado (se verifica `dotnet --version`). Flujo TDD: test rojo → implementar → verde.
- Casos mínimos:
  1. ES (pv=50, tick=0.25): entrada 5000, stop 4990 (10 pts = $500/contrato), riesgo $500 → 1 contrato, riesgoReal $500.
  2. Redondeo abajo: riesgo $432, riesgoPorContrato $120 → 3 contratos (no 3.6), riesgoReal $360.
  3. Dirección: stop < entrada → LONG; stop > entrada → SHORT; iguales → FLAT.
  4. TP prices LONG y SHORT para n=1,2,3.
  5. TargetProfit = n × riesgoReal.
  6. DistanceTicks correcto.
  7. Bordes: entrada==stop → contratos 0, FLAT; riesgo 0 → contratos 0; riesgoPorContrato > riesgo → 0.
  8. RoundToTick con tick 0.25 y 1.0.

### 6.2 Checklist manual en NT8 (parte visual)
Documento `docs/nt8-checklist.md` con pasos reproducibles:
- [ ] Compila sin errores (F5).
- [ ] Se agrega al chart y aparecen las 2 líneas.
- [ ] Arrastrar el stop actualiza contratos/riesgo/TPs **en vivo**.
- [ ] Cambiar "Riesgo ($)" en config recalcula contratos.
- [ ] Stop debajo → LONG con TPs arriba; stop arriba → SHORT con TPs abajo.
- [ ] Probar en 2 futuros distintos (p.ej. ES y NQ/MNQ): el $/contrato cambia según el instrumento.
- [ ] Redondeo: el riesgo real nunca supera el "Riesgo ($)" configurado.
- [ ] Borrar una línea y ver que se recrea.
- [ ] Caso borde entrada==stop muestra aviso y no dibuja TPs.

## 7. Despliegue

1. Copiar `PositionSizerMath.cs` y `PositionSizerRR.cs` a:
   `C:\Users\santi\OneDrive\Documentos\NinjaTrader 8\bin\Custom\Indicators\`
2. En NT8: NinjaScript Editor → Compilar (F5).
3. Agregar el indicador `PositionSizerRR` a un chart de futuros.

El código fuente versionable queda en la carpeta del proyecto (`src/`); la copia a NT8 es un
paso explícito (se puede automatizar con un script opcional más adelante).

## 8. Riesgos / cosas a confirmar durante el build
- Nombre exacto de la propiedad del ancla (`StartAnchor.Price` vs `Anchors.First().Price`) —
  se valida al compilar en NT8.
- Posición/anti-solapamiento de las etiquetas y la caja resumen — se ajusta con la checklist visual.
- Disponibilidad de `dotnet`/SDK para el runner de tests — se verifica al inicio del build.
