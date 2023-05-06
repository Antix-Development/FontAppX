using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia;
using SkiaSharp;
using System;

namespace FontAppX.Controls;

// This code from https://stackoverflow.com/questions/61627374/is-it-possible-to-create-a-skia-canvas-element-in-an-avalonia-application/74324110#74324110

public partial class SkiaCanvas : UserControl
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

    public SkiaCanvas()
    {
        InitializeComponent();

        renderingLogic = new RenderingLogic();
        renderingLogic.RenderCall += (canvas) => RenderSkia?.Invoke(canvas);
    }

    public override void Render(DrawingContext context)
    {
        renderingLogic.Bounds = new Rect(0, 0, this.Bounds.Width, this.Bounds.Height);
        context.Custom(renderingLogic);
    }
}