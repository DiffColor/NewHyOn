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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using HyonManager.Pages;
using HyonManager.DataClass;
using HyonManager.SubElement;
using HyonManager.SubWindow;


namespace HyonManager.SubElement
{
    /// <summary>
    /// PlayerElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PlayerElement : UserControl
    {
        bool g_IsSelected = false;

        PlayerInfoClass g_PlayerInfoClass = new PlayerInfoClass();
        EditPage1 g_ParentPage = null;

        public PlayerElement(EditPage1 paramPage)
        {
            InitializeComponent();
            g_ParentPage = paramPage;

            InitEventHandler();
        }

        public void EditPlayerInfo(PlayerInfoClass paramCls)
        {
            g_PlayerInfoClass.CopyData(paramCls);
            DisplayThisElement();

            g_ParentPage.EditPlayerInfo(paramCls);

        }
        
        public void UpdatePlayerInfoCls(PlayerInfoClass paramCls)
        {
            g_PlayerInfoClass.CopyData(paramCls);
            DisplayThisElement();
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += PlayerElement_PreviewMouseLeftButtonDown;
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
            //this.PreviewMouseDoubleClick += SavedPageElement_PreviewMouseDoubleClick;

            ExitRect.PreviewMouseMove += ExitRect_PreviewMouseMove;
            ExitRect.MouseLeave += ExitRect_MouseLeave;
            ExitRect.PreviewMouseLeftButtonDown += ExitRect_PreviewMouseLeftButtonDown;


            BorderBTN_Copy.Click += BorderBTN_Copy_Click;
            this.PreviewMouseDoubleClick += PlayerElement_PreviewMouseDoubleClick;
        }

        void ExitRect_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (UtilityClass.ShowMessageBox("선택한 플레이어르 삭제하시겠습니까?") == true)
            {
                g_ParentPage.DeletePlayerInfo(this.g_PlayerInfoClass);
            }
        }

        void PlayerElement_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditPlayerWindow wnd = new EditPlayerWindow(this, g_PlayerInfoClass);
            wnd.ShowDialog();
        }

        void BorderBTN_Copy_Click(object sender, RoutedEventArgs e)
        {
            EditPlayerWindow wnd = new EditPlayerWindow(this, g_PlayerInfoClass);
            wnd.ShowDialog();
        }

        void PlayerElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            g_ParentPage.SelectCurrentPlayerInfo(g_PlayerInfoClass);
        }

        void ExitRect_MouseLeave(object sender, MouseEventArgs e)
        {
            ExitText.Foreground = new SolidColorBrush(Colors.Gray);
        }

        void ExitRect_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            ExitText.Foreground = new SolidColorBrush(Colors.White);

        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#B2296EB2");
                BGRect.Fill = new SolidColorBrush(c2);
                //TextBlockPageName.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {

                //#B272B2F1
                Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#B272B2F1");
                BGRect.Fill = new SolidColorBrush(c2);
                //TextBlockPageName.Foreground = new SolidColorBrush(Colors.Blue);
            }
        }

        public void DisplayThisElement()
        {
            textBoxPlayerName.Text = this.g_PlayerInfoClass.PIF_PlayrName;

            if (this.g_PlayerInfoClass.PIF_PlayerType == "LandScape")
            {
                PortraitGrid.Visibility = Visibility.Hidden;
                LancScapeGrid.Visibility = Visibility.Visible;
            }
            else
            {
                PortraitGrid.Visibility = Visibility.Visible;
                LancScapeGrid.Visibility = Visibility.Hidden;
            }
        
            
        }
    }
}
