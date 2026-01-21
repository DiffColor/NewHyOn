using System.Collections.Generic;
using System.Windows;
using System;
using System.Windows.Input;
using System.Linq;
using System.Threading;

namespace AndoW_Manager
{
    public partial class ContentsInfoBatchUpdateWindow : Window
    {      
        private static ContentsInfoBatchUpdateWindow instance = null;

        public static ContentsInfoBatchUpdateWindow Instance
        {
            get
            {
                return instance;
            }
        }

        public ContentsInfoBatchUpdateWindow()
        {
            InitializeComponent();
            instance = this;
            InitEventHandler();
        }

    
        public void InitComboBoxes()
        {
            MinCombo.Items.Clear();
            SecCombo.Items.Clear();

            for (int i = 0; i < 60; i++)
            {
                MinCombo.Items.Add(string.Format("{0:D2}", i));
                SecCombo.Items.Add(string.Format("{0:D2}", i));
            }

            MinCombo.SelectedIndex = 0;
            SecCombo.SelectedIndex = 10;
        }

        public void InitEventHandler()
        {
            this.Loaded += WindowLoaded;

            ChangeTimeBtn.Click += ChangeTimeBtn_Click;   //시간 변경

            DeleteBtn.Click += DeleteBtn_Click;
            SelectAllBtn.Click += BTN0DO_Copy1_Click;   // 전체 선택
            BTN0DO_Copy3.Click += BTN0DO_Copy3_Click;   // 전체 해제

            PreviewKeyDown += ContentsInfoBatchUpdateWindow_KeyDown;

            this.Closing += Window_Closing;
        }

        private void ChangeTimeBtn_Click(object sender, RoutedEventArgs e)
        {
            List<ContentsInfoClass> datalist = new List<ContentsInfoClass>();

            foreach (ContentInfoElement item in ContentListBox.Items)
            {
                if (ContentListBox.SelectedItems.Contains(item))
                {
                    item.g_ContentsInfoClass.CIF_PlayMinute = MinCombo.SelectedValue.ToString();
                    item.g_ContentsInfoClass.CIF_PlaySec = SecCombo.SelectedValue.ToString();
                    item.DisplayThisElementInfo();
                }

                datalist.Add(item.g_ContentsInfoClass);
            }

            Page1.Instance.UpdateContentsListByEditWindow(datalist);
        }

        bool altF4Pressed = false;
        void ContentsInfoBatchUpdateWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == System.Windows.Input.Key.F4)
                altF4Pressed = true;
        }

        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (altF4Pressed)
            {
                e.Cancel = true;
                altF4Pressed = false; 
                Hide();
                return;
            }

            instance = null;
        }

        void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            List<ContentsInfoClass> datalist = new List<ContentsInfoClass>();

            foreach (ContentInfoElement item in ContentListBox.Items)
            {
                if (ContentListBox.SelectedItems.Contains(item))
                    continue;

                datalist.Add(item.g_ContentsInfoClass);
            }

            Page1.Instance.UpdateContentsListByEditWindow(datalist);

            SetData();
        }

        void BTN0DO_Copy3_Click(object sender, RoutedEventArgs e)   // 플레이어 모두해제
        {
            ContentListBox.UnselectAll();
        }

        void BTN0DO_Copy1_Click(object sender, RoutedEventArgs e)   // 플레이어 모두선택
        {
            ContentListBox.SelectAll();
        }

        void WindowLoaded(object sender, RoutedEventArgs e)
        {
            InitComboBoxes();
        }

        private void BtnWin_close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Hide();  
        }

        private void BtnWin_drag_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                SetData();
            }
        }

        private void SetData()
        {
            ContentListBox.Items.Clear();

            int idx = 1;

            foreach (DisplayElementForEditor defe in Page1.Instance.g_DspElmtList)
            {
                if (defe.g_ElementInfoClass.EIF_Name == Page1.Instance.g_CurrentSelectedObjName)
                {
                    foreach (ContentsInfoClass item in defe.g_ElementInfoClass.EIF_ContentsInfoClassList)
                    {
                        ContentInfoElement tmpElement = new ContentInfoElement(item);
                        tmpElement.Width = 270;
                        tmpElement.Height = 27;
                        tmpElement.g_PreventMouse = true;
                        tmpElement.ExitColumn.Width = new GridLength(0, GridUnitType.Star);
                        tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                        tmpElement.Margin = new Thickness(2);
                        tmpElement.EditColumn.Width = new GridLength(0, GridUnitType.Star);
                        ContentListBox.Items.Add(tmpElement);
                        idx++;
                    }
                    break;
                }
            }

            NoContentsTxt.Visibility = ContentListBox.Items.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SecCombo_DropDownOpened(object sender, EventArgs e)
        {
            if (MinCombo.SelectedIndex > 0) return;

            string selectedString = SecCombo.SelectedItem as string;

            SecCombo.Items.Clear();
            for (int i = 5; i < 60; i++)
            {
                SecCombo.Items.Add(string.Format("{0:D2}", i));

            }

            if (int.Parse(selectedString) < 5) selectedString = "05";

            SecCombo.SelectedItem = selectedString;
        }

        private void MinCombo_DropDownClosed(object sender, EventArgs e)
        {
            string selectedString1 = MinCombo.SelectedItem as string;
            string selectedString = SecCombo.SelectedItem as string;

            int startIdx = 0;

            if (int.Parse(selectedString1) < 1) startIdx = 5;

            SecCombo.Items.Clear();
            for (; startIdx < 60; startIdx++)
            {
                SecCombo.Items.Add(string.Format("{0:D2}", startIdx));

            }

            if (int.Parse(selectedString) < 5) selectedString = "05";

            SecCombo.SelectedItem = selectedString;
        }


        ContentInfoElement leadCtrl;
        private void ContentListBox_LayoutUpdated(object sender, EventArgs e)
        {
            if (leadCtrl != null)
            {
                Point pt = leadCtrl.TranslatePoint(new Point(0, 0), ContentListBox);
                if (Point.Equals(org_pt, pt) == false)
                {
                    List<ContentsInfoClass> datalist = new List<ContentsInfoClass>();

                    int idx = 1;
                    foreach (ContentInfoElement cie in ContentListBox.Items)
                    {
                        cie.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                        idx++;

                        datalist.Add(cie.g_ContentsInfoClass);
                    }
                    org_pt = pt;

                    Page1.Instance.UpdateContentsListByEditWindow(datalist);
                }
                leadCtrl = null;
            }
        }

        Point org_pt;
        private void ContentListBox_Drop(object sender, DragEventArgs e)
        {
            leadCtrl = ContentListBox.SelectedItem as ContentInfoElement;

            if (leadCtrl != null)
                org_pt = leadCtrl.TranslatePoint(new Point(0, 0), ContentListBox);
        }

        private void ContentListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            int _cnt = ContentListBox.SelectedItems.Count;
            if (_cnt > 0 && _cnt < 2)
            {
                ContentInfoElement _cie = ContentListBox.SelectedItem as ContentInfoElement;
                MinCombo.SelectedItem = _cie.g_ContentsInfoClass.CIF_PlayMinute;
                SecCombo.SelectedItem = _cie.g_ContentsInfoClass.CIF_PlaySec;
            }
        }
    }
}
