using System;
using System.Reflection;
using Xunit;

public class HarnessTests
{
    [Fact]
    public void RunHarness()
    {
        var main = typeof(Program).GetMethod("<Main>$", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(main);
        main!.Invoke(null, new object[] { Array.Empty<string>() });
    }
}
