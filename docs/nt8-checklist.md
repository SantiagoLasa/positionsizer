# Checklist de verificación en NinjaTrader 8 — PositionSizerRR

Lo visual del indicador solo se puede verificar dentro de NT8 (la lógica de cálculo ya está
cubierta por tests automáticos). Seguí estos pasos y marcá cada uno.

## Compilación
- [ ] Los archivos están en `...\NinjaTrader 8\bin\Custom\Indicators\` (`PositionSizerMath.cs` y `PositionSizerRR.cs`).
- [ ] En NT8: **New → NinjaScript Editor → F5 (Compile)**. Compila **sin errores**.

## Carga en el gráfico
- [ ] Abrí un gráfico de un **futuro** (ej. ES, NQ, MNQ).
- [ ] **Indicators → PositionSizerRR → agregar**. Aparecen **dos líneas**: Entrada (dorada) y Stop (roja).
- [ ] Aparece la **caja resumen** arriba a la izquierda con Contratos / Riesgo / Dir.

## Cálculo y arrastre
- [ ] Arrastrá la línea de **Stop**: contratos, riesgo real y las líneas 1R/2R/3R se actualizan **en vivo**.
- [ ] Arrastrá la línea de **Entrada**: todo se recalcula igual.
- [ ] El **riesgo real nunca supera** el "Riesgo ($)" configurado (redondeo hacia abajo).

## Dirección automática
- [ ] Stop **debajo** de la entrada → **LONG**, los 3 TP aparecen **arriba**.
- [ ] Stop **arriba** de la entrada → **SHORT**, los 3 TP aparecen **abajo**.

## Configuración
- [ ] Doble clic en el indicador → cambiar **Riesgo ($)** (ej. de 500 a 1000) → recalcula contratos.
- [ ] Cambiar **Distancia inicial del stop (ticks)** afecta solo dónde arranca la línea al cargar de nuevo.
- [ ] Cambiar los **colores** se refleja en las líneas/textos.

## Multi-instrumento (valor por contrato)
- [ ] Probá el mismo indicador en **dos futuros distintos** (ej. ES y NQ/MNQ). Para la **misma distancia en puntos**, el **$ por contrato cambia** según el instrumento (lee PointValue/TickSize solo).

## Casos borde
- [ ] Poné la línea de Stop **exactamente** sobre la de Entrada → la caja muestra **"Ajustá el stop (entrada = stop)"** y **no** dibuja TPs.
- [ ] Alejá tanto el stop que el riesgo por contrato supere el "Riesgo ($)" → muestra **"Achicá el stop o subí el riesgo"** y 0 contratos.
- [ ] Borrá una de las dos líneas (clic derecho → remove) → al llegar un tick/recargar, **se recrea**.

## Si algo falla
Anotá: qué instrumento, qué valores de Entrada/Stop/Riesgo, y qué viste vs qué esperabas.
Con eso lo depuramos (idealmente reproduciendo el número en un test de `PositionSizerMath`).
