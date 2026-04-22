using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;

using System.IO;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class Page2 : UserControl
    {
        public bool g_IsSelected = false;
     
        public int g_Idx = 0;

        string g_CurrentSelectedPageListName = string.Empty;

        public PageInfoClass g_CurrentSelectedPageInfo = new PageInfoClass();

        List<PageListNameElement> g_PageListNameElementList = new List<PageListNameElement>();

        public PlayListBatchUpdateWindow g_BatchUpdateWnd = null;

        public string CurrentSelectedPageListName => g_CurrentSelectedPageListName;

        public static Page2 Instance { get; set; }

        public Page2()
        {
            InitializeComponent();

            Instance = this;

            InitEventHandler();
            g_BatchUpdateWnd = new PlayListBatchUpdateWindow();
        }

        public bool HasAvailableUsb()
        {
            return GetAvailableUsbNames().Count > 0;
        }

        public List<string> GetAvailableUsbNames()
        {
            List<string> usbs = new List<string>();
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive == null || drive.DriveType != DriveType.Removable || drive.IsReady == false)
                    {
                        continue;
                    }

                    string name = drive.Name;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    usbs.Add(name.Replace(":\\", "").Replace(":", "").Replace("\\", ""));
                }
            }
            catch
            {
                usbs.Clear();
            }

            return usbs;
        }

        public void ExportPlaylistToUsb(string pageListName)
        {
            if (string.IsNullOrWhiteSpace(pageListName))
            {
                MessageTools.ShowMessageBox("선택된 페이지 리스트가 없습니다.", "확인");
                return;
            }

            var pageList = DataShop.Instance?.g_PageListInfoManager?.GetPageListByName(pageListName);
            if (pageList == null)
            {
                MessageTools.ShowMessageBox(string.Format("[{0}] 플레이리스트를 찾을 수 없습니다.", pageListName), "확인");
                return;
            }

            List<string> usblist = GetAvailableUsbNames();

            if (usblist.Count < 1)
            {
                MessageTools.ShowMessageBox("이동 저장소가 없습니다.");
                return;
            }

            SelectUSBWindow usbw = new SelectUSBWindow(usblist, pageListName);
            usbw.ShowDialog();
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += UserControl1_PreviewMouseLeftButtonDown;

            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);

            BTN0DO_Copy3.Click += BTN0DO_Copy3_Click;  // PageList 추가

            BTN0DO_Copy4.Click += BTN0DO_Copy4_Click;   // PageInfo ShiftUp
            BTN0DO_Copy5.Click += BTN0DO_Copy5_Click;   // PageInfo ShiftDown

            //ExitTextBlock.PreviewMouseMove += new MouseEventHandler(ExitTextBlock_PreviewMouseMove);
            //ExitTextBlock.MouseLeave += new MouseEventHandler(ExitTextBlock_MouseLeave);
            //ExitTextBlock.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(ExitTextBlock_PreviewMouseLeftButtonUp);
        }

        void BTN0DO_Copy5_Click(object sender, RoutedEventArgs e)  // PageInfo ShiftDown
        {
            if (g_CurrentSelectedPageInfo.PIC_PageName == string.Empty)
            {
                return;
            }

            DataShop.Instance.g_PageInfoManager.ShiftDownDataInfo(g_CurrentSelectedPageInfo, this.g_CurrentSelectedPageListName);

            RefreshPageListOfSelectedPageList(g_CurrentSelectedPageListName);
        }

        void BTN0DO_Copy4_Click(object sender, RoutedEventArgs e)   // PageInfo ShiftUp
        {
            if (g_CurrentSelectedPageInfo.PIC_PageName == string.Empty)
            {
                return;
            }

            DataShop.Instance.g_PageInfoManager.ShiftUpDataInfo(g_CurrentSelectedPageInfo, this.g_CurrentSelectedPageListName);

            RefreshPageListOfSelectedPageList(g_CurrentSelectedPageListName);

         
        }

        void BTN0DO_Copy3_Click(object sender, RoutedEventArgs e)
        {
            TextBoxNewPlayerName_Copy.Text = TextBoxNewPlayerName_Copy.Text.Trim();

            if (string.IsNullOrEmpty(TextBoxNewPlayerName_Copy.Text))
            {
                MessageTools.ShowMessageBox("플레이리스트의 이름을 입력해주세요.", "확인");
                return;
            }

            if (FileTools.HasWrongPathCharacter(TextBoxNewPlayerName_Copy.Text) || TextBoxNewPlayerName_Copy.Text.Contains(","))
            {
                MessageTools.ShowMessageBox("플레이리스트 이름에 (,) 또는 사용할 수 없는 특수문자가 포함되어있습니다.", "확인");
                return;
            }

            if (g_PageListNameElementList.Exists(x => x.g_PageListInfoClass.PLI_PageListName.Equals(TextBoxNewPlayerName_Copy.Text, StringComparison.CurrentCultureIgnoreCase)))
            {
                MessageTools.ShowMessageBox("다른 플레이리스트 이름을 입력해주세요.", "확인");
                TextBoxNewPlayerName_Copy.Text = string.Empty;
                return;
            }

            try
            {
                MainWindow.Instance.AddNewPageList(TextBoxNewPlayerName_Copy.Text);
                SelectPageList(TextBoxNewPlayerName_Copy.Text);
                TextBoxNewPlayerName_Copy.Text = string.Empty;

                ContentsListScrollViewer1.ScrollToBottom();          
            }
            catch (Exception ex)
            { 
                
            }
        }

        public void SaveDefaultPlaylist(string pagename, PageInfoClass pageDefinition = null)
        {
            try
            {
                EnsureDefaultPlaylistExists(pagename, pageDefinition);
                g_CurrentSelectedPageListName = pagename;

                RefreshPageNameList();

                bool addedPage = false;
                if (pageDefinition != null)
                {
                    AddPageToPageList(pagename, pageDefinition);
                    addedPage = true;
                }
                else
                {
                    RefreshPageListOfSelectedPageList(g_CurrentSelectedPageListName);
                }

                SelectPageList(pagename);

                if (!addedPage)
                {
                    Page3.Instance.UpdatePListComboForPlayer();
                }

                if (PlayListBatchUpdateWindow.Instance != null)
                    PlayListBatchUpdateWindow.Instance.InitComboBoxes();
            }
            catch (Exception ex)
            {

            }
        }

        private void EnsureDefaultPlaylistExists(string playlistName, PageInfoClass definition)
        {
            var pageList = DataShop.Instance.g_PageListInfoManager.GetPageListByName(playlistName);
            if (pageList != null)
            {
                return;
            }

            PageListInfoClass newList = new PageListInfoClass
            {
                PLI_PageListName = playlistName,
                PLI_PageDirection = definition != null
                    ? (definition.PIC_IsLandscape ? DeviceOrientation.Landscape.ToString() : DeviceOrientation.Portrait.ToString())
                    : DeviceOrientation.Landscape.ToString()
            };

            DataShop.Instance.g_PageListInfoManager.AddPageListInfoClass(newList);
            DataShop.Instance.g_PageListInfoManager.LoadDataFromDatabase();
            RefreshSavedPageList();
        }

        public void RemovePageAndList(string pagename)
        {
            DataShop.Instance.g_PageListInfoManager.DeletePageListInfoByName(pagename);
            RefreshPageNameList();
            RefreshPageListOfSelectedPageList();
            MainWindow.Instance.RefreshSavedPageList();
            Page3.Instance.UpdatePlayListForPlayer();
        }

        void UserControl1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //this.g_ParentWnd.SelectInputChannelData(this.g_InputChannelInfoClass);
            //throw new NotImplementedException();
        }

        public void UpdateDataInfo(int idx)
        {
            g_Idx = idx;
            DisplayDataInfo();
        }

        public void DisplayDataInfo()
        {
            //TextBlockOrderingNumber.Text = g_Idx.ToString();

        }

        public void RefreshPageListOfSelectedPageList()
        {
            if (g_CurrentSelectedPageListName != string.Empty)
            {
                DataShop.Instance.g_PageInfoManager.LoadPagesForList(g_CurrentSelectedPageListName);

                ContentsElementsStackPannel2.Children.Clear();

                if (DataShop.Instance.g_PageInfoManager.g_PageInfoClassList.Count > 0)
                {
                    int idx = 1;
                    foreach (PageInfoClass item in DataShop.Instance.g_PageInfoManager.g_PageInfoClassList)
                    {
                        PageInfoElement tmpElement = new PageInfoElement();
                        tmpElement.g_PreviewThumbBase64 = item.PIC_Thumb;

                        tmpElement.UpdateDataInfo(item, idx);

                        tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                        tmpElement.Margin = new Thickness(5, 2, 5, 0);
                        ContentsElementsStackPannel2.Children.Add(tmpElement);
                        idx++;

                    }

                    NoListStackPanel.Visibility = System.Windows.Visibility.Hidden;
                }
                else
                {
                    NoListStackPanel.Visibility = System.Windows.Visibility.Visible;
                }
            }
            else
            {
                ClearIncludedPageList();
                NoListStackPanel.Visibility = System.Windows.Visibility.Visible;
            }

        }

        //public void RefreshPageListOfSelectedPageList()
        //{
        //    if (g_CurrentSelectedPageListName != string.Empty)
        //    {
        //        ILYCODEDataShop.Instance.g_PageInfoManager.LoadPagesForList(g_CurrentSelectedPageListName);


        //        ContentsElementsStackPannel2.Children.Clear();

        //        if (ILYCODEDataShop.Instance.g_PageInfoManager.g_PageInfoClassList.Count > 0)
        //        {
        //            int idx = 1;
        //            foreach (PageInfoClass item in ILYCODEDataShop.Instance.g_PageInfoManager.g_PageInfoClassList)
        //            {
        //                PageInfoElement tmpElement = new PageInfoElement(this);

        //                tmpElement.UpdateDataInfo(item, idx);

        //                tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
        //                tmpElement.Margin = new Thickness(0, 0, 0, 0);
        //                ContentsElementsStackPannel2.Children.Add(tmpElement);
        //                idx++;

        //            }
        //        }
        //    }
        //    else
        //    {
        //        ClearIncludedPageList();
        //    }
           
        //}

        public void RefreshPageListOfSelectedPageList(string paramPageListName)
        {
            DataShop.Instance.g_PageInfoManager.LoadPagesForList(paramPageListName);

            ContentsElementsStackPannel2.Children.Clear();

            if (DataShop.Instance.g_PageInfoManager.g_PageInfoClassList.Count > 0)
            {
                int idx = 1;
                foreach (PageInfoClass item in DataShop.Instance.g_PageInfoManager.g_PageInfoClassList)
                {
                    PageInfoElement tmpElement = new PageInfoElement();
                    tmpElement.g_PreviewThumbBase64 = item.PIC_Thumb;

                    tmpElement.UpdateDataInfo(item, idx);

                    tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                    tmpElement.Margin = new Thickness(5, 2, 5, 0);
                    ContentsElementsStackPannel2.Children.Add(tmpElement);
                    idx++;

                }

                NoListStackPanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                NoListStackPanel.Visibility = System.Windows.Visibility.Visible;
            }

            SelectPageInfoOfPageList(g_CurrentSelectedPageInfo);
        }


        //public void RefreshPageListOfSelectedPageList(string paramPageListName)
        //{
        //    ILYCODEDataShop.Instance.g_PageInfoManager.LoadPagesForList(paramPageListName);


        //    ContentsElementsStackPannel2.Children.Clear();

        //    if (ILYCODEDataShop.Instance.g_PageInfoManager.g_PageInfoClassList.Count > 0)
        //    {
        //        int idx = 1;
        //        foreach (PageInfoClass item in ILYCODEDataShop.Instance.g_PageInfoManager.g_PageInfoClassList)
        //        {
        //            PageInfoElement tmpElement = new PageInfoElement(this);

        //            tmpElement.UpdateDataInfo(item, idx);
              
        //            tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
        //            tmpElement.Margin = new Thickness(0, 0, 0, 0);
        //            ContentsElementsStackPannel2.Children.Add(tmpElement);
        //            idx++;

        //        }
        //    }
        //}

        public void CopyPlayList(string paramPlayListName)
        {
            DataShop.Instance.g_PageInfoManager.LoadPagesForList(paramPlayListName);

            ContentsElementsStackPannel2.Children.Clear();

            string newPlayListName = string.Format("{0}_Copy", paramPlayListName);
            if (DataShop.Instance.g_PageInfoManager.g_PageInfoClassList.Count > 0)
            {

                PageListInfoClass tmpCls = new PageListInfoClass();
                tmpCls.PLI_PageListName = newPlayListName;
                tmpCls.PLI_PageDirection = DataShop.Instance.g_PageInfoManager.g_PageInfoClassList[0].PIC_IsLandscape ? DeviceOrientation.Landscape.ToString() : DeviceOrientation.Portrait.ToString();
                DataShop.Instance.g_PageListInfoManager.AddPageListInfoClass(tmpCls);


              //  ILYCODEDataShop.Instance.g_PageInfoManager.LoadPagesForList(paramPageListName);
                DataShop.Instance.g_PageInfoManager.SavePageList(newPlayListName);
                RefreshPageNameList();

                Page3.Instance.UpdatePlayListForPlayer();


                //int idx = 1;
                //foreach (PageInfoClass item in ILYCODEDataShop.Instance.g_PageInfoManager.g_PageInfoClassList)
                //{
                //    PageInfoElement tmpElement = new PageInfoElement(this);

                //    tmpElement.UpdateDataInfo(item, idx);

                //    tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                //    tmpElement.Margin = new Thickness(5, 2, 5, 0);
                //    ContentsElementsStackPannel2.Children.Add(tmpElement);
                //    idx++;

                //}
            }
            else
            {
                MessageTools.ShowMessageBox("비어있는 플레이리스트는 복사할 수 없습니다.", "확인");
            }
        }


        public void EditPlayListName(string prevName, string newName)
        {
            DataShop.Instance.g_PageInfoManager.LoadPagesForList(prevName);
            DataShop.Instance.g_PageInfoManager.SavePageList(newName);
            DataShop.Instance.g_PageListInfoManager.DeletePageListInfoByName(prevName);            
            
            PageListInfoClass tmpCls = new PageListInfoClass();
            tmpCls.PLI_PageListName = newName;

            if (DataShop.Instance.g_PageInfoManager.g_PageInfoClassList.Count > 0)
                tmpCls.PLI_PageDirection = DataShop.Instance.g_PageInfoManager.g_PageInfoClassList[0].PIC_IsLandscape ? DeviceOrientation.Landscape.ToString() : DeviceOrientation.Portrait.ToString();
            
            DataShop.Instance.g_PageListInfoManager.AddPageListInfoClass(tmpCls);
        
            ContentsElementsStackPannel2.Children.Clear();

            Page3.Instance.UpdatePlayListForPlayer();

            RefreshPageNameList();

        }



        public void ClearIncludedPageList()
        {
            ContentsElementsStackPannel2.Children.Clear();

        }

        public void EditPageInfoOfPageList(PageInfoClass paramCls, int paramIdx)
        {
            DataShop.Instance.g_PageInfoManager.EditDeviceInfoClass(g_CurrentSelectedPageListName, paramCls, paramIdx);
            RefreshPageListOfSelectedPageList(g_CurrentSelectedPageListName);
        }


        public void DeletePageInfoOfPageList(PageInfoClass paramCls)
        {
            DataShop.Instance.g_PageInfoManager.DeletePageInfoClass(g_CurrentSelectedPageListName, paramCls);

            RefreshPageListOfSelectedPageList(g_CurrentSelectedPageListName);
            MainWindow.Instance.RefreshSavedPageList();
            Page3.Instance.UpdatePListComboForPlayer();
        }

        public void DeleteThePageListName(PageListInfoClass paramCls)
        {
            DataShop.Instance.g_PageListInfoManager.DeletePageListInfo(paramCls);

            ContentsElementsStackPannel2.Children.Clear();
            TextBoxNewPlayerName_Copy.Text = string.Empty;
            CurrentPageListName.Text = "[PageName]";

            RefreshPageNameList();

            MainWindow.Instance.RefreshSavedPageList();

            Page3.Instance.UpdatePlayListForPlayer();
        }


        //public void RefreshPageNameList()
        //{
        //    g_PageListNameElementList.Clear();
        //    GC.Collect();
        //    ContentsElementsStackPannel1.Children.Clear();
        //    int idx = 1;

        //    foreach (PageListInfoClass item in ILYCODEDataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList)
        //    {
        //        PageListNameElement tmpElement = new PageListNameElement(this);
        //        tmpElement.UpdateDataInfo(item);
        //        tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
        //        tmpElement.Margin = new Thickness(0, 0, 0, 0);
        //        ContentsElementsStackPannel1.Children.Add(tmpElement);
        //        idx++;

        //        g_PageListNameElementList.Add(tmpElement);
        //    }
        //}

        public void RefreshPageNameList()   // PlayList Refresh
        {
            g_PageListNameElementList.Clear();
            GC.Collect();
            ContentsElementsStackPannel1.Children.Clear();
            int idx = 1;

            foreach (PageListInfoClass item in DataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList)
            {
                PageListNameElement tmpElement = new PageListNameElement();
                tmpElement.UpdateDataInfo(item);
                tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                tmpElement.Margin = new Thickness(0, 0, 0, 0);
                ContentsElementsStackPannel1.Children.Add(tmpElement);
                idx++;

                g_PageListNameElementList.Add(tmpElement);
            }

            if (DataShop.Instance.g_PageListInfoManager.g_PageListInfoClassList.Count > 0)
            {
                NoListGrid.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                NoListGrid.Visibility = System.Windows.Visibility.Visible;
            }
         
        }


        public void SelectPageInfoOfPageList(PageInfoClass paramCls)
        {
            g_CurrentSelectedPageInfo.CopyData(paramCls);
            SwitchSelectedPage(paramCls);
        }

        private void SwitchSelectedPage(PageInfoClass paramCls)
        {
            foreach (PageInfoElement pie in ContentsElementsStackPannel2.Children)
            {
                if (pie.g_PageInfoClass.PIC_GUID.Equals(paramCls.PIC_GUID))
                {
                    pie.Selected = true;
                    continue;
                }
                pie.Selected = false;
            }
            return;
        }

        public void RefreshSavedPageList()
        {
            wrapPanelTemplate.Children.Clear();

            List<PageInfoClass> savedPages = DataShop.Instance.g_PageInfoManager.GetAllSavedPages();
            if (savedPages.Count == 0)
            {
                ContentsListScrollViewer.ScrollToBottom();
                return;
            }

            foreach (PageInfoClass pageInfo in savedPages)
            {
                SavedPageElement2 tmpElement = new SavedPageElement2();
                tmpElement.g_PreviewThumbBase64 = pageInfo.PIC_Thumb;

                if (pageInfo.PIC_IsLandscape)
                {
                    tmpElement.Width = 148;
                    tmpElement.Height = 100;
                }
                else
                {
                    tmpElement.Width = 90;
                    tmpElement.Height = 170;
                }

                tmpElement.Margin = new Thickness(7, 5, 7, 5);

                BitmapSource preview = tmpElement.LoadPreviewImage();
                if (preview != null)
                {
                    tmpElement.pagePreviewImge.Source = preview;
                }

                tmpElement.pageNameTextBlock.Text = pageInfo.PIC_PageName;
                wrapPanelTemplate.Children.Add(tmpElement);
            }

            ContentsListScrollViewer.ScrollToBottom();
        }

        public void AddPageToPageList(string paramPageName, PageInfoClass pageDefinition = null)
        {
            if (this.g_CurrentSelectedPageListName != string.Empty)
            {
                PageInfoClass definition = pageDefinition ?? DataShop.Instance.g_PageInfoManager.GetPageDefinition(paramPageName);
                if (definition == null || string.IsNullOrWhiteSpace(definition.PIC_GUID))
                {
                    MessageTools.ShowMessageBox("저장된 페이지 정보를 찾을 수 없습니다.", "확인");
                    return;
                }

                var list = DataShop.Instance.g_PageListInfoManager.GetOrCreatePageList(g_CurrentSelectedPageListName);
                if (string.IsNullOrWhiteSpace(list.PLI_PageDirection))
                {
                    list.PLI_PageDirection = definition.PIC_IsLandscape ? DeviceOrientation.Landscape.ToString() : DeviceOrientation.Portrait.ToString();
                    DataShop.Instance.g_PageListInfoManager.SavePageList(list);
                }

                DataShop.Instance.g_PageListInfoManager.AddPageToPlaylist(g_CurrentSelectedPageListName, definition.PIC_GUID);

                RefreshPageListOfSelectedPageList(g_CurrentSelectedPageListName);

                Page3.Instance.UpdatePListComboForPlayer();
            }
            else
            {
                MessageTools.ShowMessageBox("선택된 페이지 리스트가 없습니다.", "확인");
            }
        }

        public void SelectPageList(string paramPageListName)
        {
            g_CurrentSelectedPageListName = paramPageListName;
            CurrentPageListName.Text = string.Format("[{0}]", paramPageListName);

            ///////////////////////////////////////////////////////////////
            //  페이지리스트 선택에 관련된것
            foreach (PageListNameElement item in g_PageListNameElementList)
            {
                if (item.g_PageListInfoClass.PLI_PageListName == paramPageListName)
                {
                    item.ShowAndHideSelectedBorder(true);
                }
                else
                {
                    item.ShowAndHideSelectedBorder(false);
                }
            }
            //
            ////////////////////////////////////////////////////////////////


            RefreshPageListOfSelectedPageList(paramPageListName);
        }

        void ExitTextBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //if (MessageTools.ShowMessageBox(string.Format("선택한 <{0}>를(을) 삭제하시겠습니까?", TextBlockPageName_Copy3.Text)) == true)
            //{
            //    //this.parentPage.DeleteStartStationNametByName(TextBlockPageName.Text);
            //}
        }

        //#FF212121
        void ExitTextBlock_MouseLeave(object sender, MouseEventArgs e)
        {
            Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#FF808080");
            //ExitTextBlock.Foreground = new SolidColorBrush(c2);
            //ExitTextBlock.Foreground = new SolidColorBrush(Colors.Black);
        }

        void ExitTextBlock_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            //ExitTextBlock.Foreground = new SolidColorBrush(Colors.Gray);
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                //Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#FF212121");
                //BackRectangle.Fill = new SolidColorBrush(c2);
                //BackRectangle.Fill = new SolidColorBrush(Colors.Black);
            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                //BackRectangle.Fill = new SolidColorBrush(Colors.Gray);
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                //object data = this.g_InputChannelInfoClass.ICF_ChannelName;
                //DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);

               // DragDrop.DoDragDrop(this, (string)this.g_InputChannelInfoClass.ICF_ChannelName, DragDropEffects.Copy);
            }
        }

        private void BTN0DO_Copy2_Click(object sender, RoutedEventArgs e)
        {
            g_BatchUpdateWnd.ShowDialog();
        }

        private void BtnWin_close_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.GotoPageByName("Page3");
        }
    }
}
