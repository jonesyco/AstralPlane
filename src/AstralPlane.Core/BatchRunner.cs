namespace AstralPlane.Core;

/// <summary>Converts one already-planned item. Implemented by <see cref="ConversionEngine"/>.</summary>
public interface IItemConverter
{
    void Convert(string source, string output, ConversionOptions options);
}

/// <summary>Outcome of a single item in a batch.</summary>
public enum ItemStatus
{
    Done,
    Failed,
    Skipped,
}

/// <summary>Result for one processed item.</summary>
public sealed record BatchItemResult(string SourcePath, string? OutputPath, ItemStatus Status, string? Error);

/// <summary>Progress notification after each item completes.</summary>
public sealed record BatchProgress(int Completed, int Total, BatchItemResult Last);

/// <summary>Aggregate result of a batch run.</summary>
public sealed record BatchResult(IReadOnlyList<BatchItemResult> Items)
{
    public int Succeeded => Items.Count(i => i.Status == ItemStatus.Done);
    public int Failed => Items.Count(i => i.Status == ItemStatus.Failed);
    public int Skipped => Items.Count(i => i.Status == ItemStatus.Skipped);
}

/// <summary>
/// Runs a conversion job over many items with bounded parallelism, per-file
/// failure isolation, progress reporting, and cancellation. Output paths are
/// planned sequentially up front (the planner is not thread-safe); conversions
/// then run in parallel.
/// </summary>
public sealed class BatchRunner(IItemConverter converter, int? maxParallelism = null)
{
    private readonly IItemConverter _converter = converter;
    private readonly int _maxParallelism = maxParallelism ?? Environment.ProcessorCount;

    public async Task<BatchResult> RunAsync(
        IReadOnlyList<string> sources,
        ConversionOptions options,
        OutputPathPlanner planner,
        IProgress<BatchProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Plan destinations sequentially (planner mutates shared state).
        var plan = sources
            .Select(src => (Source: src, Output: planner.Plan(src, options.TargetFormat).FullPath))
            .ToArray();

        var results = new BatchItemResult?[plan.Length];
        int completed = 0;

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _maxParallelism };

        try
        {
            await Parallel.ForEachAsync(Enumerable.Range(0, plan.Length), parallelOptions, (index, token) =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return ValueTask.CompletedTask;

                (string source, string output) = plan[index];
                BatchItemResult result;
                try
                {
                    _converter.Convert(source, output, options);
                    result = new BatchItemResult(source, output, ItemStatus.Done, null);
                }
                catch (Exception ex)
                {
                    result = new BatchItemResult(source, output, ItemStatus.Failed, ex.Message);
                }

                results[index] = result;
                int done = Interlocked.Increment(ref completed);
                progress?.Report(new BatchProgress(done, plan.Length, result));
                return ValueTask.CompletedTask;
            });
        }
        catch (OperationCanceledException)
        {
            // Cancellation stops scheduling further items; already-written files remain.
        }

        // Items never processed (cancelled) are Skipped.
        for (int i = 0; i < results.Length; i++)
            results[i] ??= new BatchItemResult(plan[i].Source, plan[i].Output, ItemStatus.Skipped, null);

        return new BatchResult(results.Cast<BatchItemResult>().ToList());
    }
}
