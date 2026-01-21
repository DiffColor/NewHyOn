using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace AndoW_Manager
{
    /// <summary>
    /// NotifyWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class EditWelcomeBoardWindow : Window
    {
        ContentsInfoClass g_CurSelectedInfo = new ContentsInfoClass();
        List<ContentsInfoClass> g_ContentsInfoClassList = new List<ContentsInfoClass>();
        public EditWelcomeBoardWindow()
        {
            InitializeComponent();
            InitEventHandler();
            g_ContentsInfoClassList.Clear();
        }

        public void RefreshPosComboBox()
        {
            scrollSpeedComboBox_Copy2.Items.Clear();

            int idx = 1;
            foreach (ContentsInfoClass item in g_ContentsInfoClassList)
            {
                scrollSpeedComboBox_Copy2.Items.Add(idx);
                idx++;
            }

            scrollSpeedComboBox_Copy2.SelectedIndex = 0;
        }

        double g_FitscaleValueX = 0;
        double g_FitscaleValueY = 0;

        public void AdjustCanvasSize()
        {
            GuideBorder.UpdateLayout();

            g_FitscaleValueX = GuideBorder.ActualWidth / DesignerCanvas.Width;
            g_FitscaleValueY = GuideBorder.ActualHeight / DesignerCanvas.Height;

        
            
            if (DesignerCanvas.ActualWidth > DesignerCanvas.ActualHeight)
            {
                if (g_FitscaleValueX > g_FitscaleValueY)
                {
                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueY);
                    DesignerCanvas.RenderTransform = scale;
                    
                }
                else
                {
                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueX);
                    DesignerCanvas.RenderTransform = scale;
                }

            
            }
            else
            {
                if (g_FitscaleValueX < g_FitscaleValueY)
                {
                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueX, g_FitscaleValueY);
                    DesignerCanvas.RenderTransform = scale;
                }
                else
                {
                    ScaleTransform scale = new ScaleTransform(g_FitscaleValueY, g_FitscaleValueY);
                    DesignerCanvas.RenderTransform = scale;
                  
                }
            }
            /**/
        }
        public void InitComboBoxes()
        {
            scrollSpeedComboBox_Copy1.SelectedIndex = 0;
            scrollSpeedComboBox_Copy.SelectedIndex = 0;
        }

        public void InitEventHandler()
        {
            BTN0DO_Copy4.Click += new RoutedEventHandler(BTNPagesListNew1_Click);  //OK
            BTN0DO_Copy.Click += new RoutedEventHandler(CancelBTN_Click);  //Cancel

            this.Loaded += EditWelcomeBoardWindow_Loaded;

            this.Closing += EditContentsListWindow_Closing;
        }

        void EditWelcomeBoardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AdjustCanvasSize();
        }

        void EditContentsListWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //if (MessageTools.ShowMessageBox("변경된 정보를 저장하시겠습니까?", "예", "아니오") == true)
            //{
            //    this.g_ParentPage.UpdateContentsListByEditWindow(this.g_ContentsInfoClassList);
            //}
        }

        void CancelBTN_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        void BTNPagesListNew1_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
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
