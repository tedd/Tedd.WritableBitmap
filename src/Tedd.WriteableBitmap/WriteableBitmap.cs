using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Windows.Media.PixelFormat;

namespace Tedd
{
    public class WriteableBitmap : IDisposable
    {
        private readonly BitmapSource BitmapSource;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Length { get; private set; }
        public int BytesPerPixel { get; private set; }
        public int Stride { get; private set; }
        public int Offset { get; private set; }
        public IntPtr IntPtr { get; private set; }
        private bool _mustDispose = false;

        #region Ctor
        public WriteableBitmap(IntPtr intPtr, int width, int height, PixelFormat pixelFormat, int stride, int offset)
        {
            Width = width;
            Height = height;
            Stride = stride;
            Offset = offset;
            BytesPerPixel = ((pixelFormat.BitsPerPixel + 7) / 8);
            Length = width * height * BytesPerPixel;

            IntPtr = intPtr;
            _mustDispose = false;

            BitmapSource = Imaging.CreateBitmapSourceFromMemorySection(IntPtr, width, height, pixelFormat, Stride, Offset);
        }



        public WriteableBitmap(IntPtr intPtr, int width, int height, PixelFormat pixelFormat)
        {
            Width = width;
            Height = height;
            Stride = CalculateStride(pixelFormat, width);
            Offset = 0;
            BytesPerPixel = ((pixelFormat.BitsPerPixel + 7) / 8);
            Length = width * height * BytesPerPixel;

            IntPtr = intPtr;
            _mustDispose = false;

            BitmapSource = Imaging.CreateBitmapSourceFromMemorySection(IntPtr, width, height, pixelFormat, Stride, Offset);
        }

        public WriteableBitmap(int width, int height, PixelFormat pixelFormat)
        {
            Width = width;
            Height = height;
            Stride = CalculateStride(pixelFormat, width);
            Offset = 0;
            BytesPerPixel = ((pixelFormat.BitsPerPixel + 7) / 8);
            Length = width * height * BytesPerPixel;

            IntPtr = Marshal.AllocHGlobal(Length);
            _mustDispose = true;

            BitmapSource = Imaging.CreateBitmapSourceFromMemorySection(IntPtr, width, height, pixelFormat, Stride, Offset);
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<byte> ToSpanByte()
        {
            return new Span<byte>(IntPtr.ToPointer(), Length);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<UInt16> ToSpanUInt16()
        {
            return new Span<UInt16>(IntPtr.ToPointer(), Length / sizeof(UInt16));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<UInt32> ToSpanUInt32()
        {
            return new Span<UInt32>(IntPtr.ToPointer(), Length / sizeof(UInt32));
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
                    return ((UInt32*)IntPtr)[p];
                if (BytesPerPixel == 2)
                    return ((UInt16*)IntPtr)[p];
                if (BytesPerPixel == 1)
                    return ((Byte*)IntPtr)[p];

                if (BytesPerPixel == 3)
                {
                    p *= 3;
                    return (UInt32)(
                          ((UInt32)(((Byte*)IntPtr)[p + 0]) << 16)
                        | ((UInt32)(((Byte*)IntPtr)[p + 1]) << 8)
                        | ((UInt32)(((Byte*)IntPtr)[p + 2]))
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
                    ((UInt32*)IntPtr)[p] = value;
                else if (BytesPerPixel == 2)
                    ((UInt16*)IntPtr)[p] = (UInt16)value;
                else if (BytesPerPixel == 1)
                    ((Byte*)IntPtr)[p] = (Byte)value;

                else if (BytesPerPixel == 3)
                {
                    p *= 3;
                    ((Byte*)IntPtr)[p + 0] = (Byte)(value >> 16);
                    ((Byte*)IntPtr)[p + 1] = (Byte)(value >> 8);
                    ((Byte*)IntPtr)[p + 2] = (Byte)value;
                }

                throw new Exception("Unsupported pixel byte size, can't use indexer. Use direct pointer or span to modify.");
            }
        }

        /// <summary>
        /// Invalidates the bitmap causing a redraw
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invalidate()
        {
            // TODO: Not implemented in .Net Core BitmapSource yet
            ////Flip the _needsUpdate flag to true.
            ////If we don't do this, the cached bitmap would be used and the image won't update
            //var field = typeof(BitmapSource).GetField("_needsUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            //field?.SetValue(this, true);
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
                        byte* dst = (byte*)ret.IntPtr.ToPointer();

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
            if (_mustDispose)
            {
                Marshal.FreeHGlobal(IntPtr);
                _mustDispose = false;
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
