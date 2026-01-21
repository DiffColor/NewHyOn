using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TurtleTools;


namespace AndoW_Manager
{
    /// <summary>
    /// GroupCommandWindow에서 사용하는 플레이어 선택 요소
    /// </summary>
    public partial class PlayerElementForGroupCommand : UserControl
    {
        private const double ExpandedHeight = 60;
        private const double ExpandedWidth = 240;
        private const double CompactHeight = 32;
        private const double CompactWidth = 240;
        private const double PlaylistRowHeight = 28;

        public bool g_IsSelected = false;

        public PlayerInfoClass g_PlayerInfoClass = new PlayerInfoClass();

        public PlayerElementForGroupCommand()
        {
            InitializeComponent();
            InitEventHandler();
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;

            ShowAndHideSelectedBorder(false);
            SelectedCheckBox.Visibility = System.Windows.Visibility.Collapsed;
        }

        public void InitEventHandler()
        {
            PreviewMouseLeftButtonDown += PlayerElementForGroupCommand_PreviewMouseLeftButtonDown;
            PreviewMouseMove += PageListElement_PreviewMouseMove;
            MouseLeave += PageListElement_MouseLeave;
        }

        void PlayerElementForGroupCommand_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (PlayerPlaylistCombo.IsDropDownOpen)
            {
                return;
            }

            if (IsClickOnPlaylistCombo(e.OriginalSource))
            {
                return;
            }

            bool isSelected = SelectedCheckBox.IsChecked == true;
            SelectThisElement(!isSelected);

            if (GroupCommandWindow.Instance != null)
            {
                GroupCommandWindow.Instance.RefreshSelectedPlayersList();
            }
        }

        public void SelectThisElement(bool isSelected)
        {
            if (isSelected)
            {
                g_IsSelected = true;
                SelectedCheckBox.IsChecked = true;
                TextBlockPageName.Foreground = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
                TextBlockOrderingNumber.Foreground = ColorTools.GetSolidBrushByColorString("#A5FFFFFF");
                SelectedDspRect.Fill = new SolidColorBrush(Colors.GreenYellow);
                ShowAndHideSelectedBorder(true);
            }
            else
            {
                g_IsSelected = false;
                SelectedCheckBox.IsChecked = false;
                TextBlockPageName.Foreground = ColorTools.GetSolidBrushByColorString("#FF1B1F5A");
                TextBlockOrderingNumber.Foreground = ColorTools.GetSolidBrushByColorString("#FF1B1F5A");
                SelectedDspRect.Fill = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
                ShowAndHideSelectedBorder(false);
            }
        }

        public void UpdateDataInfo(PlayerInfoClass paramCls)
        {
            g_PlayerInfoClass.CopyData(paramCls);
            DisplayDataInfo();
        }

        public void SetOrderingNumber(int orderNumber)
        {
            if (orderNumber < 1)
            {
                orderNumber = 1;
            }

            TextBlockOrderingNumber.Text = orderNumber.ToString("00");
        }

        public void DisplayDataInfo()
        {
            TextBlockPageName.Text = g_PlayerInfoClass.PIF_PlayerName;
        }

        public void ShowAndHideSelectedBorder(bool isShow)
        {
            if (isShow)
            {
                SelectBorder_Copy.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                SelectBorder_Copy.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        public void UpdatePlaylistOptions(IEnumerable<string> playlistNames, string selectedName)
        {
            PlayerPlaylistCombo.Items.Clear();

            if (playlistNames != null)
            {
                foreach (string name in playlistNames)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        PlayerPlaylistCombo.Items.Add(name);
                    }
                }
            }

            if (PlayerPlaylistCombo.Items.Count == 0)
            {
                PlayerPlaylistCombo.SelectedIndex = -1;
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedName) && PlayerPlaylistCombo.Items.Contains(selectedName))
            {
                PlayerPlaylistCombo.SelectedItem = selectedName;
            }
            else
            {
                PlayerPlaylistCombo.SelectedIndex = 0;
            }
        }

        public string GetSelectedPlaylistName()
        {
            return PlayerPlaylistCombo.SelectedItem == null ? string.Empty : PlayerPlaylistCombo.SelectedItem.ToString();
        }

        public void SetPlaylistSelectionEnabled(bool isEnabled)
        {
            PlayerPlaylistCombo.IsEnabled = isEnabled;
            PlayerPlaylistCombo.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
            ApplyLayoutForPlaylistSelection(isEnabled);
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            SelectBorder.Visibility = System.Windows.Visibility.Hidden;

            if (g_IsSelected)
            {
                TextBlockPageName.Foreground = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
                TextBlockOrderingNumber.Foreground = ColorTools.GetSolidBrushByColorString("#A5FFFFFF");
            }
            else
            {
                TextBlockPageName.Foreground = ColorTools.GetSolidBrushByColorString("#FF1B1F5A");
                TextBlockOrderingNumber.Foreground = ColorTools.GetSolidBrushByColorString("#FF1B1F5A");
            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            SelectBorder.Visibility = System.Windows.Visibility.Visible;
            TextBlockPageName.Foreground = ColorTools.GetSolidBrushByColorString("#FFFFFFFF");
            TextBlockOrderingNumber.Foreground = ColorTools.GetSolidBrushByColorString("#A5FFFFFF");
        }

        private bool IsClickOnPlaylistCombo(object source)
        {
            DependencyObject current = source as DependencyObject;
            while (current != null)
            {
                if (current == PlayerPlaylistCombo)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void ApplyLayoutForPlaylistSelection(bool isEnabled)
        {
            Height = isEnabled ? ExpandedHeight : CompactHeight;
            Width = isEnabled ? ExpandedWidth : CompactWidth;

            if (LayoutGrid != null && LayoutGrid.RowDefinitions.Count > 1)
            {
                LayoutGrid.RowDefinitions[0].Height = new GridLength(CompactHeight);
                LayoutGrid.RowDefinitions[1].Height = isEnabled ? new GridLength(PlaylistRowHeight) : new GridLength(0);
            }
        }
    }
}
