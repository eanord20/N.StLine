using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NStLine.App.Commands;
using NStLine.App.Services;
using NStLine.Core.Models;

namespace NStLine.App.ViewModels;

public sealed class MaterialsViewModel : INotifyPropertyChanged
{
    private LinearMaterial? _selectedMaterial;

    public ObservableCollection<LinearMaterial> Materials { get; } = new();

    public LinearMaterial? SelectedMaterial
    {
        get => _selectedMaterial;
        set
        {
            _selectedMaterial = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }

    public bool? DialogResult { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MaterialsViewModel(IEnumerable<LinearMaterial> materials)
    {
        foreach (var material in materials)
            Materials.Add(Clone(material));

        AddCommand = new RelayCommand(_ => Add());
        DeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedMaterial != null);
        CopyCommand = new RelayCommand(_ => Copy(), _ => SelectedMaterial != null);
        OkCommand = new RelayCommand(_ => Close(true));
        CancelCommand = new RelayCommand(_ => Close(false));
    }

    public (int Added, int Skipped) ImportMaterials(IEnumerable<LinearMaterial> materials)
    {
        var incoming = materials.ToList();
        MaterialsLibraryService.Validate(incoming);
        var added = new List<LinearMaterial>();
        var keys = Materials.Select(MaterialKey).ToHashSet();
        var ids = Materials.Select(material => material.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var source in incoming)
        {
            if (!keys.Add(MaterialKey(source)))
                continue;
            var material = Clone(source);
            material.Name = material.Name.Trim();
            if (string.IsNullOrWhiteSpace(material.Id) || !ids.Add(material.Id))
            {
                do material.Id = Guid.NewGuid().ToString("N");
                while (!ids.Add(material.Id));
            }
            added.Add(material);
        }
        foreach (var material in added)
            Materials.Add(material);
        return (added.Count, incoming.Count - added.Count);
    }

    private static (string Name, double Length, double CutWidth) MaterialKey(LinearMaterial material)
        => (material.Name.Trim().ToUpperInvariant(), material.StandardLength, material.CutWidth);

    private void Add()
    {
        Materials.Add(new LinearMaterial
        {
            Name = "New material",
            StandardLength = 6000,
            CutWidth = 3
        });
    }

    private void Delete()
    {
        if (SelectedMaterial != null)
            Materials.Remove(SelectedMaterial);
    }

    private void Copy()
    {
        if (SelectedMaterial == null)
            return;

        var clone = Clone(SelectedMaterial);
        clone.Id = Guid.NewGuid().ToString("N");
        clone.Name = $"{clone.Name} copy";
        Materials.Add(clone);
    }

    private void Close(bool result)
    {
        DialogResult = result;
        OnPropertyChanged(nameof(DialogResult));
    }

    private static LinearMaterial Clone(LinearMaterial source)
    {
        return new LinearMaterial
        {
            Id = source.Id,
            Name = source.Name,
            StandardLength = source.StandardLength,
            CutWidth = source.CutWidth
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
