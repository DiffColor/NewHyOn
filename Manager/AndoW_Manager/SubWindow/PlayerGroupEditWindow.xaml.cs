using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TurtleTools;
using Key = System.Windows.Input.Key;

namespace AndoW_Manager
{
    public partial class PlayerGroupEditWindow : Window
    {
        public PlayerGroupClass g_PlayerGroupClass = new PlayerGroupClass();
        private bool g_IsEditMode = false;
        
        public PlayerGroupEditWindow()
        {
            InitializeComponent();
            InitEventHandler();
        }
        
        public void InitEventHandler()
        {
            SaveBtn.Click += SaveBtn_Click;
            CloseBtn.Click += CloseBtn_Click;
            SelectLogoBtn.Click += SelectLogoBtn_Click;
            ClearLogoBtn.Click += ClearLogoBtn_Click;
            SelectAllBtn.Click += SelectAllBtn_Click;
            DeselectAllBtn.Click += DeselectAllBtn_Click;
            Loaded += PlayerGroupEditWindow_Loaded;
            PreviewKeyDown += PlayerGroupEditWindow_PreviewKeyDown;
            Closing += PlayerGroupEditWindow_Closing;
        }

        private void PlayerGroupEditWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainWindow.Instance?.SetDimOverlay(false);
        }

        private void PlayerGroupEditWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Enter)
            {
                SaveBtn_Click(this, null);
            }
        }

        private void SelectLogoBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp|모든 파일|*.*";
            
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    System.Drawing.Image image = System.Drawing.Image.FromFile(openFileDialog.FileName);
                    image.Dispose();
                    
                    string logoDir = FNDTools.GetGroupLogosDirPath();
                    
                    string fileName = Path.GetFileName(openFileDialog.FileName);
                    string newPath = Path.Combine(logoDir, fileName);
                    
                    if (File.Exists(newPath) && newPath != g_PlayerGroupClass.PG_LogoImagePath)
                    {
                        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                        string fileExt = Path.GetExtension(fileName);
                        newPath = Path.Combine(logoDir, fileNameWithoutExt + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + fileExt);
                    }
                    
                    if (newPath != g_PlayerGroupClass.PG_LogoImagePath)
                    {
                        File.Copy(openFileDialog.FileName, newPath, true);
                    }
                    
                    g_PlayerGroupClass.PG_LogoImagePath = newPath;
                    UpdateLogoPreview();
                }
                catch (Exception ex)
                {
                    MessageTools.ShowMessageBox("이미지 로드 중 오류가 발생했습니다.\n" + ex.Message);
                }
            }
        }

        private void ClearLogoBtn_Click(object sender, RoutedEventArgs e)
        {
            g_PlayerGroupClass.PG_LogoImagePath = string.Empty;
            UpdateLogoPreview();
        }

        private void PlayerGroupEditWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (g_IsEditMode)
            {
                Title = "그룹 편집";
                SaveBtn.Content = "저장";
            }
            else
            {
                Title = "새 그룹 추가";
                SaveBtn.Content = "추가";
                GroupNameTextBox.Text = "새 그룹";
            }
            
            UpdateLogoPreview();
            RefreshPlayerList();
            
            GroupNameTextBox.Focus();
            GroupNameTextBox.SelectAll();
            MainWindow.Instance?.SetDimOverlay(true);
        }
        
        private void UpdateLogoPreview()
        {
            if (!string.IsNullOrEmpty(g_PlayerGroupClass.PG_LogoImagePath) && File.Exists(g_PlayerGroupClass.PG_LogoImagePath))
            {
                try
                {
                    LogoPreviewImage.Source = MediaTools.GetBitmapSourceFromFile(g_PlayerGroupClass.PG_LogoImagePath);
                    LogoPreviewImage.Visibility = Visibility.Visible;
                    LogoPreviewText.Visibility = Visibility.Collapsed;
                    ClearLogoBtn.IsEnabled = true;
                }
                catch
                {
                    LogoPreviewImage.Source = null;
                    LogoPreviewImage.Visibility = Visibility.Collapsed;
                    LogoPreviewText.Visibility = Visibility.Visible;
                    ClearLogoBtn.IsEnabled = false;
                }
            }
            else
            {
                LogoPreviewImage.Source = null;
                LogoPreviewImage.Visibility = Visibility.Collapsed;
                LogoPreviewText.Visibility = Visibility.Visible;
                ClearLogoBtn.IsEnabled = false;
            }
        }
        
        private void RefreshPlayerList()
        {
            PlayersListBox.Items.Clear();
            
            foreach (PlayerInfoClass player in DataShop.Instance.g_PlayerInfoManager.g_PlayerInfoClassList)
            {
                Border playerItem = new Border();
                playerItem.BorderThickness = new Thickness(1);
                playerItem.CornerRadius = new CornerRadius(3);
                playerItem.Padding = new Thickness(8);
                playerItem.Margin = new Thickness(5);
                playerItem.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x2A, 0x2A));
                playerItem.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33));
                playerItem.Cursor = Cursors.Hand;
                
                StackPanel panel = new StackPanel();
                panel.Orientation = Orientation.Horizontal;
                
                CheckBox checkBox = new CheckBox();
                checkBox.IsChecked = g_PlayerGroupClass.HasPlayer(player.PIF_PlayerName);
                checkBox.Tag = player.PIF_PlayerName;
                checkBox.VerticalAlignment = VerticalAlignment.Center;
                checkBox.Margin = new Thickness(0, 0, 8, 0);
                checkBox.Checked += CheckBox_CheckedChanged;
                checkBox.Unchecked += CheckBox_CheckedChanged;
                
                TextBlock playerNameText = new TextBlock();
                playerNameText.Text = player.PIF_PlayerName;
                playerNameText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
                playerNameText.FontSize = 14;
                playerNameText.VerticalAlignment = VerticalAlignment.Center;
                
                panel.Children.Add(checkBox);
                panel.Children.Add(playerNameText);
                playerItem.Child = panel;
                
                playerItem.MouseLeftButtonDown += (sender, e) => {
                    checkBox.IsChecked = !checkBox.IsChecked;
                    e.Handled = true;
                };
                
                playerItem.MouseEnter += (sender, e) => {
                    playerItem.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x44, 0x6B));
                };
                
                playerItem.MouseLeave += (sender, e) => {
                    playerItem.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x2A, 0x2A));
                };
                
                PlayersListBox.Items.Add(playerItem);
            }
            
            UpdateSelectedPlayersCount();
        }

        private void CheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateSelectedPlayersCount();
        }

        private void UpdateSelectedPlayersCount()
        {
            int selectedCount = 0;
            
            foreach (Border playerItem in PlayersListBox.Items)
            {
                StackPanel panel = playerItem.Child as StackPanel;
                if (panel != null && panel.Children.Count > 0)
                {
                    CheckBox checkBox = panel.Children[0] as CheckBox;
                    if (checkBox != null && checkBox.IsChecked == true)
                    {
                        selectedCount++;
                    }
                }
            }
            
            SelectedPlayersCountText.Text = $"{selectedCount}대 선택됨";
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(GroupNameTextBox.Text))
            {
                MessageTools.ShowMessageBox("그룹 이름을 입력해주세요.");
                GroupNameTextBox.Focus();
                return;
            }
            
            PlayerGroupClass existingGroup = DataShop.Instance.g_PlayerGroupManager.GetGroupByName(GroupNameTextBox.Text);
            if (existingGroup != null && existingGroup.PG_GUID != g_PlayerGroupClass.PG_GUID)
            {
                MessageTools.ShowMessageBox("같은 이름의 그룹이 이미 존재합니다. 다른 이름을 사용해주세요.");
                GroupNameTextBox.Focus();
                GroupNameTextBox.SelectAll();
                return;
            }
            
            g_PlayerGroupClass.PG_GroupName = GroupNameTextBox.Text;
            
            g_PlayerGroupClass.PG_AssignedPlayerNames.Clear();
            foreach (Border playerItem in PlayersListBox.Items)
            {
                StackPanel panel = playerItem.Child as StackPanel;
                if (panel != null && panel.Children.Count > 0)
                {
                    CheckBox checkBox = panel.Children[0] as CheckBox;
                    if (checkBox != null && checkBox.IsChecked == true)
                    {
                        g_PlayerGroupClass.AddPlayer(checkBox.Tag.ToString());
                    }
                }
            }
            
            DialogResult = true;
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnWin_close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        public void InitData(PlayerGroupClass paramCls)
        {
            g_PlayerGroupClass = new PlayerGroupClass();
            g_PlayerGroupClass.CopyData(paramCls);
            g_IsEditMode = true;
            
            GroupNameTextBox.Text = g_PlayerGroupClass.PG_GroupName;
            UpdateLogoPreview();
            RefreshPlayerList();
        }

        private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
        {
            foreach (Border playerItem in PlayersListBox.Items)
            {
                StackPanel panel = playerItem.Child as StackPanel;
                if (panel != null && panel.Children.Count > 0)
                {
                    CheckBox checkBox = panel.Children[0] as CheckBox;
                    if (checkBox != null)
                    {
                        checkBox.IsChecked = true;
                    }
                }
            }
            
            UpdateSelectedPlayersCount();
        }

        private void DeselectAllBtn_Click(object sender, RoutedEventArgs e)
        {
            foreach (Border playerItem in PlayersListBox.Items)
            {
                StackPanel panel = playerItem.Child as StackPanel;
                if (panel != null && panel.Children.Count > 0)
                {
                    CheckBox checkBox = panel.Children[0] as CheckBox;
                    if (checkBox != null)
                    {
                        checkBox.IsChecked = false;
                    }
                }
            }
            
            UpdateSelectedPlayersCount();
        }
    }
}