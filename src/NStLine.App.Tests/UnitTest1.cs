using System.IO;
using System.Text.Json;
using NStLine.App.Models;
using NStLine.App.Services;
using NStLine.Core.Models;

namespace NStLine.App.Tests;

public sealed class ProjectFileServiceTests
{
    [Fact]
    public void SaveAndLoad_PreservesMultipleMaterialsAndAssignments()
    {
        var path = Path.GetTempFileName();
        try
        {
            var first = new LinearMaterial { Id = "first", Name = "Tube", StandardLength = 6000, CutWidth = 3 };
            var second = new LinearMaterial { Id = "second", Name = "Angle", StandardLength = 3000, CutWidth = 2 };
            var data = new ProjectData
            {
                Materials = [first, second],
                Parts =
                [
                    new PartRequirement { Name = "A", Length = 1000, Quantity = 2, MaterialId = first.Id },
                    new PartRequirement { Name = "B", Length = 700, Quantity = 3, MaterialId = second.Id }
                ]
            };
            var service = new ProjectFileService();

            service.Save(path, data);
            var loaded = service.Load(path);

            Assert.Equal(2, loaded.Materials.Count);
            Assert.Equal("first", loaded.Parts[0].MaterialId);
            Assert.Equal("second", loaded.Parts[1].MaterialId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_PreservesLegacyZeroCutWidth()
    {
        var path = Path.GetTempFileName();
        try
        {
            var legacy = new
            {
                Material = new LinearMaterial { Id = "legacy", Name = "Tube", StandardLength = 6000, CutWidth = 3 },
                BarLength = 6000,
                CutWidth = 0,
                Parts = new[] { new PartRequirement { Name = "A", Length = 1000, Quantity = 1 } }
            };
            File.WriteAllText(path, JsonSerializer.Serialize(legacy));

            var loaded = new ProjectFileService().Load(path);

            Assert.Equal(0, Assert.Single(loaded.Materials).CutWidth);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MigratesLegacySingleMaterialProject()
    {
        var path = Path.GetTempFileName();
        try
        {
            var legacy = new
            {
                Material = new LinearMaterial { Id = "legacy", Name = "Tube", StandardLength = 6000, CutWidth = 3 },
                BarLength = 6100,
                CutWidth = 4,
                Parts = new[] { new PartRequirement { Name = "A", Length = 1000, Quantity = 2 } }
            };
            File.WriteAllText(path, JsonSerializer.Serialize(legacy));

            var loaded = new ProjectFileService().Load(path);

            var material = Assert.Single(loaded.Materials);
            Assert.Equal("legacy", material.Id);
            Assert.Equal(6100, material.StandardLength);
            Assert.Equal(4, material.CutWidth);
            Assert.Equal("legacy", Assert.Single(loaded.Parts).MaterialId);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
