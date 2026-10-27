using BioComp.Lab;
using BioComp.Meta;
using Xunit;

namespace BioCore.Tests;

public class WonderTests : IDisposable
{
    private readonly string _reports = Path.Combine(Path.GetTempPath(), $"biowon-{Guid.NewGuid():N}");
    private readonly string _exports = Path.Combine(Path.GetTempPath(), $"biowonx-{Guid.NewGuid():N}");

    [Fact]
    public async Task MockAgent_RunsFullProtocol_AndWritesReport()
    {
        var agent = new WonderAgent(new MockLanguageModel());
        var report = await agent.RunAsync("state of the brain", outDir: _reports, exportDir: _exports);

        Assert.True(File.Exists(report.Path), "report file missing");
        string md = File.ReadAllText(report.Path);
        Assert.Contains("criticality", md);
        Assert.Contains("mmn", md);
        Assert.Contains("## Summary", md);
        Assert.True(report.Figures.Length >= 2);
        Assert.True(File.Exists(Path.Combine(_exports, "criticality.svg")));
        Assert.True(agent.Trace.Count > 0);
    }

    [Fact]
    public async Task AgentTrace_ShowsFallbackProtocol()
    {
        var agent = new WonderAgent(new MockLanguageModel());
        await agent.RunAsync("test goal", outDir: _reports, exportDir: _exports);
        Assert.Contains(agent.Trace, t => t.Contains("fixed tool battery") || t.Contains("tool call"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_reports, true); } catch { }
        try { Directory.Delete(_exports, true); } catch { }
    }
}
