# Checklist de verificación en NinjaTrader 8 — PositionSizerRR

Lo visual del indicador solo se puede verificar dentro de NT8 (la lógica de cálculo ya está
cubierta por tests automáticos). Seguí estos pasos y marcá cada uno.

## Compilación
- [ ] Los archivos están en `...\NinjaTrader 8\bin\Custom\Indicators\` (`PositionSizerMath.cs` y `PositionSizerRR.cs`).
- [ ] En NT8: **New → NinjaScript Editor → F5 (Compile)**. Compila **sin errores**.

## Carga en el gráfico
- [ ] Abrí un gráfico de un **futuro** (ej. ES, NQ, MNQ).
- [ ] **Indicators → PositionSizerRR → agregar**. Aparecen las líneas **cortas a la derecha**: Entrada (0R, dorada) y Stop (-1R, roja), con sus tags de precio en el eje.
- [ ] Las etiquetas se ven **por encima** de cada línea (no centradas sobre ella).

## Cálculo y arrastre
- [ ] Agarrá la línea de **Stop** por su tramo (a la derecha) y arrastrala: contratos, riesgo y 1R/2R/3R se actualizan **en vivo y fluido**.
- [ ] Agarrá la línea de **Entrada** y arrastrala: todo se recalcula igual.
- [ ] El **riesgo real nunca supera** el "Riesgo ($)" configurado (redondeo hacia abajo).

## Dirección automática
- [ ] Stop **debajo** de la entrada → **LONG**, los 3 TP aparecen **arriba**.
- [ ] Stop **arriba** de la entrada → **SHORT**, los 3 TP aparecen **abajo**.

## Configuración
- [ ] Doble clic en el indicador → cambiar **Riesgo ($)** (ej. de 500 a 1000) → recalcula contratos **y las líneas NO se mueven ni desaparecen** (posición persistida).
- [ ] Cambiar **Largo líneas (% ancho)** hace las líneas más cortas/largas desde la derecha.
- [ ] **Mostrar caja resumen** (off por defecto) la prende/apaga arriba a la izquierda.
- [ ] Cambiar los **colores** se refleja en las líneas/textos.

## Multi-instrumento (valor por contrato)
- [ ] Probá el mismo indicador en **dos futuros distintos** (ej. ES y NQ/MNQ). Para la **misma distancia en puntos**, el **$ por contrato cambia** según el instrumento (lee PointValue/TickSize solo).

## Casos borde
- [ ] Poné la línea de Stop **exactamente** sobre la de Entrada → la etiqueta de la entrada muestra **"0R · ajustá el stop"** y **no** dibuja TPs.
- [ ] Alejá tanto el stop que el riesgo por contrato supere el "Riesgo ($)" → la etiqueta del stop muestra **"subí riesgo o achicá SL"** y 0 contratos.

## Si algo falla
Anotá: qué instrumento, qué valores de Entrada/Stop/Riesgo, y qué viste vs qué esperabas.
Con eso lo depuramos (idealmente reproduciendo el número en un test de `PositionSizerMath`).
