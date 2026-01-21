using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.IO;
using System;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// SelectAlreadyExistFileWindow.xaml에 대한 상호 작용 논리
    /// </summary>
     public partial class SelectAlreadyExistFileWindow : Window
    {
        public List<string> videoExtentionSetList = new List<string>();
        public List<string> imageExtentionSetList = new List<string>();

        private static SelectAlreadyExistFileWindow instance = null;

        public static SelectAlreadyExistFileWindow Instance
        {
            get
            {
                return instance;
            }
        }


        public List<ContentsInfoClass> g_SelectedContentsList = new List<ContentsInfoClass>();


        public SelectAlreadyExistFileWindow()
        {
            InitializeComponent();
            instance = this;
            InitEventHandler();
            InitExtentionSet();
        }

        public void RefreshSelectedContentList()
        {
            ContentsElementsStackPannel4.Children.Clear();


            int idx = 1;
            foreach (ContentsInfoClass item in this.g_SelectedContentsList)
            {
                SelectedContentInfoElement tmpElement = new SelectedContentInfoElement(this, item);
                //tmpElement.Width = 305;
                tmpElement.Height = 23;
                tmpElement.TextBlockOrderingNumber.Text = string.Format("{0:D2}", idx);
                tmpElement.Margin = new Thickness(5, 2, 5, 0);
                ContentsElementsStackPannel4.Children.Add(tmpElement);
                idx++;
            }

        }

        public void EditContentsPlayTime(ContentsInfoClass paramcls)
        {
            int idx = 0;
            foreach (ContentsInfoClass item in g_SelectedContentsList)
            {
                if (item.CIF_StrGUID == paramcls.CIF_StrGUID)
                {
                    item.CopyDataWithOutGUID(paramcls);
                    break;
                }
                idx++;
            }
        }

        public void DeleteContentsList(ContentsInfoClass paramcls)
        {
            int idx = 0;
            foreach (ContentsInfoClass item in g_SelectedContentsList)
            {
                if (item.CIF_StrGUID == paramcls.CIF_StrGUID)
                {
                    break;
                }
                idx++;
            }

            if (idx <g_SelectedContentsList.Count )
            {
                g_SelectedContentsList.RemoveAt(idx);
                RefreshSelectedContentList();
            }
         
        }

        public void InitExtentionSet()
        {
            videoExtentionSetList.Clear();
            imageExtentionSetList.Clear();

            ///-----------Video 확장자 설정---------------------///
            videoExtentionSetList.Add(".avi");
            videoExtentionSetList.Add(".mp4");
            videoExtentionSetList.Add(".3gp");
            videoExtentionSetList.Add(".mov");
            videoExtentionSetList.Add(".mpg");
            videoExtentionSetList.Add(".mpeg");
            videoExtentionSetList.Add(".m2ts");
            videoExtentionSetList.Add(".ts");
            videoExtentionSetList.Add(".wmv");
            videoExtentionSetList.Add(".asf");

            ///-----------Image 확장자 설정---------------------///
            imageExtentionSetList.Add(".jpg");
            imageExtentionSetList.Add(".jpeg");
            imageExtentionSetList.Add(".bmp");
            imageExtentionSetList.Add(".png");
            imageExtentionSetList.Add(".gif");
        }

        public void InitEventHandler()
        {
            this.Loaded += SelectAlreadyExistFileWindow_Loaded;
            BTN0DO_Copy4.Click += new RoutedEventHandler(BTNPagesListNew1_Click);  //OK
            BTN0DO_Copy.Click += new RoutedEventHandler(CancelBTN_Click);  //Cancel
        }

        void SelectAlreadyExistFileWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ContentsElementsStackPannel4.Children.Clear(); // <--- 선택된 컨텐츠 파일 스택패널
            RefreshSavedPageList();
        }

        public void RefreshSavedPageList()
        {
            ContentsElementsStackPannel.Children.Clear();   // <---- 동영상 스택패널
            ContentsElementsStackPannel2.Children.Clear();  //  <--- 이미지 스택패널
            ContentsElementsStackPannel1.Children.Clear();  //  <---  플래시 스택패널
            ContentsElementsStackPannel3.Children.Clear();  //  <---- 기타  스택패널



            List<string> filenames = new List<string>();
            filenames.Clear();


            string[] strListNames = Directory.GetFiles(FNDTools.GetContentsDirPath());


            int tmpIndexCount1 = 1;
            int tmpIndexCount2 = 1;
            int tmpIndexCount3 = 1;
            int tmpIndexCount4 = 1;

            foreach (string item in strListNames)
            {
                string fileExtension = new System.IO.FileInfo(item).Extension.ToLowerInvariant();
                  string fileName = System.IO.Path.GetFileName(item);

                ExistContentFileNameElement tmpFileElement = new ExistContentFileNameElement(this, fileName, item);
                tmpFileElement.Margin = new Thickness(4, 2, 4, 0);

                if (this.videoExtentionSetList.Contains(fileExtension) == true)
                {
                    tmpFileElement.TextBlockOrderingNumber.Text = tmpIndexCount1.ToString();
                    ContentsElementsStackPannel.Children.Add(tmpFileElement);
                    tmpIndexCount1++;
                }
                else if (this.imageExtentionSetList.Contains(fileExtension) == true)
                {
                    tmpFileElement.TextBlockOrderingNumber.Text = tmpIndexCount2.ToString();
                    ContentsElementsStackPannel2.Children.Add(tmpFileElement);
                    tmpIndexCount2++;
                }
                else if (fileExtension.Equals(".swf", StringComparison.CurrentCultureIgnoreCase))
                {
                    tmpFileElement.TextBlockOrderingNumber.Text = tmpIndexCount3.ToString();
                    ContentsElementsStackPannel1.Children.Add(tmpFileElement);
                    tmpIndexCount3++;
                }
                else
                {
                    tmpFileElement.TextBlockOrderingNumber.Text = tmpIndexCount4.ToString();
                    ContentsElementsStackPannel3.Children.Add(tmpFileElement);
                    tmpIndexCount4++;
                }

            }
          
        }

        void CancelBTN_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        void BTNPagesListNew1_Click(object sender, RoutedEventArgs e)  // 선택 완료
        {
            //UpdateContentsListToSelectedElement();

            if (this.g_SelectedContentsList.Count > 0)
            {
                foreach (ContentsInfoClass item in this.g_SelectedContentsList)
                {
                    Page1.Instance.UpdateContentsListToSelectedElement(item);
                }
            }

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
