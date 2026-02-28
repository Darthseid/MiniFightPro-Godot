using Godot;
using System;

public partial class HistogramChart : Control
{
    private string _title = string.Empty;
    private string[] _labels = Array.Empty<string>();
    private double[] _values = Array.Empty<double>();

    public void SetData(string title, string[] labels, double[] values)
    {
        _title = title ?? string.Empty;
        _labels = labels ?? Array.Empty<string>();
        _values = values ?? Array.Empty<double>();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var rect = GetRect();
        DrawRect(rect, new Color(0.08f, 0.08f, 0.1f), true);
        DrawString(ThemeDB.FallbackFont, new Vector2(16, 28), _title, HAlignment.Left, -1, 18, Colors.White);

        if (_values.Length == 0)
            return;

        var marginLeft = 48f;
        var marginBottom = 64f;
        var top = 48f;
        var width = rect.Size.X - marginLeft - 24f;
        var height = rect.Size.Y - marginBottom - top;

        DrawLine(new Vector2(marginLeft, top + height), new Vector2(marginLeft + width, top + height), Colors.Gray, 2);

        var max = 0d;
        for (var i = 0; i < _values.Length; i++)
            max = Math.Max(max, _values[i]);
        if (max <= 0) max = 1;

        var barWidth = width / Math.Max(1, _values.Length * 2);
        for (var i = 0; i < _values.Length; i++)
        {
            var x = marginLeft + (i * 2 + 0.5f) * barWidth;
            var h = (float)(_values[i] / max) * height;
            var y = top + height - h;
            var color = i % 2 == 0 ? new Color(0.2f, 0.7f, 1f) : new Color(1f, 0.6f, 0.2f);
            DrawRect(new Rect2(x, y, barWidth, h), color, true);

            var label = i < _labels.Length ? _labels[i] : $"V{i + 1}";
            DrawString(ThemeDB.FallbackFont, new Vector2(x, top + height + 20), label, HAlignment.Left, barWidth + 20, 14, Colors.White);
            DrawString(ThemeDB.FallbackFont, new Vector2(x, y - 6), $"{_values[i]:0.##}", HAlignment.Left, barWidth + 30, 12, Colors.White);
        }
    }
}
