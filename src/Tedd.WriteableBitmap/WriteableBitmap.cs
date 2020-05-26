using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
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
        public readonly int Length;
        public readonly int BytesPerPixel;
        public readonly int Stride;
        public readonly int Offset;
        public readonly PixelFormat PixelFormat;

        private readonly IntPtr _mapView;
        private bool _mustDisposeMemoryMapSection = false;
        private bool _mustDisposeMapView = false;
        private readonly IntPtr _memoryMapSection;

        #region Ctor
        public WriteableBitmap(IntPtr intPtr, int width, int height, PixelFormat pixelFormat, int stride, int offset)
        {
            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
            Stride = stride;
            Offset = offset;
            BytesPerPixel = ((pixelFormat.BitsPerPixel + 7) / 8);
            Length = width * height * BytesPerPixel;

            _memoryMapSection = intPtr;

            _mapView = Win32Interop.MapViewOfFile(_memoryMapSection, 0xF001F, 0, 0, (UInt32)Length);
            _mustDisposeMapView = true;


            BitmapSource = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(_mapView, width, height, pixelFormat, Stride, Offset);
        }



        public WriteableBitmap(IntPtr intPtr, int width, int height, PixelFormat pixelFormat)
        {
            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
            Stride = CalculateStride(pixelFormat, width);
            Offset = 0;
            BytesPerPixel = ((pixelFormat.BitsPerPixel + 7) / 8);
            Length = width * height * BytesPerPixel;

            _memoryMapSection = intPtr;

            _mapView = Win32Interop.MapViewOfFile(_memoryMapSection, 0xF001F, 0, 0, (UInt32)Length);
            _mustDisposeMapView = true;

            BitmapSource = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(_mapView, width, height, pixelFormat, Stride, Offset);
        }

        public WriteableBitmap(int width, int height, PixelFormat pixelFormat)
        {
            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
            Stride = CalculateStride(pixelFormat, width);
            Offset = 0;
            BytesPerPixel = ((pixelFormat.BitsPerPixel + 7) / 8);
            Length = width * height * BytesPerPixel;

            //IntPtr = Marshal.AllocHGlobal(Length);
            //this.MemoryMappedFile = MemoryMappedFile.CreateNew(null, Length, MemoryMappedFileAccess.ReadWrite);
            //IntPtr = this.MemoryMappedFile.CreateViewAccessor().SafeMemoryMappedViewHandle.DangerousGetHandle();

            _memoryMapSection = Win32Interop.CreateFileMapping(new IntPtr(-1), IntPtr.Zero, 0x04, 0, (UInt32)Length, null);
            _mustDisposeMemoryMapSection = true;
            _mapView = Win32Interop.MapViewOfFile(_memoryMapSection, 0xF001F, 0, 0, (UInt32)Length);
            _mustDisposeMapView = true;

            BitmapSource = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(_memoryMapSection, width, height, pixelFormat, Stride, Offset);
        }


        #endregion


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void* ToUnsafePointer(out int length)
        {
            length = Length;
            return (void*)_mapView;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe IntPtr ToUnsafeIntPtr(out int length)
        {
            length = Length;
            return (IntPtr)_mapView;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe UInt16* ToUnsafeUInt16(out int length)
        {
            length = Length / sizeof(UInt16);
            return (UInt16*)_mapView;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe UInt32* ToUnsafeUInt32(out int length)
        {
            length = Length / sizeof(UInt32);
            return (UInt32*)_mapView;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<byte> ToSpanByte() => new Span<byte>((byte*)_mapView, Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<UInt16> ToSpanUInt16() => new Span<UInt16>((UInt16*)_mapView, Length / sizeof(UInt16));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<UInt32> ToSpanUInt32() => new Span<UInt32>((UInt32*)_mapView, Length / sizeof(UInt32));


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateStride(PixelFormat pixelFormat, int width)
        {
            int bytesPerPixel = (pixelFormat.BitsPerPixel + 7) / 8;
            return 4 * ((width * bytesPerPixel + 3) / 4);
        }

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
        public void Clear() => ToSpanUInt32().Clear();



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
            var rect = new Int32Rect(0, 0, Math.Min((int)frame.Width, Width), Math.Min((int)frame.Height, Height));
            fcb.CopyPixels(rect, ToUnsafeIntPtr(out var length), length, Stride);
        }

        private BitmapDecoder GetDecoder(string filename) => Path.GetExtension(filename).ToLower() switch
        {
            ".jpg" => new JpegBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default),
            ".jpeg" => new JpegBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default),
            ".png" => new PngBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default),
            ".bmp" => new PngBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default),
            ".gif" => new GifBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default),
            ".wmp" => new WmpBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default),
            ".ico" => new IconBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default),
            ".tif" => new TiffBitmapDecoder(new Uri(filename, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default),
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
