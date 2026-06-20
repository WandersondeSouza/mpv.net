
namespace MpvNet;

public static class Global
{
    public static readonly string BR = Environment.NewLine;
    public static readonly string BR2 = Environment.NewLine + Environment.NewLine;
    public static readonly MainPlayer Player = new MainPlayer();
    [Obsolete($"Use {nameof(Player)} instead.")]
    public static readonly MainPlayer Core = Player;
    public static readonly AppClass App = new AppClass();

    public static string _(string value) => TranslationProvider.Current!.Gettext(value);
    public static string _p(string context, string value) => TranslationProvider.Current!.GetParticularString(context, value);
}
