using System;
using System.Windows;
using TurtleTools;

namespace AndoW_Manager
{
    /// <summary>
    /// NotifyWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ResWindow : Window
    {
        public ResolutionSelection ResInfo { get; private set; }
        private bool _initializing = false;

        public ResWindow()
        {
            Initialize(new ResolutionSelection(DataShop.Instance?.g_ServerSettingsManager?.sData));
        }

        public ResWindow(PageInfoClass info)
        {
            ResolutionSelection initial = new ResolutionSelection(DataShop.Instance?.g_ServerSettingsManager?.sData);
            if (info != null)
            {
                initial.Orientation = info.PIC_IsLandscape ? DeviceOrientation.Landscape : DeviceOrientation.Portrait;
                initial.Row = info.PIC_Rows > 0 ? info.PIC_Rows : initial.Row;
                initial.Column = info.PIC_Columns > 0 ? info.PIC_Columns : initial.Column;
                initial.WidthPixels = info.PIC_CanvasWidth > 0 ? info.PIC_CanvasWidth : initial.WidthPixels;
                initial.HeightPixels = info.PIC_CanvasHeight > 0 ? info.PIC_CanvasHeight : initial.HeightPixels;
            }

            Initialize(initial);
        }

        private void Initialize(ResolutionSelection initial)
        {
            ResInfo = initial ?? new ResolutionSelection();
            _initializing = true;
            InitializeComponent();
            InitEventHandler();
            InitValues();
            ApplyResInfoToControls();
            _initializing = false;
        }

        private void ApplyResInfoToControls()
        {
            if (ResInfo == null)
                ResInfo = new ResolutionSelection();

            DisplayTypeCombo.SelectedItem = ResInfo.Orientation;

            if (!RowCombo.Items.Contains(ResInfo.Row))
                RowCombo.Items.Add(ResInfo.Row);
            if (!ColumnCombo.Items.Contains(ResInfo.Column))
                ColumnCombo.Items.Add(ResInfo.Column);

            RowCombo.SelectedItem = ResInfo.Row;
            ColumnCombo.SelectedItem = ResInfo.Column;
            WidthPixTBox.Text = ResInfo.WidthPixels.ToString();
            HeightPixTBox.Text = ResInfo.HeightPixels.ToString();
        }

        private void InitValues()
        {
            for (int i = 1; i < 43; i++)
            {
                RowCombo.Items.Add(i);
                ColumnCombo.Items.Add(i);
            }

            if (RowCombo.SelectedIndex < 0)
                RowCombo.SelectedIndex = 0;
            if (ColumnCombo.SelectedIndex < 0)
                ColumnCombo.SelectedIndex = 0;

            if (string.IsNullOrWhiteSpace(WidthPixTBox.Text))
                WidthPixTBox.Text = "1920";
            if (string.IsNullOrWhiteSpace(HeightPixTBox.Text))
                HeightPixTBox.Text = "1080";
        }

        public void InitEventHandler()
        {
            LBtn.Click += new RoutedEventHandler(BTNPagesListNew1_Click);  //OK
            RBtn.Click += new RoutedEventHandler(CancelBTN_Click);  //Cancel

            DisplayTypeCombo.SelectionChanged += DisplayTypeCombo_SelectionChanged;
            RowCombo.SelectionChanged += RowCombo_SelectionChanged;
            ColumnCombo.SelectionChanged += ColumnCombo_SelectionChanged;
        }

        void ColumnCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            UpdateFromControls();
        }

        void RowCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            UpdateFromControls();
        }

        void CancelBTN_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        void BTNPagesListNew1_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(WidthPixTBox.Text) || string.IsNullOrEmpty(HeightPixTBox.Text))
            {
                MessageTools.ShowMessageBox("해상도를 입력해주세요.");
                return;
            }

            UpdateFromControls(true);

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

        private void DisplayTypeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_initializing) return;
            UpdateFromControls();
        }

        private void UpdateFromControls(bool force = false)
        {
            if (DisplayTypeCombo.SelectedItem == null ||
                RowCombo.SelectedValue == null ||
                ColumnCombo.SelectedValue == null)
            {
                return;
            }

            DeviceOrientation orientation = (DeviceOrientation)DisplayTypeCombo.SelectedItem;
            double baseWidth = orientation == DeviceOrientation.Portrait ? 1080 : 1920;
            double baseHeight = orientation == DeviceOrientation.Portrait ? 1920 : 1080;

            int row = Convert.ToInt32(RowCombo.SelectedValue);
            int column = Convert.ToInt32(ColumnCombo.SelectedValue);

            double widthPixels = column * baseWidth;
            double heightPixels = row * baseHeight;

            WidthPixTBox.Text = widthPixels.ToString();
            HeightPixTBox.Text = heightPixels.ToString();

            if (ResInfo == null)
                ResInfo = new ResolutionSelection();

            ResInfo.Orientation = orientation;
            ResInfo.Row = row;
            ResInfo.Column = column;
            ResInfo.WidthPixels = widthPixels;
            ResInfo.HeightPixels = heightPixels;

            if (force)
            {
                if (double.TryParse(WidthPixTBox.Text, out double w))
                    ResInfo.WidthPixels = w;
                if (double.TryParse(HeightPixTBox.Text, out double h))
                    ResInfo.HeightPixels = h;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance?.SetDimOverlay(true);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainWindow.Instance?.SetDimOverlay(false);
        }
    }

    public class ResolutionSelection
    {
        public DeviceOrientation Orientation { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public double WidthPixels { get; set; }
        public double HeightPixels { get; set; }

        public ResolutionSelection(ServerSettings settings = null)
        {
            if (settings != null)
            {
                Orientation = settings.DefaultResolutionOrientation;
                Row = settings.DefaultResolutionRows > 0 ? settings.DefaultResolutionRows : 1;
                Column = settings.DefaultResolutionColumns > 0 ? settings.DefaultResolutionColumns : 1;
                WidthPixels = settings.DefaultResolutionWidthPixels > 0 ? settings.DefaultResolutionWidthPixels : 1920;
                HeightPixels = settings.DefaultResolutionHeightPixels > 0 ? settings.DefaultResolutionHeightPixels : 1080;
            }
            else
            {
                Orientation = DeviceOrientation.Landscape;
                Row = Column = 1;
                WidthPixels = 1920;
                HeightPixels = 1080;
            }
        }

        public ResolutionSelection()
            : this(null)
        {
        }
    }

}
