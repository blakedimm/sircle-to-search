using System;
using System.Windows;
using System.Windows.Media;
using CircleSearch.Core.Math;

namespace CircleSearch.UI.Overlay;

public sealed class SelectionCanvas : FrameworkElement
{
    private Pen _glowOuterPen = null!;
    private Pen _glowInnerPen = null!;
    private Pen _corePen = null!;
    private Brush _fillBrush = null!;

    private Point2D _startPoint;
    private Point2D _currentPoint;
    private bool _isDrawing;

    public SelectionCanvas()
    {
        ApplyTheme("#00D2FF", 2.5);
    }

    /// <summary>
    /// Динамически пересоздает неоновые кисти под выбранный цвет и толщину
    /// </summary>
    public void ApplyTheme(string accentHex, double thickness = 2.5)
    {
        Color baseColor;
        try
        {
            var converted = ColorConverter.ConvertFromString(accentHex);
            baseColor = converted is Color c ? c : Color.FromRgb(0, 210, 255);
        }
        catch
        {
            baseColor = Color.FromRgb(0, 210, 255);
        }

        // Внешнее рассеянное свечение (Outer Glow)
        var outerBrush = new SolidColorBrush(Color.FromArgb(60, baseColor.R, baseColor.G, baseColor.B));
        outerBrush.Freeze();
        _glowOuterPen = new Pen(outerBrush, thickness * 2.4);
        _glowOuterPen.Freeze();

        // Внутренний насыщенный ореол (Inner Glow)
        var innerBrush = new SolidColorBrush(Color.FromArgb(150, baseColor.R, baseColor.G, baseColor.B));
        innerBrush.Freeze();
        _glowInnerPen = new Pen(innerBrush, thickness * 1.2);
        _glowInnerPen.Freeze();

        // Центральная яркая сердцевина (Core)
        var coreBrush = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255));
        coreBrush.Freeze();
        _corePen = new Pen(coreBrush, Math.Max(1.0, thickness * 0.6));
        _corePen.Freeze();

        // Легкая полупрозрачная заливка внутри выделенного прямоугольника
        var fill = new SolidColorBrush(Color.FromArgb(24, baseColor.R, baseColor.G, baseColor.B));
        fill.Freeze();
        _fillBrush = fill;

        InvalidateVisual();
    }

    public void StartSelection(Point2D start)
    {
        _startPoint = start;
        _currentPoint = start;
        _isDrawing = true;
        InvalidateVisual();
    }

    public void UpdateSelection(Point2D current)
    {
        if (!_isDrawing) return;
        _currentPoint = current;
        InvalidateVisual();
    }

    public void Clear()
    {
        _isDrawing = false;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (!_isDrawing)
            return;

        double x = Math.Min(_startPoint.X, _currentPoint.X);
        double y = Math.Min(_startPoint.Y, _currentPoint.Y);
        double w = Math.Abs(_currentPoint.X - _startPoint.X);
        double h = Math.Abs(_currentPoint.Y - _startPoint.Y);

        if (w < 2 || h < 2)
            return;

        var rect = new Rect(x, y, w, h);

        dc.DrawRoundedRectangle(_fillBrush, null, rect, 6, 6);
        dc.DrawRoundedRectangle(null, _glowOuterPen, rect, 6, 6);
        dc.DrawRoundedRectangle(null, _glowInnerPen, rect, 6, 6);
        dc.DrawRoundedRectangle(null, _corePen, rect, 6, 6);
    }
}