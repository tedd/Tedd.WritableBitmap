using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Tedd.Maui;

/// <summary>
/// Owns a directly mutable, platform-native 32-bit Skia pixel swap chain.
/// </summary>
/// <remarks>
/// <see cref="TryBeginWrite"/> writes a free back buffer while the GPU retains older frames.
/// Raw span and pointer access remains available for callers that provide equivalent synchronization.
/// </remarks>
public sealed class WriteableBitmap : IDisposable
{
    private const int RequiredBytesPerPixel = sizeof(uint);

    /// <summary>The default number of native buffers used for overlapping production and presentation.</summary>
    public const int DefaultBufferCount = 3;

    private readonly System.Threading.Lock _lifetimeGate = new();
    private readonly BufferSlot[] _buffers;
    private int _frontBufferIndex;
    private int _nextBufferSearchIndex;
    private int _activeWriteBufferIndex = -1;
    private long _activeWriteGeneration;
    private long _nextWriteGeneration;
    private int _isDisposed;

    /// <summary>
    /// Creates zero-initialized native buffers using Skia's platform-native 32-bit color layout.
    /// Three buffers are used by default so CPU production can overlap GPU presentation.
    /// </summary>
    public WriteableBitmap(int width, int height, int bufferCount = DefaultBufferCount)
    {
        ValidateDimensions(width, height);
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferCount, 2);

        var info = new SKImageInfo(
            width,
            height,
            SKImageInfo.PlatformColorType,
            SKAlphaType.Premul);

        var buffers = new BufferSlot[bufferCount];
        var initialized = 0;
        try
        {
            for (; initialized < buffers.Length; initialized++)
                buffers[initialized] = new BufferSlot(this, initialized, info);
        }
        catch
        {
            for (var i = 0; i < initialized; i++)
                buffers[i].DisposeResources();

            throw;
        }

        _buffers = buffers;
        _frontBufferIndex = 0;
        _nextBufferSearchIndex = 1;

        var bitmap = buffers[0].Bitmap!;
        Width = bitmap.Width;
        Height = bitmap.Height;
        Stride = bitmap.RowBytes;
        Length = checked((int)bitmap.GetPixelSpan().Length);
        BytesPerPixel = bitmap.BytesPerPixel;
        ColorType = bitmap.ColorType;
        AlphaType = bitmap.AlphaType;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The number of native buffers in the swap chain.</summary>
    public int BufferCount => _buffers.Length;

    /// <summary>The number of bytes between adjacent rows.</summary>
    public int Stride { get; }

    /// <summary>The length of one complete buffer in bytes, including row padding.</summary>
    public int Length { get; }

    public int BytesPerPixel { get; }

    public SKColorType ColorType { get; }

    /// <summary>
    /// The alpha representation used by the native buffers. RGB channels are premultiplied by alpha.
    /// </summary>
    public SKAlphaType AlphaType { get; }

    public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

    /// <summary>
    /// Raised when presentation must be refreshed: after a completed write, <see cref="Invalidate"/>,
    /// and first disposal.
    /// </summary>
    public event EventHandler? Invalidated;

    /// <summary>
    /// Attempts to acquire an allocation-free back-buffer lease without blocking.
    /// Returns false if every back buffer is being written or retained by Skia.
    /// </summary>
    /// <remarks>
    /// Back-buffer contents may belong to an older frame. Write the complete frame before disposal.
    /// </remarks>
    public bool TryBeginWrite(out WriteLease lease)
    {
        lock (_lifetimeGate)
        {
            ThrowIfDisposedUnderLock();
            if (_activeWriteGeneration != 0)
            {
                lease = default;
                return false;
            }

            for (var offset = 0; offset < _buffers.Length; offset++)
            {
                var index = (_nextBufferSearchIndex + offset) % _buffers.Length;
                var slot = _buffers[index];
                if (index == _frontBufferIndex || slot.ActiveDraws != 0 || slot.DisposeQueued)
                    continue;

                ReleaseCachedImageUnderLock(slot);
                if (slot.InFlightImages != 0)
                    continue;

                slot.HasBeenPresented = false;
                var generation = unchecked(++_nextWriteGeneration);
                if (generation == 0)
                    generation = unchecked(++_nextWriteGeneration);

                _activeWriteBufferIndex = index;
                Volatile.Write(ref _activeWriteGeneration, generation);
                _nextBufferSearchIndex = (index + 1) % _buffers.Length;
                lease = new WriteLease(this, index, generation);
                return true;
            }

            lease = default;
            return false;
        }
    }

    /// <summary>
    /// Returns a borrowed pointer to the raw compatibility buffer and its length in bytes.
    /// Prefer <see cref="TryBeginWrite"/> when a view may render concurrently.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void* ToUnsafePointer(out int length) =>
        (void*)GetRawPixels(out length);

    /// <summary>
    /// Returns a borrowed pointer to the raw compatibility buffer and its length in bytes.
    /// Prefer <see cref="TryBeginWrite"/> when a view may render concurrently.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IntPtr ToUnsafeIntPtr(out int length) => GetRawPixels(out length);

    /// <summary>Returns a borrowed 16-bit pointer and its element count.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe ushort* ToUnsafeUInt16(out int length)
    {
        var pointer = GetRawPixels(out var byteLength);
        length = byteLength / sizeof(ushort);
        return (ushort*)pointer;
    }

    /// <summary>Returns a borrowed 32-bit pointer and its pixel-slot count.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe uint* ToUnsafeUInt32(out int length)
    {
        var pointer = GetRawPixels(out var byteLength);
        length = byteLength / sizeof(uint);
        return (uint*)pointer;
    }

    /// <summary>Returns a borrowed span over the raw compatibility buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Span<byte> ToSpanByte() => new((void*)GetRawPixels(out var length), length);

    /// <summary>Returns a borrowed 16-bit span over the raw compatibility buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<ushort> ToSpanUInt16() =>
        MemoryMarshal.Cast<byte, ushort>(ToSpanByte());

    /// <summary>Returns a borrowed 32-bit span over the raw compatibility buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<uint> ToSpanUInt32() =>
        MemoryMarshal.Cast<byte, uint>(ToSpanByte());

    /// <summary>
    /// Returns the index for a 32-bit pixel span at the specified coordinates.
    /// No bounds checks are performed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex(int x, int y) => y * (Stride / sizeof(uint)) + x;

    /// <summary>
    /// Packs unpremultiplied RGBA components into the platform-native, premultiplied 32-bit layout.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint FromRgba(byte r, byte g, byte b, byte a) =>
        PackRgba(
            r,
            g,
            b,
            a,
            SKImageInfo.PlatformColorRedShift,
            SKImageInfo.PlatformColorGreenShift,
            SKImageInfo.PlatformColorBlueShift,
            SKImageInfo.PlatformColorAlphaShift);

    /// <summary>Packs a Skia color into the native, premultiplied 32-bit layout.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint FromColor(SKColor color) =>
        FromRgba(color.Red, color.Green, color.Blue, color.Alpha);

    /// <summary>Packs a MAUI color into the native, premultiplied 32-bit layout.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint FromColor(Microsoft.Maui.Graphics.Color color)
    {
        ArgumentNullException.ThrowIfNull(color);
        color.ToRgba(out var r, out var g, out var b, out var a);
        return FromRgba(r, g, b, a);
    }

    /// <summary>Zeroes the raw compatibility buffer without requesting a redraw.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => ToSpanByte().Clear();

    /// <summary>
    /// Publishes the externally synchronized raw compatibility buffer and requests redraws.
    /// </summary>
    /// <remarks>
    /// This method throws if that buffer is still retained by Skia. Prefer a write lease for
    /// concurrent rendering; lease disposal publishes its back buffer automatically.
    /// </remarks>
    public void Invalidate()
    {
        lock (_lifetimeGate)
        {
            ThrowIfDisposedUnderLock();
            if (_activeWriteGeneration != 0)
                throw new InvalidOperationException("A write lease is active.");

            var rawSlot = _buffers[0];
            // Retire our reference before testing whether external Skia work retains the pixels.
            if (rawSlot.ActiveDraws == 0)
                ReleaseCachedImageUnderLock(rawSlot);
            if (rawSlot.InFlightImages != 0)
            {
                throw new InvalidOperationException(
                    "The raw buffer is still retained by Skia. Use TryBeginWrite for concurrent rendering.");
            }

            rawSlot.HasBeenPresented = false;
            _frontBufferIndex = 0;
        }

        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Invalidates borrowed access immediately and releases each native buffer after submitted GPU work.
    /// </summary>
    public void Dispose()
    {
        lock (_lifetimeGate)
        {
            if (_isDisposed != 0)
                return;

            Volatile.Write(ref _isDisposed, 1);
            foreach (var slot in _buffers)
            {
                ReleaseCachedImageUnderLock(slot);
                CanQueueDisposalUnderLock(slot);
            }
        }

        foreach (var slot in _buffers)
        {
            if (slot.DisposeQueued)
                slot.DisposeResources();
        }

        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    internal BitmapDrawResult TryDraw(
        SKCanvas canvas,
        SKRect destination,
        SKSamplingOptions samplingOptions,
        SKPaint paint,
        bool clearBeforeDraw)
    {
        SKImage image;
        BufferSlot slot;
        bool cachedBorrower;

        lock (_lifetimeGate)
        {
            if (_isDisposed != 0)
                return BitmapDrawResult.Unavailable;

            slot = _buffers[_frontBufferIndex];
            var pixmap = slot.Pixmap;
            if (pixmap is null)
                return BitmapDrawResult.Unavailable;

            if (!slot.HasBeenPresented)
            {
                // A newly published frame often draws only once. Preserve the transient
                // image path and release it outside the producer lock after drawing.
                var created = SKImage.FromPixels(pixmap, slot.TransientImageRelease);
                if (created is null)
                    return BitmapDrawResult.Unavailable;

                slot.InFlightImages++;
                slot.HasBeenPresented = true;
                image = created;
                cachedBorrower = false;
            }
            else
            {
                if (slot.CachedImage is null)
                {
                    // Repeated presentation benefits from keeping the same immutable image.
                    // Its release state cannot strongly reference the owner or cached image.
                    var created = SKImage.FromPixels(pixmap, slot.ImageRelease);
                    if (created is null)
                        return BitmapDrawResult.Unavailable;

                    slot.InFlightImages++;
                    slot.CachedImage = created;
                }

                image = slot.CachedImage;
                cachedBorrower = true;
                // Only cached images need managed-wrapper borrower accounting. Transient
                // images remain alive until this draw disposes its exclusive wrapper.
                slot.ActiveDraws++;
            }
        }

        try
        {
            if (clearBeforeDraw)
                canvas.Clear(SKColors.Transparent);

            canvas.DrawImage(image, destination, samplingOptions, paint);
            return BitmapDrawResult.Drawn;
        }
        finally
        {
            if (cachedBorrower)
            {
                lock (_lifetimeGate)
                {
                    slot.ActiveDraws--;
                    if (_isDisposed != 0)
                        ReleaseCachedImageUnderLock(slot);
                }
            }
            else
                image.Dispose();
        }
    }

    private static void ReleaseCachedImageUnderLock(BufferSlot slot)
    {
        if (slot.ActiveDraws != 0)
            return;

        var image = slot.CachedImage;
        slot.CachedImage = null;
        // Disposal can synchronously invoke the release callback, which reenters the lock.
        // InFlightImages reaches zero only after all external Skia references are also gone.
        image?.Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint PackRgba(
        byte r,
        byte g,
        byte b,
        byte a,
        int redShift,
        int greenShift,
        int blueShift,
        int alphaShift)
    {
        if (a == 0)
            return 0;

        if (a != byte.MaxValue)
        {
            // Two independent 16-bit lanes. Products plus rounding fit without cross-lane carry.
            // (x + 128 + ((x + 128) >> 8)) >> 8 exactly rounds x / 255 for byte products.
            var rb = ((uint)r | ((uint)b << 16)) * a + 0x00800080u;
            rb = (rb + ((rb >> 8) & 0x00FF00FFu)) >> 8;
            r = (byte)rb;
            b = (byte)(rb >> 16);
            var green = g * a + 128;
            g = (byte)((green + (green >> 8)) >> 8);
        }

        return ((uint)r << redShift)
             | ((uint)g << greenShift)
             | ((uint)b << blueShift)
             | ((uint)a << alphaShift);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SKBitmap GetRawBitmap()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
            throw new ObjectDisposedException(nameof(WriteableBitmap));

        return Volatile.Read(ref _buffers[0].Bitmap)
            ?? throw new ObjectDisposedException(nameof(WriteableBitmap));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IntPtr GetRawPixels(out int length)
    {
        // Allocation metadata never changes. Raw access requires external lifetime synchronization.
        _ = GetRawBitmap();
        var slot = _buffers[0];
        length = slot.ByteLength;
        return slot.Pixels;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe Span<byte> GetWriteSpan(int bufferIndex, long generation)
    {
        // Generations are unique across slots. A release-store on completion invalidates copied
        // leases; disposal invalidates access before native storage can be released.
        if (Volatile.Read(ref _isDisposed) != 0 ||
            Volatile.Read(ref _activeWriteGeneration) != generation)
        {
            throw new ObjectDisposedException(nameof(WriteLease));
        }

        var slot = _buffers[bufferIndex];
        return new Span<byte>((void*)slot.Pixels, slot.ByteLength);
    }

    private void CompleteWrite(int bufferIndex, long generation)
    {
        BufferSlot? disposeNow = null;
        var notify = false;

        lock (_lifetimeGate)
        {
            if (_activeWriteBufferIndex != bufferIndex ||
                _activeWriteGeneration != generation)
            {
                return;
            }

            _activeWriteBufferIndex = -1;
            Volatile.Write(ref _activeWriteGeneration, 0);

            var slot = _buffers[bufferIndex];
            if (_isDisposed != 0)
            {
                if (CanQueueDisposalUnderLock(slot))
                    disposeNow = slot;
            }
            else
            {
                _frontBufferIndex = bufferIndex;
                notify = true;
            }
        }

        disposeNow?.DisposeResources();

        if (notify)
            Invalidated?.Invoke(this, EventArgs.Empty);
    }

    private void OnImageReleased(BufferSlot slot)
    {
        var disposeNow = false;

        lock (_lifetimeGate)
        {
            if (slot.InFlightImages <= 0)
                return;

            slot.InFlightImages--;
            if (_isDisposed != 0 && CanQueueDisposalUnderLock(slot))
                disposeNow = true;
        }

        if (disposeNow)
            slot.DisposeResources();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanQueueDisposalUnderLock(BufferSlot slot)
    {
        if (slot.DisposeQueued ||
            slot.InFlightImages != 0 ||
            slot.Index == _activeWriteBufferIndex)
        {
            return false;
        }

        slot.DisposeQueued = true;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposedUnderLock()
    {
        if (_isDisposed != 0)
            throw new ObjectDisposedException(nameof(WriteableBitmap));
    }

    private static void ValidateDimensions(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var minimumStride = (long)width * RequiredBytesPerPixel;
        if (minimumStride > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "A bitmap row must not exceed Int32.MaxValue bytes.");
        }

        if (height > int.MaxValue / minimumStride)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                "A bitmap allocation must not exceed Int32.MaxValue bytes.");
        }
    }

    /// <summary>
    /// Exclusive access to one back buffer. Dispose the lease to publish the completed frame.
    /// Write the complete frame, do not copy the lease, and do not use derived spans after disposal.
    /// </summary>
    public ref struct WriteLease
    {
        private WriteableBitmap? _owner;
        private readonly int _bufferIndex;
        private readonly long _generation;

        internal WriteLease(WriteableBitmap owner, int bufferIndex, long generation)
        {
            _owner = owner;
            _bufferIndex = bufferIndex;
            _generation = generation;
        }

        /// <summary>The complete back buffer, including row padding.</summary>
        public readonly Span<byte> Bytes =>
            _owner is null
                ? throw new ObjectDisposedException(nameof(WriteLease))
                : _owner.GetWriteSpan(_bufferIndex, _generation);

        /// <summary>Every platform-native 32-bit pixel slot, including row padding.</summary>
        public readonly Span<uint> Pixels => MemoryMarshal.Cast<byte, uint>(Bytes);

        /// <summary>Publishes the completed frame and releases exclusive write access.</summary>
        public void Dispose()
        {
            var owner = _owner;
            if (owner is null)
                return;

            _owner = null;
            owner.CompleteWrite(_bufferIndex, _generation);
        }
    }

    private sealed class BufferSlot
    {
        private readonly WriteableBitmap _owner;

        public BufferSlot(WriteableBitmap owner, int index, SKImageInfo info)
        {
            _owner = owner;
            Index = index;
            TransientImageRelease = HandleTransientImageRelease;

            var bitmap = new SKBitmap();
            try
            {
                if (!bitmap.TryAllocPixels(info))
                    throw new OutOfMemoryException($"Unable to allocate a {info.Width} x {info.Height} bitmap.");

                if (bitmap.BytesPerPixel != RequiredBytesPerPixel)
                {
                    throw new NotSupportedException(
                        $"The platform-native color type {bitmap.ColorType} is not 32-bit.");
                }

                var pixels = bitmap.GetPixels(out var nativeLength);
                var length = checked((int)nativeLength.ToInt64());
                if (pixels == IntPtr.Zero || length <= 0)
                {
                    throw new OutOfMemoryException(
                        $"Unable to allocate a {info.Width} x {info.Height} pixel buffer.");
                }

                bitmap.GetPixelSpan().Clear();
                Pixels = pixels;
                ByteLength = length;
                Bitmap = bitmap;
                Pixmap = new SKPixmap(info, pixels, bitmap.RowBytes);
                ImageRelease = new ImageReleaseState(owner, index, bitmap, Pixmap).HandleRelease;
            }
            catch
            {
                Pixmap?.Dispose();
                bitmap.Dispose();
                throw;
            }
        }

        public int Index { get; }

        public SKImageRasterReleaseDelegate ImageRelease { get; }

        public SKImageRasterReleaseDelegate TransientImageRelease { get; }

        public IntPtr Pixels { get; }

        public int ByteLength { get; }

        public SKBitmap? Bitmap;

        public SKPixmap? Pixmap;

        public int InFlightImages;

        public SKImage? CachedImage;

        public int ActiveDraws;

        public bool HasBeenPresented;

        public bool DisposeQueued;

        public void DisposeResources()
        {
            var pixmap = Interlocked.Exchange(ref Pixmap, null);
            var bitmap = Interlocked.Exchange(ref Bitmap, null);
            pixmap?.Dispose();
            bitmap?.Dispose();
        }

        private void HandleTransientImageRelease(IntPtr _, object __) =>
            _owner.OnImageReleased(this);
    }

    private sealed class ImageReleaseState
    {
        private readonly WeakReference<WriteableBitmap> _owner;
        private readonly int _bufferIndex;
        private readonly SKBitmap _bitmap;
        private readonly SKPixmap _pixmap;

        public ImageReleaseState(WriteableBitmap owner, int bufferIndex, SKBitmap bitmap, SKPixmap pixmap)
        {
            _owner = new WeakReference<WriteableBitmap>(owner);
            _bufferIndex = bufferIndex;
            _bitmap = bitmap;
            _pixmap = pixmap;
        }

        public void HandleRelease(IntPtr _, object __)
        {
            if (_owner.TryGetTarget(out var owner))
                owner.OnImageReleased(owner._buffers[_bufferIndex]);

            // Skia roots this callback until the native image releases its pixels. Keep the
            // allocation alive without rooting the owner, slot, or cached managed image.
            // Several native images may share this state, so an orphaned allocation must
            // remain owned by its Skia wrappers until the last callback handle is released.
            GC.KeepAlive(_bitmap);
            GC.KeepAlive(_pixmap);
        }
    }
}

internal enum BitmapDrawResult : byte
{
    Unavailable,
    Drawn
}

