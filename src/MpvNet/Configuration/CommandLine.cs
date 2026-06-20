using MpvNet.Help;

namespace MpvNet;

public class CommandLine
{
    static List<StringPair>? _arguments;

    static string[] _preInitProperties { get; } = {
        "input-terminal", "terminal", "input-file", "config", "o", "config-dir", "input-conf",
        "load-scripts", "scripts", "script-opts", "player-operation-mode", "idle", "log-file",
        "msg-color", "dump-stats", "msg-level", "really-quiet" };

    static string[] _postFileProperties { get; } = {
        "profile" };

    public static List<StringPair> Arguments
    {
        get
        {
            if (_arguments != null)
                return _arguments;

            _arguments = ParseArguments(Environment.GetCommandLineArgs().Skip(1));
            return _arguments;
        }
    }

    internal static List<StringPair> ParseArguments(IEnumerable<string> args)
    {
        List<StringPair> arguments = [];
        string[] inputs = args.ToArray();

        for (int index = 0; index < inputs.Length; index++)
        {
            string input = inputs[index];

            if (TryParseArgument(input, out StringPair? pair))
            {
                if (ShouldConsumeSeparatedValue(pair!.Name, pair.Value, inputs, index))
                {
                    pair = new StringPair(pair.Name, NormalizeSeparatedValue(pair.Name, inputs[index + 1]));
                    index++;
                }

                arguments.Add(pair!);
                Log.Debug($"Parsed command line option: {pair!.Name}='{Log.SafeValue(pair.Value)}'");
            }
            else
            {
                Log.Debug($"Ignored non-option command line argument while parsing properties: '{Log.SafeValue(input)}'");
            }
        }

        return arguments;
    }

    public static void ProcessCommandLineArgsPreInit()
    {
        Log.Debug($"Processing pre-init command line properties. count={Arguments.Count}");

        foreach (var pair in Arguments)
        {
            if (IsChangeListOperation(pair.Name))
            {
                Log.Debug($"Skipping change-list command before mpv initialization: {pair.Name}='{Log.SafeValue(pair.Value)}'");
                continue;
            }

            ApplyPropertyArgument(pair, "pre-init");
        }
    }

    public static void ProcessCommandLineArgsPostInit()
    {
        Log.Debug($"Processing post-init command line properties. count={Arguments.Count}");

        foreach (var pair in Arguments)
        {
            if (IsPostFileProperty(pair.Name))
            {
                Log.Debug($"Deferring command line property until after media loading: {pair.Name}='{Log.SafeValue(pair.Value)}'");
                continue;
            }

            if (IsPreInitProperty(pair.Name))
            {
                Log.Debug($"Skipping pre-init property during post-init processing: {pair.Name}='{Log.SafeValue(pair.Value)}'");
                continue;
            }

            if (!TryProcessChangeListArgument(pair))
                ApplyPropertyArgument(pair, "post-init");
        }
    }

    public static void ProcessCommandLineArgsPostFile()
    {
        Log.Debug($"Processing post-file command line properties. count={Arguments.Count}");

        bool appliedProfile = false;

        foreach (var pair in Arguments)
        {
            if (!IsPostFileProperty(pair.Name))
                continue;

            Log.Debug($"Applying post-file command line profile: {pair.Name}='{Log.SafeValue(pair.Value)}'");
            Player.CommandV("apply-profile", pair.Value);
            appliedProfile = true;
        }

        if (!appliedProfile)
            return;

        foreach (var pair in Arguments)
        {
            if (!IsPostProfileOverrideProperty(pair.Name))
                continue;

            ApplyPropertyArgument(pair, "post-profile override");
        }
    }

    public static void ProcessCommandLineFiles()
    {
        CommandLineMediaRequest request = ResolveMediaRequest(Environment.GetCommandLineArgs().Skip(1), Arguments);
        List<string> files = request.Files;

        Log.Info($"Command line media inputs selected: count={files.Count}, queue={App.Queue}, loadFolder={!App.Queue}, primary='{Log.SafeValue(request.PrimaryMedia)}', title='{Log.SafeValue(request.Title)}', source='{request.Source}', inputs={Log.SafeValues(files)}");

        if (!string.IsNullOrWhiteSpace(request.Title) && !string.IsNullOrWhiteSpace(request.PrimaryMedia))
        {
            Log.Info($"Applying command line media title before playback. title='{Log.SafeValue(request.Title)}', media='{Log.SafeValue(request.PrimaryMedia)}'");
            Player.SetPropertyString("force-media-title", request.Title);
        }

        Player.LoadFiles([.. files], !App.Queue, App.Queue);

        if (App.CommandLine.Contains("--shuffle"))
        {
            Log.Info("Applying command line shuffle to playlist.");
            Player.Command("playlist-shuffle");
            Player.SetPropertyInt("playlist-pos", 0);
        }
    }

    public static bool IsLoadableFileArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg) || arg.StartsWith("--"))
            return false;

        if (arg == "-" || FileTypes.IsStreamingUrl(arg))
            return true;

        if (arg.Contains(":\\") || (arg.Contains(":/") && !arg.Contains("://")) || arg.StartsWith("\\\\"))
            return true;

        if (arg.StartsWith('.'))
            return true;

        return File.Exists(arg) || FileTypes.IsSupportedMediaInput(arg);
    }

    public static bool Contains(string name)
    {
        foreach (StringPair pair in Arguments)
        {
            if (pair.Name == name)
                return true;
        }

        return false;
    }

    public static string GetValue(string name)
    {
        foreach (StringPair pair in Arguments)
        {
            if (pair.Name == name)
                return pair.Value;
        }

        return "";
    }

    internal static string GetCommandLinePlaylistTitle(IEnumerable<StringPair> arguments)
    {
        string title = "";

        foreach (var pair in arguments)
        {
            if (pair.Name == "force-media-title")
                title = pair.Value;
            else if (pair.Name == "title" && !pair.Value.Contains("${"))
                title = pair.Value;
        }

        return title;
    }

    internal static CommandLineMediaRequest ResolveMediaRequest(
        IEnumerable<string> rawArgs,
        IEnumerable<StringPair> parsedArguments)
    {
        List<string> files = [];
        List<string> positionalNonFiles = [];

        foreach (string arg in rawArgs)
        {
            bool isLoadable = IsLoadableFileArgument(arg);
            Log.Debug($"Command line file candidate: loadable={isLoadable}, value='{Log.SafeValue(arg)}'");

            if (isLoadable)
                files.Add(arg);
            else if (!arg.StartsWith("--"))
                positionalNonFiles.Add(arg);
        }

        string title = GetCommandLinePlaylistTitle(parsedArguments);
        string source = "command-line";

        if (string.IsNullOrWhiteSpace(title) && files.Count == 1 && positionalNonFiles.Count > 0)
        {
            title = TitleHelp.NormalizeMediaTitle(positionalNonFiles[0]);
            source = "command-line-title-and-media";
        }

        string primary = files.FirstOrDefault("") ?? "";
        return new CommandLineMediaRequest(files, primary, title, source);
    }

    static bool ShouldConsumeSeparatedValue(string name, string value, string[] inputs, int index) =>
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) &&
        index + 1 < inputs.Length &&
        ShouldNormalizeTitleArgument(name, inputs[index + 1]) &&
        !inputs[index + 1].StartsWith("--");

    static string NormalizeSeparatedValue(string name, string value) =>
        ShouldNormalizeTitleArgument(name, value) ? TitleHelp.NormalizeMediaTitle(value) : value;

    static bool ShouldNormalizeTitleArgument(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (name == "force-media-title")
            return true;

        return name == "title" && !value.Contains("${");
    }

    static bool TryParseArgument(string input, out StringPair? pair)
    {
        pair = null;
        string arg = input;

        if (!arg.StartsWith("--"))
            return false;

        if (!arg.Contains('='))
        {
            if (arg.Contains("--no-"))
            {
                arg = arg.Replace("--no-", "--");
                arg += "=no";
            }
            else
                arg += "=yes";
        }

        string left = arg[2..arg.IndexOf('=')];
        string right = arg[(left.Length + 3)..];

        if (string.IsNullOrEmpty(left))
            return false;

        left = NormalizeArgumentName(left);

        if (ShouldNormalizeTitleArgument(left, right))
            right = TitleHelp.NormalizeMediaTitle(right);

        pair = new StringPair(left, right);
        return true;
    }

    static string NormalizeArgumentName(string name) =>
        name switch
        {
            "script" => "scripts",
            "script-opt" => "script-opts",
            "audio-file" => "audio-files",
            "sub-file" => "sub-files",
            "external-file" => "external-files",
            _ => name
        };

    internal static bool IsPreInitProperty(string name) => _preInitProperties.Contains(name);

    internal static bool IsPostFileProperty(string name) => _postFileProperties.Contains(name);

    internal static bool IsPostProfileOverrideProperty(string name) =>
        !IsChangeListOperation(name) &&
        !IsPostFileProperty(name) &&
        !IsStrictlyInitOnlyProperty(name);

    static bool IsStrictlyInitOnlyProperty(string name) =>
        name is "input-terminal" or "terminal" or "input-file" or "config" or "o" or
            "config-dir" or "input-conf" or "load-scripts" or "scripts" or "script-opts" or
            "player-operation-mode" or "log-file" or "msg-color" or "dump-stats" or
            "msg-level" or "really-quiet";

    static void ApplyPropertyArgument(StringPair pair, string phase)
    {
        Log.Debug($"Applying {phase} command line property: {pair.Name}='{Log.SafeValue(pair.Value)}'");
        Player.ProcessProperty(pair.Name, pair.Value);

        if (!App.ProcessProperty(pair.Name, pair.Value))
        {
            Log.Debug($"Forwarding {phase} property to mpv: {pair.Name}='{Log.SafeValue(pair.Value)}'");
            Player.SetPropertyString(pair.Name, pair.Value);
        }
        else
        {
            Log.Debug($"Applied {phase} property in mpv.net frontend: {pair.Name}='{Log.SafeValue(pair.Value)}'");
        }
    }

    static bool TryProcessChangeListArgument(StringPair pair)
    {
        if (pair.Name.EndsWith("-add"))
            Player.CommandV("change-list", pair.Name[..^4], "add", pair.Value);
        else if (pair.Name.EndsWith("-set"))
            Player.CommandV("change-list", pair.Name[..^4], "set", pair.Value);
        else if (pair.Name.EndsWith("-append"))
            Player.CommandV("change-list", pair.Name[..^7], "append", pair.Value);
        else if (pair.Name.EndsWith("-pre"))
            Player.CommandV("change-list", pair.Name[..^4], "pre", pair.Value);
        else if (pair.Name.EndsWith("-clr"))
            Player.CommandV("change-list", pair.Name[..^4], "clr", "");
        else if (pair.Name.EndsWith("-remove"))
            Player.CommandV("change-list", pair.Name[..^7], "remove", pair.Value);
        else if (pair.Name.EndsWith("-toggle"))
            Player.CommandV("change-list", pair.Name[..^7], "toggle", pair.Value);
        else
            return false;

        Log.Debug($"Applied command line change-list operation: {pair.Name}='{Log.SafeValue(pair.Value)}'");
        return true;
    }

    static bool IsChangeListOperation(string name) =>
        name.EndsWith("-add") ||
        name.EndsWith("-set") ||
        name.EndsWith("-pre") ||
        name.EndsWith("-clr") ||
        name.EndsWith("-append") ||
        name.EndsWith("-remove") ||
        name.EndsWith("-toggle");
}

public sealed record CommandLineMediaRequest(
    List<string> Files,
    string PrimaryMedia,
    string Title,
    string Source);
