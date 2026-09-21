using NStLine.App.Models;

namespace NStLine.App.Services;

public sealed class PartRowsUndoHistory
{
    private readonly int _capacity;
    private readonly LinkedList<PartRowsSnapshot> _snapshots = new();

    public PartRowsUndoHistory(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;
    }

    public bool CanUndo => _snapshots.Count > 0;

    public void Push(IEnumerable<PartRow> rows, string? activeMaterialId)
    {
        while (_snapshots.Count >= _capacity)
            _snapshots.RemoveFirst();

        _snapshots.AddLast(new PartRowsSnapshot(rows.Select(Clone).ToList(), activeMaterialId));
    }

    public bool TryPop(out PartRowsSnapshot? snapshot)
    {
        if (_snapshots.Last == null)
        {
            snapshot = null;
            return false;
        }

        snapshot = _snapshots.Last.Value;
        _snapshots.RemoveLast();
        return true;
    }

    public void Clear()
    {
        _snapshots.Clear();
    }

    private static PartRow Clone(PartRow row)
        => new()
        {
            RowNumber = row.RowNumber,
            MaterialId = row.MaterialId,
            Name = row.Name,
            Length = row.Length,
            Quantity = row.Quantity
        };
}

public sealed record PartRowsSnapshot(IReadOnlyList<PartRow> Rows, string? ActiveMaterialId);
