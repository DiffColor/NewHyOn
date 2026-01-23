using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// DisplayElementForEditor.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ScrollTextForEditor : UserControl
    {
        /////////////////////////////////////////////////////////////////
        // Commaon Property
        public ElementInfoClass g_ElementInfoClass = new ElementInfoClass();
        public EditFontInfoClass g_EditFontInfoClass = new EditFontInfoClass();

        public bool g_IsSelected = false;

        public Size g_PrevSize = new Size();

        public ScrollTextForEditor()
        {
            InitializeComponent();
            InitEventHandler();
        }

        public void UpdateFontStyle(EditFontInfoClass paramCls)
        {
            g_EditFontInfoClass.CopyData(paramCls);

            foreach (ContentsInfoClass item in g_ElementInfoClass.EIF_ContentsInfoClassList)
	        {
                item.CIF_PlayMinute = g_EditFontInfoClass.EFT_ForeGoundColor;
                item.CIF_PlaySec = g_EditFontInfoClass.EFT_BackGoundColor;
                item.CIF_ContentType = g_EditFontInfoClass.EFT_FontName;
	        }  
        }

        public void InitEventHandler()
        {
            this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(MoveTopRectangle_PreviewMouseLeftButtonDown);
            this.resizingSpotRect.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(ResizeRectangle_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += DisplayElementForEditor_PreviewMouseMove;
            this.MouseLeave += DisplayElementForEditor_MouseLeave;
            this.PreviewMouseUp += DisplayElementForEditor_PreviewMouseUp;
            ElementDelete.Click += ElementDelete_Click;

            MenuBringToFront.Click += MenuBringToFront_Click;
            MenuBringToBack.Click += MenuBringToBack_Click;
            MenuBringToFrontOneStep.Click += MenuBringToFrontOneStep_Click;
            MenuBringToBackOneStep.Click += MenuBringToBackOneStep_Click;
        }

        void MenuBringToBackOneStep_Click(object sender, RoutedEventArgs e)
        {
            Page1.Instance.BringToBackOneStepByElementName(this.Name);
        }

        void MenuBringToFrontOneStep_Click(object sender, RoutedEventArgs e)
        {
            Page1.Instance.BringToFrontOneStepByElementName(this.Name);
        }

        void MenuBringToBack_Click(object sender, RoutedEventArgs e)
        {
            Page1.Instance.BringToBackByElementName(this.Name);
        }

        void MenuBringToFront_Click(object sender, RoutedEventArgs e)
        {
            Page1.Instance.BringToFrontByElementName(this.Name);
        }

        public void UpdateZidxInfo(int paramIdx)
        {
            this.g_ElementInfoClass.EIF_ZIndex = paramIdx;
        }

        void ElementDelete_Click(object sender, RoutedEventArgs e)
        {
            if (MessageTools.ShowMessageBox("선택한 객체를 삭제하시겠습니까?", "예", "아니오") == true)
            {
                Page1.Instance.DeleteObjectByName(g_ElementInfoClass.EIF_Name);
            }
        }


        public void UpdateElemenetnInfoCls(ElementInfoClass paramCls)
        {
            g_ElementInfoClass.CopyData(paramCls);
            DisplayContentsListCount();

            if (paramCls.EIF_ContentsInfoClassList.Count > 0)
            {
                g_EditFontInfoClass.EFT_ForeGoundColor = paramCls.EIF_ContentsInfoClassList[0].CIF_PlayMinute;
                g_EditFontInfoClass.EFT_BackGoundColor = paramCls.EIF_ContentsInfoClassList[0].CIF_PlaySec;
                g_EditFontInfoClass.EFT_FontName = paramCls.EIF_ContentsInfoClassList[0].CIF_ContentType;
            }

            Page1.Instance.ScrollTextTBox.Text = string.Empty;
        }

        void DisplayElementForEditor_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            Reposition();
            this.ReleaseMouseCapture();

        }

        public void Reposition()
        {
            if (Page1.Instance.LockCheckBox.IsChecked == true) return;

            if (Page1.Instance.NeedGuideCheckBox.IsChecked == true)
            {
                UpdateElementSizeAndPosThroughGridBase();
            }
            else
            {
                UpdateElementSizeAndPosNoGridBase();
            }
        }


        public void UpdateElementSizeAndPosNoGridBase()
        {
            double leftVal = Canvas.GetLeft(this);
            double topVal = Canvas.GetTop(this);

            this.g_ElementInfoClass.EIF_PosLeft = leftVal;
            this.g_ElementInfoClass.EIF_PosTop = topVal;

            ////////////////////////////////////////////////////////////////////////
            // 2. 그다음엔 사이즈를 잡는다.

            this.g_ElementInfoClass.EIF_Width = g_PrevSize.Width = Width;
            this.g_ElementInfoClass.EIF_Height = g_PrevSize.Height = Height;

            SetBackOversized(leftVal, topVal);
        
            UpdateElementLandSizeAndPos();
        }

        public void UpdateElementSizeAndPosByElementInfoClass()
        {
            if ((bool)Page1.Instance.NeedGuideCheckBox.IsChecked)
            {
                ///////////////////////////////////////////////////////
                // Set Position
                double unitWidth = Page1.Instance.GuideGrid.ColumnDefinitions[0].ActualWidth;
                double unitHeight = Page1.Instance.GuideGrid.RowDefinitions[0].ActualHeight;

                double posX = unitWidth * this.g_ElementInfoClass.EIF_ColVal;
                double posY = unitHeight * this.g_ElementInfoClass.EIF_RowVal;

                this.g_ElementInfoClass.EIF_PosTop = posY;
                this.g_ElementInfoClass.EIF_PosLeft = posX;

                Canvas.SetLeft(this, posX);
                Canvas.SetTop(this, posY);

                ///////////////////////////////////////////////////////
                // Set Size

                this.Width = unitWidth * this.g_ElementInfoClass.EIF_ColSpanVal;
                this.Height = unitHeight * this.g_ElementInfoClass.EIF_RowSpanVal;

                this.g_ElementInfoClass.EIF_Width = unitWidth * this.g_ElementInfoClass.EIF_ColSpanVal;
                this.g_ElementInfoClass.EIF_Height = unitHeight * this.g_ElementInfoClass.EIF_RowSpanVal;
            }
        }

        public void UpdateElementSizeAndPosThroughGridBase()
        {
            double unitWidth = Page1.Instance.GuideGrid.ColumnDefinitions[0].ActualWidth;
            double unitHeight = Page1.Instance.GuideGrid.RowDefinitions[0].ActualHeight;

            ////////////////////////////////////////////////////////////////////////
            // 1. 위치부터 잡고 간다.
            int row1 = 0;
            int col1 = 0;

            double leftVal = Canvas.GetLeft(this);
            double topVal = Canvas.GetTop(this);

            GetPosColumn(new Point(leftVal, topVal), out row1, out col1);

            double posX = unitWidth * col1;
            double posY = unitHeight * row1;

            if (col1 < 0)
            {
                posX = 0;
                col1 = 0;
            }

            if (row1 < 0)
            {
                posY = 0;
                row1 = 0;
            }

            Canvas.SetLeft(this, posX);
            Canvas.SetTop(this, posY);

            this.g_ElementInfoClass.EIF_RowVal = row1;
            this.g_ElementInfoClass.EIF_ColVal = col1;

            this.g_ElementInfoClass.EIF_PosLeft = posX;
            this.g_ElementInfoClass.EIF_PosTop = posY;

            ////////////////////////////////////////////////////////////////////////
            // 2. 그다음엔 사이즈를 잡는다.

            SetBackOversized(posX, posY);

            int row2 = 0;
            int col2 = 0;

            if (this.ActualWidth == 0 || this.ActualHeight == 0)   // 처음 생성될때는 실제크기가 빵이다.
            {
                GetSizeColumns(new Point(leftVal + 1200, topVal + 90), out row2, out col2);  //1200,  90 은 처음 생성될때 임의로 정해준크기다.
            }
            else
            {
                GetSizeColumns(new Point(leftVal + this.ActualWidth, topVal + this.ActualHeight), out row2, out col2);

                row2++;
                col2++;
            }

            int gapX = col2 - col1;
            int gapY = row2 - row1;

            this.Width = g_PrevSize.Width = unitWidth * gapX;
            this.Height = g_PrevSize.Height = unitHeight * gapY;

            this.g_ElementInfoClass.EIF_Width = Width;
            this.g_ElementInfoClass.EIF_Height = Height;

            this.g_ElementInfoClass.EIF_ColSpanVal = gapX;
            this.g_ElementInfoClass.EIF_RowSpanVal = gapY;

            UpdateElementLandSizeAndPos();
        }


        public void UpdateElementLandSizeAndPos()
        {
            double leftVal = Canvas.GetLeft(this);
            double topVal = Canvas.GetTop(this);

            SetBackOversized(leftVal, topVal);

            g_ElementInfoClass.EIF_PosLeft = leftVal;
            g_ElementInfoClass.EIF_PosTop = topVal;
            g_ElementInfoClass.EIF_Width = Width;
            g_ElementInfoClass.EIF_Height = Height;

            //ConvertToLandData();
        }

        //public void ConvertToLandData()
        //{
        //    if (MainWindow.Instance.isPortraitEditor)
        //    {
        //        g_ElementInfoClass.EIF_PosLeft *= MainWindow.Instance.g_wLandScale;
        //        g_ElementInfoClass.EIF_PosTop *= MainWindow.Instance.g_hLandScale;
        //        g_ElementInfoClass.EIF_Width *= MainWindow.Instance.g_wLandScale;
        //        g_ElementInfoClass.EIF_Height *= MainWindow.Instance.g_hLandScale;
        //    }
        //}

        void SetBackOversized(double posX, double posY)
        {
            if (posX < 0)
            {
                Canvas.SetLeft(this, 0);
            }

            if (posY < 0)
            {
                Canvas.SetTop(this, 0);
            }

            if (posX + Width > Page1.Instance.DesignerCanvas.ActualWidth)
            {
                if (Page1.Instance.g_IsSelecteControlResizing)
                    Width = Page1.Instance.DesignerCanvas.ActualWidth - posX;
                else
                    Canvas.SetLeft(this, Page1.Instance.DesignerCanvas.ActualWidth - Width);
            }
            if (posY + Height > Page1.Instance.DesignerCanvas.ActualHeight)
            {
                if (Page1.Instance.g_IsSelecteControlResizing)
                    Height = Page1.Instance.DesignerCanvas.ActualHeight - posY;
                else
                    Canvas.SetTop(this, Page1.Instance.DesignerCanvas.ActualHeight - Height);
            }
        }

        public void GetPosColumn(Point position, out int row, out int column)
        {
            column = 0;
            double total = 0;
            foreach (ColumnDefinition clm in Page1.Instance.GuideGrid.ColumnDefinitions)
            {
                if (position.X < total + clm.ActualWidth / 2)
                {
                    break;
                }
                column++;
                total += clm.ActualWidth;
            }

            row = 0;
            total = 0;
            foreach (RowDefinition rowDef in Page1.Instance.GuideGrid.RowDefinitions)
            {
                if (position.Y < total + rowDef.ActualHeight / 2)
                {
                    break;
                }
                row++;
                total += rowDef.ActualHeight;
            }
        }

        public void GetSizeColumns(Point position, out int row, out int column)
        {
            column = -1;
            double total = 0;
            foreach (ColumnDefinition clm in Page1.Instance.GuideGrid.ColumnDefinitions)
            {
                if (position.X < total + clm.ActualWidth / 2)
                {
                    break;
                }
                column++;
                total += clm.ActualWidth;
            }

            row = -1;
            total = 0;
            foreach (RowDefinition rowDef in Page1.Instance.GuideGrid.RowDefinitions)
            {
                if (position.Y < total + rowDef.ActualHeight / 2)
                {
                    break;
                }
                row++;
                total += rowDef.ActualHeight;
            }
        }

        void DisplayElementForEditor_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == true)
            {
                BoundaryBorder2.BorderBrush = new SolidColorBrush(Colors.Gold);
                TextDisplayName.Foreground = new SolidColorBrush(Colors.Gold);
            }
            else
            {
                BoundaryBorder2.BorderBrush = new SolidColorBrush(Colors.DimGray);
                TextDisplayName.Foreground = new SolidColorBrush(Colors.White);
            }
            IsFirstMouseEnter = false;
        }

        bool IsFirstMouseEnter = false;
        void DisplayElementForEditor_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (IsFirstMouseEnter == false)
            {
                BoundaryBorder2.BorderBrush = new SolidColorBrush(Colors.Gold);
                TextDisplayName.Foreground = new SolidColorBrush(Colors.Gold);
                IsFirstMouseEnter = true;
            }
        }

        public void SetFreeThisElementFromSelecting()
        {
            g_IsSelected = false;
            BoundaryBorder2.BorderBrush = new SolidColorBrush(Colors.DimGray);
            TextDisplayName.Foreground = new SolidColorBrush(Colors.White);
        }

        void ResizeRectangle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Page1.Instance.g_IsSelecteControlResizing = true;
        }

        void MoveBottomRectangle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Page1.Instance.SetCurrentSelectedElementMovable(true, e.GetPosition(this));
        }

        void MoveTopRectangle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Page1.Instance.SetCurrentSelectedElementMovable(true, e.GetPosition(this));
            ChooseThisElement();


            if (g_IsSelected == false)
            {
                g_IsSelected = true;
                BoundaryBorder2.BorderBrush = new SolidColorBrush(Colors.Gold);
                TextDisplayName.Foreground = new SolidColorBrush(Colors.Gold);
            }
            else
            {
                g_IsSelected = false;
                BoundaryBorder2.BorderBrush = new SolidColorBrush(Colors.DimGray);
                TextDisplayName.Foreground = new SolidColorBrush(Colors.White);
            }

            Mouse.Capture(this);
        }

        void contentsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ChooseThisElement();
        }
      
        public void ChooseThisElement()
        {
            g_PrevSize.Width = this.ActualWidth;
            g_PrevSize.Height = this.ActualHeight;

            Page1.Instance.SetCurrentSelectedObjectName(this.Name, TextDisplayName.Text, this.g_ElementInfoClass);
            Page1.Instance.SetCurrentSelectedElement(this);
            Page1.Instance.ReleaseSelectedObjectExceptSelectedObject();
        }

        public void AddContentInfoCls(ContentsInfoClass paramCls)
        {
            ContentsInfoClass tmpCls = new ContentsInfoClass();
            tmpCls.CopyData(paramCls);

            this.g_ElementInfoClass.EIF_ContentsInfoClassList.Add(tmpCls);
            Page1.Instance.SetCurrentSelectedObjectName(this.Name, TextDisplayName.Text, this.g_ElementInfoClass);

            DisplayContentsListCount();
        }

        public void EditScrollText(string paramGuid, string newText, int paramScrollSpeed)
        {
            int idx = 0;
            foreach (ContentsInfoClass item in this.g_ElementInfoClass.EIF_ContentsInfoClassList)
            {
                if (item.CIF_StrGUID == paramGuid)
                {
                    idx++;
                    item.CIF_FileName = newText;
                    item.CIF_ScrollTextSpeedSec = paramScrollSpeed;
                    break;
                }
            }

            Page1.Instance.SetCurrentSelectedObjectName(this.Name, TextDisplayName.Text, this.g_ElementInfoClass);

            DisplayContentsListCount();
        }

        public void DisplayContentsListCount()
        {
            TextInputChannelName.Text = string.Format("      SubTitle: {0}ea", g_ElementInfoClass.EIF_ContentsInfoClassList.Count);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width < 800) this.Width = 799;
            if (e.NewSize.Height < 90) this.Height = 89;
            pxTBox.Text = string.Format("{0:F0} x {1:F0}", e.NewSize.Width, e.NewSize.Height);
        }

        public void EditContentInfoCls(ContentsInfoClass paramCls)
        {
            ContentsInfoClass tmpCls = new ContentsInfoClass();
            tmpCls.CopyData(paramCls);


            foreach (ContentsInfoClass item in this.g_ElementInfoClass.EIF_ContentsInfoClassList)
            {
                if (item.CIF_StrGUID == paramCls.CIF_StrGUID)
                {
                    item.CopyData(paramCls);
                    break;
                }
            }


            Page1.Instance.SetCurrentSelectedObjectName(this.Name, TextDisplayName.Text, this.g_ElementInfoClass);

            DisplayContentsListCount();
        }
    }
}
