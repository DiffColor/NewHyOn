using System.Collections.Generic;
using System.Windows;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// NotifyWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class EditContentsListWindow : Window
    {
        ContentsInfoClass g_CurSelectedInfo = new ContentsInfoClass();
        List<ContentsInfoClass> g_ContentsInfoClassList = new List<ContentsInfoClass>();
        public EditContentsListWindow(List<ContentsInfoClass> paramList)
        {
            InitializeComponent();
            InitEventHandler();
            
            g_ContentsInfoClassList.Clear();
            if (paramList.Count > 0)
            {
                foreach (ContentsInfoClass item in paramList)
                {
                    ContentsInfoClass tmpInfo = new ContentsInfoClass();
                    tmpInfo.CopyData(item);
                    g_ContentsInfoClassList.Add(tmpInfo);                 
                }
            }
            RefreshContentsList();
            RefreshPosComboBox();
            ResetTimeComboSelection();
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

        public void DeleteContentsInfo(string guidStr)
        {
           
            int idx = 0;
            foreach (ContentsInfoClass item in g_ContentsInfoClassList)
            {
                if (item.CIF_StrGUID == guidStr)
                {
                    break;
                }
                idx++;
            }

            if (g_ContentsInfoClassList.Count > idx)
            {
                g_ContentsInfoClassList.RemoveAt(idx);
                RefreshContentsList();
            }
        }

        private void ResetTimeComboSelection()
        {
            scrollSpeedComboBox_Copy1.SelectedIndex = 0;
            scrollSpeedComboBox_Copy.SelectedIndex = 0;
        }

        public void InitEventHandler()
        {
            BTN0DO_Copy4.Click += new RoutedEventHandler(BTNPagesListNew1_Click);  //OK
            BTN0DO_Copy.Click += new RoutedEventHandler(CancelBTN_Click);  //Cancel

            BTN0DO_Copy1.Click += BTN0DO_Copy1_Click;  // Save Selected ContentsData
            BTN0DO_Copy2.Click += BTN0DO_Copy2_Click;  // Goto Shift Selected ContentsInfo

            this.Closing += EditContentsListWindow_Closing;
        }

        void EditContentsListWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageTools.ShowMessageBox("변경된 정보를 저장하시겠습니까?", "예", "아니오") == true)
            {
                Page1.Instance.UpdateContentsListByEditWindow(this.g_ContentsInfoClassList);
            }
        }

        void BTN0DO_Copy2_Click(object sender, RoutedEventArgs e)
        {
            if (this.g_CurSelectedInfo.CIF_FileName != string.Empty)
            {
                int idx = 0;
                foreach (ContentsInfoClass item in g_ContentsInfoClassList)
                {
                    if (item.CIF_StrGUID == g_CurSelectedInfo.CIF_StrGUID)
                    {                       
                        break;
                    }
                    idx++;
                }

                if (g_ContentsInfoClassList.Count > idx)
                {
                    ContentsInfoClass tmpCls = new ContentsInfoClass();
                    tmpCls.CopyData(g_CurSelectedInfo);
                    g_ContentsInfoClassList.RemoveAt(idx);
                    g_ContentsInfoClassList.Insert(scrollSpeedComboBox_Copy2.SelectedIndex, tmpCls);
                    RefreshContentsList();
                }
            }
        }

        void BTN0DO_Copy1_Click(object sender, RoutedEventArgs e)
        {
            if (this.g_CurSelectedInfo.CIF_FileName != string.Empty)
            {
                g_CurSelectedInfo.CIF_PlayMinute = scrollSpeedComboBox_Copy1.SelectedItem.ToString();
                g_CurSelectedInfo.CIF_PlaySec =   scrollSpeedComboBox_Copy.SelectedItem.ToString() ;
                
                foreach (ContentsInfoClass item in g_ContentsInfoClassList)
                {
                    if (item.CIF_StrGUID == g_CurSelectedInfo.CIF_StrGUID)
                    {
                        item.CIF_PlayMinute = g_CurSelectedInfo.CIF_PlayMinute;
                        item.CIF_PlaySec = g_CurSelectedInfo.CIF_PlaySec;
                        break;
                    }
                }

                RefreshContentsList();
            }
            else
            {
                MessageTools.ShowMessageBox("컨텐츠를 먼저 선택해주세요.", "확인");
            }
        }


        public void RefreshContentsList()
        {
            wrapPanelTemplate.Children.Clear();
            int idx = 1;
            foreach (ContentsInfoClass item in g_ContentsInfoClassList)
            {
                ContentsEditInfoElement tmpElement = new ContentsEditInfoElement(this);
                tmpElement.UpdateDataInfo(item);
                tmpElement.TextBlockOrderingNumber.Text = string.Format("[{0:D3}]", idx);
                tmpElement.Margin = new Thickness(4, 4, 0, 0);
                wrapPanelTemplate.Children.Add(tmpElement);
                idx++;
            }
        }
        
        public void SelectContentsInfo(ContentsInfoClass paramCls)
        {
            g_CurSelectedInfo.CopyData(paramCls);
            DisplaySelectedContentsInfo();
                  
        }

        public void DisplaySelectedContentsInfo()
        {
            if (g_CurSelectedInfo.CIF_FileName != string.Empty)
            {
                TextAngleGrade5_Copy1.Text = g_CurSelectedInfo.CIF_FileName;

                scrollSpeedComboBox_Copy1.SelectedItem = g_CurSelectedInfo.CIF_PlayMinute;
                scrollSpeedComboBox_Copy.SelectedItem = g_CurSelectedInfo.CIF_PlaySec;
                TextAngleGrade5_Copy4.Text = g_CurSelectedInfo.CIF_ContentType;

                int idx = 0;
                foreach (ContentsInfoClass item in g_ContentsInfoClassList)
                {
                    if (item.CIF_StrGUID == g_CurSelectedInfo.CIF_StrGUID)
                    {
                        break;
                    }
                    idx++;
                }

                if (g_ContentsInfoClassList.Count > idx)
                {
                    scrollSpeedComboBox_Copy2.SelectedIndex = idx;
                }
            }

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
