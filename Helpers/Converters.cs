using System.Globalization;

namespace DriverLedger.Helpers
{
    /// <summary>
    /// Returns Green for "Active", Red for "Inactive".
    /// </summary>
    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "Active"   => Color.FromArgb("#1B5E20"),
                "Inactive" => Color.FromArgb("#B71C1C"),
                _          => Color.FromArgb("#37474F")
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Returns true when the value is NOT null — used to show/hide info panels.
    /// </summary>
    public class NotNullConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is not null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Returns true when a numeric value is non-zero — used to conditionally show expense rows.
    /// </summary>
    public class NonZeroToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                decimal d  => d  != 0m,
                double  db => db != 0.0,
                float   f  => f  != 0f,
                int     i  => i  != 0,
                long    l  => l  != 0L,
                _          => false
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

