namespace MpvNet.Windows.Native;

public sealed class TaskbarThumbnailButtonCache
{
    TaskbarThumbnailButton[]? _buttons;
    bool _lightTheme;

    public bool IsCurrent(IReadOnlyList<TaskbarThumbnailButton> buttons, bool lightTheme) =>
        _buttons != null && _lightTheme == lightTheme && _buttons.SequenceEqual(buttons);

    public void Remember(IReadOnlyList<TaskbarThumbnailButton> buttons, bool lightTheme)
    {
        _buttons = buttons.ToArray();
        _lightTheme = lightTheme;
    }

    public void Invalidate() => _buttons = null;
}
