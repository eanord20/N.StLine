using System.IO;
using NStLine.App.Services;
using NStLine.App.ViewModels;
using NStLine.Core.Models;

namespace NStLine.App.Tests;

public sealed class MaterialsTransferTests
{
    [Fact]
    public void Import_AddsWithoutDuplicatesAndPreservesExistingIds()
    {
        var original = Material("same", "Tube");
        var viewModel = new MaterialsViewModel([original]);
        var incoming = Material("same", "Angle");

        var counts = viewModel.ImportMaterials([Material("other", " tube "), incoming, incoming]);

        Assert.Equal((1, 2), counts);
        Assert.Equal(2, viewModel.Materials.Count);
        Assert.Equal("same", viewModel.Materials[0].Id);
        Assert.NotEqual("same", viewModel.Materials[1].Id);
        Assert.Equal("same", incoming.Id);
        Assert.Equal((0, 1), viewModel.ImportMaterials([incoming]));
        Assert.Equal("Tube", original.Name);
    }

    [Fact]
    public void Import_InvalidLastEntryDoesNotPartiallyChangeCollection()
    {
        var viewModel = new MaterialsViewModel([Material("one", "Tube")]);
        var invalid = Material("bad", "Bad");
        invalid.StandardLength = double.NaN;

        Assert.Throws<InvalidDataException>(() => viewModel.ImportMaterials([Material("two", "Angle"), invalid]));

        Assert.Single(viewModel.Materials);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("")]
    [InlineData("{}")]
    public void ImportFile_RejectsInvalidLibrary(string json)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            Assert.ThrowsAny<Exception>(() => MaterialsLibraryService.LoadForImport(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExportImport_RoundTripsCurrentLibrary()
    {
        var path = Path.GetTempFileName();
        try
        {
            var expected = Material("one", "Швеллер 12П");
            new MaterialsLibraryService(path).SaveAll([expected]);

            var actual = Assert.Single(MaterialsLibraryService.LoadForImport(path));

            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.StandardLength, actual.StandardLength);
            Assert.Equal(expected.CutWidth, actual.CutWidth);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static LinearMaterial Material(string id, string name)
        => new() { Id = id, Name = name, StandardLength = 6000, CutWidth = 3 };
}
