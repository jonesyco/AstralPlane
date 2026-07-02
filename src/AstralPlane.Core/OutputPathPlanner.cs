namespace AstralPlane.Core;

/// <summary>Where converted files are written.</summary>
public enum OutputLocationMode
{
    /// <summary>A timestamped subfolder inside each source file's own folder.</summary>
    SameAsSource,
    /// <summary>A timestamped subfolder inside one user-chosen folder.</summary>
    ChosenFolder,
}

/// <summary>A resolved destination for a single converted file.</summary>
public sealed record PlannedOutput(string FullPath, string DirectoryPath, string FileName);

/// <summary>
/// Builds destination paths for a conversion job. All outputs go into a new
/// timestamped subfolder (<c>YYYY-MM-DD_HH-mm-ss</c>) fixed at job start, so an
/// existing file is never overwritten — collision handling is structural.
/// </summary>
public sealed class OutputPathPlanner
{
    private readonly OutputLocationMode _mode;
    private readonly string? _chosenFolder;
    private readonly Func<string, bool> _directoryExists;
    private readonly string _timestamp;

    // Resolved timestamped subfolder per base directory (stable within a job).
    private readonly Dictionary<string, string> _resolvedFolders = new(StringComparer.OrdinalIgnoreCase);
    // Output file paths already planned, to resolve same-base-name clashes.
    private readonly HashSet<string> _usedPaths = new(StringComparer.OrdinalIgnoreCase);

    public OutputPathPlanner(
        OutputLocationMode mode,
        string? chosenFolder,
        TimeProvider clock,
        Func<string, bool>? directoryExists = null)
    {
        if (mode == OutputLocationMode.ChosenFolder && string.IsNullOrWhiteSpace(chosenFolder))
            throw new ArgumentException("A chosen folder is required in ChosenFolder mode.", nameof(chosenFolder));

        _mode = mode;
        _chosenFolder = chosenFolder;
        _directoryExists = directoryExists ?? Directory.Exists;
        _timestamp = clock.GetLocalNow().ToString("yyyy-MM-dd_HH-mm-ss");
    }

    public PlannedOutput Plan(string sourcePath, OutputFormat target)
    {
        string baseDirectory = _mode == OutputLocationMode.ChosenFolder
            ? _chosenFolder!
            : Path.GetDirectoryName(Path.GetFullPath(sourcePath))
              ?? throw new ArgumentException($"Cannot determine folder of '{sourcePath}'.", nameof(sourcePath));

        string outputDirectory = ResolveTimestampFolder(baseDirectory);

        string extension = FormatRegistry.GetOutput(target).FileExtension;
        string baseName = Sanitize(Path.GetFileNameWithoutExtension(sourcePath));

        string fileName = baseName + extension;
        string fullPath = Path.Combine(outputDirectory, fileName);

        // Resolve rare same-base-name clashes within the single output folder.
        int counter = 1;
        while (!_usedPaths.Add(fullPath))
        {
            fileName = $"{baseName} ({counter++}){extension}";
            fullPath = Path.Combine(outputDirectory, fileName);
        }

        return new PlannedOutput(fullPath, outputDirectory, fileName);
    }

    private string ResolveTimestampFolder(string baseDirectory)
    {
        if (_resolvedFolders.TryGetValue(baseDirectory, out string? cached))
            return cached;

        string candidate = Path.Combine(baseDirectory, _timestamp);
        int counter = 1;
        while (_directoryExists(candidate))
            candidate = Path.Combine(baseDirectory, $"{_timestamp}_({counter++})");

        _resolvedFolders[baseDirectory] = candidate;
        return candidate;
    }

    private static string Sanitize(string baseName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(c, '_');
        return baseName.Trim();
    }
}
