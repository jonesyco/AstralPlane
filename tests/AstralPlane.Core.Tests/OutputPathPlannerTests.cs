using AstralPlane.Core;

namespace AstralPlane.Core.Tests;

public class OutputPathPlannerTests
{
    // Deterministic clock fixed to a known local time.
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static readonly DateTimeOffset When =
        new(2026, 7, 1, 14, 30, 5, TimeSpan.Zero);

    private static OutputPathPlanner ChosenFolderPlanner(
        string chosen = @"D:\out", Func<string, bool>? dirExists = null) =>
        new(OutputLocationMode.ChosenFolder, chosen, new FixedClock(When), dirExists ?? (_ => false));

    [Fact]
    public void CreatesTimestampedSubfolderInChosenFolder()
    {
        var planned = ChosenFolderPlanner().Plan(@"C:\pics\a.png", OutputFormat.WebP);
        Assert.Equal(@"D:\out\2026-07-01_14-30-05", planned.DirectoryPath);
    }

    [Fact]
    public void SameAsSourceModeUsesEachSourceFolder()
    {
        var planner = new OutputPathPlanner(OutputLocationMode.SameAsSource, null, new FixedClock(When), _ => false);
        var a = planner.Plan(@"C:\pics\a.png", OutputFormat.Jpg);
        var b = planner.Plan(@"C:\other\b.png", OutputFormat.Jpg);
        Assert.Equal(@"C:\pics\2026-07-01_14-30-05", a.DirectoryPath);
        Assert.Equal(@"C:\other\2026-07-01_14-30-05", b.DirectoryPath);
    }

    [Fact]
    public void SwapsExtensionToTargetFormat()
    {
        var planned = ChosenFolderPlanner().Plan(@"C:\pics\photo.arw", OutputFormat.Jpg);
        Assert.Equal("photo.jpg", planned.FileName);
    }

    [Fact]
    public void SameBaseNameClashGetsNumericSuffix()
    {
        var planner = ChosenFolderPlanner();
        var first = planner.Plan(@"C:\a\photo.arw", OutputFormat.Jpg);
        var second = planner.Plan(@"C:\b\photo.cr2", OutputFormat.Jpg);
        Assert.Equal("photo.jpg", first.FileName);
        Assert.Equal("photo (1).jpg", second.FileName);
    }

    [Fact]
    public void SameSecondFolderCollisionAppendsSuffix()
    {
        // The plain timestamp folder already exists on disk -> use a suffixed one.
        var taken = @"D:\out\2026-07-01_14-30-05";
        var planner = ChosenFolderPlanner(dirExists: p => p == taken);
        var planned = planner.Plan(@"C:\pics\a.png", OutputFormat.Png);
        Assert.Equal(@"D:\out\2026-07-01_14-30-05_(1)", planned.DirectoryPath);
    }

    [Fact]
    public void FullPathCombinesDirectoryAndFileName()
    {
        var planned = ChosenFolderPlanner().Plan(@"C:\pics\a.tiff", OutputFormat.Avif);
        Assert.Equal(@"D:\out\2026-07-01_14-30-05\a.avif", planned.FullPath);
    }
}
