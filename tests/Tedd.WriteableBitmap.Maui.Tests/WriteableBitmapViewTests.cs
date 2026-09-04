using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using Xunit;

namespace Tedd.Maui.Tests;

// MAUI's dispatcher provider is process-wide; these tests restore it after every case.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MauiViewCollection
{
    public const string Name = "MAUI view infrastructure";
}

[Collection(MauiViewCollection.Name)]
public sealed class WriteableBitmapViewTests : IDisposable
{
    private readonly IDispatcherProvider _previousProvider = DispatcherProvider.Current;
    private readonly TestDispatcher _dispatcher = new();
    private readonly List<WriteableBitmapView> _views = [];
    private readonly List<ServiceProvider> _serviceProviders = [];

    public WriteableBitmapViewTests()
    {
        DispatcherProvider.SetCurrent(new TestDispatcherProvider(_dispatcher));
    }

    [Fact]
    public void DefaultsUseOnDemandRenderingWithNearestNeighborAspectFit()
    {
        var view = CreateView();

        Assert.Null(view.Source);
        Assert.Equal(Aspect.AspectFit, view.Aspect);
        Assert.Equal(SKFilterMode.Nearest, view.FilterMode);
        Assert.False(view.HasRenderLoop);
        Assert.Null(view.Handler);
        Assert.Equal(0, _dispatcher.DispatchAttempts);
    }

    [Fact]
    public void BindablePropertiesRoundTripAndRestoreDefaults()
    {
        using var source = new WriteableBitmap(1, 1);
        var view = CreateView();

        view.SetValue(WriteableBitmapView.SourceProperty, source);
        view.SetValue(WriteableBitmapView.AspectProperty, Aspect.Fill);
        view.SetValue(WriteableBitmapView.FilterModeProperty, SKFilterMode.Linear);

        Assert.Same(source, view.Source);
        Assert.Equal(Aspect.Fill, view.Aspect);
        Assert.Equal(SKFilterMode.Linear, view.FilterMode);

        view.ClearValue(WriteableBitmapView.SourceProperty);
        view.ClearValue(WriteableBitmapView.AspectProperty);
        view.ClearValue(WriteableBitmapView.FilterModeProperty);

        Assert.Null(view.Source);
        Assert.Equal(Aspect.AspectFit, view.Aspect);
        Assert.Equal(SKFilterMode.Nearest, view.FilterMode);
        Assert.Equal(0, _dispatcher.DispatchAttempts);
    }

    [Fact]
    public void SourceChangesBeforeHandlerAttachmentUseTheLatestSource()
    {
        using var first = SolidBitmap(SKColors.Red);
        using var second = SolidBitmap(SKColors.Blue);
        var view = CreateView();
        view.Source = first;
        view.Source = second;
        first.Invalidate();
        second.Invalidate();
        Assert.Equal(0, _dispatcher.DispatchAttempts);

        var handler = Attach(view);
        Assert.Equal(1, handler.Invalidations);
        using var result = Paint(view);
        Assert.Equal(SKColors.Blue, result.GetPixel(0, 0));

        first.Invalidate();
        Assert.Equal(1, handler.Invalidations);
        second.Invalidate();
        Assert.Equal(2, handler.Invalidations);
    }

    [Fact]
    public void ImmediateInvalidationsAreCoalescedUntilTheNextPaint()
    {
        using var source = SolidBitmap(SKColors.Red);
        var view = CreateView(source);
        var handler = Attach(view);

        source.Invalidate();
        source.Invalidate();
        view.Aspect = Aspect.Fill;
        view.FilterMode = SKFilterMode.Linear;

        Assert.Equal(1, handler.Invalidations);
        Assert.Equal(0, _dispatcher.DispatchAttempts);

        using var result = Paint(view);
        source.Invalidate();

        Assert.Equal(2, handler.Invalidations);
    }

    [Fact]
    public void BackgroundInvalidationsShareOneQueuedDispatchAndWaitForPaint()
    {
        _dispatcher.IsDispatchRequired = true;
        using var source = SolidBitmap(SKColors.Red);
        var view = CreateView(source);
        var handler = Attach(view);

        source.Invalidate();
        view.Aspect = Aspect.Fill;
        Assert.Equal(1, _dispatcher.DispatchAttempts);
        Assert.Equal(0, handler.Invalidations);

        _dispatcher.RunNext();
        source.Invalidate();
        Assert.Equal(1, handler.Invalidations);
        Assert.Equal(1, _dispatcher.DispatchAttempts);

        using var result = Paint(view);
        source.Invalidate();
        Assert.Equal(2, _dispatcher.DispatchAttempts);
        _dispatcher.RunNext();
        Assert.Equal(2, handler.Invalidations);
    }

    [Fact]
    public void RejectedDispatchCanBeRetriedByTheNextInvalidation()
    {
        _dispatcher.IsDispatchRequired = true;
        _dispatcher.AcceptDispatch = false;
        using var source = SolidBitmap(SKColors.Red);
        var view = CreateView(source);
        var handler = Attach(view);
        Assert.Equal(1, _dispatcher.DispatchAttempts);

        source.Invalidate();
        Assert.Equal(2, _dispatcher.DispatchAttempts);
        Assert.Equal(0, handler.Invalidations);

        _dispatcher.AcceptDispatch = true;
        source.Invalidate();
        _dispatcher.RunNext();
        Assert.Equal(3, _dispatcher.DispatchAttempts);
        Assert.Equal(1, handler.Invalidations);
    }

    [Fact]
    public void ReplacingSourceUnsubscribesPreviousBitmapAndPaintsReplacement()
    {
        using var first = SolidBitmap(SKColors.Red);
        using var second = SolidBitmap(SKColors.Blue);
        var view = CreateView(first);
        var handler = Attach(view);
        using var initial = Paint(view);

        view.Source = second;
        Assert.Equal(2, handler.Invalidations);
        using var replacement = Paint(view);
        Assert.Equal(SKColors.Blue, replacement.GetPixel(0, 0));

        first.Invalidate();
        Assert.Equal(2, handler.Invalidations);
        second.Invalidate();
        Assert.Equal(3, handler.Invalidations);
    }

    [Fact]
    public void ClearingSourceUnsubscribesBitmapAndErasesPreviousPixels()
    {
        using var source = SolidBitmap(SKColors.Red);
        var view = CreateView(source);
        var handler = Attach(view);
        using var initial = Paint(view);

        view.Source = null;
        using var cleared = Paint(view);
        AssertTransparent(cleared);
        Assert.Equal(2, handler.Invalidations);

        source.Invalidate();
        Assert.Equal(2, handler.Invalidations);
    }

    [Fact]
    public void AssigningTheSameSourceDoesNotRequestAnotherFrame()
    {
        using var source = SolidBitmap(SKColors.Red);
        var view = CreateView(source);
        var handler = Attach(view);
        using var initial = Paint(view);

        view.Source = source;
        Assert.Equal(1, handler.Invalidations);

        source.Invalidate();
        Assert.Equal(2, handler.Invalidations);
    }

    [Fact]
    public void DetachedHandlerStopsSourceNotificationsAndClearsRenderingState()
    {
        using var source = SolidBitmap(SKColors.Red);
        var view = CreateView(source);
        var handler = Attach(view);
        using var initial = Paint(view);

        view.Handler = null;
        source.Invalidate();
        Assert.Equal(1, handler.Invalidations);
        using var detached = Paint(view);
        AssertTransparent(detached);
        Assert.Same(source, view.Source);

        var replacement = Attach(view);
        Assert.Equal(1, replacement.Invalidations);
        using var reattached = Paint(view);
        Assert.Equal(SKColors.Red, reattached.GetPixel(0, 0));
        source.Invalidate();
        Assert.Equal(2, replacement.Invalidations);
    }

    [Fact]
    public void CallbackQueuedBeforeHandlerRemovalDoesNotInvalidateDetachedHandler()
    {
        _dispatcher.IsDispatchRequired = true;
        using var source = SolidBitmap(SKColors.Red);
        var view = CreateView(source);
        var handler = Attach(view);

        view.Handler = null;
        _dispatcher.RunNext();
        source.Invalidate();
        Assert.Equal(0, handler.Invalidations);
        Assert.Equal(1, _dispatcher.DispatchAttempts);

        var replacement = Attach(view);
        _dispatcher.RunNext();
        Assert.Equal(1, replacement.Invalidations);
        Assert.Equal(2, _dispatcher.DispatchAttempts);
    }

    [Fact]
    public void HandlerReplacementRetainsSourceAndUsesNewHandler()
    {
        using var source = SolidBitmap(SKColors.Red);
        var view = CreateView(source);
        var first = Attach(view);
        using var initial = Paint(view);

        var second = Attach(view);
        Assert.Equal(1, second.Invalidations);
        using var replacement = Paint(view);
        source.Invalidate();

        Assert.Equal(SKColors.Red, replacement.GetPixel(0, 0));
        Assert.Equal(1, first.Invalidations);
        Assert.Equal(2, second.Invalidations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PaintingWithoutAHandlerClearsEvenWhenSourceIsSet(bool hasSource)
    {
        using var source = SolidBitmap(SKColors.Red);
        var view = CreateView(hasSource ? source : null);

        using var result = Paint(view);

        AssertTransparent(result);
    }

    [Fact]
    public void PaintingWithAHandlerAndNoSourceClearsTheSurface()
    {
        var view = CreateView();
        Attach(view);

        using var result = Paint(view);

        AssertTransparent(result);
    }

    [Fact]
    public void DisposedSourceSchedulesAClearAndCanBeReplaced()
    {
        using var source = SolidBitmap(SKColors.Red);
        using var replacement = SolidBitmap(SKColors.Blue);
        var view = CreateView(source);
        var handler = Attach(view);
        using var initial = Paint(view);

        source.Dispose();
        Assert.Equal(2, handler.Invalidations);
        using var disposed = Paint(view);
        AssertTransparent(disposed);

        view.Source = replacement;
        using var result = Paint(view);
        Assert.Equal(SKColors.Blue, result.GetPixel(0, 0));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(-1, 4)]
    [InlineData(4, -1)]
    public void InvalidPaintDimensionsClearTheExistingSurface(int width, int height)
    {
        using var source = SolidBitmap(SKColors.Red);
        var view = CreateView(source);
        Attach(view);

        using var result = Paint(view, info: new SKImageInfo(width, height));

        AssertTransparent(result);
    }

    [Theory]
    [InlineData(Aspect.Fill, "RRBB", "RRBB", "RRBB", "RRBB")]
    [InlineData(Aspect.AspectFit, "....", "RRBB", "RRBB", "....")]
    [InlineData(Aspect.AspectFill, "RRBB", "RRBB", "RRBB", "RRBB")]
    public void AspectControlsScalingAndClearsLetterboxing(
        Aspect aspect, string row0, string row1, string row2, string row3)
    {
        using var source = new WriteableBitmap(2, 1);
        source.ToSpanUInt32()[0] = WriteableBitmap.FromColor(SKColors.Red);
        source.ToSpanUInt32()[1] = WriteableBitmap.FromColor(SKColors.Blue);
        var view = CreateView(source);
        view.Aspect = aspect;
        Attach(view);

        using var result = Paint(view, 4, 4);

        AssertRows(result, row0, row1, row2, row3);
    }

    [Fact]
    public void CenterPreservesPixelSizeAndClearsSurroundingPixels()
    {
        using var source = new WriteableBitmap(2, 2);
        source.ToSpanUInt32().Fill(WriteableBitmap.FromColor(SKColors.Red));
        var view = CreateView(source);
        view.Aspect = Aspect.Center;
        Attach(view);

        using var result = Paint(view, 4, 4);

        AssertRows(result, "....", ".RR.", ".RR.", "....");
    }

    [Fact]
    public void ChangingAspectAfterAttachmentChangesTheNextRenderedFrame()
    {
        using var source = new WriteableBitmap(2, 1);
        source.ToSpanUInt32().Fill(WriteableBitmap.FromColor(SKColors.Red));
        var view = CreateView(source);
        var handler = Attach(view);
        using var initial = Paint(view, 4, 4);
        AssertRows(initial, "....", "RRRR", "RRRR", "....");

        view.Aspect = Aspect.Fill;
        Assert.Equal(2, handler.Invalidations);
        using var changed = Paint(view, 4, 4);
        AssertRows(changed, "RRRR", "RRRR", "RRRR", "RRRR");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinearSamplingInterpolatesNeighboringPixels(bool setBeforeAttachment)
    {
        using var source = new WriteableBitmap(2, 1);
        source.ToSpanUInt32()[0] = WriteableBitmap.FromColor(SKColors.Black);
        source.ToSpanUInt32()[1] = WriteableBitmap.FromColor(SKColors.White);
        var view = CreateView(source);
        view.Aspect = Aspect.Fill;
        if (setBeforeAttachment)
            view.FilterMode = SKFilterMode.Linear;
        Attach(view);

        if (!setBeforeAttachment)
        {
            using var nearest = Paint(view, 4, 1);
            Assert.Equal(SKColors.Black, nearest.GetPixel(1, 0));
            Assert.Equal(SKColors.White, nearest.GetPixel(2, 0));
            view.FilterMode = SKFilterMode.Linear;
        }

        using var linear = Paint(view, 4, 1);
        Assert.InRange(linear.GetPixel(1, 0).Red, 63, 65);
        Assert.InRange(linear.GetPixel(2, 0).Red, 190, 192);
        Assert.Equal(SKColors.Black, linear.GetPixel(0, 0));
        Assert.Equal(SKColors.White, linear.GetPixel(3, 0));
    }

    [Fact]
    public void SourceBlendReplacesExistingPixelsIncludingTheirAlpha()
    {
        using var source = SolidBitmap(new SKColor(255, 0, 0, 128));
        var view = CreateView(source);
        Attach(view);

        using var result = Paint(view);
        var pixel = result.GetPixel(0, 0);

        Assert.Equal(128, pixel.Alpha);
        Assert.Equal(255, pixel.Red);
        Assert.Equal(0, pixel.Green);
        Assert.Equal(0, pixel.Blue);
    }

    [Fact]
    public void PaintSurfaceEventRunsAfterBitmapRenderingAndReceivesOriginalArguments()
    {
        using var source = SolidBitmap(SKColors.Red);
        var view = CreateView(source);
        Attach(view);
        using var surface = SKSurface.Create(new SKImageInfo(1, 1));
        var args = new SKPaintGLSurfaceEventArgs(surface, null!, GRSurfaceOrigin.TopLeft,
            new SKImageInfo(1, 1));
        var calls = 0;
        view.PaintSurface += (sender, received) =>
        {
            calls++;
            Assert.Same(view, sender);
            Assert.Same(args, received);
            using var snapshot = received.Surface.Snapshot();
            using var pixels = SKBitmap.FromImage(snapshot);
            Assert.Equal(SKColors.Red, pixels.GetPixel(0, 0));
        };

        ((ISKGLView)view).OnPaintSurface(args);

        Assert.Equal(1, calls);
    }

    public void Dispose()
    {
        foreach (var view in _views)
            view.Handler = null;
        foreach (var provider in _serviceProviders)
            provider.Dispose();
        DispatcherProvider.SetCurrent(_previousProvider);
    }

    private WriteableBitmapView CreateView(WriteableBitmap? source = null)
    {
        var view = new WriteableBitmapView { Source = source };
        _views.Add(view);
        return view;
    }

    private TestViewHandler Attach(WriteableBitmapView view)
    {
        var services = new ServiceCollection().AddSingleton<IDispatcher>(_dispatcher).BuildServiceProvider();
        _serviceProviders.Add(services);
        var handler = new TestViewHandler(new MauiContext(services));
        view.Handler = handler;
        return handler;
    }

    private static WriteableBitmap SolidBitmap(SKColor color)
    {
        var source = new WriteableBitmap(1, 1);
        source.ToSpanUInt32()[0] = WriteableBitmap.FromColor(color);
        return source;
    }

    private static SKBitmap Paint(WriteableBitmapView view, int width = 1, int height = 1,
        SKImageInfo? info = null)
    {
        var actualInfo = new SKImageInfo(width, height);
        using var surface = SKSurface.Create(actualInfo);
        surface.Canvas.Clear(SKColors.Lime);
        ((ISKGLView)view).OnPaintSurface(new SKPaintGLSurfaceEventArgs(surface, null!,
            GRSurfaceOrigin.TopLeft, info ?? actualInfo));
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private static void AssertTransparent(SKBitmap bitmap)
    {
        Assert.All(bitmap.Pixels, pixel => Assert.Equal((byte)0, pixel.Alpha));
    }

    private static void AssertRows(SKBitmap bitmap, params string[] rows)
    {
        Assert.Equal(rows.Length, bitmap.Height);
        for (var y = 0; y < rows.Length; y++)
        {
            Assert.Equal(rows[y].Length, bitmap.Width);
            for (var x = 0; x < rows[y].Length; x++)
            {
                var expected = rows[y][x] switch
                {
                    'R' => SKColors.Red,
                    'B' => SKColors.Blue,
                    _ => new SKColor(0, 0, 0, 0)
                };
                Assert.Equal(expected, bitmap.GetPixel(x, y));
            }
        }
    }

    private sealed class TestDispatcherProvider(IDispatcher dispatcher) : IDispatcherProvider
    {
        public IDispatcher GetForCurrentThread() => dispatcher;
    }

    private sealed class TestDispatcher : IDispatcher
    {
        private readonly Queue<Action> _pending = new();
        public bool IsDispatchRequired { get; set; }
        public bool AcceptDispatch { get; set; } = true;
        public int DispatchAttempts { get; private set; }

        public bool Dispatch(Action action)
        {
            DispatchAttempts++;
            if (!AcceptDispatch)
                return false;
            _pending.Enqueue(action);
            return true;
        }

        public void RunNext() => _pending.Dequeue()();
        public bool DispatchDelayed(TimeSpan delay, Action action) => throw new NotSupportedException();
        public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
    }

    private sealed class TestViewHandler(IMauiContext context) : IViewHandler
    {
        public int Invalidations { get; private set; }
        public IMauiContext MauiContext { get; private set; } = context;
        public IView VirtualView { get; private set; } = null!;
        IElement IElementHandler.VirtualView => VirtualView;
        public object PlatformView { get; } = new();
        public object ContainerView => PlatformView;
        public bool HasContainer { get; set; }

        public void SetMauiContext(IMauiContext mauiContext) => MauiContext = mauiContext;
        public void SetVirtualView(IElement view) => VirtualView = (IView)view;
        public void UpdateValue(string property) { }
        public void DisconnectHandler() { }
        public Size GetDesiredSize(double widthConstraint, double heightConstraint) => Size.Zero;
        public void PlatformArrange(Rect frame) { }

        public void Invoke(string command, object? args)
        {
            if (command == nameof(ISKGLView.InvalidateSurface))
                Invalidations++;
        }
    }
}
