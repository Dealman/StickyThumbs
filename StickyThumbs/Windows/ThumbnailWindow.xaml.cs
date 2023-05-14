using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using Windows.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ControlzEx.Theming;

namespace StickyThumbs.Windows
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct DWM_THUMBNAIL_PROPERTIES
    {
        public int dwFlags;
        public global::Windows.Win32.Foundation.RECT rcDestination;
        public global::Windows.Win32.Foundation.RECT rcSource;
        public byte opacity;
        public bool fVisible;
        public bool fSourceClientAreaOnly;
    }

    // TODO: The regions here are a bit overill and messy, should clean them up
    public partial class ThumbnailWindow : Window
    {
        public IntPtr WindowHandle { get; set; } = IntPtr.Zero;
        public Process MonitoredProcess { get; private set; }

        #region DWM Constants
        private static readonly uint DWM_TNP_RECTDESTINATION = 0x1;
        private static readonly uint DWM_TNP_RECTSOURCE = 0x2;
        private static readonly uint DWM_TNP_OPACITY = 0x4;
        private static readonly uint DWM_TNP_VISIBLE = 0x8;
        private static readonly uint DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;
        #endregion
        private IntPtr ThumbnailID = IntPtr.Zero;
        private Size SourceSize = new();
        private Point SourceRegion = new();
        private Point CurrentMousePosition = new();
        private double SourceAspectRatio = 1.77777777777777777778; // Default to 16:9 ratio
        private bool MaintainRatio = false;

        public ThumbnailWindow(Process process)
        {
            InitializeComponent();

            MonitoredProcess = process;

            #region Event Subscription
            Loaded += ThumbnailWindow_Loaded;
            Closing += ThumbnailWindow_Closing;
            // TitleBar
            DummyGrid.MouseEnter += DummyGrid_MouseEnter;
            MyPopup.MouseLeave += MyPopup_MouseLeave;
            LocationChanged += ThumbnailWindow_LocationChanged;
            // Key Up Event
            KeyUp += ThumbnailWindow_KeyUp;
            // Bring Process Into View
            MouseDoubleClick += ThumbnailWindow_MouseDoubleClick;
            // Moving Window Around Events
            MouseEnter += ThumbnailWindow_MouseEnter;
            MouseLeftButtonDown += ThumbnailWindow_MouseLeftButtonDown;
            MouseLeftButtonUp += ThumbnailWindow_MouseLeftButtonUp;
            // Source Region Panning
            MouseRightButtonDown += ThumbnailWindow_MouseRightButtonDown;
            MouseRightButtonUp += ThumbnailWindow_MouseRightButtonUp;
            MouseMove += ThumbnailWindow_MouseMove;
            // Zooming
            MouseWheel += ThumbnailWindow_MouseWheel;
            // Resizing Window Events
            ResizeThumb.MouseEnter += ResizeThumb_MouseEnter;
            ResizeThumb.MouseMove += ResizeThumb_MouseMove;
            ResizeThumb.MouseLeave += ResizeThumb_MouseLeave;
            ResizeThumb.MouseLeftButtonDown += ResizeThumb_MouseLeftButtonDown;
            ResizeThumb.MouseLeftButtonUp += ResizeThumb_MouseLeftButtonUp;
            #endregion
        }

        #region UI Control Events
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender == BorderCheckBox && BorderCheckBox.IsLoaded)
            {
                if (BorderCheckBox.IsChecked.Value)
                    BorderThickness = new Thickness(1);
                else
                    BorderThickness = new Thickness();
            }

            if (sender == AspectRadioCheckBox && AspectRadioCheckBox.IsLoaded)
            {
                MaintainRatio = AspectRadioCheckBox.IsChecked.GetValueOrDefault();
            }
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender == CloseButton)
                Close();
        }
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender == OpacitySlider && OpacitySlider.IsLoaded)
                UpdateThumbnailOpacity(OpacitySlider.Value);

            if (sender == XOffsetSlider && XOffsetSlider.IsLoaded)
            {
                SourceRegion.X = Math.Clamp(XOffsetSlider.Value, 0, (SourceSize.Width * (1.0 % zoomFactor)));
                MoveCaptureRegion();
            }

            if (sender == YOffsetSlider && YOffsetSlider.IsLoaded)
            {
                SourceRegion.Y = Math.Clamp(YOffsetSlider.Value, 0, (SourceSize.Height * (1.0 % zoomFactor)));
                MoveCaptureRegion();
            }
        }
        #endregion

        #region Popup Events
        private void DummyGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            MyPopup.IsOpen = true;
        }
        private void MyPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            MyPopup.IsOpen = false;
        }
        private void ThumbnailWindow_LocationChanged(object? sender, EventArgs e)
        {
            // Workaround to update the popup to follow ze window around
            var currentOffset = MyPopup.HorizontalOffset;
            MyPopup.HorizontalOffset = currentOffset + 0.01;
            MyPopup.HorizontalOffset = currentOffset;
            DummyPopup.HorizontalOffset = currentOffset + 0.01;
            DummyPopup.HorizontalOffset = currentOffset;
        }
        #endregion

        #region Overriden Methods
        protected override void OnSourceInitialized(EventArgs e)
        {
            // This is the earliest we can fetch the MainWindowHandle of a WPF Window as far as I know
            WindowHandle = new WindowInteropHelper(this).Handle;
            base.OnSourceInitialized(e);
        }
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            if (!AspectRadioCheckBox.IsChecked.GetValueOrDefault())
                return;

            var percentWidthChange = Math.Abs(sizeInfo.NewSize.Width - sizeInfo.PreviousSize.Width) / sizeInfo.PreviousSize.Width;
            var percentHeightChange = Math.Abs(sizeInfo.NewSize.Height - sizeInfo.PreviousSize.Height) / sizeInfo.PreviousSize.Height;

            if (percentWidthChange > percentHeightChange)
                Height = sizeInfo.NewSize.Width / 1.77777777777777777778;
            else
                Width = sizeInfo.NewSize.Height * 1.77777777777777777778;

            base.OnRenderSizeChanged(sizeInfo);
        }
        #endregion

        #region Window Events: Loaded + Closing
        private void ThumbnailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetupThumbnail();
        }
        private void ThumbnailWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // TODO: Unregister thumbnail? Not sure if necessary to prevent memory leaks, needs investigation
        }
        #endregion

        #region Events: Window Movement (ThumbnailWindow)
        private void ThumbnailWindow_MouseEnter(object sender, MouseEventArgs e)
        {
            if (IsMouseOver && !ResizeThumb.IsMouseOver)
                Cursor = Cursors.ScrollAll;
        }
        private void ThumbnailWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseOver && !ResizeThumb.IsMouseOver)
                DragMove();
        }
        private void ThumbnailWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
                //SaveTransform();
            }
        }
        #endregion

        #region Events: Window Resize (ResizeThumb)
        private void ResizeThumb_MouseEnter(object sender, MouseEventArgs e)
        {
            Cursor = Cursors.SizeNWSE;
        }
        private void ResizeThumb_MouseMove(object sender, MouseEventArgs e)
        {
            if (ResizeThumb.IsMouseCaptured)
            {
                Point mousePos = e.GetPosition(this);
                Width = Math.Clamp(mousePos.X, 100, SystemParameters.PrimaryScreenWidth / 2);
                Height = Math.Clamp(mousePos.Y, 100, SystemParameters.PrimaryScreenHeight / 2);

                UpdateThumbnailProperties();
            }
        }
        private void ResizeThumb_MouseLeave(object sender, MouseEventArgs e)
        {
            if (IsMouseOver)
                Cursor = Cursors.SizeAll;
            else
                Cursor = null;
        }
        private void ResizeThumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CurrentMousePosition = e.GetPosition(this);

            if (ResizeThumb.IsMouseOver)
                ResizeThumb.CaptureMouse();
        }
        private void ResizeThumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ResizeThumb.ReleaseMouseCapture();
            //SaveTransform();
        }
        #endregion

        #region Event: Bring Process Into View (MouseDoubleClick)
        private void ThumbnailWindow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MonitoredProcess is null)
                return;

            PInvoke.SetForegroundWindow((global::Windows.Win32.Foundation.HWND)MonitoredProcess.MainWindowHandle);
        }
        #endregion

        #region Events: Thumbnail Panning (RightMouseButton) TODO
        private void ThumbnailWindow_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseOver && !ResizeThumb.IsMouseOver)
            {
                //var mousePos = e.GetPosition(this);
                //CurrentMousePosition = new Point(mousePos.X, mousePos.Y;
                //CaptureMouse();
            }
        }
        private void ThumbnailWindow_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            //ReleaseMouseCapture();
        }
        private void ThumbnailWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (IsMouseCaptured)
            {
                if (zoomFactor == 1.0)
                    return;
                // SourceRegion.X = Math.Clamp(XOffsetSlider.Value, 0, (SourceSize.Width * (1.0 % zoomFactor)));
                var mousePos = e.GetPosition(this);
                Vector diff = mousePos - CurrentMousePosition;
                SourceRegion.X = Math.Clamp(diff.X, 0, (SourceSize.Width * (1.0 % zoomFactor)));
                SourceRegion.Y = Math.Clamp(diff.Y, 0, (SourceSize.Height * (1.0 % zoomFactor)));
                MoveCaptureRegion();

                // TODO: Implement better panning
                // Use SourceRegion.X to "move the camera around"
                // SourceSize contains the source size of the thumbnail source, eg; 2560x1440
                // My brain hurts trying to think of the math
                //MoveCaptureRegion();
            }
        }
        #endregion

        #region Event: Zooming
        private void ThumbnailWindow_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            switch (e.Delta)
            {
                case -120:
                    ZoomOut();
                    break;
                case 120:
                    ZoomIn();
                    break;
                default:
                    return;
            }
        }
        #endregion

        #region Thumbnail Methods
        #region Thumbnail Setup
        void SetupThumbnail()
        {
            if (MonitoredProcess is null || WindowHandle == IntPtr.Zero)
                return;

            var processHandle = MonitoredProcess.MainWindowHandle;
            if (processHandle != IntPtr.Zero)
            {
                PInvoke.DwmUnregisterThumbnail(processHandle);

                var registerResult = PInvoke.DwmRegisterThumbnail((global::Windows.Win32.Foundation.HWND)WindowHandle, (global::Windows.Win32.Foundation.HWND)processHandle, out ThumbnailID);

                if (registerResult == IntPtr.Zero)
                {
                    // Get Size of the Source Thumbnail
                    global::Windows.Win32.Foundation.SIZE sourceSize;
                    if (PInvoke.DwmQueryThumbnailSourceSize(ThumbnailID, out sourceSize) == IntPtr.Zero)
                    {
                        SourceSize = new Size(sourceSize.Width, sourceSize.Height);
                        SourceAspectRatio = SourceSize.Width / SourceSize.Height;
                        XOffsetSlider.Maximum = SourceSize.Width / 2;
                        YOffsetSlider.Maximum = SourceSize.Height / 2;
                        SourceSizeText.Text = $"Source: {SourceSize.Width}x{SourceSize.Height}";
                    }

                    global::Windows.Win32.Graphics.Dwm.DWM_THUMBNAIL_PROPERTIES props = new();
                    props.dwFlags = DWM_TNP_VISIBLE | DWM_TNP_RECTDESTINATION | DWM_TNP_SOURCECLIENTAREAONLY;
                    props.fVisible = true;
                    props.fSourceClientAreaOnly = true;
                    props.rcDestination = new(new System.Drawing.Rectangle(1, 1, (int)Width - 2, (int)Height - 2));

                    var updateResult = PInvoke.DwmUpdateThumbnailProperties(ThumbnailID, props);
                    // TODO: Resizing the source window after a thumbnail is created may throw things off, if necessary - investigate possible solutions
                    // eg, hooking to a Win32 event to capture the resizing and update accordingly
                }
            }

            ToolTip = GetDisplayName();
        }
        #endregion
        #region Thumbnail Updating
        void UpdateThumbnailOpacity(double newValue)
        {
            if (!ThumbnailIsValid())
                return;

            newValue = Math.Clamp(newValue, 0.0, 100.0);
            global::Windows.Win32.Graphics.Dwm.DWM_THUMBNAIL_PROPERTIES props = new();
            props.dwFlags = DWM_TNP_OPACITY;
            props.opacity = (byte)((newValue/100.0) * 255);
            PInvoke.DwmUpdateThumbnailProperties(ThumbnailID, props);
        }
        void MoveCaptureRegion()
        {
            if (ThumbnailID == IntPtr.Zero)
                return;

            global::Windows.Win32.Graphics.Dwm.DWM_THUMBNAIL_PROPERTIES props = new();
            props.dwFlags = DWM_TNP_RECTSOURCE;
            props.rcSource = new(new System.Drawing.Rectangle((int)SourceRegion.X, (int)SourceRegion.Y, (int)(SourceSize.Width * zoomFactor) - 2, (int)(SourceSize.Height * zoomFactor) - 2));

            PInvoke.DwmUpdateThumbnailProperties(ThumbnailID, props);
        }
        void UpdateThumbnailProperties()
        {
            if (ThumbnailID == IntPtr.Zero)
                return;

            global::Windows.Win32.Graphics.Dwm.DWM_THUMBNAIL_PROPERTIES props = new();
            props.dwFlags = DWM_TNP_RECTDESTINATION;
            props.rcDestination = new(new System.Drawing.Rectangle(1, 1, (int)ActualWidth - 2, (int)ActualHeight - 2));
            //System.Windows.SystemParameters.WorkArea.Height // To account for taskbar? D:

            PInvoke.DwmUpdateThumbnailProperties(ThumbnailID, props);
        }
        #endregion
        #region Close Thumbnail Keybind(Delete)
        private void ThumbnailWindow_KeyUp(object sender, KeyEventArgs e)
        {
            // TODO: Unregister thumbnail necessary?
            if (e.Key == Key.Delete && IsMouseOver && !IsMouseCaptured)
                Close();
        }
        #endregion
        #region Zooming In/Out
        double zoomFactor = 1.00;
        void ZoomIn()
        {
            if (ThumbnailID == IntPtr.Zero)
                return;

            zoomFactor = Math.Clamp(zoomFactor - 0.1, 0.1, 1.0);
            XOffsetSlider.Maximum = (SourceSize.Width * (1.0 % zoomFactor));
            YOffsetSlider.Maximum = (SourceSize.Height * (1.0 % zoomFactor));

            global::Windows.Win32.Graphics.Dwm.DWM_THUMBNAIL_PROPERTIES props = new();
            props.dwFlags = DWM_TNP_RECTSOURCE;
            props.rcSource = new(new System.Drawing.Rectangle(1, 1, (int)(SourceSize.Width * zoomFactor) - 2, (int)(SourceSize.Height * zoomFactor) - 2));

            PInvoke.DwmUpdateThumbnailProperties(ThumbnailID, props);
        }
        void ZoomOut()
        {
            if (ThumbnailID == IntPtr.Zero)
                return;

            zoomFactor = Math.Clamp(zoomFactor + 0.1, 0.1, 1.0);
            XOffsetSlider.Maximum = (SourceSize.Width * (1.0 % zoomFactor));
            YOffsetSlider.Maximum = (SourceSize.Height * (1.0 % zoomFactor));

            global::Windows.Win32.Graphics.Dwm.DWM_THUMBNAIL_PROPERTIES props = new();
            props.dwFlags = DWM_TNP_RECTSOURCE;
            props.rcSource = new(new System.Drawing.Rectangle(1, 1, (int)(SourceSize.Width * zoomFactor) - 2, (int)(SourceSize.Height * zoomFactor) - 2));

            PInvoke.DwmUpdateThumbnailProperties(ThumbnailID, props);
        }
        #endregion
        #region Utility
        bool ThumbnailIsValid() => ThumbnailID != IntPtr.Zero;
        public string GetDisplayName()
        {
            if (MonitoredProcess is null)
                return "N/A";

            try
            {
                if (!String.IsNullOrWhiteSpace(MonitoredProcess.MainModule.FileVersionInfo.FileDescription))
                    return MonitoredProcess.MainModule.FileVersionInfo.FileDescription;
            }
            catch
            {
                return MonitoredProcess.ProcessName;
            }

            return "N/A";
        }
        #endregion

        #endregion
    }
}
