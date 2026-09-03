using Microsoft.Maui.Controls;
using Tedd.Maui;

namespace Tedd.Maui.Compatibility;

public partial class ProbePage : ContentPage
{
    public ProbePage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public WriteableBitmap Bitmap { get; } = new(16, 16);
}
