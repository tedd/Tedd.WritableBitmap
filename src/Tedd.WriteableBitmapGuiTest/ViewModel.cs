using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Tedd.WriteableBitmapGuiTest.Annotations;

namespace Tedd.WriteableBitmapGuiTest
{
    public class ViewModel : INotifyPropertyChanged
    {
        public const int Width = 1920;
        public const int Height = 1080;
        private WriteableBitmap _testBitmap = new Tedd.WriteableBitmap(Width, Height, PixelFormats.Bgra32);
        public WriteableBitmap Bitmap {get;set;}= new Tedd.WriteableBitmap(100, 100, PixelFormats.Bgra32);

        public Tedd.WriteableBitmap TestBitmap
        {
            get => _testBitmap;
            set
            {
                if (Equals(value, _testBitmap)) return;
                _testBitmap = value;
                OnPropertyChanged();
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public unsafe ViewModel()
        {

            var red = WriteableBitmap.FromRgba(255, 0, 0, 255);
            var yellow = WriteableBitmap.FromColor(Colors.Yellow);

            // Use spain pointer to fill image with red
            var spanPtr = Bitmap.ToSpanUInt32();
            for (var i = 0; i < spanPtr.Length; i++)
            {
                spanPtr[i] = red;
            }
            // Might as well use Span's Fill-function though. Much faster.
            spanPtr.Fill(red);

            // This requires us to compile with unsafe, and set method unsafe or surround with unsafe block.
            var ptr = Bitmap.ToUnsafeUInt32(out var len);
            // ptr is a raw pointer to memory area. Nobody is holding your hand here telling you to stop.
            // Our memory is ptr[0] to ptr[len-1]. If we go outside of this it is very bad!

            // Lets paint a square.
            for (var x = 25; x < 75; x++)
            {
                for (var y = 25; y < 75; y++)
                {
                    // Finding the index of a position is calculated as:
                    var i = y * Bitmap.Width + x;
                    // The helper function GetIndex() will do this for you
                    //var i = bitmap.GetIndex(x, y);
                    ptr[i] = yellow;
                }
            }

        }
    }
}
