using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using System.IO;
using TurtleTools;
using System;


namespace AndoW_Manager
{
    /// <summary>
    /// TextElementForEditor4.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TextElementForEditor4 : UserControl
    {
        /////////////////////////////////////////////////////////////////
        // Commaon Property
        public ElementInfoClass g_ElementInfoClass = new ElementInfoClass();

        public TextInfoClass g_TextInfoClass = new TextInfoClass();

        public PagePreviewWindow g_ParentPage = null;
        public bool g_IsSelected = false;

        public Size g_PrevSize = new Size();

        public TextElementForEditor4(PagePreviewWindow paramPage)
        {
            InitializeComponent();
            g_ParentPage = paramPage;

            InitEventHandler();
          
        }

        public void InitEventHandler()
        {
            this.PreviewMouseMove += DisplayElementForEditor_PreviewMouseMove;
            this.MouseLeave += DisplayElementForEditor_MouseLeave;
        }

        public void UpdateZidxInfo(int paramIdx)
        {
            this.g_ElementInfoClass.EIF_ZIndex = paramIdx;
        }


        public void UpdateElemenetnInfoCls(ElementInfoClass paramCls)
        {
            g_ElementInfoClass.CopyData(paramCls);
        }



        void DisplayElementForEditor_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == true)
            {
                BoundaryBorder2.BorderBrush = new SolidColorBrush(Colors.Gold);
            }
            else
            {
                BoundaryBorder2.BorderBrush = new SolidColorBrush(Colors.DimGray);
            }
            IsFirstMouseEnter = false;
        }

        bool IsFirstMouseEnter = false;
        void DisplayElementForEditor_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (IsFirstMouseEnter == false)
            {
                BoundaryBorder2.BorderBrush = new SolidColorBrush(Colors.Gold);
                IsFirstMouseEnter = true;
            }
        }

        public void UpdateElementSizeAndPos()
        {
            double leftVal = Canvas.GetLeft(this);
            double topVal = Canvas.GetTop(this);

            g_ElementInfoClass.EIF_PosLeft = leftVal;
            g_ElementInfoClass.EIF_PosTop = topVal;
            //g_ElementInfoClass.EIF_Width = this.ActualWidth;
            //g_ElementInfoClass.EIF_Height = this.ActualHeight;
        }

        public void UpdateControlSize(double paramW, double paramH)
        {
            this.g_ElementInfoClass.EIF_Width = paramW;
            this.g_ElementInfoClass.EIF_Height = paramH;
        }

        public void SetFreeThisElementFromSelecting()
        {
            g_IsSelected = false;
            BoundaryBorder2.BorderBrush = new SolidColorBrush(Colors.DimGray);
        }

        //-----------------------------------------
     

        public void DeleteThisObjectByName(string objName)
        {
            //g_ParentPage.DeleteObjectByName(objName);
        }


        void contentsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ChooseThisElement();
        }
      
        public void ChooseThisElement()
        {
            g_PrevSize.Width = this.ActualWidth;
            g_PrevSize.Height = this.ActualHeight;
        }

        //public void AddContentInfoCls(ContentsInfoClass paramCls)
        //{
        //    ContentsInfoClass tmpCls = new ContentsInfoClass();
        //    tmpCls.CopyData(paramCls);

        //    this.g_ElementInfoClass.EIF_ContentsInfoClassList.Add(tmpCls);
        //    g_ParentPage.SetCurrentSelectedObjectName(this.Name, TextDisplayName.Text, this.g_ElementInfoClass);
        //}

        public void UpdateTextInfoClsFromPage(TextInfoClass paramCls)
        {
            this.g_TextInfoClass.CopyData(paramCls);

            TextDisplayName.Foreground = ColorTools.GetSolidBrushByColorString(this.g_TextInfoClass.CIF_FontColor);
            TextDisplayName.FontFamily = new FontFamily(this.g_TextInfoClass.CIF_FontName);

            if (this.g_TextInfoClass.CIF_IsBGImageExist == true)
            {
                string bgPath = FNDTools.GetWelcomeBoardBackgroundPath(this.g_TextInfoClass);
                if (string.IsNullOrWhiteSpace(bgPath) == false)
                {
                    MediaTools.DisplayImage(LayoutRoot, bgPath);
                }
                else
                {
                    LayoutRoot.Background = ColorTools.GetSolidBrushByColorString(this.g_TextInfoClass.CIF_BGColor);
                }
            }
            else
            {
                LayoutRoot.Background = ColorTools.GetSolidBrushByColorString(this.g_TextInfoClass.CIF_BGColor);
            }

            TextDisplayName.FontSize = this.g_TextInfoClass.CIF_FontSize;
            TextDisplayName.Text = this.g_TextInfoClass.CIF_TextContent;

            if (g_TextInfoClass.CIF_IsBold == true)
            {
                TextDisplayName.FontWeight = FontWeights.Bold;
            }
            else
            {
                TextDisplayName.FontWeight = FontWeights.Normal;
            }

            if (g_TextInfoClass.CIF_IsItalic == true)
            {
                TextDisplayName.FontStyle = FontStyles.Italic;
            }
            else
            {
                TextDisplayName.FontStyle = FontStyles.Normal;
            }
        }

        public void RefreshContentsInfoList(List<ContentsInfoClass> paramList)
        {
            if (paramList.Count > 0)
            {
                this.g_ElementInfoClass.EIF_ContentsInfoClassList.Clear();

                foreach (ContentsInfoClass item in paramList)
                {
                    ContentsInfoClass tmpCls = new ContentsInfoClass();
                    tmpCls.CopyData(item);
                    this.g_ElementInfoClass.EIF_ContentsInfoClassList.Add(tmpCls);
                }
                
            }
            ChooseThisElement();
        }

    }
}
