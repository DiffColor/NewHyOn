using System.ComponentModel;
using System.Windows.Media;   //WPF

namespace TurtleTools
{
    public class ColorTools
    {
        #region Color Converter

        public static System.Drawing.Color GetDrawingColorByHexString(string hexStr)
        {
            return (System.Drawing.Color)TypeDescriptor.GetConverter(typeof(System.Drawing.Color)).ConvertFromString(hexStr);
        }

        public static System.Drawing.SolidBrush GetSolidBrushByHexString(string hexStr)
        {
            return new System.Drawing.SolidBrush(GetDrawingColorByHexString(hexStr));
        }

        public static bool IsHexColorString(string hexStr)
        {
            //string sPattern = @"^#\w{6,8}$";
            string sPattern = @"^(#[0-9a-fA-F]{3}|#(?:[0-9a-fA-F]{2}){2,4}|(rgb|hsl)a?\((-?\d+%?[,\s]+){2,3}\s*[\d\.]+%?\))$";
            bool ret = System.Text.RegularExpressions.Regex.IsMatch(hexStr, sPattern);
            return ret;
        }

        /* WPF */
        public static Color GetMediaColorByHexString(string hexStr)
        {
            return (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString(hexStr);
        }

        public static SolidColorBrush GetSolidColorBrushByHexString(string hexStr)
        {
            return new SolidColorBrush(GetMediaColorByHexString(hexStr));
        }

        #endregion
    }
}
