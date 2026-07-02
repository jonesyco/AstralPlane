using System.Collections.Concurrent;
using AstralPlane.Core;

namespace AstralPlane.Core.Tests;

public class BatchRunnerTests
{
    // A fake converter so orchestration is tested without real image I/O.
    private sealed class FakeConverter : IItemConverter
    {
        private readonly Action<string>? _onConvert;
        public FakeConverter(Action<string>? onConvert = null) => _onConvert = onConvert;
        public ConcurrentBag<string> Converted { get; } = new();
        public void Convert(string source, string output, ConversionOptions options)
        {
            _onConvert?.Invoke(source);
            Converted.Add(source);
        }
    }

    private sealed class CollectingProgress : IProgress<BatchProgress>
    {
        private readonly object _lock = new();
        public List<BatchProgress> Reports { get; } = new();
        public void Report(BatchProgress value) { lock (_lock) Reports.Add(value); }
    }

    private static OutputPathPlanner Planner() =>
        new(OutputLocationMode.ChosenFolder, @"D:\out", TimeProvider.System, _ => false);

    private static readonly ConversionOptions Options = ConversionOptions.For(OutputFormat.Jpg);

    [Fact]
    public async Task ConvertsEveryItemOnSuccess()
    {
        var converter = new FakeConverter();
        var runner = new BatchRunner(converter);
        string[] sources = [@"C:\a.png", @"C:\b.png", @"C:\c.png"];

        BatchResult result = await runner.RunAsync(sources, Options, Planner(), progress: null, CancellationToken.None);

        Assert.Equal(3, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.Equal(3, converter.Converted.Count);
    }

    [Fact]
    public async Task IsolatesFailures_OneCorruptFileYieldsExactlyOneFailure()
    {
        var converter = new FakeConverter(src =>
        {
            if (src.EndsWith("bad.png")) throw new InvalidOperationException("corrupt file");
        });
        var runner = new BatchRunner(converter);
        string[] sources = [@"C:\good1.png", @"C:\bad.png", @"C:\good2.png"];

        BatchResult result = await runner.RunAsync(sources, Options, Planner(), progress: null, CancellationToken.None);

        Assert.Equal(2, result.Succeeded);
        Assert.Equal(1, result.Failed);
        var failure = result.Items.Single(i => i.Status == ItemStatus.Failed);
        Assert.EndsWith("bad.png", failure.SourcePath);
        Assert.Contains("corrupt", failure.Error);
    }

    [Fact]
    public async Task ReportsProgressForEachItem()
    {
        var runner = new BatchRunner(new FakeConverter());
        var progress = new CollectingProgress();
        string[] sources = [@"C:\a.png", @"C:\b.png", @"C:\c.png"];

        BatchResult result = await runner.RunAsync(sources, Options, Planner(), progress, CancellationToken.None);

        Assert.Equal(3, progress.Reports.Count);
        Assert.All(progress.Reports, r => Assert.Equal(3, r.Total));
        Assert.Equal(3, progress.Reports.Max(r => r.Completed));
    }

    [Fact]
    public async Task CancellationStopsFurtherItems()
    {
        using var cts = new CancellationTokenSource();
        var converter = new FakeConverter(_ => cts.Cancel()); // cancel on the first item
        var runner = new BatchRunner(converter, maxParallelism: 1);
        string[] sources = Enumerable.Range(0, 100).Select(i => $@"C:\f{i}.png").ToArray();

        BatchResult result = await runner.RunAsync(sources, Options, Planner(), progress: null, cts.Token);

        Assert.True(converter.Converted.Count < sources.Length, "cancellation should stop further items");
        Assert.True(result.Skipped > 0, "unprocessed items should be marked Skipped");
    }
}
