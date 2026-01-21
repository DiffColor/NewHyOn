using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TurtleTools
{
    /// <summary>
    /// ScrollViewer 클래스에 대한 유틸리티 기능을 제공하는 헬퍼 클래스입니다.
    /// </summary>
    public static class ScrollViewerHelper
    {
        /// <summary>
        /// ModernScrollViewer 스타일이 적용된 ScrollViewer에서 마우스 휠 스크롤을 활성화합니다.
        /// </summary>
        /// <param name="scrollViewer">마우스 휠 스크롤을 활성화할 ScrollViewer 객체</param>
        public static void EnableMouseWheelScrolling(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null)
                return;

            scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
        }

        /// <summary>
        /// 모든 ScrollViewer에서 마우스 휠 이벤트를 처리하는 공통 이벤트 핸들러
        /// </summary>
        private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null)
            {
                // 스크롤 속도 조정 - 더 자연스러운 스크롤을 위해 48픽셀씩 이동
                double scrollAmount = 48;
                
                // 마우스 휠 방향에 따라 스크롤 뷰어를 스크롤합니다.
                if (e.Delta < 0)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollAmount);
                }
                else
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - scrollAmount);
                }
                
                e.Handled = true; // 이벤트가 처리되었음을 표시하여 더 이상 전파되지 않도록 합니다.
            }
        }

        /// <summary>
        /// ModernScrollViewer 스타일이 적용된 ScrollViewer에서 마우스 휠 스크롤을 비활성화합니다.
        /// </summary>
        /// <param name="scrollViewer">마우스 휠 스크롤을 비활성화할 ScrollViewer 객체</param>
        public static void DisableMouseWheelScrolling(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null)
                return;

            scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
        }
        
        /// <summary>
        /// 특정 컨트롤 내의 모든 ModernScrollViewer에 마우스 휠 스크롤을 활성화합니다.
        /// </summary>
        /// <param name="parent">검색을 시작할 부모 DependencyObject</param>
        public static void EnableMouseWheelScrollingForAllScrollViewers(DependencyObject parent)
        {
            if (parent == null)
                return;
                
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                // ScrollViewer 확인
                if (child is ScrollViewer scrollViewer)
                {
                    // 모든 ScrollViewer에 마우스 휠 스크롤 활성화
                    EnableMouseWheelScrolling(scrollViewer);
                }
                
                // 재귀적으로 모든 자식 요소 검색
                EnableMouseWheelScrollingForAllScrollViewers(child);
            }
        }
    }
} 