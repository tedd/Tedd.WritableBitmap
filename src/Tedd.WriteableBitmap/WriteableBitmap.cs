using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Windows.Media.PixelFormat;

namespace Tedd
{
    public class WriteableBitmap : IDisposable
    {
        public InteropBitmap BitmapSource { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Length { get; private set; }
        public int BytesPerPixel { get; private set; }
        public int Stride { get; private set; }
        public int Offset { get; private set; }
        public IntPtr MapView { get; private set; }
        //private bool _mustDispose = false;
        private bool _mustDisposeMemoryMapSection = false;
        private bool _mustDisposeMapView = false;
        public IntPtr MemoryMapSection { get; private set; }

        #region Ctor
        public WriteableBitmap(IntPtr intPtr, int width, int height, PixelFormat pixelFormat, int stride, int offset)
        {
            Width = width;
            Height = height;
            Stride = stride;
            Offset = offset;
            BytesPerPixel = ((pixelFormat.BitsPerPixel + 7) / 8);
            Length = width * height * BytesPerPixel;

            MemoryMapSection = intPtr;

            MapView = Win32Interop.MapViewOfFile(MemoryMapSection, 0xF001F, 0, 0, (UInt32)Length);
            _mustDisposeMapView = true;


            BitmapSource = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(MapView, width, height, pixelFormat, Stride, Offset);
        }



        public WriteableBitmap(IntPtr intPtr, int width, int height, PixelFormat pixelFormat)
        {
            Width = width;
            Height = height;
            Stride = CalculateStride(pixelFormat, width);
            Offset = 0;
            BytesPerPixel = ((pixelFormat.BitsPerPixel + 7) / 8);
            Length = width * height * BytesPerPixel;

            MemoryMapSection = intPtr;

            MapView = Win32Interop.MapViewOfFile(MemoryMapSection, 0xF001F, 0, 0, (UInt32)Length);
            _mustDisposeMapView = true;

            BitmapSource = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(MapView, width, height, pixelFormat, Stride, Offset);
        }

        public WriteableBitmap(int width, int height, PixelFormat pixelFormat)
        {
            Width = width;
            Height = height;
            Stride = CalculateStride(pixelFormat, width);
            Offset = 0;
            BytesPerPixel = ((pixelFormat.BitsPerPixel + 7) / 8);
            Length = width * height * BytesPerPixel;

            //IntPtr = Marshal.AllocHGlobal(Length);
            //this.MemoryMappedFile = MemoryMappedFile.CreateNew(null, Length, MemoryMappedFileAccess.ReadWrite);
            //IntPtr = this.MemoryMappedFile.CreateViewAccessor().SafeMemoryMappedViewHandle.DangerousGetHandle();

            MemoryMapSection = Win32Interop.CreateFileMapping(new IntPtr(-1), IntPtr.Zero, 0x04, 0, (UInt32)Length, null);
            _mustDisposeMemoryMapSection = true;
            MapView = Win32Interop.MapViewOfFile(MemoryMapSection, 0xF001F, 0, 0, (UInt32)Length);
            _mustDisposeMapView = true;

            BitmapSource = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(MemoryMapSection, width, height, pixelFormat, Stride, Offset);
        }


        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<byte> ToSpanByte()
        {
            return new Span<byte>(MapView.ToPointer(), Length);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<UInt16> ToSpanUInt16()
        {
            return new Span<UInt16>(MapView.ToPointer(), Length / sizeof(UInt16));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<UInt32> ToSpanUInt32()
        {
            return new Span<UInt32>(MapView.ToPointer(), Length / sizeof(UInt32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateStride(PixelFormat pixelFormat, int width)
        {
            int bytesPerPixel = (pixelFormat.BitsPerPixel + 7) / 8;
            return 4 * ((width * bytesPerPixel + 3) / 4);
        }

        public unsafe UInt32 this[int x, int y]
        {
            get
            {
                if (x >= Width)
                    throw new ArgumentOutOfRangeException(nameof(x));
                if (y >= Height)
                    throw new ArgumentOutOfRangeException(nameof(y));

                var p = y * Width + x;
                if (BytesPerPixel != 4)
                    return ((UInt32*)MapView)[p];
                if (BytesPerPixel == 2)
                    return ((UInt16*)MapView)[p];
                if (BytesPerPixel == 1)
                    return ((Byte*)MapView)[p];

                if (BytesPerPixel == 3)
                {
                    p *= 3;
                    return (UInt32)(
                          ((UInt32)(((Byte*)MapView)[p + 0]) << 16)
                        | ((UInt32)(((Byte*)MapView)[p + 1]) << 8)
                        | ((UInt32)(((Byte*)MapView)[p + 2]))
                        );
                }

                throw new Exception("Unsupported pixel byte size, can't use indexer. Use direct pointer or span to modify.");
            }
            set
            {
                if (x >= Width)
                    throw new ArgumentOutOfRangeException(nameof(x));
                if (y >= Height)
                    throw new ArgumentOutOfRangeException(nameof(y));

                var p = y * Width + x;
                if (BytesPerPixel == 4)
                    ((UInt32*)MapView)[p] = value;
                else if (BytesPerPixel == 2)
                    ((UInt16*)MapView)[p] = (UInt16)value;
                else if (BytesPerPixel == 1)
                    ((Byte*)MapView)[p] = (Byte)value;

                else if (BytesPerPixel == 3)
                {
                    p *= 3;
                    ((Byte*)MapView)[p + 0] = (Byte)(value >> 16);
                    ((Byte*)MapView)[p + 1] = (Byte)(value >> 8);
                    ((Byte*)MapView)[p + 2] = (Byte)value;
                }

                throw new Exception("Unsupported pixel byte size, can't use indexer. Use direct pointer or span to modify.");
            }
        }

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

        #region CreateFrom
        public static WriteableBitmap CreateFromFile(string file)
        {
            // TODO: Fix
            using (Image image = Image.FromFile(file))
            {
                using (Bitmap bmp = new Bitmap(image))
                {

                    System.Drawing.Imaging.PixelFormat format = bmp.PixelFormat;
                    var ret = new WriteableBitmap(image.Width, image.Height, PixelFormats.Bgr32);

                    var data = bmp.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly,
                        format);
                    int stride = data.Stride;
                    int offset = stride - image.Width * ret.BytesPerPixel;
                    unsafe
                    {
                        byte* src = (byte*)data.Scan0.ToPointer();
                        byte* dst = (byte*)ret.MapView.ToPointer();

                        int mp = image.Height * image.Width * ret.BytesPerPixel;
                        for (int p = 0; p < mp; p++)
                        {
                            dst[p] = src[p];
                        }
                    }
                    return ret;

                }
            }
        }

#endregion

#region IDisposable
        private void ReleaseUnmanagedResources()
        {
            if (_mustDisposeMapView)
            {
                //Marshal.FreeHGlobal(MapView);

                Win32Interop.UnmapViewOfFile(MapView);
                _mustDisposeMapView = false;
            }

            if (_mustDisposeMemoryMapSection)
            {
                Win32Interop.CloseHandle(MemoryMapSection);
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
