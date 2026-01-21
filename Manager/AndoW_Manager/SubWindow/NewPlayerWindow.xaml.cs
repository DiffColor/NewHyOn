using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HyonManager.Pages;
using HyonManager.SubWindow;
using HyonManager.DataClass;

namespace HyonManager.SubWindow
{
    /// <summary>
    /// NewPlayerWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class NewPlayerWindow : Window
    {
        EditPage1 g_ParentPage = null;

        public bool g_IsPlayerTypeLand = true;

        public NewPlayerWindow(EditPage1 paramPage)
        {
            InitializeComponent();
            g_ParentPage = paramPage;
            InitEventHandler();

            RectangleLand.Stroke = new SolidColorBrush(Colors.Gold);
            RectanglePortrait.Stroke = new SolidColorBrush(Colors.Gray);
        }

        public void InitEventHandler()
        {
            PageNavigator2.Click += PageNavigator2_Click;  // 취소

            LancScapeGrid.PreviewMouseMove += LancScapeGrid_PreviewMouseMove;
            LancScapeGrid.MouseLeave += LancScapeGrid_MouseLeave;
            LancScapeGrid.PreviewMouseLeftButtonUp += LancScapeGrid_PreviewMouseLeftButtonUp;

            PortraitGrid.PreviewMouseMove += PortraitGrid_PreviewMouseMove;
            PortraitGrid.MouseLeave += PortraitGrid_MouseLeave;
            PortraitGrid.PreviewMouseLeftButtonUp += LancScapeGrid_PreviewMouseLeftButtonUp;

            PageNavigator1.Click += PageNavigator1_Click;  // 추가       
        }


        void PageNavigator1_Click(object sender, RoutedEventArgs e)
        {
            if (TextBoxNewPlayerName.Text == string.Empty)
            {
                UtilityClass.ShowMessageBox("플레이어 이름을 입력해주세요.");
                return;
            }

            if (TextBoxNewPlayerName1.Text == string.Empty)
            {
                UtilityClass.ShowMessageBox("플레이어IP를 입력해주세요.");
                return;
            }

            PlayerInfoClass tmpCls = new PlayerInfoClass();
            tmpCls.PIF_PlayrName = TextBoxNewPlayerName.Text;
            tmpCls.PIF_PlayrIP = TextBoxNewPlayerName1.Text;

            if (g_IsPlayerTypeLand == true)
            {
                tmpCls.PIF_PlayerType = "LandScape";
            }
            else
            {
                tmpCls.PIF_PlayerType = "Portrait";    
            }

            tmpCls.PIF_DataFilename = string.Format("{0}_Shedule.xml", TextBoxNewPlayerName.Text);

            this.g_ParentPage.AddPlayerInfoClass(tmpCls);

            //UtilityClass.ShowMessageBox("플레이어를 추가했습니다.");
            this.Close();
        }

        public void DisplayPlayerType()
        {
            if (this.g_IsPlayerTypeLand == true)
            {
                this.g_IsPlayerTypeLand = false;
                RectangleLand.Stroke = new SolidColorBrush(Colors.Gray);
                RectanglePortrait.Stroke = new SolidColorBrush(Colors.Gold);
                //

            }
            else
            {
                this.g_IsPlayerTypeLand = true;
                RectangleLand.Stroke = new SolidColorBrush(Colors.Gold);
                RectanglePortrait.Stroke = new SolidColorBrush(Colors.Gray);
            }
        }

        void LancScapeGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DisplayPlayerType();
        }

        

        void PortraitGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            portraitText.Foreground = new SolidColorBrush(Colors.Gray);
        }

        void PortraitGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            portraitText.Foreground = new SolidColorBrush(Colors.White);
        }

        void LancScapeGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            landscapeText.Foreground = new SolidColorBrush(Colors.Gray);
        }

        void LancScapeGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            landscapeText.Foreground = new SolidColorBrush(Colors.White);
        }

        void PageNavigator2_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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
