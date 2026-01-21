using System.Windows.Controls;

namespace AndoW_Manager
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class Samples : UserControl
    {

        public bool g_IsSelected = false;

     
        public int g_Idx = 0;

        MainWindow g_ParentWnd = null;
       // InputChannelInfoClass g_InputChannelInfoClass = new InputChannelInfoClass();

        public Samples(MainWindow paramWnd)
        {
            InitializeComponent();
        }

    }
}
