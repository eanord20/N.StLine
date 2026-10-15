using System.ComponentModel;
using System.Globalization;

namespace NStLine.App.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    private readonly Dictionary<string, Dictionary<string, string>> _resources = new();
    private string _culture = "en";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Culture
    {
        get => _culture;
        set
        {
            if (_culture == value) return;
            _culture = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    public string this[string key]
    {
        get
        {
            if (_resources.TryGetValue(_culture, out var dict) && dict.TryGetValue(key, out var value))
                return value;
            if (_resources.TryGetValue("en", out var fallback) && fallback.TryGetValue(key, out var fb))
                return fb;
            return key;
        }
    }

    public string WindowTitle => this["WindowTitle"];
    public string MenuFile => this["MenuFile"];
    public string MenuEdit => this["MenuEdit"];
    public string MenuOpenProject => this["MenuOpenProject"];
    public string MenuSaveProject => this["MenuSaveProject"];
    public string MenuSettings => this["MenuSettings"];
    public string MenuLanguage => this["MenuLanguage"];
    public string MenuTheme => this["MenuTheme"];
    public string MenuEnglish => this["MenuEnglish"];
    public string MenuUkrainian => this["MenuUkrainian"];
    public string MenuThemeLight => this["MenuThemeLight"];
    public string MenuThemeDark => this["MenuThemeDark"];
    public string MenuHelp => this["MenuHelp"];
    public string MenuAbout => this["MenuAbout"];
    public string AboutWindowTitle => this["AboutWindowTitle"];
    public string AboutVersion => this["AboutVersion"];
    public string AboutAuthor => this["AboutAuthor"];
    public string AboutContact => this["AboutContact"];
    public string AboutDescription => this["AboutDescription"];
    public string AboutCopyright => this["AboutCopyright"];
    public string AboutLicense => this["AboutLicense"];
    public string AboutThirdPartyLibraries => this["AboutThirdPartyLibraries"];
    public string ErrorOpenLicense => this["ErrorOpenLicense"];
    public string ButtonMaterials => this["ButtonMaterials"];
    public string ButtonPaste => this["ButtonPaste"];
    public string ButtonCalculate => this["ButtonCalculate"];
    public string ButtonCancelCalculation => this["ButtonCancelCalculation"];
    public string ButtonCopyResults => this["ButtonCopyResults"];
    public string ButtonExportDocx => this["ButtonExportDocx"];
    public string ButtonGenerateReport => this["ButtonGenerateReport"];
    public string ButtonBrowse => this["ButtonBrowse"];
    public string ButtonOpenLastReport => this["ButtonOpenLastReport"];
    public string ButtonClear => this["ButtonClear"];
    public string ButtonAddRow => this["ButtonAddRow"];
    public string ButtonDeleteRow => this["ButtonDeleteRow"];
    public string ButtonUndo => this["ButtonUndo"];
    public string ButtonClose => this["ButtonClose"];
    public string ButtonAdd => this["ButtonAdd"];
    public string ButtonDelete => this["ButtonDelete"];
    public string ButtonCopy => this["ButtonCopy"];
    public string ButtonOk => this["ButtonOk"];
    public string ButtonLicense => this["ButtonLicense"];
    public string ButtonCancel => this["ButtonCancel"];
    public string ButtonMoveToTask => this["ButtonMoveToTask"];
    public string ButtonMoveToLibrary => this["ButtonMoveToLibrary"];
    public string LabelMaterial => this["LabelMaterial"];
    public string LabelBarLength => this["LabelBarLength"];
    public string LabelCutWidth => this["LabelCutWidth"];
    public string UnitMillimeters => this["UnitMillimeters"];
    public string LabelParts => this["LabelParts"];
    public string LabelLibrary => this["LabelLibrary"];
    public string LabelInTask => this["LabelInTask"];
    public string LabelTaskMaterials => this["LabelTaskMaterials"];
    public string LabelReportOutputPath => this["LabelReportOutputPath"];
    public string ReportTitle => this["ReportTitle"];
    public string ReportStatus => this["ReportStatus"];
    public string ReportBarsOne => this["ReportBarsOne"];
    public string ReportBarsFew => this["ReportBarsFew"];
    public string ReportBarsMany => this["ReportBarsMany"];
    public string LabelExactProfile => this["LabelExactProfile"];
    public string ProfileFast => this["ProfileFast"];
    public string ProfileBalanced => this["ProfileBalanced"];
    public string ProfileCustom => this["ProfileCustom"];
    public string LabelExactTimeLimit => this["LabelExactTimeLimit"];
    public string LabelExactPatternLimit => this["LabelExactPatternLimit"];
    public string TooltipExactProfile => this["TooltipExactProfile"];
    public string TooltipExactTimeLimit => this["TooltipExactTimeLimit"];
    public string TooltipExactPatternLimit => this["TooltipExactPatternLimit"];
    public string EmptyNoMaterials => this["EmptyNoMaterials"];
    public string EmptyNoParts => this["EmptyNoParts"];
    public string EmptyNoResult => this["EmptyNoResult"];
    public string TooltipAddMaterial => this["TooltipAddMaterial"];
    public string TooltipRemoveMaterial => this["TooltipRemoveMaterial"];
    public string TooltipActiveMaterial => this["TooltipActiveMaterial"];
    public string ResultWindowTitle => this["ResultWindowTitle"];
    public string ButtonOpenResult => this["ButtonOpenResult"];
    public string StatusResultStale => this["StatusResultStale"];
    public string LabelSummary => this["LabelSummary"];
    public string LabelPatterns => this["LabelPatterns"];
    public string ColumnCuts => this["ColumnCuts"];
    public string ColumnCutLoss => this["ColumnCutLoss"];
    public string ColumnParts => this["ColumnParts"];
    public string ColumnRemainder => this["ColumnRemainder"];
    public string ColumnSeq => this["ColumnSeq"];
    public string ColumnName => this["ColumnName"];
    public string ColumnLength => this["ColumnLength"];
    public string ColumnCutWidth => this["ColumnCutWidth"];
    public string ColumnQuantity => this["ColumnQuantity"];
    public string ColumnMaterial => this["ColumnMaterial"];
    public string StatusOptimal => this["StatusOptimal"];
    public string StatusBestFound => this["StatusBestFound"];
    public string LabelTotalBars => this["LabelTotalBars"];
    public string LabelTotalPartLength => this["LabelTotalPartLength"];
    public string LabelTotalCutLoss => this["LabelTotalCutLoss"];
    public string LabelTotalRemainder => this["LabelTotalRemainder"];
    public string LabelUtilization => this["LabelUtilization"];
    public string LabelMethod => this["LabelMethod"];
    public string MaterialsWindowTitle => this["MaterialsWindowTitle"];
    public string ErrorTitle => this["ErrorTitle"];
    public string ErrorInvalidInput => this["ErrorInvalidInput"];
    public string MessagePasteFailed => this["MessagePasteFailed"];
    public string ConfirmRemoveMaterial => this["ConfirmRemoveMaterial"];
    public string StatusCalculating => this["StatusCalculating"];
    public string StatusCanceled => this["StatusCanceled"];
    public string StatusBestFoundTimeLimit => this["StatusBestFoundTimeLimit"];
    public string StatusBestFoundPatternLimit => this["StatusBestFoundPatternLimit"];
    public string StatusBestFoundUnknown => this["StatusBestFoundUnknown"];
    public string StatusGeneratingReport => this["StatusGeneratingReport"];
    public string StatusReportSaved => this["StatusReportSaved"];
    public string StatusResultsCopied => this["StatusResultsCopied"];
    public string ErrorResultActionFailed => this["ErrorResultActionFailed"];
    public string StatusProjectSaved => this["StatusProjectSaved"];
    public string StatusProjectLoaded => this["StatusProjectLoaded"];
    public string ErrorMaterialNotAssigned => this["ErrorMaterialNotAssigned"];
    public string ErrorLengthPositive => this["ErrorLengthPositive"];
    public string ErrorQuantityPositive => this["ErrorQuantityPositive"];
    public string ErrorPartDoesNotFit => this["ErrorPartDoesNotFit"];

    private LocalizationService()
    {
        _resources["en"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WindowTitle"] = "N.StLine",
            ["MenuFile"] = "File",
            ["MenuEdit"] = "Edit",
            ["MenuOpenProject"] = "Open project",
            ["MenuSaveProject"] = "Save project",
            ["MenuSettings"] = "Settings",
            ["MenuLanguage"] = "Language",
            ["MenuTheme"] = "Theme",
            ["MenuEnglish"] = "English",
            ["MenuUkrainian"] = "Українська",
            ["MenuThemeLight"] = "Light",
            ["MenuThemeDark"] = "Dark",
            ["MenuHelp"] = "Help",
            ["MenuAbout"] = "About",
            ["AboutWindowTitle"] = "About",
            ["AboutVersion"] = "Version",
            ["AboutAuthor"] = "Author",
            ["AboutContact"] = "Contact",
            ["AboutDescription"] = "N.StLine is a tool designed for cutting optimization of long stock materials, such as pipes, profiles, and structural sections.",
            ["AboutCopyright"] = "© 2026 Nikolay Sukhomlin",
            ["AboutLicense"] = "Licensed under GNU GPL v3 or later",
            ["AboutThirdPartyLibraries"] = "Third-party libraries",
            ["ErrorOpenLicense"] = "Could not open the license page: {0}",
            ["ButtonMaterials"] = "Materials",
            ["ButtonPaste"] = "Paste Excel",
            ["ButtonCalculate"] = "Calculate",
            ["ButtonCancelCalculation"] = "Cancel",
            ["ButtonCopyResults"] = "Copy results",
            ["ButtonExportDocx"] = "Export DOCX",
            ["ButtonGenerateReport"] = "Generate report",
            ["ButtonBrowse"] = "Browse",
            ["ButtonOpenLastReport"] = "Open last report",
            ["ButtonClear"] = "Clear",
            ["ButtonAddRow"] = "Add row",
            ["ButtonDeleteRow"] = "Delete row",
            ["ButtonUndo"] = "Undo",
            ["ButtonClose"] = "Close",
            ["ButtonAdd"] = "Add",
            ["ButtonDelete"] = "Delete",
            ["ButtonCopy"] = "Copy",
            ["ButtonOk"] = "OK",
            ["ButtonLicense"] = "License",
            ["ButtonCancel"] = "Cancel",
            ["ButtonMoveToTask"] = "Add to task >",
            ["ButtonMoveToLibrary"] = "< Move to library",
            ["LabelMaterial"] = "Material",
            ["LabelBarLength"] = "Bar length, mm",
            ["LabelCutWidth"] = "Cut width, mm",
            ["UnitMillimeters"] = "mm",
            ["LabelParts"] = "Parts",
            ["LabelLibrary"] = "Library",
            ["LabelInTask"] = "In task",
            ["LabelTaskMaterials"] = "Task materials",
            ["LabelReportOutputPath"] = "Report folder",
            ["ReportTitle"] = "Cutting report",
            ["ReportStatus"] = "Status",
            ["ReportBarsOne"] = "bar",
            ["ReportBarsFew"] = "bars",
            ["ReportBarsMany"] = "bars",
            ["LabelExactProfile"] = "Calculation mode",
            ["ProfileFast"] = "Fast",
            ["ProfileBalanced"] = "Balanced",
            ["ProfileCustom"] = "Custom",
            ["LabelExactTimeLimit"] = "Exact time limit, s",
            ["LabelExactPatternLimit"] = "Pattern limit",
            ["EmptyNoMaterials"] = "Add a material from the library to begin",
            ["EmptyNoParts"] = "Add parts manually or paste them from Excel",
            ["EmptyNoResult"] = "Calculate the task to see the cutting result",
            ["TooltipExactProfile"] = "Search budget preset. Fast — quick answer for simple tasks. Balanced — default. Custom — enter limits manually.",
            ["TooltipExactTimeLimit"] = "Maximum exact search time in seconds. When reached, the best found plan is returned (BestFound).",
            ["TooltipExactPatternLimit"] = "Maximum number of generated cutting patterns. Limits memory usage; when reached, the search stops (BestFound).",
            ["TooltipAddMaterial"] = "Add material from library",
            ["TooltipRemoveMaterial"] = "Remove material from task",
            ["TooltipActiveMaterial"] = "Use this material for new rows",
            ["ResultWindowTitle"] = "Cutting result",
            ["ButtonOpenResult"] = "Open separately",
            ["StatusResultStale"] = "Input data changed. Recalculate the task.",
            ["LabelSummary"] = "Summary",
            ["LabelPatterns"] = "Cutting schemes",
            ["ColumnCuts"] = "Cuts",
            ["ColumnCutLoss"] = "Cut loss",
            ["ColumnParts"] = "Parts",
            ["ColumnRemainder"] = "Remainder",
            ["ColumnSeq"] = "#",
            ["ColumnName"] = "Name",
            ["ColumnLength"] = "Length, mm",
            ["ColumnCutWidth"] = "Cut width, mm",
            ["ColumnQuantity"] = "Quantity",
            ["ColumnMaterial"] = "Material",
            ["StatusOptimal"] = "Optimal solution",
            ["StatusBestFound"] = "Best found solution",
            ["LabelTotalBars"] = "Bars",
            ["LabelTotalPartLength"] = "Total part length",
            ["LabelTotalCutLoss"] = "Total cut loss",
            ["LabelTotalRemainder"] = "Total remainder",
            ["LabelUtilization"] = "Utilization",
            ["LabelMethod"] = "Method",
            ["MaterialsWindowTitle"] = "Materials",
            ["ErrorTitle"] = "Error",
            ["ErrorInvalidInput"] = "Check the input data and try again.",
            ["MessagePasteFailed"] = "Could not paste data. Make sure it has columns for name, length and quantity.",
            ["ConfirmRemoveMaterial"] = "Remove '{0}' and all its parts?",
            ["StatusCalculating"] = "Calculating...",
            ["StatusCanceled"] = "Calculation canceled.",
            ["StatusBestFoundTimeLimit"] = "Best found: exact search reached time limit.",
            ["StatusBestFoundPatternLimit"] = "Best found: exact search reached pattern limit.",
            ["StatusBestFoundUnknown"] = "Best found: exact optimality was not proven.",
            ["StatusGeneratingReport"] = "Generating report...",
            ["StatusReportSaved"] = "Report saved: {0}",
            ["StatusResultsCopied"] = "Results copied to clipboard.",
            ["ErrorResultActionFailed"] = "Result action failed: {0}",
            ["StatusProjectSaved"] = "Project saved: {0}",
            ["StatusProjectLoaded"] = "Project loaded: {0}",
            ["ErrorMaterialNotAssigned"] = "Material is not assigned.",
            ["ErrorLengthPositive"] = "Length must be greater than zero.",
            ["ErrorQuantityPositive"] = "Quantity must be greater than zero.",
            ["ErrorPartDoesNotFit"] = "Part does not fit into the bar."
        };

        _resources["ua"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WindowTitle"] = "N.StLine",
            ["MenuFile"] = "Файл",
            ["MenuEdit"] = "Правка",
            ["MenuOpenProject"] = "Відкрити проект",
            ["MenuSaveProject"] = "Зберегти проект",
            ["MenuSettings"] = "Налаштування",
            ["MenuLanguage"] = "Мова",
            ["MenuTheme"] = "Тема",
            ["MenuEnglish"] = "English",
            ["MenuUkrainian"] = "Українська",
            ["MenuThemeLight"] = "Світла",
            ["MenuThemeDark"] = "Темна",
            ["MenuHelp"] = "Довідка",
            ["MenuAbout"] = "Про програму",
            ["AboutWindowTitle"] = "Про програму",
            ["AboutVersion"] = "Версія",
            ["AboutAuthor"] = "Автор",
            ["AboutContact"] = "Контакт",
            ["AboutDescription"] = "N.StLine — інструмент для оптимізації розкрою довгомірних матеріалів: труб, профілів та конструкційних елементів.",
            ["AboutCopyright"] = "© 2026 Nikolay Sukhomlin",
            ["AboutLicense"] = "Ліцензовано за GNU GPL v3 або новішою версією",
            ["AboutThirdPartyLibraries"] = "Сторонні бібліотеки",
            ["ErrorOpenLicense"] = "Не вдалося відкрити сторінку ліцензії: {0}",
            ["ButtonMaterials"] = "Матеріали",
            ["ButtonPaste"] = "Вставити Excel",
            ["ButtonCalculate"] = "Розрахувати",
            ["ButtonCancelCalculation"] = "Скасувати",
            ["ButtonCopyResults"] = "Копіювати результати",
            ["ButtonExportDocx"] = "Експорт DOCX",
            ["ButtonGenerateReport"] = "Сформувати звіт",
            ["ButtonBrowse"] = "Огляд",
            ["ButtonOpenLastReport"] = "Відкрити останній звіт",
            ["ButtonClear"] = "Очистити",
            ["ButtonAddRow"] = "Додати рядок",
            ["ButtonDeleteRow"] = "Видалити рядок",
            ["ButtonUndo"] = "Скасувати дію",
            ["ButtonClose"] = "Закрити",
            ["ButtonAdd"] = "Додати",
            ["ButtonDelete"] = "Видалити",
            ["ButtonCopy"] = "Копіювати",
            ["ButtonOk"] = "OK",
            ["ButtonLicense"] = "Ліцензія",
            ["ButtonCancel"] = "Скасувати",
            ["ButtonMoveToTask"] = "Додати в задачу >",
            ["ButtonMoveToLibrary"] = "< Перенести в бібліотеку",
            ["LabelMaterial"] = "Матеріал",
            ["LabelBarLength"] = "Довжина заготовки, мм",
            ["LabelCutWidth"] = "Ширина різу, мм",
            ["UnitMillimeters"] = "мм",
            ["LabelParts"] = "Деталі",
            ["LabelLibrary"] = "Бібліотека",
            ["LabelInTask"] = "У задачі",
            ["LabelTaskMaterials"] = "Матеріали задачі",
            ["LabelReportOutputPath"] = "Папка звіту",
            ["ReportTitle"] = "Звіт з розкрою",
            ["ReportStatus"] = "Статус",
            ["ReportBarsOne"] = "заготовка",
            ["ReportBarsFew"] = "заготовки",
            ["ReportBarsMany"] = "заготовок",
            ["LabelExactProfile"] = "Режим розрахунку",
            ["ProfileFast"] = "Швидкий",
            ["ProfileBalanced"] = "Збалансований",
            ["ProfileCustom"] = "Власний",
            ["LabelExactTimeLimit"] = "Ліміт часу exact, с",
            ["LabelExactPatternLimit"] = "Ліміт патернів",
            ["EmptyNoMaterials"] = "Додайте матеріал з бібліотеки, щоб почати",
            ["EmptyNoParts"] = "Додайте деталі вручну або вставте їх з Excel",
            ["EmptyNoResult"] = "Виконайте розрахунок, щоб побачити результат розкрою",
            ["TooltipExactProfile"] = "Бюджет точного пошуку. Швидкий — швидка відповідь для простих задач. Збалансований — за замовчуванням. Власний — ліміти вручну.",
            ["TooltipExactTimeLimit"] = "Максимальний час exact-пошуку в секундах. Після його досягнення повертається найкращий знайдений план (BestFound).",
            ["TooltipExactPatternLimit"] = "Максимальна кількість згенерованих схем розкрою. Обмежує пам'ять; після досягнення пошук зупиняється (BestFound).",
            ["TooltipAddMaterial"] = "Додати матеріал з бібліотеки",
            ["TooltipRemoveMaterial"] = "Видалити матеріал із задачі",
            ["TooltipActiveMaterial"] = "Використовувати цей матеріал для нових рядків",
            ["ResultWindowTitle"] = "Результат розкрою",
            ["ButtonOpenResult"] = "Відкрити окремо",
            ["StatusResultStale"] = "Вхідні дані змінено. Виконайте розрахунок повторно.",
            ["LabelSummary"] = "Підсумок",
            ["LabelPatterns"] = "Схеми розкрою",
            ["ColumnCuts"] = "Різів",
            ["ColumnCutLoss"] = "Втрати на різ",
            ["ColumnParts"] = "Деталі",
            ["ColumnRemainder"] = "Залишок",
            ["ColumnSeq"] = "№",
            ["ColumnName"] = "Назва",
            ["ColumnLength"] = "Довжина, мм",
            ["ColumnCutWidth"] = "Ширина різу, мм",
            ["ColumnQuantity"] = "Кількість",
            ["ColumnMaterial"] = "Матеріал",
            ["StatusOptimal"] = "Оптимальне рішення",
            ["StatusBestFound"] = "Найкращий знайдений варіант",
            ["LabelTotalBars"] = "Заготовок",
            ["LabelTotalPartLength"] = "Загальна довжина деталей",
            ["LabelTotalCutLoss"] = "Втрати на різ",
            ["LabelTotalRemainder"] = "Загальний залишок",
            ["LabelUtilization"] = "Використання",
            ["LabelMethod"] = "Метод",
            ["MaterialsWindowTitle"] = "Матеріали",
            ["ErrorTitle"] = "Помилка",
            ["ErrorInvalidInput"] = "Перевірте вхідні дані та спробуйте ще раз.",
            ["MessagePasteFailed"] = "Не вдалося вставити дані. Переконайтеся, що є стовпці назви, довжини та кількості.",
            ["ConfirmRemoveMaterial"] = "Видалити '{0}' та всі його деталі?",
            ["StatusCalculating"] = "Виконується розрахунок...",
            ["StatusCanceled"] = "Розрахунок скасовано.",
            ["StatusBestFoundTimeLimit"] = "Найкращий знайдений варіант: exact-пошук досяг ліміту часу.",
            ["StatusBestFoundPatternLimit"] = "Найкращий знайдений варіант: exact-пошук досяг ліміту патернів.",
            ["StatusBestFoundUnknown"] = "Найкращий знайдений варіант: оптимальність exact не доведено.",
            ["StatusGeneratingReport"] = "Формується звіт...",
            ["StatusReportSaved"] = "Звіт збережено: {0}",
            ["StatusResultsCopied"] = "Результати скопійовано в буфер обміну.",
            ["ErrorResultActionFailed"] = "Не вдалося виконати дію з результатом: {0}",
            ["StatusProjectSaved"] = "Проект збережено: {0}",
            ["StatusProjectLoaded"] = "Проект завантажено: {0}",
            ["ErrorMaterialNotAssigned"] = "Матеріал не призначено.",
            ["ErrorLengthPositive"] = "Довжина має бути більшою за нуль.",
            ["ErrorQuantityPositive"] = "Кількість має бути більшою за нуль.",
            ["ErrorPartDoesNotFit"] = "Деталь не поміщається в заготовку."
        };
    }
}
