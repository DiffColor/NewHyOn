using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using TurtleTools;

namespace AndoW_Manager
{
    public partial class PlayerGroupButtonElement : UserControl
    {
        public PlayerGroupClass g_PlayerGroupClass = new PlayerGroupClass();
        public bool g_IsSelected = false;
        
        public PlayerGroupButtonElement()
        {
            InitializeComponent();
            InitEventHandler();
        }
        
        public void InitEventHandler()
        {
            PreviewMouseLeftButtonDown += PlayerGroupButtonElement_PreviewMouseLeftButtonDown;            
            MouseEnter += PlayerGroupButtonElement_MouseEnter;
            MouseLeave += PlayerGroupButtonElement_MouseLeave;
        }

        private void GroupDeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MessageTools.ShowMessageBox(string.Format("그룹 '{0}'을(를) 삭제하시겠습니까?\n그룹 삭제 시 그룹에 할당된 플레이어 연결이 모두 해제됩니다.", g_PlayerGroupClass.PG_GroupName), "예", "아니오") == true)
            {
                DataShop.Instance.g_PlayerGroupManager.DeletePlayerGroup(g_PlayerGroupClass);
                Page3.Instance.RefreshPlayerGroups();
            }
        }

        private void GroupEditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            PlayerGroupEditWindow editWindow = new PlayerGroupEditWindow();
            editWindow.InitData(g_PlayerGroupClass);
            if (editWindow.ShowDialog() == true)
            {
                DataShop.Instance.g_PlayerGroupManager.UpdatePlayerGroup(g_PlayerGroupClass, editWindow.g_PlayerGroupClass);
                Page3.Instance.RefreshPlayerGroups();
            }
        }

        private void PlayerGroupButtonElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Page3.Instance != null &&
                Page3.Instance.g_CurrentSelectedPlayerGroupClass != null &&
                Page3.Instance.g_CurrentSelectedPlayerGroupClass.PG_GUID == g_PlayerGroupClass.PG_GUID)
            {
                Page3.Instance.DeselectPlayerGroup();
            }
            else
            {
                Page3.Instance.SelectPlayerGroup(g_PlayerGroupClass);
            }
        }
        
        private void PlayerGroupButtonElement_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!g_IsSelected)
                MainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4176DE"));
        }

        private void PlayerGroupButtonElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!g_IsSelected)
                MainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00333333"));
        }
        
        public void UpdateGroupInfo(PlayerGroupClass paramCls)
        {
            g_PlayerGroupClass.CopyData(paramCls);
            DisplayGroupInfo();
        }
        
        public void DisplayGroupInfo()
        {
            GroupNameText.Text = g_PlayerGroupClass.PG_GroupName;
            int count = g_PlayerGroupClass.PG_AssignedPlayerNames == null ? 0 : g_PlayerGroupClass.PG_AssignedPlayerNames.Count;
            GroupPlayerCountText.Text = string.Format("{0}대", count);

            UpdateGroupLogo();
        }

        private void UpdateGroupLogo()
        {
            string logoPath = g_PlayerGroupClass.PG_LogoImagePath;
            if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            {
                try
                {
                    GroupLogoImage.Source = MediaTools.GetBitmapSourceFromFile(logoPath);
                    GroupLogoImage.Visibility = Visibility.Visible;
                    return;
                }
                catch
                {
                }
            }

            GroupLogoImage.Source = null;
            GroupLogoImage.Visibility = Visibility.Collapsed;
        }
        
        public void ShowSelection(bool isSelected)
        {
            g_IsSelected = isSelected;
            
            if (isSelected)
            {
                SelectBorder.Visibility = Visibility.Visible;
                MainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4176DE"));
            }
            else
            {
                SelectBorder.Visibility = Visibility.Hidden;
                MainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00333333"));
            }
        }

        private void UserControl_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Page3.Instance.SelectAllPlayerInGroup();
        }
    }
}
