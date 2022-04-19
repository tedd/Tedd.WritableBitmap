using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Media;
using Tedd.WriteableBitmapGuiTestCore.Annotations;

namespace Tedd.WriteableBitmapGuiTestCore
{
    public class ViewModel
    {
        private WriteableBitmap _testBitmap = new Tedd.WriteableBitmap(400, 400, PixelFormats.Bgra32);

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
