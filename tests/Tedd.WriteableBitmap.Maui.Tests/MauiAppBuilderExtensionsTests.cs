using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Views.Maui.Handlers;
using Xunit;

namespace Tedd.Maui.Tests;

public sealed class MauiAppBuilderExtensionsTests
{
    [Fact]
    public void NullBuilderIsRejectedWithParameterName()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => MauiAppBuilderExtensions.UseTeddWriteableBitmap(null!));

        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void RegistrationReturnsTheOriginalBuilderForFluentConfiguration()
    {
        var builder = MauiApp.CreateBuilder();

        var result = builder.UseTeddWriteableBitmap();

        Assert.Same(builder, result);
    }

    [Fact]
    public void RegistrationProvidesBothSkiaSurfaceHandlers()
    {
        var builder = MauiApp.CreateBuilder(useDefaults: false).UseTeddWriteableBitmap();
        using var app = builder.Build();
        var handlers = app.Services.GetRequiredService<IMauiHandlersFactory>();

        Assert.Equal(typeof(SKGLViewHandler), handlers.GetHandlerType(typeof(SKGLView)));
        Assert.Equal(typeof(SKCanvasViewHandler), handlers.GetHandlerType(typeof(SKCanvasView)));
    }

    [Fact]
    public void RepeatedRegistrationPreservesHandlerResolution()
    {
        var builder = MauiApp.CreateBuilder(useDefaults: false);

        Assert.Same(builder, builder.UseTeddWriteableBitmap().UseTeddWriteableBitmap());
        using var app = builder.Build();
        var handlers = app.Services.GetRequiredService<IMauiHandlersFactory>();

        Assert.Equal(typeof(SKGLViewHandler), handlers.GetHandlerType(typeof(SKGLView)));
        Assert.Equal(typeof(SKCanvasViewHandler), handlers.GetHandlerType(typeof(SKCanvasView)));
    }
}
