using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia;
using SkiaSharp;
using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using System.Diagnostics;

namespace FontAppX;

// This code from https://stackoverflow.com/questions/61627374/is-it-possible-to-create-a-skia-canvas-element-in-an-avalonia-application/74324110#74324110

public partial class DrawableCanvas : UserControl
{
    class RenderingLogic : ICustomDrawOperation
    {
        public Action<SKCanvas> RenderCall;

        public Rect Bounds { get; set; }

        public void Dispose() { }

        public bool Equals(ICustomDrawOperation? other) => other == this;

        public bool HitTest(Point p) { return false; }

        public void Render(IDrawingContextImpl context)
        {
            var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
            if (canvas != null) Render(canvas);
        }

        private void Render(SKCanvas canvas) => RenderCall?.Invoke(canvas);
    }

    RenderingLogic renderingLogic;

    public event Action<SKCanvas> RenderSkia;

    public DrawableCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        Bounds = new Rect(0, 0, width, height);

        renderingLogic = new RenderingLogic();
        renderingLogic.RenderCall += (canvas) => RenderSkia?.Invoke(canvas);

        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

        Initialized += DrawableCanvas_Initialized;
    }

    private void DrawableCanvas_Initialized(object? sender, EventArgs e)
    {
        Debug.WriteLine($"Initiated: {Width},{Height}");
    }

    public override void Render(DrawingContext context)
    {
        renderingLogic.Bounds = new Rect(0, 0, this.Bounds.Width, this.Bounds.Height);
        context.Custom(renderingLogic);
    }
}