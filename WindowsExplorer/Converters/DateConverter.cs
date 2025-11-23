using System;
using Microsoft.UI.Xaml.Data;

namespace WindowsExplorer.Converters
{
    /// <summary>
    /// Converts DateTime to a formatted date string.
    /// </summary>
    public class DateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime dateTime)
            {
                return dateTime.ToString("g");
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

