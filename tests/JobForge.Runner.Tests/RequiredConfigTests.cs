using JobForge.Runner.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace JobForge.Runner.Tests;

public class RequiredConfigTests
{
    [Fact]
    public void Ensure_throws_listing_missing_keys()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["A"] = "x" })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RequiredConfig.Ensure(config, "A", "B", "C"));

        Assert.Contains("B", ex.Message);
        Assert.Contains("C", ex.Message);
        Assert.DoesNotContain("A,", ex.Message);
    }

    [Fact]
    public void Ensure_passes_when_all_present()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["A"] = "x", ["B"] = "y", ["C"] = "z"
            })
            .Build();

        RequiredConfig.Ensure(config, "A", "B", "C");
    }

    [Fact]
    public void Ensure_treats_whitespace_as_missing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["A"] = "  " })
            .Build();

        Assert.Throws<InvalidOperationException>(() => RequiredConfig.Ensure(config, "A"));
    }
}
