using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// SavingFileWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class FontStyleEditWindow : Window
    {
        ColorPicker.ColorPicker ColorPicker = new ColorPicker.ColorPicker();
        EditFontInfoClass g_EditFontInfoClass = new EditFontInfoClass();

        public FontStyleEditWindow(EditFontInfoClass efic)
        {
            InitializeComponent();
            InitColorPicker();
            IsBackgroundCheck.IsChecked = true;
            FontComboOnTextElem.ItemsSource = Fonts.SystemFontFamilies;

            LogicTools.SelectItemByName(FontComboOnTextElem, efic.EFT_FontName);

            FontComboOnTextElem.SelectionChanged += FontComboOnTextElem_SelectionChanged;

            BTN0DO_Copy12.Click += BTN0DO_Copy12_Click;  /// 저장
            BTN0DO_Copy13.Click += BTN0DO_Copy13_Click;   // 취소

            RestoreFontSettings(efic);
        }

        private void RestoreFontSettings(EditFontInfoClass efic)
        {
            if (string.IsNullOrEmpty(efic.EFT_FontName))
            {
                FontComboOnTextElem.SelectedIndex = 0;
            }
            else
            {
                IEnumerable<FontFamily> query =
                                        from FontFamily item in FontComboOnTextElem.Items
                                        where (item.ToString().Equals(efic.EFT_FontName))
                                        select item;

                foreach (FontFamily obj in query)
                {
                    FontComboOnTextElem.SelectedItem = obj;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(efic.EFT_BackGoundColor))
            {
                g_EditFontInfoClass.EFT_BackGoundColor = efic.EFT_BackGoundColor;
                ColorPicker.SelectedColor = ColorTools.GetColorByColorString(efic.EFT_BackGoundColor);
                TextBackgroundRect.Fill = new SolidColorBrush(ColorTools.GetColorByColorString(efic.EFT_BackGoundColor));
            }

            if (!string.IsNullOrEmpty(efic.EFT_ForeGoundColor))
            {
                g_EditFontInfoClass.EFT_ForeGoundColor = efic.EFT_ForeGoundColor;
                SampleTextBlk.Foreground = new SolidColorBrush(ColorTools.GetColorByColorString(efic.EFT_ForeGoundColor));
            }
        }

        void BTN0DO_Copy13_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        void BTN0DO_Copy12_Click(object sender, RoutedEventArgs e)
        {
            Page1.Instance.ChangeFontStyle(this.g_EditFontInfoClass);
            this.Close();
        }

        void FontComboOnTextElem_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SampleTextBlk.FontFamily = new FontFamily(FontComboOnTextElem.SelectedItem.ToString());
            g_EditFontInfoClass.EFT_FontName = FontComboOnTextElem.SelectedItem.ToString();
        }

        public void InitColorPicker()
        {
            ColorPicker.EventChangeColor += ColorPicker_EventChangeColor;
            ColorPickerGrid.Children.Add(ColorPicker);
        }

        void ColorPicker_EventChangeColor(Color clientName)
        {
            
            if (IsBackgroundCheck.IsChecked == true)
            {
                TextBackgroundRect.Fill = new SolidColorBrush(clientName);
                g_EditFontInfoClass.EFT_BackGoundColor = clientName.ToString();
            }
            else
            {
                SampleTextBlk.Foreground = new SolidColorBrush(clientName);
                g_EditFontInfoClass.EFT_ForeGoundColor = clientName.ToString();
            }

        }

        private void BtnWin_close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}
