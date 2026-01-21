using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TurtleTools
{
    public class GuidelineGrid : Grid
    {
        #region GridLinesVisibilityEnum
        public enum GridLinesVisibilityEnum
        {
            Both,
            Vertical,
            Horizontal,
            None
        }

        public enum LineStyleEnum
        {
            Solid,
            Dash,
            DashDot,
            DashDotDot,
            Dot
        }
        #endregion

        #region Properties
        public bool ShowCustomGridLines
        {
            get { return (bool)GetValue(ShowCustomGridLinesProperty); }
            set { SetValue(ShowCustomGridLinesProperty, value); }
        }

        public static readonly DependencyProperty ShowCustomGridLinesProperty =
            DependencyProperty.Register("ShowCustomGridLines", typeof(bool), typeof(GuidelineGrid), new UIPropertyMetadata(false));

        public GridLinesVisibilityEnum GridLinesVisibility
        {
            get { return (GridLinesVisibilityEnum)GetValue(GridLinesVisibilityProperty); }
            set { SetValue(GridLinesVisibilityProperty, value); }
        }

        public static readonly DependencyProperty GridLinesVisibilityProperty =
            DependencyProperty.Register("GridLinesVisibility", typeof(GridLinesVisibilityEnum), typeof(GuidelineGrid), new UIPropertyMetadata(GridLinesVisibilityEnum.Both));

        public Brush GridLineBrush
        {
            get { return (Brush)GetValue(GridLineBrushProperty); }
            set { SetValue(GridLineBrushProperty, value); }
        }

        public static readonly DependencyProperty GridLineBrushProperty =
            DependencyProperty.Register("GridLineBrush", typeof(Brush), typeof(GuidelineGrid), new UIPropertyMetadata(Brushes.Black));

        public double GridLineThickness
        {
            get { return (double)GetValue(GridLineThicknessProperty); }
            set { SetValue(GridLineThicknessProperty, value); }
        }

        public static readonly DependencyProperty GridLineThicknessProperty =
            DependencyProperty.Register("GridLineThickness", typeof(double), typeof(GuidelineGrid), new UIPropertyMetadata(1.0));

        public LineStyleEnum LineStyle
        {
            get { return (LineStyleEnum)GetValue(LineStyleProperty); }
            set { SetValue(LineStyleProperty, value); }
        }

        public static readonly DependencyProperty LineStyleProperty =
            DependencyProperty.Register("LineStyle", typeof(LineStyleEnum), typeof(GuidelineGrid), new PropertyMetadata(LineStyleEnum.Solid, OnLineStyleChanged)
            );

        private static void OnLineStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            GuidelineGrid gg = d as GuidelineGrid;
            gg.LineStyleChanged((LineStyleEnum)e.NewValue);
        }

        private void LineStyleChanged(LineStyleEnum style)
        {
            switch (style)
            {
                case LineStyleEnum.Solid:
                    penStyle = DashStyles.Solid;
                    break;

                case LineStyleEnum.Dash:
                    penStyle = DashStyles.Dash;
                    break;

                case LineStyleEnum.DashDot:
                    penStyle = DashStyles.DashDot;
                    break;

                case LineStyleEnum.DashDotDot:
                    penStyle = DashStyles.DashDotDot;
                    break;

                case LineStyleEnum.Dot:
                    penStyle = DashStyles.Dot;
                    break;
            }
        }

        DashStyle penStyle = DashStyles.Solid;
        #endregion

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (ShowCustomGridLines)
            {
                Pen _pen = new Pen(GridLineBrush, GridLineThickness);
                _pen.DashStyle = penStyle;

                if (GridLinesVisibility == GridLinesVisibilityEnum.Both)
                {
                    foreach (var rowDefinition in RowDefinitions)
                    {
                        dc.DrawLine(_pen, new Point(0, rowDefinition.Offset), new Point(ActualWidth, rowDefinition.Offset));
                    }

                    foreach (var columnDefinition in ColumnDefinitions)
                    {
                        dc.DrawLine(_pen, new Point(columnDefinition.Offset, 0), new Point(columnDefinition.Offset, ActualHeight));
                    }
                    dc.DrawRectangle(Brushes.Transparent, _pen, new Rect(0, 0, ActualWidth, ActualHeight));
                }
                else if (GridLinesVisibility == GridLinesVisibilityEnum.Vertical)
                {
                    foreach (var columnDefinition in ColumnDefinitions)
                    {
                        dc.DrawLine(_pen, new Point(columnDefinition.Offset, 0), new Point(columnDefinition.Offset, ActualHeight));
                    }
                    dc.DrawRectangle(Brushes.Transparent, _pen, new Rect(0, 0, ActualWidth, ActualHeight));
                }
                else if (GridLinesVisibility == GridLinesVisibilityEnum.Horizontal)
                {
                    foreach (var rowDefinition in RowDefinitions)
                    {
                        dc.DrawLine(_pen, new Point(0, rowDefinition.Offset), new Point(ActualWidth, rowDefinition.Offset));
                    }
                    dc.DrawRectangle(Brushes.Transparent, _pen, new Rect(0, 0, ActualWidth, ActualHeight));
                }
            }
        }

        static GuidelineGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GuidelineGrid), new FrameworkPropertyMetadata(typeof(GuidelineGrid)));
        }
    }
}
