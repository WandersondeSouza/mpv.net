using System.Text;

namespace MpvNet;

internal static class InputBindingSerializer
{
    public static string Serialize(IEnumerable<Binding> bindings)
    {
        StringBuilder builder = new();

        foreach (Binding binding in bindings)
        {
            if (binding.IsEmpty())
            {
                builder.AppendLine();
                continue;
            }

            if (IsStandaloneComment(binding))
            {
                builder.AppendLine("#" + binding.Comment.Trim());
                continue;
            }

            string input = string.IsNullOrWhiteSpace(binding.Input) ? "_" : binding.Input.Trim();
            string command = string.IsNullOrWhiteSpace(binding.Command) ? "ignore" : binding.Command.Trim();
            string line = input.PadRight(10) + "  " + command;
            string comment = GetComment(binding);

            if (comment != "")
            {
                string separator = IsMenuComment(comment) ? "  #" : "  # ";
                line = line.PadRight(40) + separator + comment;
            }

            builder.AppendLine(line);
        }

        return builder.ToString().TrimEnd() + BR;
    }

    static bool IsStandaloneComment(Binding binding) =>
        binding.Comment != "" &&
        binding.Command == "" &&
        binding.Input == "" &&
        !binding.IsMenu;

    static string GetComment(Binding binding)
    {
        if (binding.IsMenu)
            return (binding.IsShortMenuSyntax ? "! " : "menu: ") + binding.Comment.Trim();

        if (binding.IsCustomMenu)
            return "custom-menu: " + binding.Comment.Trim();

        return binding.Comment.Trim();
    }

    static bool IsMenuComment(string comment) =>
        comment.StartsWith("menu: ", StringComparison.Ordinal) ||
        comment.StartsWith("custom-menu: ", StringComparison.Ordinal) ||
        comment.StartsWith("! ", StringComparison.Ordinal);
}
