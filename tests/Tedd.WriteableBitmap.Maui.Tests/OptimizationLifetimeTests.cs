using System.Runtime.CompilerServices;
using SkiaSharp;
using Xunit;

namespace Tedd.Maui.Tests;

public sealed class OptimizationLifetimeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void AbandonedBitmapCanBeCollectedAfterRasterPresentation(int presentationCount)
    {
        var reference = CreateAndPresentAbandonedBitmap(presentationCount);

        ForceFullCollection();

        Assert.False(reference.TryGetTarget(out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecordedPixelsSurviveCollectionOfAnAbandonedBitmap(bool presentBeforeRecording)
    {
        using var picture = RecordAbandonedBitmap(presentBeforeRecording);

        ForceFullCollection();

        using var target = new SKBitmap(2, 1);
        using var canvas = new SKCanvas(target);
        canvas.DrawPicture(picture);

        Assert.Equal(SKColors.Red, target.GetPixel(0, 0));
        Assert.Equal(SKColors.Blue, target.GetPixel(1, 0));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<WriteableBitmap> CreateAndPresentAbandonedBitmap(int presentationCount)
    {
        // Deliberately omit Dispose: a completed draw must not create a permanent
        // native callback root that defeats reclamation of an abandoned bitmap.
        var bitmap = new WriteableBitmap(2, 1);
        bitmap.ToSpanUInt32().Fill(WriteableBitmap.FromColor(SKColors.Red));
        using var target = new SKBitmap(2, 1);
        using var canvas = new SKCanvas(target);
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        for (var presentation = 0; presentation < presentationCount; presentation++)
        {
            Assert.Equal(BitmapDrawResult.Drawn, bitmap.TryDraw(
                canvas, new SKRect(0, 0, 2, 1), default, paint, false));
        }
        return new WeakReference<WriteableBitmap>(bitmap);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SKPicture RecordAbandonedBitmap(bool presentBeforeRecording)
    {
        var bitmap = new WriteableBitmap(2, 1);
        bitmap.ToSpanUInt32()[0] = WriteableBitmap.FromColor(SKColors.Red);
        bitmap.ToSpanUInt32()[1] = WriteableBitmap.FromColor(SKColors.Blue);
        using var recorder = new SKPictureRecorder();
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        var bounds = new SKRect(0, 0, 2, 1);
        if (presentBeforeRecording)
        {
            // Complete the transient presentation before recording a cached image, so
            // the cache's weak-owner release state alone must retain the native pixels.
            using var target = new SKBitmap(2, 1);
            using var rasterCanvas = new SKCanvas(target);
            Assert.Equal(BitmapDrawResult.Drawn, bitmap.TryDraw(
                rasterCanvas, bounds, default, paint, false));
        }
        var canvas = recorder.BeginRecording(bounds);
        Assert.Equal(BitmapDrawResult.Drawn, bitmap.TryDraw(
            canvas, bounds, default, paint, false));
        return recorder.EndRecording();
    }

    private static void ForceFullCollection()
    {
        // Native images can release callback handles from their finalizers; another
        // collection then reclaims managed resources formerly retained by those handles.
        for (var pass = 0; pass < 3; pass++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }
}
