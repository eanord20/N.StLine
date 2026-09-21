using NStLine.App.Models;
using NStLine.App.Services;

namespace NStLine.App.Tests;

public sealed class PartRowsUndoHistoryTests
{
    [Fact]
    public void TryPop_ReturnsDeepSnapshotAndActiveMaterial()
    {
        var row = Row("first", "Part 1", 1000, 2);
        var history = new PartRowsUndoHistory(10);
        history.Push([row], "first");
        row.Name = "Changed";
        row.Length = 2000;

        var restored = AssertSnapshot(history);

        Assert.Equal("first", restored.ActiveMaterialId);
        var restoredRow = Assert.Single(restored.Rows);
        Assert.Equal("Part 1", restoredRow.Name);
        Assert.Equal(1000, restoredRow.Length);
        Assert.Equal(2, restoredRow.Quantity);
        Assert.Equal("first", restoredRow.MaterialId);
    }

    [Fact]
    public void Push_DiscardsOldestSnapshotAtCapacity()
    {
        var history = new PartRowsUndoHistory(2);
        history.Push([Row("m", "First", 100, 1)], "m");
        history.Push([Row("m", "Second", 200, 1)], "m");
        history.Push([Row("m", "Third", 300, 1)], "m");

        Assert.Equal("Third", Assert.Single(AssertSnapshot(history).Rows).Name);
        Assert.Equal("Second", Assert.Single(AssertSnapshot(history).Rows).Name);
        Assert.False(history.TryPop(out _));
    }

    [Fact]
    public void Clear_RemovesAllSnapshots()
    {
        var history = new PartRowsUndoHistory(10);
        history.Push([Row("m", "Part", 100, 1)], "m");

        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.TryPop(out _));
    }

    private static PartRowsSnapshot AssertSnapshot(PartRowsUndoHistory history)
    {
        Assert.True(history.TryPop(out var snapshot));
        return snapshot!;
    }

    private static PartRow Row(string materialId, string name, double length, int quantity)
        => new() { MaterialId = materialId, Name = name, Length = length, Quantity = quantity };
}
