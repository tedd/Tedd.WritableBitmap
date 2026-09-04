using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using PixelFormat = System.Windows.Media.PixelFormat;

namespace Tedd
{
    public class WriteableBitmap : IDisposable
    {
        public InteropBitmap BitmapSource { get; private set; }
        public readonly int Width;
        public readonly int Height;
        /// <summary>The number of bytes in the pixel buffer, including row padding.</summary>
        public readonly int Length;
        public readonly int BytesPerPixel;
        /// <summary>The number of bytes between adjacent pixel rows.</summary>
        public readonly int Stride;
        /// <summary>The offset of the pixel buffer within the memory section. Spans and pointers begin here.</summary>
        public readonly int Offset;
        public readonly PixelFormat PixelFormat;

        private readonly IntPtr _mapView;
        private bool _mustDisposeMemoryMapSection = false;
        private bool _mustDisposeMapView = false;
        private readonly IntPtr _memoryMapSection;

        #region Ctor
        /// <summary>Maps a borrowed memory section with an explicit pixel layout. The caller retains ownership of the section handle.</summary>
        public WriteableBitmap(IntPtr intPtr, int width, int height, PixelFormat pixelFormat, int stride, int offset)
            : this(intPtr, width, height, pixelFormat, stride, offset, false)
        {
        }
        /// <summary>Maps a borrowed memory section using four-byte-aligned rows. The caller retains ownership of the section handle.</summary>
        public WriteableBitmap(IntPtr intPtr, int width, int height, PixelFormat pixelFormat)
            : this(intPtr, width, height, pixelFormat, CalculateStride(pixelFormat, width), 0, false)
        {
        }

        public WriteableBitmap(int width, int height, PixelFormat pixelFormat)
            : this(IntPtr.Zero, width, height, pixelFormat, CalculateStride(pixelFormat, width), 0, true)
        {
        }

        private WriteableBitmap(IntPtr intPtr, int width, int height, PixelFormat pixelFormat, int stride, int offset, bool ownsSection)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));
            if (pixelFormat == default(PixelFormat))
                throw new ArgumentException("A concrete pixel format is required.", nameof(pixelFormat));
            if (stride < ((long)width * pixelFormat.BitsPerPixel + 7) / 8)
                throw new ArgumentOutOfRangeException(nameof(stride));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (!ownsSection && (intPtr == IntPtr.Zero || intPtr == new IntPtr(-1)))
                throw new ArgumentException("A valid memory section handle is required.", nameof(intPtr));

            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
            Stride = stride;
            Offset = offset;
            BytesPerPixel = ((pixelFormat.BitsPerPixel + 7) / 8);
            Length = checked(stride * height);
            var mappedLength = checked(offset + Length);

            _memoryMapSection = intPtr;
            try
            {
                if (ownsSection)
                {
                    _memoryMapSection = Win32Interop.CreateFileMapping(new IntPtr(-1), IntPtr.Zero, 0x04, 0, (UInt32)mappedLength, null);
                    if (_memoryMapSection == IntPtr.Zero)
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    _mustDisposeMemoryMapSection = true;
                }

                _mapView = Win32Interop.MapViewOfFile(_memoryMapSection, 0xF001F, 0, 0, (UInt32)mappedLength);
                if (_mapView == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                _mustDisposeMapView = true;

                BitmapSource = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(_memoryMapSection, width, height, pixelFormat, Stride, Offset);
            }
            catch
            {
                ReleaseUnmanagedResources();
                throw;
            }
        }


        #endregion


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void* ToUnsafePointer(out int length)
        {
            length = Length;
            return (byte*)_mapView + Offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe IntPtr ToUnsafeIntPtr(out int length)
        {
            length = Length;
            return IntPtr.Add(_mapView, Offset);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe UInt16* ToUnsafeUInt16(out int length)
        {
            length = Length / sizeof(UInt16);
            return (UInt16*)((byte*)_mapView + Offset);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe UInt32* ToUnsafeUInt32(out int length)
        {
            length = Length / sizeof(UInt32);
            return (UInt32*)((byte*)_mapView + Offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<byte> ToSpanByte() => new Span<byte>((byte*)_mapView + Offset, Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<UInt16> ToSpanUInt16() => new Span<UInt16>((byte*)_mapView + Offset, Length / sizeof(UInt16));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<UInt32> ToSpanUInt32() => new Span<UInt32>((byte*)_mapView + Offset, Length / sizeof(UInt32));


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateStride(PixelFormat pixelFormat, int width)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (pixelFormat == default(PixelFormat))
                throw new ArgumentException("A concrete pixel format is required.", nameof(pixelFormat));
            return checked((int)(((long)width * pixelFormat.BitsPerPixel + 31) / 32 * 4));
        }

        /// <summary>Returns the logical row-major pixel index without bounds checks.</summary>
        /// <remarks>This index does not include row padding. For byte-aligned pixel formats, address a padded byte buffer using y * Stride + x * BytesPerPixel.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetIndex(int x, int y) => y * Width + x;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt32 FromRgba(byte r, byte g, byte b, byte a) =>
            ((UInt32) a << 24) | ((UInt32) r << 16) | ((UInt32) g << 8) | (UInt32) b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt32 FromColor(Color color) =>
            FromRgba(color.R, color.G, color.B, color.A);

        //public unsafe UInt32 this[int x, int y]
        //{
        //    get
        //    {
        //        if (x >= Width)
        //            throw new ArgumentOutOfRangeException(nameof(x));
        //        if (y >= Height)
        //            throw new ArgumentOutOfRangeException(nameof(y));

        //        var p = y * Width + x;
        //        if (BytesPerPixel != 4)
        //            return ((UInt32*)_mapView)[p];
        //        if (BytesPerPixel == 2)
        //            return ((UInt16*)_mapView)[p];
        //        if (BytesPerPixel == 1)
        //            return ((Byte*)_mapView)[p];

        //        if (BytesPerPixel == 3)
        //        {
        //            p *= 3;
        //            return (UInt32)(
        //                  ((UInt32)(((Byte*)_mapView)[p + 0]) << 16)
        //                | ((UInt32)(((Byte*)_mapView)[p + 1]) << 8)
        //                | ((UInt32)(((Byte*)_mapView)[p + 2]))
        //                );
        //        }

        //        throw new Exception("Unsupported pixel byte size, can't use indexer. Use direct pointer or span to modify.");
        //    }
        //    set
        //    {
        //        if (x >= Width)
        //            throw new ArgumentOutOfRangeException(nameof(x));
        //        if (y >= Height)
        //            throw new ArgumentOutOfRangeException(nameof(y));

        //        var p = y * Width + x;
        //        if (BytesPerPixel == 4)
        //            ((UInt32*)_mapView)[p] = value;
        //        else if (BytesPerPixel == 2)
        //            ((UInt16*)_mapView)[p] = (UInt16)value;
        //        else if (BytesPerPixel == 1)
        //            ((Byte*)_mapView)[p] = (Byte)value;

        //        else if (BytesPerPixel == 3)
        //        {
        //            p *= 3;
        //            ((Byte*)_mapView)[p + 0] = (Byte)(value >> 16);
        //            ((Byte*)_mapView)[p + 1] = (Byte)(value >> 8);
        //            ((Byte*)_mapView)[p + 2] = (Byte)value;
        //        }
        //        else
        //            throw new Exception("Unsupported pixel byte size, can't use indexer. Use direct pointer or span to modify.");
        //    }
        //}
        public void Clear() => ToSpanByte().Clear();



        public void LoadFile(string filename)
        {
            var decoder = GetDecoder(filename);
            var frame = decoder.Frames[0];
            //frame.Thumbnail.
            var fcb = new FormatConvertedBitmap();
            fcb.BeginInit();
            fcb.Source = frame;
            fcb.DestinationFormat = PixelFormat;
            fcb.EndInit();
            var rect = new Int32Rect(0, 0, Math.Min(frame.PixelWidth, Width), Math.Min(frame.PixelHeight, Height));
            fcb.CopyPixels(rect, ToUnsafeIntPtr(out var length), length, Stride);
        }

        private BitmapDecoder GetDecoder(string filename) => Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".jpg" => new JpegBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
            ".jpeg" => new JpegBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
            ".png" => new PngBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
            ".bmp" => new BmpBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
            ".gif" => new GifBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
            ".wmp" => new WmpBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
            ".ico" => new IconBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
            ".tif" => new TiffBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
            _ => throw new Exception("Unknown format")
        };

#if HAS_INVALIDATE
#endif
        /// <summary>
        /// Invalidates the bitmap causing a redraw
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invalidate()
        {
            BitmapSource.Invalidate();
            //// TODO: Not implemented in .Net Core BitmapSource yet
            ////Flip the _needsUpdate flag to true.
            ////If we don't do this, the cached bitmap would be used and the image won't update
            //var field = typeof(BitmapSource).GetField("_needsUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            //field?.SetValue(BitmapSource, true);
        }

        //#region CreateFrom
        //public static WriteableBitmap CreateFromFile(string file)
        //{
        //    // TODO: Fix
        //    using (Image image = Image.FromFile(file))
        //    {
        //        using (Bitmap bmp = new Bitmap(image))
        //        {

        //            System.Drawing.Imaging.PixelFormat format = bmp.PixelFormat;
        //            var ret = new WriteableBitmap(image.Width, image.Height, PixelFormats.Bgr32);

        //            var data = bmp.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly,
        //                format);
        //            int stride = data.Stride;
        //            int offset = stride - image.Width * ret.BytesPerPixel;
        //            unsafe
        //            {
        //                byte* src = (byte*)data.Scan0.ToPointer();
        //                byte* dst = (byte*)ret._mapView.ToPointer();

        //                int mp = image.Height * image.Width * ret.BytesPerPixel;
        //                for (int p = 0; p < mp; p++)
        //                {
        //                    dst[p] = src[p];
        //                }
        //            }
        //            return ret;

        //        }
        //    }
        //}

        //#endregion

        #region IDisposable
        private void ReleaseUnmanagedResources()
        {
            if (_mustDisposeMapView)
            {
                //Marshal.FreeHGlobal(MapView);

                Win32Interop.UnmapViewOfFile(_mapView);
                _mustDisposeMapView = false;
            }

            if (_mustDisposeMemoryMapSection)
            {
                Win32Interop.CloseHandle(_memoryMapSection);
                _mustDisposeMemoryMapSection = false;
            }

        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        /// <summary>Allows an object to try to free resources and perform other cleanup operations before it is reclaimed by garbage collection.</summary>
        ~WriteableBitmap()
        {
            ReleaseUnmanagedResources();
        }
        #endregion

    }

}

