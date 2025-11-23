using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace WindowsExplorer.Converters
{
    /// <summary>
    /// Converts a boolean IsDirectory value to a Symbol icon.
    /// </summary>
    public class SymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isDirectory)
            {
                return isDirectory ? Symbol.Folder : Symbol.Document;
            }

            return Symbol.Document;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

