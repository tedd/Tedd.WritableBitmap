using System;
using System.Collections.Generic;
using System.Linq;
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
using Timer = System.Timers.Timer;

namespace Tedd.WriteableBitmapGuiTest
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

            _randomizeThread = new Thread(RandomizeLoop) {IsBackground=true};
            _randomizeThread.Start();

            _timer = new Timer(100);
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
            var fr = new Random();
            var span = _viewModel.TestBitmap.ToSpanUInt32();
            for (; ; )
            {
                var x = fr.Next(0, 199);
                var y = fr.Next(0, 199);
                var i = y * 200 + x;
                span[i] = (UInt32)fr.Next();
            }
        }

      
    }
}
