using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace MpvNet.Windows.WPF;

/// <summary>Shared, reversible WPF direction and regional formatting resources.</summary>
public static class InterfaceLayout
{
    public static void ApplyCulture(CultureInfo textCulture)
    {
        Application? application = Application.Current;
        if (application == null)
            return;

        CultureInfo formattingCulture = CultureInfo.CurrentCulture;
        void Apply()
        {
            FlowDirection direction = textCulture.TextInfo.IsRightToLeft
                ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            if (!Equals(application.Resources["InterfaceFlowDirection"], direction))
                application.Resources["InterfaceFlowDirection"] = direction;
            XmlLanguage language = XmlLanguage.GetLanguage(formattingCulture.IetfLanguageTag);
            if (!Equals(application.Resources["InterfaceXmlLanguage"], language))
                application.Resources["InterfaceXmlLanguage"] = language;
        }

        if (application.Dispatcher.CheckAccess())
            Apply();
        else
            application.Dispatcher.BeginInvoke(Apply);
    }

    // Isolate display copies; never put bidi markers in paths/commands/config.
    public static string TechnicalText(string value) =>
        value.Length > 0 && CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? "\u2066" + value + "\u2069" : value;
}
