using System.ComponentModel;
using System.Runtime.CompilerServices;
using NStLine.Core.Models;

namespace NStLine.App.Models;

public sealed class PartRow : INotifyPropertyChanged
{
    private int _rowNumber;
    private string _materialId = string.Empty;
    private string _name = string.Empty;
    private double _length;
    private int _quantity = 1;
    private string? _error;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int RowNumber
    {
        get => _rowNumber;
        set { _rowNumber = value; OnPropertyChanged(); }
    }

    public string MaterialId
    {
        get => _materialId;
        set { _materialId = value ?? string.Empty; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value ?? string.Empty; ClearError(); OnPropertyChanged(); }
    }

    public double Length
    {
        get => _length;
        set { _length = value; ClearError(); OnPropertyChanged(); }
    }

    public int Quantity
    {
        get => _quantity;
        set { _quantity = value; ClearError(); OnPropertyChanged(); }
    }

    public string? Error
    {
        get => _error;
        private set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(_error);

    public void SetError(string message)
    {
        Error = message;
    }

    public void ClearError()
    {
        Error = null;
    }

    public PartRequirement ToPartRequirement()
    {
        return new PartRequirement
        {
            MaterialId = MaterialId,
            Name = Name,
            Length = Length,
            Quantity = Quantity
        };
    }

    public static PartRow FromPartRequirement(PartRequirement part, int rowNumber)
    {
        return new PartRow
        {
            MaterialId = part.MaterialId,
            Name = part.Name,
            Length = part.Length,
            Quantity = part.Quantity,
            RowNumber = rowNumber
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
