using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WallMod.Converters;

/**
 * Converter used for translating viewmodel code to automatic UI updates for the RememberFilters setting
 */
// https://stackoverflow.com/questions/34458592/how-to-update-radio-button-in-view-from-viewmodel
public class BoolToStrConverter: IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // When the user changes the UI (bool) update your property (string)
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue)
            return parameter.ToString();
        return null;
    }
}
