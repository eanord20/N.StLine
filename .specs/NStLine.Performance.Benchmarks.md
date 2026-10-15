# N.StLine — Benchmark заметки (Cutting/Exact)

## Среда

- Замер: 12.10.2026, Windows x64; Intel Core i7-14650HX (24 логических процессора), ОЗУ 31,7 ГиБ, SDK .NET 10.0.203; приложение и benchmark таргетируют .NET 8.
- Один локальный прогон `dotnet run -c Release` без прогрева/JIT-статистики. Время не является устойчивой оценкой ускорения.
- Проект: `src/NStLine.Cutting.Benchmarks`.
- Цель: диагностика exact solver и сравнение результатов на фиксированных наборах. Имена `Production-*` обозначают синтетические входы, не данные реального цеха.

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

## Последний замер (12.10.2026)

Это профили **benchmark harness**, а не Fast/Balanced/Deep интерфейса:
- `Default`: `TimeLimitSeconds=8`, `MaxPatternCount=100000`; для Medium-Mixed, Pattern-Stress и Production-* фактически применяется 6 с / 25000 паттернов.
- `Extended`: 20 с / 200000 паттернов для Production-100, без ограничения `BuildCaseOptions`. Сравнение с Default одновременно меняет два лимита; это не A/B-тест отдельной оптимизации.

| Profile | Case | PartTypes | TotalQty | Status | Reason | Bars | ElapsedMs | MeasuredMs | Patterns | Nodes | PrunedBar | PrunedRem | PrunedDom |
|---|---|---:|---:|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Default | Small-Exact | 3 | 36 | Optimal | None | 8 | 21 | 41 | 35 | 36,613 | 18,970 | 0 | 0 |
| Default | Medium-Mixed | 5 | 175 | BestFound | TimeLimitReached | 18 | 6004 | 6004 | 3,706 | 28,754,811 | 399,612 | 0 | 0 |
| Default | Pattern-Stress | 6 | 144 | BestFound | TimeLimitReached | 24 | 6001 | 6001 | 923 | 36,282,728 | 6,791,396 | 2,397 | 0 |
| Default | Many-Small | 1 | 200 | Optimal | None | 4 | 0 | 0 | 58 | 820 | 66 | 0 | 0 |
| Default | Adversarial-FFD-1 | 5 | 10 | Optimal | None | 3 | 1 | 2 | 37 | 7 | 0 | 3 | 0 |
| Default | Adversarial-FFD-2 | 7 | 8 | Optimal | None | 3 | 0 | 0 | 43 | 306 | 72 | 5 | 1 |
| Default | Production-100 | 8 | 104 | BestFound | TimeLimitReached | 12 | 6006 | 6006 | 17,883 | 19,919,182 | 107,074 | 0 | 0 |
| Default | Production-300 | 8 | 300 | BestFound | PatternLimitReached | 32 | 1 | 2 | 25,001 | 0 | 0 | 0 | 0 |
| Extended | Production-100 | 8 | 104 | BestFound | TimeLimitReached | 12 | 20017 | 20017 | 17,883 | 68,156,666 | 381,181 | 0 | 0 |

## Сравнение с предыдущим состоянием (до dominance cache)

Исторические замеры сделаны на другом состоянии кода; это ориентир, а не контролируемый A/B-тест:

- `Small-Exact`: узлы ~`2,619,809` → `71,177`, время exact ~`265 ms` → `25 ms`.
- `Many-Small`: узлы ~`350,774` → `36,123`, время exact ~`14 ms` → `6 ms`.

## Интерпретация

1. Ранее внесённые ограничения генерации паттернов и suffix-bound уменьшают пространство поиска на отдельных фиксированных задачах; этот прогон не изолирует вклад каждой оптимизации.
2. В тяжёлых кейсах exact не доказывает оптимальность за 6 секунд (`TimeLimitReached`), но возвращает план со статусом `BestFound`. В Production-100 увеличение бюджета до 20 секунд оставляет те же 12 заготовок и не доказывает оптимальность; по одному прогону нельзя утверждать, что дальнейшее увеличение бесполезно.
3. Для Production-300 достигается `PatternLimitReached` на стадии генерации паттернов.
4. В этом прогоне счётчик `PrunedByDominance` почти везде равен нулю: нельзя приписывать ему большую часть ускорения.
5. Измерение памяти и калибровка пресетов на реальных данных ещё не проведены; по этой таблице нельзя объявлять программу готовой к коммерческому релизу.

## До/после на репрезентативных кейсах

- `Small-Exact`: `Nodes ~71k -> ~36k` (примерно ×1.9 меньше), при сохранении `Optimal`.
- `Many-Small`: `Nodes ~36k -> ~820` (примерно ×44 меньше), `Optimal` сохраняется.
- `Production-100`: количество узлов при одинаковом коротком лимите времени нестабильно и не является самостоятельным показателем ускорения; в последнем прогоне статус остаётся `BestFound` по time limit.
