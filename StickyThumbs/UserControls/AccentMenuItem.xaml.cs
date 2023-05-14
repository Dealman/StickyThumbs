using System.Windows;
using System.Windows.Controls;
using ControlzEx.Theming;

namespace StickyThumbs.UserControls
{
    public partial class AccentMenuItem : UserControl
    {
        public Theme ThemeProperty
        {
            get { return (Theme)GetValue(ThemePropertyProperty); }
            set { SetValue(ThemePropertyProperty, value); }
        }
        public static readonly DependencyProperty ThemePropertyProperty = DependencyProperty.Register("ThemeProperty", typeof(Theme), typeof(AccentMenuItem));

        public AccentMenuItem()
        {
            InitializeComponent();
        }
    }
}
