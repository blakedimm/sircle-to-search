using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CircleSearch.Core.Imaging;
using CircleSearch.Core.Math;
using CircleSearch.Core.Models;
using Point = System.Windows.Point;

namespace CircleSearch.UI.Overlay;

public partial class OverlayWindow : Window
{
    private Bitmap? _fullFrame;
    private Point _startPoint;
    private bool _isSelecting;

    public event Action<byte[], SelectionBounds, Point2D>? SelectionCompleted;
    public event Action? SelectionCanceled;

    public OverlayWindow(Bitmap fullFrame) : this(fullFrame, "#00D2FF", 2.5, 0.40)
    {
    }

    public OverlayWindow(Bitmap fullFrame, string accentColorHex, double thickness, double dimmingOpacity)
    {
        InitializeComponent();

        _fullFrame = fullFrame;

        // Корректное позиционирование для сетапов с любым количеством мониторов
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        byte alpha = (byte)Math.Clamp((int)(dimmingOpacity * 255), 0, 255);
        Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0, 0, 0));

        TraceCanvas.ApplyTheme(accentColorHex, thickness);

        MouseDown += Window_MouseDown;
        MouseMove += Window_MouseMove;
        MouseUp += Window_MouseUp;
        KeyDown += Window_KeyDown;

        Loaded += (_, _) =>
        {
            Activate();
            Focus();
        };
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right)
        {
            CancelSelection();
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            _startPoint = e.GetPosition(this);
            _isSelecting = true;
            CaptureMouse();

            TraceCanvas.StartSelection(new Point2D(_startPoint.X, _startPoint.Y));
        }
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting)
            return;

        var currentPoint = e.GetPosition(this);
        TraceCanvas.UpdateSelection(new Point2D(currentPoint.X, currentPoint.Y));
    }

    private void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting || e.ChangedButton != MouseButton.Left)
            return;

        _isSelecting = false;
        ReleaseMouseCapture();
        TraceCanvas.Clear();

        var currentPoint = e.GetPosition(this);

        double x = Math.Min(_startPoint.X, currentPoint.X);
        double y = Math.Min(_startPoint.Y, currentPoint.Y);
        double w = Math.Abs(currentPoint.X - _startPoint.X);
        double h = Math.Abs(currentPoint.Y - _startPoint.Y);

        var bounds = new SelectionBounds((int)x, (int)y, (int)w, (int)h);
        FinishSelection(bounds);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelSelection();
        }
    }

    private void FinishSelection(SelectionBounds bounds)
    {
        if (_fullFrame == null || bounds.Width <= 4 || bounds.Height <= 4)
        {
            CancelSelection();
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        double scaleX = dpi.DpiScaleX;
        double scaleY = dpi.DpiScaleY;

        int cropX = Math.Max(0, (int)Math.Round(bounds.X * scaleX));
        int cropY = Math.Max(0, (int)Math.Round(bounds.Y * scaleY));
        int cropW = Math.Min((int)Math.Round(bounds.Width * scaleX), _fullFrame.Width - cropX);
        int cropH = Math.Min((int)Math.Round(bounds.Height * scaleY), _fullFrame.Height - cropY);

        if (cropW <= 0 || cropH <= 0)
        {
            CancelSelection();
            return;
        }

        byte[] encodedBytes;
        var rect = new System.Drawing.Rectangle(cropX, cropY, cropW, cropH);

        using (var croppedBitmap = _fullFrame.Clone(rect, _fullFrame.PixelFormat))
        {
            var bmpData = croppedBitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, cropW, cropH),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                int byteCount = bmpData.Stride * cropH;
                byte[] bgraData = new byte[byteCount];
                Marshal.Copy(bmpData.Scan0, bgraData, 0, byteCount);
                encodedBytes = ImageEncoder.EncodeToPngStream(bgraData, cropW, cropH);
            }
            finally
            {
                croppedBitmap.UnlockBits(bmpData);
            }
        }

        var originPoint = new Point2D(bounds.X, bounds.Y);
        SelectionCompleted?.Invoke(encodedBytes, bounds, originPoint);

        Close();
    }

    private void CancelSelection()
    {
        TraceCanvas.Clear();
        SelectionCanceled?.Invoke();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Немедленно освобождаем тяжелый полноэкранный битмап
        if (_fullFrame != null)
        {
            _fullFrame.Dispose();
            _fullFrame = null;
        }

        base.OnClosed(e);

        // Сбрасываем мусор от вырезанных массивов пикселей
        GC.Collect(1, GCCollectionMode.Forced);
    }
}