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
    public class ViewModel: INotifyPropertyChanged
    {
        public const int Width= 1920;
        public const int Height= 1080;
        private WriteableBitmap _testBitmap = new Tedd.WriteableBitmap(Width, Height, PixelFormats.Bgra32);

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
    }
}
