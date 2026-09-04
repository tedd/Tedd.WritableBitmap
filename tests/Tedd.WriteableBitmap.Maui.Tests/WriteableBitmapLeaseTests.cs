using SkiaSharp;
using Xunit;

namespace Tedd.Maui.Tests;

public sealed class WriteableBitmapLeaseTests
{
    [Fact]
    public void DefaultLeaseRejectsAccessAndCanBeDisposedRepeatedly()
    {
        WriteableBitmap.WriteLease lease = default;

        AssertUnavailable(ref lease);
        lease.Dispose();
        lease.Dispose();
        AssertUnavailable(ref lease);
    }

    [Fact]
    public void FailedAcquisitionReturnsAnUnusableLease()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        Assert.True(bitmap.TryBeginWrite(out var active));
        try
        {
            Assert.False(bitmap.TryBeginWrite(out var rejected));
            AssertUnavailable(ref rejected);
            rejected.Dispose();
            active.Pixels[0] = WriteableBitmap.FromColor(SKColors.Blue);
        }
        finally
        {
            active.Dispose();
        }

        Assert.True(bitmap.TryBeginWrite(out var next));
        next.Dispose();
    }

    [Fact]
    public void LeaseBytesAndPixelsAliasTheSameBackBuffer()
    {
        using var bitmap = new WriteableBitmap(3, 2);
        Assert.True(bitmap.TryBeginWrite(out var lease));
        try
        {
            lease.Bytes.Fill(0x7F);
            Assert.All(lease.Pixels.ToArray(), pixel => Assert.Equal(0x7F7F7F7Fu, pixel));
            lease.Pixels[^1] = 0x11223344;
            Assert.Equal(BitConverter.GetBytes(0x11223344u), lease.Bytes[^sizeof(uint)..].ToArray());
            Assert.All(bitmap.ToSpanByte().ToArray(), value => Assert.Equal(0, value));
        }
        finally
        {
            lease.Dispose();
        }

        AssertUnavailable(ref lease);
    }

    [Fact]
    public void DisposingLeaseRepeatedlyPublishesOnlyOnce()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        var notifications = 0;
        bitmap.Invalidated += (_, _) => notifications++;
        Assert.True(bitmap.TryBeginWrite(out var lease));

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(1, notifications);
        AssertUnavailable(ref lease);
        Assert.True(bitmap.TryBeginWrite(out var next));
        next.Dispose();
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void StaleCopiedLeaseCannotAccessOrCompleteAReusedBuffer()
    {
        using var bitmap = new WriteableBitmap(1, 1, bufferCount: 2);
        var notifications = 0;
        bitmap.Invalidated += (_, _) => notifications++;
        Assert.True(bitmap.TryBeginWrite(out var first));
        var stale = first;
        first.Dispose();

        Assert.True(bitmap.TryBeginWrite(out var intermediate));
        intermediate.Dispose();
        Assert.True(bitmap.TryBeginWrite(out var current));
        try
        {
            current.Pixels[0] = WriteableBitmap.FromColor(SKColors.Blue);
            AssertUnavailable(ref stale);
            stale.Dispose();
            Assert.Equal(2, notifications);
            Assert.False(bitmap.TryBeginWrite(out _));
            Assert.Equal(WriteableBitmap.FromColor(SKColors.Blue), current.Pixels[0]);
        }
        finally
        {
            current.Dispose();
        }

        Assert.Equal(3, notifications);
    }

    [Fact]
    public void RawInvalidationDuringLeaseDoesNotPublishOrCancelTheWrite()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        var notifications = 0;
        bitmap.Invalidated += (_, _) => notifications++;
        Assert.True(bitmap.TryBeginWrite(out var lease));
        try
        {
            lease.Pixels[0] = WriteableBitmap.FromColor(SKColors.Lime);

            Assert.Throws<InvalidOperationException>(bitmap.Invalidate);
            Assert.Equal(0, notifications);
            Assert.False(bitmap.TryBeginWrite(out _));
            Assert.Equal(WriteableBitmap.FromColor(SKColors.Lime), lease.Pixels[0]);
        }
        finally
        {
            lease.Dispose();
        }

        Assert.Equal(1, notifications);
    }

    [Fact]
    public void OwnerDisposalInvalidatesActiveLeaseAndSuppressesItsPublication()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        var notifications = 0;
        bitmap.Invalidated += (_, _) => notifications++;
        Assert.True(bitmap.TryBeginWrite(out var lease));
        var copy = lease;

        bitmap.Dispose();

        AssertUnavailable(ref lease);
        AssertUnavailable(ref copy);
        lease.Dispose();
        copy.Dispose();
        bitmap.Dispose();
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void PublicationRemainsCompleteWhenAnEventSubscriberThrows()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        var expected = new InvalidOperationException("Subscriber failure.");
        EventHandler handler = (_, _) => throw expected;
        bitmap.Invalidated += handler;
        try
        {
            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => PublishFrame(bitmap)));
        }
        finally
        {
            bitmap.Invalidated -= handler;
        }

        Assert.True(bitmap.TryBeginWrite(out var next));
        next.Dispose();
    }

    [Fact]
    public void PublicationSubscriberCanStartTheNextFrame()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        var notifications = 0;
        EventHandler handler = null!;
        handler = (sender, args) =>
        {
            notifications++;
            Assert.Same(bitmap, sender);
            Assert.Same(EventArgs.Empty, args);
            bitmap.Invalidated -= handler;
            Assert.True(bitmap.TryBeginWrite(out var next));
            next.Pixels[0] = WriteableBitmap.FromColor(SKColors.Blue);
            next.Dispose();
        };
        bitmap.Invalidated += handler;

        PublishFrame(bitmap);

        Assert.Equal(1, notifications);
    }

    [Fact]
    public void DisposalSubscriberObservesTheDisposedState()
    {
        using var bitmap = new WriteableBitmap(1, 1);
        var notifications = 0;
        bitmap.Invalidated += (sender, args) =>
        {
            notifications++;
            Assert.Same(bitmap, sender);
            Assert.Same(EventArgs.Empty, args);
            Assert.True(bitmap.IsDisposed);
            Assert.Throws<ObjectDisposedException>(bitmap.Invalidate);
            bitmap.Dispose();
        };

        bitmap.Dispose();

        Assert.Equal(1, notifications);
    }

    private static void PublishFrame(WriteableBitmap bitmap)
    {
        Assert.True(bitmap.TryBeginWrite(out var lease));
        lease.Pixels[0] = WriteableBitmap.FromColor(SKColors.Red);
        lease.Dispose();
    }

    private static void AssertUnavailable(ref WriteableBitmap.WriteLease lease)
    {
        ObjectDisposedException? bytesException = null;
        try
        {
            _ = lease.Bytes;
        }
        catch (ObjectDisposedException exception)
        {
            bytesException = exception;
        }

        Assert.NotNull(bytesException);
        Assert.Equal(nameof(WriteableBitmap.WriteLease), bytesException.ObjectName);

        ObjectDisposedException? pixelsException = null;
        try
        {
            _ = lease.Pixels;
        }
        catch (ObjectDisposedException exception)
        {
            pixelsException = exception;
        }

        Assert.NotNull(pixelsException);
        Assert.Equal(nameof(WriteableBitmap.WriteLease), pixelsException.ObjectName);
    }
}
