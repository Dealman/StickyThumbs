using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Interop;
using Windows.Win32;
using System.Runtime.InteropServices;
using System.Text;

namespace StickyThumbs.UserControls
{
    public partial class ProcessControl : UserControl
    {
        public Process Process { get; set; }
        public ImageSource ProcessImage { get; set; }

        public ProcessControl()
        {
            InitializeComponent();
        }

        public ProcessControl(Process process)
        {
            InitializeComponent();

            Process = process;

            if (Process is null)
                return;

            try
            {
                // Name
                if (!String.IsNullOrWhiteSpace(Process.MainModule.FileVersionInfo.FileDescription))
                    ToolTip = Process.MainModule.FileVersionInfo.FileDescription;
                else
                    ToolTip = Process.ProcessName;

                // Icon
                var icon = Icon.ExtractAssociatedIcon(Process.MainModule.FileName);
                if (icon is not null)
                    ProcessImage = ToImageSource(icon);
                else
                    ProcessImage = new BitmapImage(new Uri("pack://application:,,,/Resources/404.png"));

                ProcessIcon.Source = ProcessImage;
            }
            catch
            {
                ToolTip = Process.ProcessName;
                ProcessImage = new BitmapImage(new Uri("pack://application:,,,/Resources/404.png"));
                ProcessIcon.Source = ProcessImage;
            }
        }

        private ImageSource ToImageSource(Icon icon)
        {
            ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            return imageSource;
        }
    }
}
