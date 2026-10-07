namespace NStLine.Core.Models;

public enum BestFoundReason
{
    None = 0,
    TimeLimitReached = 1,
    PatternLimitReached = 2,
    Cancelled = 3,
    ExactNotCompleted = 4
}
