using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Text.RegularExpressions;
using System.Windows.Media;     //WPF
using System.Windows.Controls;  //WPF
//using System.Windows.Forms;     //Winforms

namespace TurtleTools
{
    public static class LogicTools
    {
        public static T GetChildOfType<T>(this DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);

                var result = (child as T) ?? GetChildOfType<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        public static T TryFindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return TryFindParent<T>(parentObject);
        }

        public static List<E> ShuffleList<E>(List<E> inputList)
        {
            List<E> randomList = new List<E>();

            Random r = new Random();
            int randomIndex = 0;
            while (inputList.Count > 0)
            {
                randomIndex = r.Next(0, inputList.Count); //Choose a random object in the list
                randomList.Add(inputList[randomIndex]); //add it to the new, random list
                inputList.RemoveAt(randomIndex); //remove to avoid duplicates
            }

            return randomList; //return the new random list
        }

        public static void SelectItemByName(ComboBox combobox, string value)
        {
            IEnumerable<Object> query =
                                from Object item in combobox.Items
                                where (item.ToString().Equals(value, StringComparison.OrdinalIgnoreCase))
                                select item;

            foreach (Object obj in query)
            {
                combobox.SelectedItem = obj;
                return;
            }

            if (combobox.Items.Count > 0 && combobox.SelectedItem == null) combobox.SelectedIndex = 0;
        }

        public static bool CheckIsOverlappedIntDate(int startDate1, int endDate1, int startDate2, int endDate2)
        {
            if (startDate2 >= startDate1 && startDate2 <= endDate1) return true;
            if (endDate2 >= startDate1 && endDate2 <= endDate1) return true;
            if (startDate2 <= startDate1 && endDate2 >= endDate1) return true;
            return false;
        }

        //datetime ignore chars : "abceijklnopqvwxABCEIJLNOPQRSUVWXY";
        public static string RandomSizeString(int min, int max, string characters)
        {
            Random _rng = new Random(Guid.NewGuid().GetHashCode());
            int size = _rng.Next(min, max);
            char[] buffer = new char[size];

            for (int i = 0; i < size; i++)
            {
                buffer[i] = characters[_rng.Next(characters.Length)];
            }
            return new string(buffer);
        }

        public static int ConvertToUInt(String input)
        {
            // Replace everything that is no a digit.
            String inputCleaned = Regex.Replace(input, "[^0-9]", "");

            int value = -1;

            // Tries to parse the int, returns false on failure.
            if (int.TryParse(inputCleaned, out value))
            {
                // The result from parsing can be safely returned.
                return value;
            }

            return -1; // Or any other default value.
        }

        public static string Number2String(int number, bool isCaps = false)
        {
            Char c = (Char)((isCaps ? 65 : 97) + (number - 1));
            return c.ToString();
        }

        public static byte ShiftNBitLeft(byte value, byte n)
        {
            return (byte)(value << n | value >> (8 - n));
        }

        public static byte ShiftNBitRight(byte value, byte n)
        {
            return (byte)(value >> n | value << (8 - n));
        }
    }
}
