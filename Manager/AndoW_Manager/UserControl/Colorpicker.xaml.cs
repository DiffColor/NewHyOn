using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AndoW_Manager
{
    /// <summary>
    /// Interaction logic for Colorpicker.xaml
    /// </summary>
    public partial class ColorpickerForMultiText : UserControl
    {
        public ColorpickerForMultiText()
        {
            InitializeComponent();
        }


        public Brush SelectedColor
        {
            get { return (Brush)GetValue(SelectedColorProperty); }
            set 
            {              
                SetValue(SelectedColorProperty, value); 
            }
        }

        public int SelectedIndex
        {
            get { return superCombo.SelectedIndex; }
            set
            {
                superCombo.SelectedIndex = value;
            }
        }

        // Using a DependencyProperty as the backing store for SelectedColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register("SelectedColor", typeof(Brush), typeof(ColorpickerForMultiText), new UIPropertyMetadata(null));
       
        
    }
}
