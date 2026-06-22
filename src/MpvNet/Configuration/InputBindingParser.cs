namespace MpvNet;

internal static class InputBindingParser
{
    public static List<Binding> Parse(string content)
    {
        List<Binding> bindings = [];

        if (string.IsNullOrEmpty(content))
            return bindings;

        if (content.Contains('\t'))
            content = content.Replace('\t', ' ');

        foreach (string item in content.Split('\n'))
        {
            string line = item.Trim();
            Binding binding = new();

            if (line == "")
            {
                bindings.Add(binding);
                continue;
            }

            if (line.StartsWith('#'))
            {
                binding.Comment = line[1..].Trim();
                bindings.Add(binding);
                continue;
            }

            int inputSeparatorIndex = line.IndexOf(' ');

            if (inputSeparatorIndex < 0)
                continue;

            binding.Input = NormalizeInput(line[..inputSeparatorIndex]);
            line = line[(inputSeparatorIndex + 1)..];
            ParseCommandAndComment(line, binding);
            bindings.Add(binding);
        }

        return bindings;
    }

    static string NormalizeInput(string input) =>
        input == "_"
            ? ""
            : input
                .Replace("CTRL+", "Ctrl+")
                .Replace("ctrl+", "Ctrl+")
                .Replace("SHIFT+", "Shift+")
                .Replace("shift+", "Shift+")
                .Replace("ALT+", "Alt+")
                .Replace("alt+", "Alt+");

    static void ParseCommandAndComment(string line, Binding binding)
    {
        if (line.Contains(App.MenuSyntax))
        {
            int menuIndex = line.IndexOf(App.MenuSyntax);
            binding.Comment = line[(menuIndex + App.MenuSyntax.Length)..].Trim();
            binding.IsMenu = true;
            line = line[..menuIndex];
        }
        else if (line.Contains("#custom-menu:"))
        {
            int customMenuIndex = line.IndexOf("#custom-menu:");
            binding.Comment = line[(customMenuIndex + 13)..].Trim();
            binding.IsCustomMenu = true;
            line = line[..customMenuIndex];
        }
        else if (line.Contains('#'))
        {
            int commentIndex = line.IndexOf('#');
            binding.Comment = line[(commentIndex + 1)..].Trim();
            line = line[..commentIndex];
        }

        binding.Command = line.Trim();

        if (binding.Command.Equals("ignore", StringComparison.OrdinalIgnoreCase))
            binding.Command = "";
    }
}
