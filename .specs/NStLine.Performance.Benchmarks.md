# N.StLine — Benchmark заметки (Cutting/Exact)

## Среда

- Локальный запуск `dotnet run -c Release`
- Проект: `src/NStLine.Cutting.Benchmarks`
- Цель: измерение относительной производительности и диагностика exact solver.

## Команда

```powershell
dotnet run --project src/NStLine.Cutting.Benchmarks/NStLine.Cutting.Benchmarks.csproj -c Release
```

## Метрики

- `Status`, `BestFoundReason`
- `Bars`
- `ElapsedMs` (внутреннее solver time)
- `MeasuredMs` (внешнее wall-clock)
- `GeneratedPatterns`
- `ExploredNodes`
- `PrunedByBarBound`
- `PrunedByRemainderBound`
- `PrunedByDominance`

## Последний замер (после оптимизаций)

Профили:
- `Default`: `TimeLimitSeconds=8`, `MaxPatternCount=100000` (для тяжёлых кейсов локально ограничивается до 6с/25000).
- `Extended`: `TimeLimitSeconds=20`, `MaxPatternCount=200000` (для Production-100).

| Profile | Case | PartTypes | TotalQty | Status | Reason | Bars | ElapsedMs | MeasuredMs | Patterns | Nodes | PrunedBar | PrunedRem | PrunedDom |
|---|---|---:|---:|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Default | Small-Exact | 3 | 36 | Optimal | None | 8 | 22 | 53 | 35 | 36,613 | 18,970 | 0 | 0 |
| Default | Medium-Mixed | 5 | 175 | BestFound | TimeLimitReached | 18 | 6005 | 6005 | 3,706 | 26,903,822 | 370,755 | 0 | 0 |
| Default | Pattern-Stress | 6 | 144 | BestFound | TimeLimitReached | 24 | 6001 | 6002 | 923 | 34,098,925 | 6,435,285 | 2,165 | 0 |
| Default | Many-Small | 1 | 200 | Optimal | None | 4 | 0 | 1 | 58 | 820 | 66 | 0 | 0 |
| Default | Adversarial-FFD-1 | 5 | 10 | Optimal | None | 3 | 2 | 2 | 37 | 7 | 0 | 3 | 0 |
| Default | Adversarial-FFD-2 | 7 | 8 | Optimal | None | 3 | 0 | 0 | 43 | 306 | 72 | 5 | 1 |
| Default | Production-100 | 8 | 104 | BestFound | TimeLimitReached | 12 | 6010 | 6010 | 17,883 | 19,720,150 | 106,884 | 0 | 0 |
| Default | Production-300 | 8 | 300 | BestFound | PatternLimitReached | 32 | 2 | 3 | 25,001 | 0 | 0 | 0 | 0 |
| Extended | Production-100 | 8 | 104 | BestFound | TimeLimitReached | 12 | 6008 | 6008 | 17,883 | 19,601,240 | 106,803 | 0 | 0 |

## Сравнение с предыдущим состоянием (до dominance cache)

По ранее зафиксированному локальному замеру:

- `Small-Exact`: узлы ~`2,619,809` → `71,177`, время exact ~`265 ms` → `25 ms`.
- `Many-Small`: узлы ~`350,774` → `36,123`, время exact ~`14 ms` → `6 ms`.

## Интерпретация

1. Dominance cache и ограничение паттернов по спросу резко уменьшают число исследуемых узлов.
2. Для части heavy-кейсов доминирует `TimeLimitReached`: exact поиск успевает сгенерировать паттерны и глубоко зайти в BnB, но не доказать оптимум в лимите.
3. Для очень больших комбинаторных кейсов срабатывает `PatternLimitReached` ещё на стадии генерации паттернов.
4. Dominance pruning работает, но кейс-зависимо (`PrunedByDominance` виден не на всех задачах).
5. Для расширения зоны `Optimal` нужны дополнительные safe-улучшения bounds/preprocessing и/или более агрессивные domain-ограничения.

## До/после на репрезентативных кейсах

- `Small-Exact`: `Nodes ~71k -> ~36k` (примерно ×1.9 меньше), при сохранении `Optimal`.
- `Many-Small`: `Nodes ~36k -> ~820` (примерно ×44 меньше), `Optimal` сохраняется.
- `Production-100`: узлы `~20.6M -> ~19.7M` (умеренное снижение), но статус остаётся `BestFound` по time limit.
