using Microsoft.Maui;
using Microsoft.Maui.Controls;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Tedd.Maui;

/// <summary>
/// Presents a <see cref="WriteableBitmap"/> through SkiaSharp's GPU-backed MAUI surface.
/// </summary>
public sealed class WriteableBitmapView : SKGLView
{
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(WriteableBitmap),
        typeof(WriteableBitmapView),
        default(WriteableBitmap),
        propertyChanged: static (bindable, oldValue, newValue) =>
            ((WriteableBitmapView)bindable).OnSourceChanged(
                (WriteableBitmap?)oldValue,
                (WriteableBitmap?)newValue));

    public static readonly BindableProperty AspectProperty = BindableProperty.Create(
        nameof(Aspect),
        typeof(Aspect),
        typeof(WriteableBitmapView),
        Aspect.AspectFit,
        propertyChanged: static (bindable, _, newValue) =>
            ((WriteableBitmapView)bindable).OnAspectChanged((Aspect)newValue));

    public static readonly BindableProperty FilterModeProperty = BindableProperty.Create(
        nameof(FilterMode),
        typeof(SKFilterMode),
        typeof(WriteableBitmapView),
        SKFilterMode.Nearest,
        propertyChanged: static (bindable, _, newValue) =>
            ((WriteableBitmapView)bindable).OnFilterModeChanged((SKFilterMode)newValue));

    private readonly object _renderGate = new();
    private readonly Action _invalidateSurfaceAction;
    private WriteableBitmap? _subscribedSource;
    private WriteableBitmap? _renderSource;
    private Aspect _renderAspect = Aspect.AspectFit;
    private SKPaint? _sourcePaint;
    private SKSamplingOptions _samplingOptions = new(SKFilterMode.Nearest);
    private int _renderQueued;

    public WriteableBitmapView()
    {
        _invalidateSurfaceAction = InvalidateQueuedSurface;
        HasRenderLoop = false;
    }

    public WriteableBitmap? Source
    {
        get => (WriteableBitmap?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Aspect Aspect
    {
        get => (Aspect)GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    /// <summary>
    /// Selects nearest-neighbor or linear sampling. Nearest-neighbor is the fastest default.
    /// </summary>
    public SKFilterMode FilterMode
    {
        get => (SKFilterMode)GetValue(FilterModeProperty);
        set => SetValue(FilterModeProperty, value);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            Interlocked.Exchange(ref _renderQueued, 0);
            lock (_renderGate)
            {
                DetachSource();
                _renderSource = null;
                _sourcePaint?.Dispose();
                _sourcePaint = null;
            }
            return;
        }

        lock (_renderGate)
        {
            _renderSource = Source;
            _renderAspect = Aspect;
            _samplingOptions = new SKSamplingOptions(FilterMode);
            _sourcePaint ??= new SKPaint
            {
                BlendMode = SKBlendMode.Src,
                IsAntialias = false
            };
            AttachSource(Source);
        }

        QueueRender();
    }

    protected override void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
    {
        Interlocked.Exchange(ref _renderQueued, 0);

        lock (_renderGate)
        {
            var canvas = e.Surface.Canvas;
            var source = _renderSource;
            var paint = _sourcePaint;

            if (source is null || paint is null || e.Info.Width <= 0 || e.Info.Height <= 0)
            {
                canvas.Clear(SKColors.Transparent);
            }
            else
            {
                var destination = BitmapLayout.CalculateDestination(
                    source.Width,
                    source.Height,
                    e.Info.Width,
                    e.Info.Height,
                    _renderAspect);

                var clearBeforeDraw = BitmapLayout.RequiresClear(
                    destination,
                    e.Info.Width,
                    e.Info.Height);
                var result = source.TryDraw(
                    canvas,
                    destination,
                    _samplingOptions,
                    paint,
                    clearBeforeDraw);

                if (result != BitmapDrawResult.Drawn)
                    canvas.Clear(SKColors.Transparent);
            }
        }

        base.OnPaintSurface(e);
    }

    private void OnSourceChanged(WriteableBitmap? oldSource, WriteableBitmap? newSource)
    {
        lock (_renderGate)
        {
            _renderSource = newSource;

            if (ReferenceEquals(_subscribedSource, oldSource))
                DetachSource();

            if (Handler is not null)
                AttachSource(newSource);
        }

        QueueRender();
    }

    private void OnAspectChanged(Aspect aspect)
    {
        lock (_renderGate)
            _renderAspect = aspect;

        QueueRender();
    }

    private void OnFilterModeChanged(SKFilterMode filterMode)
    {
        lock (_renderGate)
            _samplingOptions = new SKSamplingOptions(filterMode);

        QueueRender();
    }

    private void AttachSource(WriteableBitmap? source)
    {
        if (ReferenceEquals(_subscribedSource, source))
            return;

        DetachSource();
        _subscribedSource = source;

        if (source is not null)
            source.Invalidated += OnSourceInvalidated;
    }

    private void DetachSource()
    {
        if (_subscribedSource is not null)
            _subscribedSource.Invalidated -= OnSourceInvalidated;

        _subscribedSource = null;
    }

    private void OnSourceInvalidated(object? sender, EventArgs e) => QueueRender();

    private void QueueRender()
    {
        if (Handler is null || Interlocked.CompareExchange(ref _renderQueued, 1, 0) != 0)
            return;

        var dispatcher = Dispatcher;
        if (dispatcher is null)
        {
            Interlocked.Exchange(ref _renderQueued, 0);
            return;
        }

        if (!dispatcher.IsDispatchRequired)
        {
            InvalidateQueuedSurface();
            return;
        }

        if (!dispatcher.Dispatch(_invalidateSurfaceAction))
            Interlocked.Exchange(ref _renderQueued, 0);
    }

    private void InvalidateQueuedSurface()
    {
        if (Handler is null)
        {
            Interlocked.Exchange(ref _renderQueued, 0);
            return;
        }

        InvalidateSurface();
    }
}
