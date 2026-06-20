using System.Globalization;

namespace MpvNet;

internal static class GuiCommandArgumentParser
{
    public static bool TryGetRequired(
        IList<string> arguments,
        string commandName,
        out string value)
    {
        if (arguments.Count > 0 && !string.IsNullOrWhiteSpace(arguments[0]))
        {
            value = arguments[0];
            return true;
        }

        value = "";
        Terminal.WriteError($"Missing argument for mpv.net command: {commandName}");
        return false;
    }

    public static bool TryGetInvariantFloat(
        IList<string> arguments,
        string commandName,
        out float value)
    {
        value = 0;

        if (!TryGetRequired(arguments, commandName, out string rawValue))
            return false;

        if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        Terminal.WriteError($"Invalid numeric argument for mpv.net command: {commandName} {rawValue}");
        return false;
    }
}
