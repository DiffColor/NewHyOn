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
using HyonManager.Pages;
using System.ComponentModel;
using HyonManager.DataClass;
using HyonManager.SubWindow;
using System.IO;
using AndoW_Manager;
using TurtleTools;

namespace HyonManager.SubElement
{
    /// <summary>
    /// SavedPageElement.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PageScheduleElement : UserControl
    {   
        EditPage1 g_ParentPage = null;
        public bool g_IsSelected = false;
        public ScheduleDataInfoClass g_ScheduleDataInfoClass = new ScheduleDataInfoClass();


        public PageScheduleElement(EditPage1 paramPage)
        {
            InitializeComponent();
            g_ParentPage = paramPage;
            InitEventHandler();
        }

        public void EditScheduleDataInfoClassInfo(ScheduleDataInfoClass paramCls)
        {
            g_ScheduleDataInfoClass.CopyData(paramCls);
            DisplayThisElement();

            this.g_ParentPage.EditScheduleDataInfoClassInfo(paramCls);
        }

        public void UpdateScheduleDataInfoClassInfo(ScheduleDataInfoClass paramCls)
        {
            g_ScheduleDataInfoClass.CopyData(paramCls);
            DisplayThisElement();
        }

        public void DisplayThisElement()
        {
            pageNameTextBlock.Text = g_ScheduleDataInfoClass.SDI_PageName;

            SetPreviewImg();
            DisplayWeekOfDay();

            pageNameTextBlock_Copy5.Text = string.Format("{0}분 {1}초", 
                                    g_ScheduleDataInfoClass.SDI_PlayMin, 
                                    g_ScheduleDataInfoClass.SDI_PlaySec);

            if (g_ScheduleDataInfoClass.SDI_ScheduleType == "General")
            {
                pageNameTextBlock_Copy6.Text = "일반";
                pageNameTextBlock_Copy6.Foreground = new SolidColorBrush(Colors.White);
                PeriodStackPanel.Visibility = Visibility.Hidden;
                WeekScheduleGrid.Visibility = Visibility.Hidden;
                pageNameTextBlock_Copy7.Visibility = Visibility.Visible;
            }
            else
            {
                pageNameTextBlock_Copy6.Text = "예약";
                pageNameTextBlock_Copy6.Foreground = new SolidColorBrush(Colors.Red);
                PeriodStackPanel.Visibility = Visibility.Visible;
                WeekScheduleGrid.Visibility = Visibility.Visible;
                pageNameTextBlock_Copy7.Visibility = Visibility.Hidden;
            }

            pageNameTextBlock_Copy4.Text = string.Format("{0}/{1}/{2} ~ {3}/{4}/{5}",
                                    this.g_ScheduleDataInfoClass.SDI_PeriodStartYear,  // 기간설정
                                    this.g_ScheduleDataInfoClass.SDI_PeriodStartMonth,
                                    this.g_ScheduleDataInfoClass.SDI_PeriodStartDay,
                                    this.g_ScheduleDataInfoClass.SDI_PeriodEndYear,
                                    this.g_ScheduleDataInfoClass.SDI_PeriodEndMonth,
                                    this.g_ScheduleDataInfoClass.SDI_PeriodEndDay);

            // 표출시간설정
            pageNameTextBlock_Copy1.Text = string.Format("{0}:{1} ~ {2}:{3}",
                                    this.g_ScheduleDataInfoClass.SDI_DisplayStartH, 
                                    this.g_ScheduleDataInfoClass.SDI_DisplayStartM,
                                    this.g_ScheduleDataInfoClass.SDI_DisplayEndH,
                                    this.g_ScheduleDataInfoClass.SDI_DisplayEndM);

            if (this.g_ScheduleDataInfoClass.SDI_IsPeriodEnable == true)
            {
                PeriodStackPanel.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                PeriodStackPanel.Visibility = System.Windows.Visibility.Hidden;
            }
        
        }

        public void SetPreviewImg()
        {
            BitmapImage preview = LoadThumbnailBitmap();
            if (preview != null)
            {
                pagePreviewImge.Source = preview;
                return;
            }

            string filePath = UtilityClass.GetPreviewImageFilePathByPageName(g_ScheduleDataInfoClass.SDI_PageName);
            if (File.Exists(filePath))
            {
                BitmapImage bmpImg = new BitmapImage();
                bmpImg.BeginInit();
                bmpImg.StreamSource = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                bmpImg.CacheOption = BitmapCacheOption.OnLoad;
                bmpImg.EndInit();
                bmpImg.StreamSource.Dispose();
                pagePreviewImge.Source = bmpImg;
            }
        }

        private BitmapImage LoadThumbnailBitmap()
        {
            try
            {
                PageInfoClass definition = DataShop.Instance.g_PageInfoManager.GetPageDefinition(g_ScheduleDataInfoClass.SDI_PageName);
                return MediaTools.CreateBitmapFromBase64(definition?.PIC_Thumb);
            }
            catch
            {
                return null;
            }
        }

        public void DisplayWeekOfDay()
        {
            if (g_ScheduleDataInfoClass.SDI_DayOfWeek1)
                DayOfWeekRect1.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect1.Fill = new SolidColorBrush(Colors.Gray);

            if (g_ScheduleDataInfoClass.SDI_DayOfWeek2)
                DayOfWeekRect2.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect2.Fill = new SolidColorBrush(Colors.Gray);

            if (g_ScheduleDataInfoClass.SDI_DayOfWeek3)
                DayOfWeekRect3.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect3.Fill = new SolidColorBrush(Colors.Gray);

            if (g_ScheduleDataInfoClass.SDI_DayOfWeek4)
                DayOfWeekRect4.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect4.Fill = new SolidColorBrush(Colors.Gray);

            if (g_ScheduleDataInfoClass.SDI_DayOfWeek5)
                DayOfWeekRect5.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect5.Fill = new SolidColorBrush(Colors.Gray);

            if (g_ScheduleDataInfoClass.SDI_DayOfWeek6)
                DayOfWeekRect6.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect6.Fill = new SolidColorBrush(Colors.Gray);

            if (g_ScheduleDataInfoClass.SDI_DayOfWeek7)
                DayOfWeekRect7.Fill = new SolidColorBrush(Colors.GreenYellow);
            else
                DayOfWeekRect7.Fill = new SolidColorBrush(Colors.Gray);
        }

        public void InitEventHandler()
        {
            //this.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PageListElement_PreviewMouseLeftButtonDown);
            this.PreviewMouseMove += new MouseEventHandler(PageListElement_PreviewMouseMove);
            this.MouseLeave += new MouseEventHandler(PageListElement_MouseLeave);
            this.PreviewMouseDoubleClick += SavedPageElement_PreviewMouseDoubleClick;
            this.PreviewMouseLeftButtonDown += PageScheduleElement_PreviewMouseLeftButtonDown;

            BorderBTN_Copy.Click += BorderBTN_Copy_Click;
            BorderBTN_Copy1.Click += BorderBTN_Copy1_Click;
        }

        void PageScheduleElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.g_ParentPage.SelectScheduleInfo(this.g_ScheduleDataInfoClass);
        }

        void BorderBTN_Copy1_Click(object sender, RoutedEventArgs e)
        {
            EditPageScheduleWindow wnd = new EditPageScheduleWindow(this, this.g_ScheduleDataInfoClass);
            wnd.ShowDialog();

        }

        void BorderBTN_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (UtilityClass.ShowMessageBox("선택한 페이지 스케줄을 삭제하시겠습니까?") == true)
            {
                this.g_ParentPage.DeleteScheduleInfo(g_ScheduleDataInfoClass);
            }
            else
            {

            }
        }


        void SavedPageElement_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditPageScheduleWindow wnd = new EditPageScheduleWindow(this, this.g_ScheduleDataInfoClass);
            wnd.ShowDialog();

            //if (UtilityClass.ShowMessageBox(string.Format("페이지 [{0}]을(를) 로드하시겠습니까?", pageNameTextBlock.Text)) == true)
            //{
            //    //this.g_ParentPage.LoadSelectedPage(pageNameTextBlock.Text);
            //}
            //else
            //{ 
            
            //}
        }

        void PageListElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {
                Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#B2296EB2");
                BackRectangle.Fill = new SolidColorBrush(c2);
                //TextBlockPageName.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        void PageListElement_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (g_IsSelected == false)
            {

                //#B272B2F1
                Color c2 = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString("#B272B2F1");
                BackRectangle.Fill = new SolidColorBrush(c2);
                //TextBlockPageName.Foreground = new SolidColorBrush(Colors.Blue);
            }
        }

        void PageListElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //parentPage.SelectNoticeElement(this.noticeTitle, this.noticeImageName);    
        }    
    }
}
