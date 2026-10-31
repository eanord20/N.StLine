namespace NStLine.App.Models;

public enum ReportTimestampPosition { None, Prefix, Suffix }
public enum ReportPartSeparator { Plus, Pipe, Dash, Asterisk, Space, Comma, Dot }

public sealed record ReportSettings
{
    public string FileName { get; set; } = string.Empty;
    public ReportTimestampPosition TimestampPosition { get; set; } = ReportTimestampPosition.Suffix;
    public bool FileNameIncludeDate { get; set; } = true;
    public bool FileNameIncludeTime { get; set; } = true;
    public string Title { get; set; } = string.Empty;
    public bool TitleIncludeDate { get; set; }
    public bool TitleIncludeTime { get; set; }
    public bool ShowSummary { get; set; } = true;
    public ReportPartSeparator PartSeparator { get; set; }
    public bool IncludeMaterial { get; set; }
    public bool ShowPartNames { get; set; } = true;

    [System.Text.Json.Serialization.JsonIgnore]
    public string SeparatorText => PartSeparator switch
    {
        ReportPartSeparator.Pipe => " | ",
        ReportPartSeparator.Dash => " - ",
        ReportPartSeparator.Asterisk => " * ",
        ReportPartSeparator.Space => " ",
        ReportPartSeparator.Comma => ", ",
        ReportPartSeparator.Dot => ". ",
        _ => " + "
    };
}

public sealed record ReportGenerationOptions(ReportSettings Settings, DateTime GeneratedAt, string Culture);
