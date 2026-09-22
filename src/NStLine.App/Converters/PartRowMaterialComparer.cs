using System.Collections;
using NStLine.App.Models;

namespace NStLine.App.Converters;

internal sealed class PartRowMaterialComparer : IComparer
{
    private readonly Func<string, int> _getMaterialOrder;

    public PartRowMaterialComparer(Func<string, int> getMaterialOrder)
    {
        _getMaterialOrder = getMaterialOrder;
    }

    public int Compare(object? x, object? y)
    {
        if (x is not PartRow first || y is not PartRow second)
            return 0;

        var materialComparison = _getMaterialOrder(first.MaterialId).CompareTo(_getMaterialOrder(second.MaterialId));
        return materialComparison != 0 ? materialComparison : first.RowNumber.CompareTo(second.RowNumber);
    }
}
