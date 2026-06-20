namespace MpvNet;

internal static class RuntimeComponentPaths
{
    public static string ComponentsFolder { get; } =
        Path.Combine(AppPaths.LocalAppData, "mpv.net", "Component");

    public static string TempFolder { get; } =
        Path.Combine(TemporaryFileCleanup.DefaultTempFolder, "RuntimeComponents");

    public static string GetTargetPath(string fileName) => Path.Combine(ComponentsFolder, fileName);

    public static string GetMetadataPath(RuntimeComponentDefinition definition) =>
        definition.Kind == RuntimeComponentDownloadKind.GitHubZip
            ? Path.Combine(ComponentsFolder, "ffmpeg-bundle.json")
            : GetTargetPath(definition.FileName) + ".json";
}

internal static class RuntimeComponentPathResolver
{
    public static string Resolve(string fileName)
    {
        string componentPath = RuntimeComponentPaths.GetTargetPath(fileName);
        if (File.Exists(componentPath))
        {
            Log.Info($"Resolved runtime component from component folder. file='{fileName}', path='{Log.SafeValue(componentPath)}'");
            return componentPath;
        }

        string startupPath = Path.Combine(AppPaths.Startup, fileName);
        if (File.Exists(startupPath))
        {
            Log.Info($"Resolved runtime component from startup folder. file='{fileName}', path='{Log.SafeValue(startupPath)}'");
            return startupPath;
        }

        string? pathCandidate = ResolveFromWindowsPath(fileName);
        if (!string.IsNullOrWhiteSpace(pathCandidate))
        {
            Log.Info($"Resolved runtime component from PATH. file='{fileName}', path='{Log.SafeValue(pathCandidate)}'");
        }

        Log.Debug($"Resolved runtime component fallback. file='{fileName}', componentPath='{Log.SafeValue(componentPath)}', startupPath='{Log.SafeValue(startupPath)}', pathCandidate='{Log.SafeValue(pathCandidate)}'");
        return pathCandidate ?? componentPath;
    }

    public static string? ResolveFromWindowsPath(string fileName)
    {
        string? windowsPath = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(windowsPath))
            return null;

        foreach (string rawDirectory in windowsPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string directory = rawDirectory.Trim();
            if (directory.Length == 0)
                continue;

            string candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
