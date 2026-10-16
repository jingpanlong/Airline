using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Drawing=System.Drawing;
using Forms=System.Windows.Forms;

namespace Airline {
    internal sealed class CaptureOverlay : Window {
        readonly Drawing.Rectangle bounds;
        readonly BitmapSource desktop;
        readonly Canvas canvas;
        readonly System.Windows.Shapes.Path shade;
        readonly Rectangle outline;
        readonly Border hint;
        readonly TextBlock dimensions;
        Point start;
        bool selecting;
        double sx=1,sy=1;
        public BitmapSource Result;
        public CaptureOverlay() {
            bounds=Forms.SystemInformation.VirtualScreen;
            // Take the screen once, before showing the selection overlay.
            using(var bitmap=new Drawing.Bitmap(bounds.Width,bounds.Height,Drawing.Imaging.PixelFormat.Format32bppArgb)) {
                using(var graphics=Drawing.Graphics.FromImage(bitmap)) graphics.CopyFromScreen(bounds.Left,bounds.Top,0,0,bounds.Size,Drawing.CopyPixelOperation.SourceCopy);
                using(var memory=new MemoryStream()) {
                    bitmap.Save(memory,Drawing.Imaging.ImageFormat.Png); memory.Position=0;
                    var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.StreamSource=memory; image.EndInit(); image.Freeze(); desktop=image;
                }
            }
            Title="晾图 · 框选截图"; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; ShowInTaskbar=false; Topmost=true;
            Background=Brushes.Black; Cursor=Cursors.Cross; Left=bounds.Left; Top=bounds.Top; Width=bounds.Width; Height=bounds.Height;
            canvas=new Canvas { Background=Brushes.Transparent }; Content=canvas;
            var background=new Image { Source=desktop,Stretch=Stretch.Fill }; canvas.Children.Add(background);
            shade=new System.Windows.Shapes.Path { Fill=new SolidColorBrush(Color.FromArgb(112,0,0,0)),IsHitTestVisible=false }; canvas.Children.Add(shade);
            outline=new Rectangle { Stroke=Ui.B("#D8F2D5"),StrokeThickness=1.5,Fill=Brushes.Transparent,IsHitTestVisible=false }; canvas.Children.Add(outline);
            dimensions=Ui.Text("拖动框选区域   ·   Esc 取消",13,Brushes.White);
            hint=new Border { Background=Ui.B("#303A35"),Padding=new Thickness(16,10,16,10),CornerRadius=new CornerRadius(7),Child=dimensions,IsHitTestVisible=false }; canvas.Children.Add(hint);
            SourceInitialized+=(s,e)=> {
                var source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); var t=source.CompositionTarget.TransformFromDevice;
                sx=t.M11; sy=t.M22; var p=t.Transform(new Point(bounds.Left,bounds.Top)); Left=p.X; Top=p.Y;
                Width=bounds.Width*sx; Height=bounds.Height*sy; background.Width=Width; background.Height=Height;
                Shade(new Rect()); Canvas.SetLeft(hint,Math.Max(12,Width/2-140)); Canvas.SetTop(hint,Math.Max(12,Height/2-24));
                Native.ToolWindow(this); Native.ExcludeFromCapture(this); Activate();
            };
            KeyDown+=(s,e)=> { if(e.Key==Key.Escape) { DialogResult=false; Close(); } };
            MouseLeftButtonDown+=(s,e)=> { start=e.GetPosition(canvas); selecting=true; canvas.CaptureMouse(); };
            MouseMove+=(s,e)=> {
                if(!selecting) return; var rect=Selection(start,e.GetPosition(canvas),Width,Height); DrawSelection(rect);
            };
            MouseLeftButtonUp+=(s,e)=> {
                if(!selecting) return; selecting=false; canvas.ReleaseMouseCapture(); var rect=Selection(start,e.GetPosition(canvas),Width,Height);
                int x=(int)Math.Floor(rect.X/sx), y=(int)Math.Floor(rect.Y/sy);
                int w=Math.Min(bounds.Width-x,(int)Math.Ceiling(rect.Width/sx)), h=Math.Min(bounds.Height-y,(int)Math.Ceiling(rect.Height/sy));
                if(w<3 || h<3) { Shade(new Rect()); outline.Width=outline.Height=0; dimensions.Text="区域太小，再框选一次   ·   Esc 取消"; return; }
                Result=new CroppedBitmap(desktop,new Int32Rect(x,y,w,h)); Result.Freeze(); DialogResult=true; Close();
            };
        }
        internal static Rect Selection(Point a,Point b,double width,double height) {
            a.X=Math.Max(0,Math.Min(width,a.X)); a.Y=Math.Max(0,Math.Min(height,a.Y));
            b.X=Math.Max(0,Math.Min(width,b.X)); b.Y=Math.Max(0,Math.Min(height,b.Y));
            return new Rect(a,b);
        }
        void Shade(Rect selected) {
            var group=new GeometryGroup { FillRule=FillRule.EvenOdd }; group.Children.Add(new RectangleGeometry(new Rect(0,0,Width,Height)));
            if(!selected.IsEmpty && selected.Width>0 && selected.Height>0) group.Children.Add(new RectangleGeometry(selected)); shade.Data=group;
        }
        void DrawSelection(Rect r) {
            Shade(r); Canvas.SetLeft(outline,r.X); Canvas.SetTop(outline,r.Y); outline.Width=r.Width; outline.Height=r.Height;
            dimensions.Text=((int)(r.Width/sx))+" × "+((int)(r.Height/sy))+" px   ·   松开挂好";
            Canvas.SetLeft(hint,Math.Max(8,Math.Min(Width-260,r.X))); Canvas.SetTop(hint,Math.Max(8,Math.Min(Height-48,r.Bottom+12)));
        }
    }
}
