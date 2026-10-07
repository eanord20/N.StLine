# N.StLine — Exact Solver (техническое описание)

## Назначение

`ExactPatternSolver` решает задачу минимизации числа заготовок для 1D-раскроя с учётом пропила, а при равенстве числа заготовок — минимизации суммарного остатка.

## Входные данные

- `BarLength` — длина заготовки.
- `CutWidth` — ширина реза.
- `Parts` — набор типов деталей `(Length, Quantity)`.
- `CuttingOptions`:
  - `TimeLimitSeconds`
  - `MaxPatternCount`
  - `ToleranceMm`

## Целевая функция

Лексикографическая:
1. `min TotalBarCount`
2. `min TotalRemainder`

## Ограничения

Для каждой заготовки:

`Σ(x_i * L_i) + (Σx_i) * CutWidth <= BarLength`, где `x_i >= 0`.

## Алгоритм

1. Генерация допустимых паттернов одной заготовки (recursive).
2. Сортировка паттернов по убыванию полезной длины.
3. Branch-and-bound поиск по мультипликативному использованию паттернов.
4. Обновление лучшего решения по целевой функции.

## Search strategy / Branching

- Узел состояния: `(patternIndex, remainingDemand, barsUsed, remainderUsed)`.
- Для текущего паттерна перебирается multiplicity `maxMult..0`.
- `maxMult` ограничивается остаточным спросом.

## Bounds

Используется нижняя оценка на число дополнительных заготовок:
- length bound: по суммарной требуемой длине с учётом пропила;
- per-type bound: по максимуму покрываемости каждого типа.
- suffix per-type bound: для текущего `patternIndex` берётся максимум покрытия по хвосту списка паттернов, что даёт более tight оценку в глубине дерева.

## Pruning

Безопасные отсечения:
- `projectedBars > bestBars`
- `projectedBars == bestBars && remainderUsed >= bestRemainder`
- dominance cache для `(patternIndex, remainingDemand)`: если уже было состояние с не худшим `(barsUsed, remainderUsed)`.
- infeasible tail: если в хвосте паттернов текущий тип детали не покрывается (`maxCount == 0` при `remaining > 0`), ветвь немедленно отсекается.

## Dominance

Префикс `A` доминирует `B`, если:
- `A.BarsUsed < B.BarsUsed`, или
- `A.BarsUsed == B.BarsUsed && A.RemainderUsed <= B.RemainderUsed`.

При одинаковом `patternIndex` и одинаковом `remainingDemand` продолжение у них одно и то же, поэтому `B` можно безопасно отсечь.

## Symmetry breaking

Частично достигается:
- фиксированным порядком паттернов;
- перебором multiplicity сверху вниз.

Явного полного symmetry-breaking между эквивалентными состояниями пока нет.

## Caching

- `Dictionary<StateKey, PrefixCost>` с ограничением размера (`MaxStateCacheEntries`).
- `StateKey = (patternIndex, clone(remainingDemand))`.

## Optimality

`Optimal` возвращается только если поиск завершён и оптимум доказан.

Если поиск не завершён по времени/лимитам — возвращается `BestFound` с причиной.

## Time limit / Cancellation

- Проверка лимита времени в горячем цикле поиска.
- Поддержка `CancellationToken`.
- Причины `BestFound`:
  - `TimeLimitReached`
  - `PatternLimitReached`
  - `ExactNotCompleted`
  - `Cancelled`

## Performance-факторы

Наибольшее влияние:
- число типов деталей;
- длины, близкие к кратным заготовки (рост числа паттернов);
- величина `MaxPatternCount`;
- эффективность incumbent (heuristic seed);
- качество отсечений dominance/bounds.

## Последние улучшения (в текущем коде)

1. Порядок типов деталей в exact нормализован: сначала длинные, затем по количеству (уменьшает ветвление на практике).
2. Генерация паттернов ограничивается спросом `Quantity`.
3. Добавлен dominance cache с ограничением размера.
4. Добавлен suffix-bound по покрытию типов для хвоста паттернов.
5. Добавлены диагностические счётчики узлов/отсечений для benchmark и production-диагностики.
