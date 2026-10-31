# QA6 — раунд 1, технический solver audit (финал после выполнения harness)

Версия: **1.1.0 + uncommitted**, HEAD `9408582acbfa5256d0257c77c7a8e399c92546f9`, 2026-10-30. Прочитаны baseline.md, AGENTS, текущая MVP spec, Core/Cutting исходники, существующие oracle/reference/project тесты, `git log`, `git show --stat 460bf27`, `git diff --stat`. Чужие QA-отчеты не открывались. Production не изменялся. App.OnStartup не запускался.

## Итог

**Подтверждено 5 дефектов** независимым DLL-harness (прямые ссылки на Release-DLL из `src/NStLine.App/bin/Release/net8.0-windows`, без ProjectReference/пакетов). Оба запуска завершились exit 0. 31 существующий Cutting test PASS.

Ключевой результат: **на 3000 детерминированных десятичных задачах (доли десятой мм, seed 601030) найдено 3 случая, где движок возвращает `Optimal` при числе заготовок больше математического минимума** (доказано независимым целочисленным перебором). Физическая вместимость (decimal-проверка) не нарушена ни в одном из 3000 случаев; все 3000 имеют статус Optimal.

| Сценарий | Тип | Статус | Доказательство |
|---|---|---|---|
| Existing Cutting suite (31) | API | PASS | qa6-cutting.trx |
| 3000 задач vs integer oracle | API | **FAIL (3/3000)** | suite-evidence.txt: oracle-mismatch ×3, matrix-summary |
| decimal-ffd-trap (ручной case) | API | **FAIL** | suite-evidence.txt:1–139, Optimal/3 вместо 2 |
| tiny (1e-10 бар) | API | **FAIL** | suite-evidence.txt:141–201, отрицательный остаток, 120% |
| decimal-exact (0.1×3 в 0.3) | API | FAIL (minor) | suite-evidence.txt:330–399, остаток −5.6e-17 |
| overflow-total (1e308) | API | **FAIL** | suite-evidence.txt:203–266, Infinity/NaN |
| floor-int-overflow | API | **FAIL** | suite-evidence.txt:268–328, BestFound на тривиальной задаче |
| null-part в Parts | API | **FAIL** | suite-evidence.txt:401–405, NullReferenceException |
| Decimal-проверка вместимости bar | API | PASS (0 нарушений) | matrix-summary: physical=0 |
| Bars/Patterns multiplicity | API | PASS | pattern-multiplicity-fail отсутствует |
| Отмена во время эвристики | API | **FAIL (задержка 810 мс)** | cancel-evidence.txt |
| Pre-cancel / retry / determinism ×3 | API | PASS | suite-evidence.txt:867–1391, идентичные результаты |
| UI/VM, DOCX, проекты, библиотека материалов | UI/API | NOT TESTED | вне численного фокуса QA6 |

## QA6-01 — ПОДТВЕРЖДЕНО: ложный статус Optimal на десятичных длинах (High)

- Категория: точность/оптимальность. Серьезность: **High** — нарушен главный контракт «Optimal = доказанный минимум». Практический масштаб реален: случаи найдены на длинах с точностью 0.1 мм (пример — хлыст 2.3 мм, детали 0.7/0.9), частота ~0.1% на случайной матрице; на типичных целых мм, вероятно, реже, но не исключено (тест не покрывал).
- Предусловия: конечные положительные десятичные размеры (не целые мм), точный поиск в пределах лимитов.
- Воспроизведение (case 1791 из evidence): `BarLength=2.3, CutWidth=0; parts: 0.7×1 (p0), 0.9×2 (p1), 0.7×3 (p2)`.
- Шаги/команда: `dotnet bin/Release/net8.0-windows/Harness.dll` в `qa-focus-20261030\agent6` → `oracle-mismatch` (caseIndex 1791, 1876, 2986) и `decimal-ffd-trap` (stock=1; 0.6+0.5+0.3+0.2×3).
- Ожидание (источник): `.specs/NStLine.MVP.Spec.md:229` — Optimal только если пространство исчерпано; независимый целочисленный перебор (все ×10, decimal-free oracle в Program.cs) доказывает минимум **2**: `[0.7+0.7+0.9]` и `[0.7+0.7+0.9]`; сумма спроса 4.6 / 2.3 = 2 — нижняя граница достигнута.
- Факт: `status=Optimal, Method="Exact branch-and-bound", TotalBarCount=3` (evidence:407–528). Еще 2 аналогичных расхождения: case 1876 (ожид. 2, факт 3, kerf 0.2, evidence:531–676) и case 2986 (ожид. 3, факт 4, evidence:678–853). Matrix-summary: mismatches=3/3000, notOptimal=0.
- Причина (код): `ExactPatternSolver.cs:136–139` — `maxCount = Floor(available/denominator)` без компенсации ошибки double: при `available` чуть меньше целого кратного теряется ветка с допустимым количеством; проверка tolerance на листе (:119–123) не восстанавливает пропущенные ветки. Затем `CuttingEngine.cs:42–46` помечает heuristic Optimal, т.к. SearchCompleted.
- Частота: 3/3000 на десятых мм; детерминировано.
- Влияние: лишние заготовки, неверная оценка материала, статус вводит в заблуждение.
- Уверенность: подтверждено runtime; oracle независим (целочисленная DFS-укладка копий, capacity/длины в десятых единицах — без погрешности). Проверена также физическая корректность возвращенных баров через decimal.

## QA6-02 — ПОДТВЕРЖДЕНО: недопустимая укладка и отрицательный остаток на микроразмерах (Low–Medium)

- Категория: арифметика/валидация. Серьезность: **Low** для продукта (длины ~1e-10 мм непрактичны), но демонстрирует отсутствие scale-aware сравнений.
- Ввод: `BarLength=1e-10, CutWidth=0, part=6e-11, qty=2`. Ожидание: 2 бара, остатки 4e-11 каждый (`Σ=1.2e-10 > 1e-10`, превышение 20%).
- Факт: `Optimal, 1 bar, TotalRemainder=-2e-11, UtilizationPercent=120` (evidence:141–201).
- Причина: `FirstFitDecreasingSolver.cs:115` `newUsedLength <= Length + 1e-9` — абсолютный epsilon больше вместимости; exact подтверждает heuristic (incumbent). Сопутствующе `decimal-exact` показывает незажатый остаток −5.6e-17 и utilization 100.00000000000003% (evidence:330–399) — косметическое следствие.
- Частота: детерминировано на указанном масштабе. Влияние: отрицательные остатки могут просочиться в UI/DOCX на нестандартных единицах измерения. Уверенность: подтверждено.

## QA6-03 — ПОДТВЕРЖДЕНО: отмена игнорируется во время эвристики, ~0.8 c (Medium)

- Категория: отмена/отзывчивость. Серьезность: **Medium** — SLA не задан, но 810 мс CPU-работы после Cancel ощутимы и масштабируются квадратично.
- Ввод: stock=1000, kerf=1, part=600, qty=30000 (каждая деталь — отдельный бар); отмена через ~20 мс в дочернем процессе.
- Факт: `OperationCanceledException`, cancelAt=28 ms, elapsed=838 ms, **delayAfterCancel=810 ms**, peak 44.7 MB (cancel-evidence.txt). Приложение вернуло исключение без нового результата — состояние корректно (retry PASS).
- Причина: token проверяется до FFD (`CuttingEngine.cs:14`), сам FFD не получает token; линейный поиск бара для каждой из 30000 деталей ≈ 4.5e8 попыток.
- Частота: растет O(n·bars); на типичных задачах незаметно. Уверенность: подтверждено измерением.

## QA6-04 — ПОДТВЕРЖДЕНО: Infinity/NaN в итогах при экстремальных конечных входах (Low)

- Ввод: `BarLength=1e308, part=9e307, qty=2`. Ожидание: 2 бара, доля 90%, либо честный отказ.
- Факт: `Optimal, TotalPartLength=Infinity, UtilizationPercent=NaN` (evidence:203–266); bar-уровень остатков корректен.
- Причина: `CuttingResult.cs:15–18` суммирует double без проверки; валидация (`CuttingEngine.cs:122–143`) не ограничивает произведения/суммы. Уверенность: подтверждено; непрактичные значения, но статус Optimal на нечисловом итоге — нарушение честности результата.

## QA6-05 — ПОДТВЕРЖДЕНО: две согласованности контракта (Medium)

- **(a) NullReferenceException вместо валидационной ошибки.** `Parts` с null-элементом: `CuttingEngine.Validate` делает `continue` для null (:133–134), затем FFD падает NRE (`FirstFitDecreasingSolver.cs:10`, evidence:401–405). Ожидание по спецификации — построчная/задачная валидация (ArgumentException), не падение.
- **(b) Тривиальная задача помечена BestFound.** `BarLength=6000, kerf=0, part=1e-6, qty=2`: результат `BestFound / First Fit Decreasing / ExactNotCompleted (reason=4), GeneratedPatterns=0` (evidence:268–328). Независимый расчет: 1 бар с остатком 5999.999998 — доказуемый оптимум (сумма спроса 2e-6 ≤ 6000, нижняя граница 1 достигнута). Точный поиск не сгенерировал ни одной схемы и не объяснил прерывание — вероятная неперехваченная ошибка/переполнение в `Floor→int` (`ExactPatternSolver.cs:137`: `available/denominator ≈ 6e9 > int.MaxValue`, unchecked-cast дает отрицательное и исключает ветку). Пользователь видит нечестный/необъяснимый статус на решаемой задаче. Уверенность: runtime подтверждено; точная внутренняя причина — гипотеза по коду.

## Ограничения

UI/VM-старение результата, DOCX/графика/текст/перезапись, сохранение/загрузка проектов, импорт/экспорт материалов — не проверялись этим агентом (численный фокус). Матрица покрывала только 3 типа деталей и шаг 0.1; для целых мм и большего числа типов частота QA6-01 не оценена — при необходимости координатору: расширить `ps`/seed в Program.cs и повторить команду suite.

## Изменения и артефакты (только agent6)

`Harness.csproj`, `Program.cs`, `execution-evidence.txt`, `suite-evidence.txt` (1391 строка фактических результатов), `cancel-evidence.txt`, `qa6-cutting.trx`, `report.md`, собственные bin/obj. Команды: `dotnet build Harness.csproj -c Release` (0 warn/err), `dotnet test ... Cutting.Tests --no-build --no-restore` (31 PASS), два запуска harness координатором (exit 0). Production, общие DLL, .gitignore, пользовательские файлы/буфер не тронуты.
