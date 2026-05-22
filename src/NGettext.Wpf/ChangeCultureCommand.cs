
using System.Globalization;
using System.Windows.Input;

namespace NGettext.Wpf
{
    public class ChangeCultureCommand : ICommand
    {
        public bool CanExecute(object? parameter)
        {
            return parameter is string cultureName &&
                CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                    .Any(cultureInfo => cultureInfo.Name == cultureName);
        }

        public void Execute(object? parameter)
        {
            if (CultureTracker is null)
            {
                CompositionRoot.WriteMissingInitializationErrorMessage();
                return;
            }

            if (parameter is not string cultureName)
                return;

            CultureTracker.CurrentCulture =
                CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                    .Single(cultureInfo => cultureInfo.Name == cultureName);
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public static ICultureTracker? CultureTracker { get; set; }
    }
}
