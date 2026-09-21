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
    public string ButtonMaterials => this["ButtonMaterials"];
    public string ButtonPaste => this["ButtonPaste"];
    public string ButtonCalculate => this["ButtonCalculate"];
    public string ButtonCopyResults => this["ButtonCopyResults"];
    public string ButtonExportDocx => this["ButtonExportDocx"];
    public string ButtonClear => this["ButtonClear"];
    public string ButtonAddRow => this["ButtonAddRow"];
    public string ButtonDeleteRow => this["ButtonDeleteRow"];
    public string ButtonUndo => this["ButtonUndo"];
    public string ButtonClose => this["ButtonClose"];
    public string ButtonAdd => this["ButtonAdd"];
    public string ButtonDelete => this["ButtonDelete"];
    public string ButtonCopy => this["ButtonCopy"];
    public string ButtonOk => this["ButtonOk"];
    public string ButtonCancel => this["ButtonCancel"];
    public string ButtonMoveToTask => this["ButtonMoveToTask"];
    public string ButtonMoveToLibrary => this["ButtonMoveToLibrary"];
    public string LabelMaterial => this["LabelMaterial"];
    public string LabelBarLength => this["LabelBarLength"];
    public string LabelCutWidth => this["LabelCutWidth"];
    public string LabelParts => this["LabelParts"];
    public string LabelLibrary => this["LabelLibrary"];
    public string LabelInTask => this["LabelInTask"];
    public string LabelTaskMaterials => this["LabelTaskMaterials"];
    public string EmptyNoMaterials => this["EmptyNoMaterials"];
    public string EmptyNoParts => this["EmptyNoParts"];
    public string TooltipAddMaterial => this["TooltipAddMaterial"];
    public string TooltipRemoveMaterial => this["TooltipRemoveMaterial"];
    public string ResultWindowTitle => this["ResultWindowTitle"];
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
            ["ButtonMaterials"] = "Materials",
            ["ButtonPaste"] = "Paste Excel",
            ["ButtonCalculate"] = "Calculate",
            ["ButtonCopyResults"] = "Copy results",
            ["ButtonExportDocx"] = "Export DOCX",
            ["ButtonClear"] = "Clear",
            ["ButtonAddRow"] = "Add row",
            ["ButtonDeleteRow"] = "Delete row",
            ["ButtonUndo"] = "Undo",
            ["ButtonClose"] = "Close",
            ["ButtonAdd"] = "Add",
            ["ButtonDelete"] = "Delete",
            ["ButtonCopy"] = "Copy",
            ["ButtonOk"] = "OK",
            ["ButtonCancel"] = "Cancel",
            ["ButtonMoveToTask"] = "Add to task >",
            ["ButtonMoveToLibrary"] = "< Move to library",
            ["LabelMaterial"] = "Material",
            ["LabelBarLength"] = "Bar length, mm",
            ["LabelCutWidth"] = "Cut width, mm",
            ["LabelParts"] = "Parts",
            ["LabelLibrary"] = "Library",
            ["LabelInTask"] = "In task",
            ["LabelTaskMaterials"] = "Task materials",
            ["EmptyNoMaterials"] = "Add a material from the library to begin",
            ["EmptyNoParts"] = "Add parts manually or paste them from Excel",
            ["TooltipAddMaterial"] = "Add material from library",
            ["TooltipRemoveMaterial"] = "Remove material from task",
            ["ResultWindowTitle"] = "Cutting result",
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
            ["ButtonMaterials"] = "Матеріали",
            ["ButtonPaste"] = "Вставити Excel",
            ["ButtonCalculate"] = "Розрахувати",
            ["ButtonCopyResults"] = "Копіювати результати",
            ["ButtonExportDocx"] = "Експорт DOCX",
            ["ButtonClear"] = "Очистити",
            ["ButtonAddRow"] = "Додати рядок",
            ["ButtonDeleteRow"] = "Видалити рядок",
            ["ButtonUndo"] = "Скасувати дію",
            ["ButtonClose"] = "Закрити",
            ["ButtonAdd"] = "Додати",
            ["ButtonDelete"] = "Видалити",
            ["ButtonCopy"] = "Копіювати",
            ["ButtonOk"] = "OK",
            ["ButtonCancel"] = "Скасувати",
            ["ButtonMoveToTask"] = "Додати в задачу >",
            ["ButtonMoveToLibrary"] = "< Перенести в бібліотеку",
            ["LabelMaterial"] = "Матеріал",
            ["LabelBarLength"] = "Довжина заготовки, мм",
            ["LabelCutWidth"] = "Ширина різу, мм",
            ["LabelParts"] = "Деталі",
            ["LabelLibrary"] = "Бібліотека",
            ["LabelInTask"] = "У задачі",
            ["LabelTaskMaterials"] = "Матеріали задачі",
            ["EmptyNoMaterials"] = "Додайте матеріал з бібліотеки, щоб почати",
            ["EmptyNoParts"] = "Додайте деталі вручну або вставте їх з Excel",
            ["TooltipAddMaterial"] = "Додати матеріал з бібліотеки",
            ["TooltipRemoveMaterial"] = "Видалити матеріал із задачі",
            ["ResultWindowTitle"] = "Результат розкрою",
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
            ["StatusProjectSaved"] = "Проект збережено: {0}",
            ["StatusProjectLoaded"] = "Проект завантажено: {0}",
            ["ErrorMaterialNotAssigned"] = "Матеріал не призначено.",
            ["ErrorLengthPositive"] = "Довжина має бути більшою за нуль.",
            ["ErrorQuantityPositive"] = "Кількість має бути більшою за нуль.",
            ["ErrorPartDoesNotFit"] = "Деталь не поміщається в заготовку."
        };
    }
}
