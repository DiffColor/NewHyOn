using System.Windows;

namespace TurtleTools
{
    /// <summary>
    /// ModernScrollViewer 스타일이 적용된 모든 ScrollViewer에 대해 자동으로 마우스 휠 스크롤을 활성화하는 정적 클래스입니다.
    /// 윈도우가 로드될 때 자동으로 모든 ModernScrollViewer를 찾아 스크롤 기능을 활성화합니다.
    /// </summary>
    public static class ModernScrollViewerBehavior
    {
        /// <summary>
        /// 윈도우가 로드될 때 자동으로 모든 ModernScrollViewer에 마우스 휠 스크롤 기능을 활성화합니다.
        /// </summary>
        /// <param name="window">대상 윈도우</param>
        public static void ApplyToWindow(Window window)
        {
            if (window == null)
                return;
                
            // 윈도우가 로드될 때 모든 스크롤 뷰어를 검색하고 이벤트 핸들러를 추가합니다.
            window.Loaded += (sender, e) =>
            {
                ScrollViewerHelper.EnableMouseWheelScrollingForAllScrollViewers(window);
            };
            
            // 컨텐츠가 변경될 때마다 스크롤 뷰어를 다시 검색합니다.
            window.ContentRendered += (sender, e) =>
            {
                ScrollViewerHelper.EnableMouseWheelScrollingForAllScrollViewers(window);
            };
        }
        
        /// <summary>
        /// 컨트롤이 로드될 때 자동으로 모든 ModernScrollViewer에 마우스 휠 스크롤 기능을 활성화합니다.
        /// </summary>
        /// <param name="control">대상 컨트롤</param>
        public static void ApplyToControl(FrameworkElement control)
        {
            if (control == null)
                return;
                
            // 컨트롤이 로드될 때 모든 스크롤 뷰어를 검색하고 이벤트 핸들러를 추가합니다.
            control.Loaded += (sender, e) =>
            {
                ScrollViewerHelper.EnableMouseWheelScrollingForAllScrollViewers(control);
            };
        }
    }
} 