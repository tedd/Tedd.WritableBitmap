using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Tedd.RandomUtils;
using Tedd.WriteableBitmapGuiTestCore.Annotations;
using Timer = System.Timers.Timer;

namespace Tedd.WriteableBitmapGuiTestCore
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Timer _timer;
        private ViewModel _viewModel;
        private Thread _randomizeThread;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel = new ViewModel();

            _viewModel.TestBitmap.LoadFile("Test.jpg");

            _randomizeThread = new Thread(RandomizeLoop) { IsBackground = true };
            _randomizeThread.Start();

            _timer = new Timer(1);
            _timer.Start();
            _timer.Elapsed += (sender, args) =>
            {
                if (Dispatcher.CheckAccess())
                    _viewModel.TestBitmap.Invalidate();
                else
                    Dispatcher.Invoke(_viewModel.TestBitmap.Invalidate);
            };
        }

        private void RandomizeLoop()
        {
            var fr = new FastRandom();
            var span = _viewModel.TestBitmap.ToSpanUInt32();
            var width = _viewModel.TestBitmap.Width;
            var height = _viewModel.TestBitmap.Height;
            for (; ; )
            {
                var x = fr.Next(0, width);
                var y = fr.Next(0, height);
                var i = y * width + x;
                span[i] = fr.NextUInt32() | 0xFF000000;// & 0x00FFFFFF;
            }
        }


    }
}
