# Position Sizer RR — Indicador NinjaTrader 8

Herramienta de *position sizing* para **operar manualmente** en NinjaTrader 8, al estilo del EA
"Position Sizer" de MT5. Definís cuánto arriesgar en dólares, arrastrás dos líneas (Entrada y
Stop) sobre el gráfico, y el indicador calcula **cuántos contratos** operar y dibuja los
objetivos en **R-múltiplos (1R / 2R / 3R)**.

> **No coloca órdenes.** Solo calcula y dibuja. Vos ejecutás la orden a mano en NT8.

## Qué muestra
- **Número de contratos** (redondeado hacia abajo, nunca supera tu riesgo).
- **Riesgo real** en $ y en ticks/puntos.
- **Ganancia potencial (+$)** en cada TP (1R, 2R, 3R).
- **Dirección automática**: stop debajo de la entrada = LONG (TPs arriba); stop arriba = SHORT (TPs abajo).

## Estructura del proyecto
```
src/
  PositionSizerMath.cs   # Aritmética pura (sin dependencia de NinjaTrader). Testeable.
  PositionSizerRR.cs     # El indicador NT8 (líneas arrastrables + render + config).
test/
  PositionSizerMath.Tests.csproj
  PositionSizerMathTests.cs   # 24 tests xUnit de la lógica de cálculo.
scripts/
  deploy-to-nt8.ps1      # Copia los .cs a la carpeta Custom\Indicators de NT8.
docs/
  nt8-checklist.md       # Verificación manual de la parte visual en NT8.
  superpowers/specs/     # Spec de diseño.
```

## Desarrollo

Correr los tests de la lógica:
```bash
cd test
dotnet test
```

## Instalación en NinjaTrader 8
1. Copiar los fuentes a NT8:
   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts\deploy-to-nt8.ps1
   ```
   (o copiar `src\PositionSizerMath.cs` y `src\PositionSizerRR.cs` a
   `...\NinjaTrader 8\bin\Custom\Indicators\` a mano).
2. En NT8: **NinjaScript Editor → F5 (Compile)**.
3. Agregar el indicador **PositionSizerRR** a un gráfico de futuros.
4. Verificar con [docs/nt8-checklist.md](docs/nt8-checklist.md).

## Uso
Agarrás la línea de **Entrada** o **Stop** por su tramo (a la derecha del gráfico) y la arrastrás;
todo recalcula en vivo. La posición de las líneas se **persiste**, así que cambiar el riesgo (u otro
parámetro) no las mueve.

## Parámetros (config del indicador)
| Parámetro | Default | Qué hace |
|---|---|---|
| Riesgo ($) | 500 | Monto fijo a arriesgar en dólares. |
| Distancia inicial del stop (ticks) | 40 | Dónde arranca la línea de stop al cargar por primera vez. |
| Tamaño texto | 13 | Tamaño de las etiquetas. |
| Mostrar tags de precio | sí | Cajitas de precio pegadas al eje derecho. |
| Mostrar caja resumen | no | Caja con contratos/riesgo/dirección arriba a la izquierda. |
| Largo líneas (% ancho) | 35 | Qué parte del ancho (desde la derecha) ocupan las líneas. |
| Color entrada / stop / objetivos / texto | dorado / rojo / verde / blanco | Colores. |

## Notas técnicas
- La lógica de cálculo está aislada en `PositionSizerMath` (C# 7.3, sin referencias a NT) para
  poder testearla con `dotnet test` fuera de NinjaTrader.
- El indicador **no usa objetos de dibujo de NT**: dibuja las líneas cortas en `OnRender` (SharpDX)
  y maneja el arrastre con **eventos de mouse** (`PreviewMouseLeftButtonDown/Up` + `MouseMove`),
  convirtiendo la posición del cursor a precio con `ChartingExtensions.ConvertToVerticalPixels` +
  `ChartScale.GetValueByY`. Repinta con `ChartControl.InvalidateVisual()` solo cuando el precio
  cambia de tick, para que el arrastre sea fluido.
- Las posiciones de entrada/stop se guardan en propiedades serializadas, así sobreviven a recargas
  (p. ej. al cambiar el riesgo $).
- Verificado: la lógica pasa 24/24 tests y el indicador compila contra las DLLs reales de NT8
  (net48 / C# 7.3) sin errores.
