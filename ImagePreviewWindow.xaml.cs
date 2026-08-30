using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace U_Wii_X_Fusion
{
    public partial class ImagePreviewWindow : Window
    {
        private bool _isDragging;
        private Point _dragStartPoint;
        private double _currentScale = 1.0;
        private const double MinScale = 0.1;
        private const double MaxScale = 5.0;
        private const double ScaleStep = 0.2;

        public ImagePreviewWindow()
        {
            InitializeComponent();
            this.Loaded += ImagePreviewWindow_Loaded;
        }

        public ImagePreviewWindow(BitmapImage imageSource) : this()
        {
            SetImageSource(imageSource);
        }

        private void ImagePreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 设置窗口初始位置为屏幕中心
            var workingArea = SystemParameters.WorkArea;
            this.Left = workingArea.Left + (workingArea.Width - this.ActualWidth) / 2;
            this.Top = workingArea.Top + (workingArea.Height - this.ActualHeight) / 2;
        }

        public void SetImageSource(BitmapImage imageSource)
        {
            if (imageSource == null) return;
            
            imgPreview.Source = imageSource;
            
            // 根据图片大小调整窗口大小
            double maxWidth = SystemParameters.WorkArea.Width * 0.8;
            double maxHeight = SystemParameters.WorkArea.Height * 0.8;
            
            double imgWidth = imageSource.PixelWidth;
            double imgHeight = imageSource.PixelHeight;
            
            double scale = Math.Min(maxWidth / imgWidth, maxHeight / imgHeight);
            scale = Math.Min(scale, 1.0); // 不超过原始大小
            
            this.Width = imgWidth * scale;
            this.Height = imgHeight * scale;
            
            // 重置缩放和平移
            _currentScale = 1.0;
            scaleTransform.ScaleX = 1.0;
            scaleTransform.ScaleY = 1.0;
            translateTransform.X = 0;
            translateTransform.Y = 0;
        }

        private void ImgPreview_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                _currentScale = Math.Min(_currentScale + ScaleStep, MaxScale);
            }
            else
            {
                _currentScale = Math.Max(_currentScale - ScaleStep, MinScale);
            }
            
            scaleTransform.ScaleX = _currentScale;
            scaleTransform.ScaleY = _currentScale;
            e.Handled = true;
        }

        private void ImgPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            imgPreview.CaptureMouse();
            e.Handled = true;
        }

        private void ImgPreview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            imgPreview.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void ImgPreview_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            
            Point currentPosition = e.GetPosition(this);
            Vector delta = currentPosition - _dragStartPoint;
            
            translateTransform.X += delta.X;
            translateTransform.Y += delta.Y;
            
            _dragStartPoint = currentPosition;
            e.Handled = true;
        }

        private void Border_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
            e.Handled = true;
        }
    }
}