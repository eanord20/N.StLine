# QA Agent 4 — Независимый математический раунд 1

Кандидат: N.StLine 1.1.0 + uncommitted, HEAD 9408582 (feat/nstline-mvp).
Дата: 2026-10-30. Тестировались только Core/Cutting (headless API `CuttingEngine`, `ProjectCuttingEngine`), готовые Release DLL из `src/NStLine.*/bin/Release/net8.0`. UI/DOCX/AppData/буфер не тестировались.

## Установленные правила (из .specs/NStLine.MVP.Spec.md §7 и кода)

- Модель реза: в заготовке с N деталями `CutCount=N`, `CutLoss=N·K`, `Remainder=L−Σparts−N·K`; `Remainder<0` недопустимо.
- Паттерн допустим если `Σxi·Li + Σxi·K ≤ L` (движок допускает остаток ≥ −ToleranceMm=0.01 с клампом до 0).
- Целевая функция: минимум числа заготовок; при равенстве — минимум суммарного остатка. Замечание: при фиксированном числе заготовок B и фиксированном мультимножестве деталей суммарный остаток `B·L−P−N·K` константа (N=общее число деталей), второй критерий факультативен.
- Статус `Optimal` только при исчерпанном поиске; иначе `BestFound` + причина.
- Валидация: `ArgumentException` при Bar≤0, K<0, K≥L, пустой список, Length≤0/не finite, Qty≤0, `Length+K>L`.
- ProjectCuttingEngine: группировка деталей по `MaterialId`, материалы без деталей пропускаются, неизвестный `MaterialId` → `ArgumentException`.

## Независимый oracle

`qa-focus-20261030/agent4/harness/Program.cs`: exhaustive DFS по назначению копий деталей в заготовки (канонические состояния, мемоизация, ветвление от нижней границы `ceil((Σ+n·K)/L)`). Никакого общего кода с production solver. Для каждого результата проверяются: полный мультимножество-охват (по Id, count==Quantity), вместимость и согласованность каждой заготовки (`CutCount`, `CutLoss`, `Remainder`, `BarLength`), итоговые метрики (`Total*`, `UtilizationPercent`), согласованность `Patterns` c `Bars` (multiplicity и мультимножества), число заготовок против oracle.

Воспроизведение: `cd qa-focus-20261030/agent4/harness && dotnet build -c Release -t:Run`. Вывод: `../out/log.txt`, `../out/results.json`.

## Сводная таблица

| ID | Кат | Ввод (L, K, детали) | Ожидание (oracle/контракт) | Факт | Вердикт |
|----|-----|--------------------|-----------------------------|------|---------|
| QA4-A1 | A | 6000,3: 2000×2+1500×1 | 1 заг., rem=491 (5500+9) | 1, Optimal | PASS |
| QA4-A2 | A | 6000,3: 1000×6 | 2 заг. (макс 5 на заг.: 5·1003=5015; 6·1003=6018>6000) | 2, Optimal | PASS |
| QA4-A3 | A | 6000,3: 6500×1 | ArgumentException | ArgumentException | PASS |
| QA4-A4 | A | 6000,3: 2800×2+1700+900 | 2 заг. (Σ=8200>6000) | 2, Optimal | PASS* |
| QA4-A5 | A | пустой список | ArgumentException | ArgumentException | PASS |
| QA4-A6..A13 | A | −100; 0; qty 0; K=−1; K=6000; L=0; NaN; +∞ | ArgumentException | ArgumentException ×8 | PASS |
| QA4-A14 | A | деталь=L=6000 при K=3 | ArgumentException (нужен рез) | ArgumentException | PASS |
| QA4-A15 | A | деталь=6000 при K=0 | 1 заг., rem=0 | 1, Optimal | PASS |
| QA4-B1 | B | 6000,3: 1497×4 | ровно 6000 → 1 заг., rem=0 | 1, rem=0 | PASS |
| QA4-B2 | B | 6000,3: 1500×4 | 6012>6000 → 2 заг. | 2 | PASS |
| QA4-B3 | B | 6000,0: 1500×4 | 1 заг., rem=0 | 1 | PASS |
| QA4-B4 | B | 6000,3: 5997×1 | граница L−K → 1 заг. | 1 | PASS |
| QA4-B5 | B | 6000,3: 5998×1 | L−K+1 → отказ | ArgumentException | PASS |
| QA4-B6 | B | 6000,3: 3000+2994 | ровно → 1 заг. | 1 | PASS |
| QA4-B7 | B | 6000,3: 3000+2995 | +1 мм → 2 заг. | 2 | PASS |
| QA4-C1 | C | 6000,3: 1999×3 | 6006>6000 → 2 заг. | 2 | PASS* |
| QA4-C2 | C | 6000,3: 999×6 | 6012>6000 → 2 | 2 | PASS |
| QA4-C3 | C | 6000,3: 1000×5+1000×5 (2 типа) | 2 | 2 | PASS |
| QA4-C4 | C | 6000,2: 500×12 | ⌊6000/502⌋=11 → 2 заг. | 2 | PASS* |
| QA4-D1 | D | 6000,3: 2500+2000+1494 | 6003>6000 → 2 | 2 | PASS* |
| QA4-D2 | D | 6000,3: 3500+2400 | 5906≤6000 → 1 | 1 | PASS* |
| QA4-D3 | D | 6000,3: 3500×2+2400 | 2 | 2 | PASS |
| QA4-E1 | E | 6000,0: 4000+3500+2500+2000+2000 | Σ=14000 → ≥3 | 3 | PASS* |
| QA4-E2 | E | 100,0: 60²+50²+40²+30+20 | Σ=350 → ≥4 | 4 | PASS* |
| QA4-E3 | E | 6000,3: 3100²+2900²+1400²+1490 | oracle=3 | 3, Optimal | PASS |
| QA4-F1 | F | 6000,3: 12 типов, 20 копий (seed 4), лимит 8с | корректность без гарантии оптимума | 7 заг., BestFound/TimeLimitReached, структура валидна | PASS |
| QA4-F2 | F | 6000,3: 250×200 | oracle=9 (⌊6000/253⌋=23→⌈200/23⌉=9) | 9, Optimal | PASS |
| QA4-G1 | G | повтор Solve ×2 | структурная идентичность | идентичны | PASS |
| QA4-H1 | multi | m1 6000/3 + m2 4000/2, детали по MaterialId | 2 секции, по 1 заг. каждая | корректно | PASS |
| QA4-H2 | multi | неизвестный MaterialId | ArgumentException | ArgumentException | PASS |
| QA4-H3 | multi | материал без деталей | секция пропускается | 1 секция | PASS |
| QA4-H4 | multi | 0 материалов | ArgumentException | ArgumentException | PASS |
| QA4-FZ | fuzz | 120 случайных малых задач (seed 20261030) | bars==oracle ∧ все проверки | 120/120 | PASS |
| QA4-X1 | edge | список деталей содержит null | ArgumentException | **NullReferenceException** | **FAIL** |
| QA4-X2 | edge | TimeLimitSeconds=0 | BestFound, результат валиден | BestFound/TimeLimitReached | PASS |
| QA4-X3 | edge | MaxPatternCount=1 | честный статус | BestFound/PatternLimitReached | PASS |

`*` — первоначальная гипотеза тестировщика о числе заготовок была неверна; oracle (exhaustive) подтвердил ответ движка. Дефектов продукта в этих строках нет.

## Дефекты

### QA4-X1 — NullReferenceException на null-элементе списка деталей

- Категория: валидация/API-контракт. Серьезность: minor (недостижимо из UI: строки сетки не бывают null).
- Версия: 1.1.0+uncommitted, HEAD 9408582.
- Ввод: `CuttingTask{BarLength=6000, CutWidth=3, Parts=[Part(1000×1), null]}`.
- Шаги: `new CuttingEngine().Solve(task, options)`.
- Ожидание: `ArgumentException` (Validate в `CuttingEngine.cs:131-134` явно предусматривает null → `continue`, т.е. договор намеревался).
- Факт: `NullReferenceException` в `ExactPatternSolver` ctor (`task.Parts.OrderByDescending(p=>p.Length)`), `ExactPatternSolver.cs:250`.
- Доказательства: `out/log.txt`, `out/results.json` (QA4-X1).
- Частота: всегда при null-элементе. Влияние: необработанное исключение неверного типа; ловля `ArgumentException` вызывающим кодом не спасёт.
- Гипотеза: Validate пропускает null, но ExactPatternSolver/FFD не переносят фильтрацию. Уверенность: высокая. Ограничения: только API-уровень, UI-путь не проверен.

## Статические риски (не подтверждены исполнением)

- `ExactPatternSolver.cs:306`: статус `Optimal` присваивается, если поиск завершился и `elapsed < limit`; при генерации паттернов флаг `_patternLimitReached` обрабатывается корректно (Solve→null→BestFound). Риска несанкционированного `Optimal` не выявлено.
- Паттерн с остатком в [−0.01, 0) принимается и клампится к 0 — допускает «отрицательный» остаток до 0.01 мм (документированный ToleranceMm); FFD использует более жёсткий ε=1e-9 — теоретическая граница расхождения FFD/exact на волосок, в тестах не проявилась.
- `GroupPatterns` ключ содержит порядок Id в заготовке — теоретически одинаковые мультимножества в разном порядке дадут разные «схемы»; не воспроизведено.
- UI-уровень, DOCX, сериализация проектов/материалов, AppData — вне скоупа (не тестированы).

## Ограничения раунда

- DLL-only harness, UI/App.OnStartup не выполнялись.
- Fuzz ограничен малыми задачами (≤4 типов, ≤3 копий — глубина exhaustive oracle); крупные задачи проверялись только на внутреннюю согласованность и честный статус.
- Тайминги не сравнивались; детерминизм — структурный.

## Итог

40 кейсов + 120 фазз: все математические проверки (мультимножество, вместимость, число заготовок против exhaustive oracle, N резов, потери, остатки, метрики, Patterns↔Bars, статусы, детерминизм, мультиматериал) — PASS. Один minor дефект валидации (NRE на null-элементе). Оптимальность exact solver подтверждена независимым oracle во всех 120+ проверенных случаях; ложных `Optimal` не обнаружено.
