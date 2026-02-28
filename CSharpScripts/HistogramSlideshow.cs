using Godot;
using System.Collections.Generic;

public sealed class HistogramSlide
{
    public string Title { get; set; } = string.Empty;
    public string[] Labels { get; set; } = System.Array.Empty<string>();
    public double[] Values { get; set; } = System.Array.Empty<double>();
}

public partial class HistogramSlideshow : Control
{
    private Label _slideLabel = null!;
    private HistogramChart _chart = null!;
    private Button _prev = null!;
    private Button _next = null!;
    private readonly List<HistogramSlide> _slides = new();
    private int _index;

    public override void _Ready()
    {
        _slideLabel = GetNode<Label>("%SlideLabel");
        _chart = GetNode<HistogramChart>("%HistogramChart");
        _prev = GetNode<Button>("%BtnPrev");
        _next = GetNode<Button>("%BtnNext");
        GetNode<Button>("%BtnClose").Pressed += QueueFree;
        _prev.Pressed += () => { if (_index > 0) { _index--; Render(); } };
        _next.Pressed += () => { if (_index < _slides.Count - 1) { _index++; Render(); } };
    }

    public void SetSlides(List<HistogramSlide> slides)
    {
        _slides.Clear();
        _slides.AddRange(slides);
        _index = 0;
        Render();
    }

    private void Render()
    {
        if (_slides.Count == 0)
        {
            _slideLabel.Text = "No histograms available.";
            _chart.SetData("No Data", System.Array.Empty<string>(), System.Array.Empty<double>());
            return;
        }

        var slide = _slides[_index];
        _slideLabel.Text = $"Slide {_index + 1}/{_slides.Count}: {slide.Title}";
        _chart.SetData(slide.Title, slide.Labels, slide.Values);
        _prev.Disabled = _index <= 0;
        _next.Disabled = _index >= _slides.Count - 1;
    }
}
