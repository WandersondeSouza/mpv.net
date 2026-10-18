namespace MpvNet;

internal static class RuntimeComponentPaths
{
    public static string ComponentsFolder { get; } = AppPaths.Components;

    public static string TempFolder { get; } = AppPaths.ComponentTemp;

    public static string CurrentFolder { get; } = Path.Combine(ComponentsFolder, "current");

    public static string PreviousFolder { get; } = Path.Combine(ComponentsFolder, "previous");

    public static string GetTargetPath(string fileName) => Path.Combine(CurrentFolder, fileName);

    public static string GetLegacyTargetPath(string fileName) => Path.Combine(ComponentsFolder, fileName);

    public static string GetMetadataPath(RuntimeComponentDefinition definition) =>
        definition.Kind == RuntimeComponentDownloadKind.GitHubZip
            ? Path.Combine(CurrentFolder, "ffmpeg-bundle.json")
            : GetTargetPath(definition.FileName) + ".json";

    public static string GetLegacyMetadataPath(RuntimeComponentDefinition definition) =>
        definition.Kind == RuntimeComponentDownloadKind.GitHubZip
            ? Path.Combine(ComponentsFolder, "ffmpeg-bundle.json")
            : GetLegacyTargetPath(definition.FileName) + ".json";
}

internal static class RuntimeComponentPathResolver
{
    public static string Resolve(string fileName)
    {
        if (!IsSafeComponentFileName(fileName))
            return string.Empty;

        ComponentResolutionResult result = ResolveResult(fileName);
        return result.ResolvedPath ?? RuntimeComponentPaths.GetTargetPath(fileName);
    }

    public static ComponentResolutionResult ResolveResult(string fileName) =>
        ResolveResult(
            fileName,
            AppPaths.Startup,
            RuntimeComponentPaths.CurrentFolder,
            GetWindowsPathEntries(),
            RuntimeComponentPaths.ComponentsFolder);

    internal static ComponentResolutionResult ResolveResult(
        string fileName,
        string applicationDirectory,
        string componentDirectory,
        IEnumerable<string> pathEntries,
        string? legacyDirectory = null)
    {
        if (!IsSafeComponentFileName(fileName))
        {
            return new ComponentResolutionResult(
                fileName,
                null,
                ComponentSource.None,
                false,
                false,
                null,
                null,
                "The component file name is invalid.");
        }

        var candidates = new List<(string Path, ComponentSource Source)>();
        bool bundledLibrary = fileName.Equals("MediaInfo.dll", StringComparison.OrdinalIgnoreCase);
        if (bundledLibrary)
            candidates.Add((Path.Combine(applicationDirectory, fileName), ComponentSource.ApplicationDirectory));
        candidates.Add((Path.Combine(componentDirectory, fileName), ComponentSource.ComponentCache));
        if (legacyDirectory is not null)
            candidates.Add((Path.Combine(legacyDirectory, fileName), ComponentSource.ComponentCache));
        if (!bundledLibrary)
            candidates.Add((Path.Combine(applicationDirectory, fileName), ComponentSource.ApplicationDirectory));

        foreach ((string path, ComponentSource source) in candidates)
        {
            ComponentResolutionResult? result = TryResolveCandidate(fileName, path, source);
            if (result is not null)
                return result;
        }

        foreach (string rawDirectory in pathEntries)
        {
            string? directory = WindowsPathEnvironment.NormalizeEntry(rawDirectory);
            if (directory is null)
                continue;
            string path = Path.Combine(directory, fileName);

            ComponentResolutionResult? result = TryResolveCandidate(fileName, path, ComponentSource.EnvironmentPath);
            if (result is not null)
                return result;
        }

        return new ComponentResolutionResult(
            fileName,
            null,
            ComponentSource.None,
            false,
            false,
            null,
            null,
            "No valid component file was found in the component cache, application directory, or PATH.");
    }

    public static string? ResolveFromWindowsPath(string fileName)
    {
        if (!IsSafeComponentFileName(fileName))
            return null;

        foreach (string rawDirectory in GetWindowsPathEntries())
        {
            string? directory = WindowsPathEnvironment.NormalizeEntry(rawDirectory);
            if (directory is null)
                continue;

            ComponentResolutionResult? candidate = TryResolveCandidate(
                fileName,
                Path.Combine(directory, fileName),
                ComponentSource.EnvironmentPath);
            if (candidate is not null)
                return candidate.ResolvedPath;
        }

        return null;
    }

    static ComponentResolutionResult? TryResolveCandidate(string fileName, string candidate, ComponentSource source)
    {
        if (!Path.IsPathFullyQualified(candidate) || !File.Exists(candidate))
            return null;

        ComponentValidationResult validation = RuntimeComponentValidator.Validate(fileName, candidate);
        if (!validation.IsValid)
        {
            Log.Debug($"Runtime component candidate rejected. file='{fileName}', source={source}, path='{Log.SafeValue(candidate)}', reason='{Log.SafeValue(validation.DiagnosticMessage)}'");
            return null;
        }

        string fullPath = Path.GetFullPath(candidate);
        Log.Debug($"Resolved runtime component. file='{fileName}', source={source}, path='{Log.SafeValue(fullPath)}', version='{Log.SafeValue(validation.Version)}'");
        return new ComponentResolutionResult(
            fileName,
            fullPath,
            source,
            true,
            true,
            validation.Version,
            null,
            null);
    }

    static bool IsSafeComponentFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        fileName is not "." and not ".." &&
        fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    static IEnumerable<string> GetWindowsPathEntries()
    {
        string? windowsPath = Environment.GetEnvironmentVariable("PATH");
        return string.IsNullOrWhiteSpace(windowsPath)
            ? []
            : windowsPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
    }
}
