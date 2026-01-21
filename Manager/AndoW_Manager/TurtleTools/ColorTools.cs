using System.ComponentModel;
using System.Windows.Media; //WPF
//using System.Drawing;       //Winforms

namespace TurtleTools
{
    public class ColorTools
    {
        #region Color Converter

        /* WPF */
        public static SolidColorBrush GetSolidBrushByColorString(string paramStr)
        {
            return new SolidColorBrush((Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString(paramStr));
        }

        /* Winforms */
        //public static SolidBrush GetSolidBrushByColorString(string paramStr)
        //{
        //    return new SolidBrush((Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString(paramStr));
        //}

        public static Color GetColorByColorString(string paramStr)
        {
            return (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString(paramStr);
        }

        public static bool IsHexColorString(string hexStr)
        {
            string sPattern = @"^#\w{6,8}$";
            bool ret = System.Text.RegularExpressions.Regex.IsMatch(hexStr, sPattern);
            return ret;
        }
        #endregion
    }
}
