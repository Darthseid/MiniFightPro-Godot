using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public partial class HistogramChart : Control
{
    private sealed class HistogramSeries
    {
        public string Label { get; set; } = string.Empty;
        public Color Color { get; set; } = Colors.SkyBlue;
        public List<double> Values { get; set; } = new();
    }

    private readonly List<HistogramSeries> _series = new();
    private string _title = string.Empty;
    private string _xAxisLabel = string.Empty;
    private int _bins = 10;

    public override void _Draw()
    {    
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.10f, 0.10f, 0.12f)); // Draw the background of the histogram panel
     
        if (_series.Count == 0 || _series.All(s => s.Values.Count == 0))   // If no data exists, show a placeholder message and stop drawing
        {
            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(18, 36),
                "Run a simulation to render histograms.",
                HorizontalAlignment.Left,
                -1f,
                16,
                Colors.White
            );
            return;
        }
        var left = 58f;      // Define plot margins and compute the drawable plot rectangle
        var top = 56f;
        var right = 20f;
        var bottom = 74f;
        var plot = new Rect2(
            left,
            top,
            Math.Max(120f, Size.X - left - right),
            Math.Max(120f, Size.Y - top - bottom)
        );

       
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(12, 28),
            _title,
            HorizontalAlignment.Left,
            -1f,
            18,
            Colors.White
        );  // Draw the chart title

        
        var min = _series.SelectMany(s => s.Values).DefaultIfEmpty(0d).Min();
        var max = _series.SelectMany(s => s.Values).DefaultIfEmpty(1d).Max(); // Determine global min/max across all series
   
        if (Math.Abs(max - min) < 0.0001)
            max = min + 1.0;  // Avoid zero-width ranges
      
        var binCount = Math.Clamp(_bins, 5, 30);
        var widths = (max - min) / binCount; // Compute histogram binning parameters
     
        var histograms = new List<int[]>();
        var peak = 1;  // Build histograms for each series

        foreach (var series in _series)
        {
            var bins = new int[binCount];
            
            foreach (var value in series.Values)  // Assign each value to a bin
            {
                var idx = (int)Math.Floor((value - min) / widths);
                idx = Math.Clamp(idx, 0, binCount - 1);
                bins[idx]++;
            }      
            peak = Math.Max(peak, bins.Max());   // Track the highest bin count across all series for scaling
            histograms.Add(bins);
        }

        DrawLine(plot.Position + new Vector2(0, plot.Size.Y), plot.Position + plot.Size, Colors.White);
        DrawLine(plot.Position, plot.Position + new Vector2(0, plot.Size.Y), Colors.White);    // Draw X/Y axes
  
        var groupWidth = plot.Size.X / binCount;
        var barWidth = groupWidth / Math.Max(1, _series.Count + 0.5f);     // Compute bar widths for grouped histograms
      
        for (var bin = 0; bin < binCount; bin++)  // Draw histogram bars
        {
            for (var s = 0; s < _series.Count; s++)
            {
                var count = histograms[s][bin];
                var barHeight = (float)count / peak * plot.Size.Y;

                var x = plot.Position.X + groupWidth * bin + (s * barWidth) + 2f;
                var y = plot.Position.Y + plot.Size.Y - barHeight;

                DrawRect(
                    new Rect2(x, y, Math.Max(2f, barWidth - 4f), barHeight),
                    _series[s].Color
                );
            }
        }
        
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(plot.Position.X, plot.Position.Y + plot.Size.Y + 22),
            min.ToString("0.##", CultureInfo.InvariantCulture),
            HorizontalAlignment.Left,
            -1f,
            12,
            Colors.White     // Draw min/max labels on the X-axis
        );

        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(plot.Position.X + plot.Size.X - 40, plot.Position.Y + plot.Size.Y + 22),
            max.ToString("0.##", CultureInfo.InvariantCulture),
            HorizontalAlignment.Left,
            -1f,
            12,
            Colors.White
        );

       
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(plot.Position.X + (plot.Size.X / 2f) - 40f, Size.Y - 18),
            _xAxisLabel,
            HorizontalAlignment.Left,
            -1f,
            12,
            Colors.White  // Draw X-axis label
        );
        
        var legendX = plot.Position.X;
        var legendY = 36f; // Draw legend (color box + label + mean)

        for (var i = 0; i < _series.Count; i++)
        {
            DrawRect(new Rect2(legendX + (i * 170), legendY, 14, 14), _series[i].Color);

            var mean = _series[i].Values.Count > 0 ? _series[i].Values.Average() : 0d;

            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(legendX + 20 + (i * 170), legendY + 12),
                $"{_series[i].Label} (avg {mean:0.##})",
                HorizontalAlignment.Left,
                -1f,
                12,
                Colors.White
            );
        }
    }

    public void SetData(string title, string xAxisLabel, Godot.Collections.Array valuesA, Color colorA, string labelA, Godot.Collections.Array valuesB, Color colorB, string labelB, int bins = 10)
    {
        _title = title;
        _xAxisLabel = xAxisLabel;
        _bins = bins;
        _series.Clear();

        var parsedA = ParseValues(valuesA);
        if (parsedA.Count > 0)
            _series.Add(new HistogramSeries { Label = string.IsNullOrWhiteSpace(labelA) ? "Series A" : labelA, Color = colorA, Values = parsedA });

        var parsedB = ParseValues(valuesB);
        if (parsedB.Count > 0)
            _series.Add(new HistogramSeries { Label = string.IsNullOrWhiteSpace(labelB) ? "Series B" : labelB, Color = colorB, Values = parsedB });

        QueueRedraw();
    }

    private static List<double> ParseValues(Godot.Collections.Array raw)
    {
        var output = new List<double>();
        foreach (var item in raw)
        {
            switch (item.VariantType)
            {
                case Variant.Type.Int:
                    output.Add(item.AsInt64());
                    break;
                case Variant.Type.Float:
                    output.Add(item.AsDouble());
                    break;
            }
        }
        return output;
    }
}
