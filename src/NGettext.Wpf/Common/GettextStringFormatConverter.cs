
using System.Globalization;
using System.Windows.Data;

namespace NGettext.Wpf.Common
{
    public class GettextStringFormatConverter : IValueConverter
    {
        public string MsgId { get; private set; }

        public GettextStringFormatConverter(string msgId)
        {
            MsgId = msgId;
        }

        public static ILocalizer? Localizer { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (Localizer is null)
            {
                CompositionRoot.WriteMissingInitializationErrorMessage();
                return string.Format(MsgId, value);
            }

            return Localizer.Gettext(MsgId, value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
