using Microsoft.UI.Xaml.Data;

namespace WindowsExplorer.Converters
{
    /// <summary>
    /// Converts file size in bytes to a human-readable format.
    /// </summary>
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is long size)
            {
                if (size == 0)
                    return string.Empty;

                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = size;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }

                return $"{len:0.##} {sizes[order]}";
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

