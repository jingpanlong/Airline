using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Airline {
    internal sealed class ImagePreview : Window {
        public ImagePreview(Shot shot) {
            Title="晾图 · 预览"; Width=1000; Height=730; MinWidth=420; MinHeight=300; Background=Ui.Paper; WindowStartupLocation=WindowStartupLocation.CenterScreen;
            var root=new DockPanel(); Content=root;
            var toolbar=new DockPanel { Margin=new Thickness(18,12,18,12) }; DockPanel.SetDock(toolbar,Dock.Top); root.Children.Add(toolbar);
            var open=Ui.Button("在系统图片查看器打开 ↗",()=>Process.Start(new ProcessStartInfo(shot.Path) { UseShellExecute=true })); DockPanel.SetDock(open,Dock.Right); toolbar.Children.Add(open);
            toolbar.Children.Add(Ui.Text("预览   /   "+shot.Created.ToString("MM-dd  HH:mm"),13,Ui.Ink));
            root.Children.Add(new Image { Source=Library.Load(shot.Path,0),Stretch=Stretch.Uniform,Margin=new Thickness(24) });
            KeyDown+=(s,e)=> { if(e.Key==Key.Escape) Close(); };
        }
    }
    internal sealed class Editor : Window {
        readonly Library library;
        readonly BitmapSource original;
        readonly Canvas canvas;
        readonly List<UIElement[]> edits=new List<UIElement[]>();
        readonly TextBlock state;
        string tool="pen";
        Brush color=Ui.B("#DF6653");
        Point start;
        bool drawing;
        Polyline stroke;
        Shape shape;
        Line arrow1,arrow2;
        readonly List<Button> toolButtons=new List<Button>();
        public Editor(Shot shot,Library data) {
            library=data; original=Library.Load(shot.Path,0);
            Title="晾图 · 标注"; Width=1080; Height=780; MinWidth=620; MinHeight=420; Background=Ui.Paper; WindowStartupLocation=WindowStartupLocation.CenterScreen;
            var root=new DockPanel(); Content=root;
            var toolbar=new Grid { Margin=new Thickness(20,12,20,12) }; toolbar.ColumnDefinitions.Add(new ColumnDefinition()); toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            DockPanel.SetDock(toolbar,Dock.Top); root.Children.Add(toolbar);
            var tools=new StackPanel { Orientation=Orientation.Horizontal }; toolbar.Children.Add(tools);
            Tool(tools,"画笔","pen"); Tool(tools,"方框","rectangle"); Tool(tools,"箭头","arrow");
            tools.Children.Add(Ui.Button("撤销",()=>Undo()));
            foreach(string c in new[]{"#DF6653","#557260","#DCA747","#303A35"}) {
                string selected=c; var b=Ui.Button("●",()=>color=Ui.B(selected)); b.Foreground=Ui.B(c); b.FontSize=21; b.Padding=new Thickness(8,1,8,1); tools.Children.Add(b);
            }
            var save=Ui.Button("✓  标注完成，另挂一份",()=>Save()); save.Background=Ui.B("#E5E9DF"); Grid.SetColumn(save,1); toolbar.Children.Add(save);
            state=Ui.Text("原图会保留。支持画笔、方框、箭头；Ctrl+Z 撤销。",12,Ui.Muted); state.Margin=new Thickness(24,10,24,16); DockPanel.SetDock(state,Dock.Bottom); root.Children.Add(state);
            var scroll=new ScrollViewer { HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=Ui.B("#E5E7DF"),Padding=new Thickness(24) }; root.Children.Add(scroll);
            double displayWidth=Math.Min(original.PixelWidth,960), ratio=displayWidth/original.PixelWidth;
            canvas=new Canvas { Width=displayWidth,Height=original.PixelHeight*ratio,Background=Brushes.White,ClipToBounds=true,Cursor=Cursors.Pen };
            canvas.Children.Add(new Image { Source=original,Width=canvas.Width,Height=canvas.Height,Stretch=Stretch.Fill,IsHitTestVisible=false }); scroll.Content=canvas;
            canvas.MouseLeftButtonDown+=(s,e)=>Begin(e.GetPosition(canvas));
            canvas.MouseMove+=(s,e)=> { if(drawing && e.LeftButton==MouseButtonState.Pressed) Update(e.GetPosition(canvas)); };
            canvas.MouseLeftButtonUp+=(s,e)=>Finish();
            canvas.LostMouseCapture+=(s,e)=> { if(drawing) Finish(); };
            PreviewKeyDown+=(s,e)=> {
                if(e.Key==Key.Z && Keyboard.Modifiers==ModifierKeys.Control) { Undo(); e.Handled=true; }
                if(e.Key==Key.Escape) Close();
            };
        }
        void Tool(StackPanel panel,string label,string name) {
            Button b=null; b=Ui.Button(label,()=> { tool=name; foreach(var button in toolButtons) button.FontWeight=FontWeights.Normal; b.FontWeight=FontWeights.Bold; });
            if(name==tool) b.FontWeight=FontWeights.Bold; toolButtons.Add(b); panel.Children.Add(b);
        }
        void Begin(Point point) {
            start=point; drawing=true; canvas.CaptureMouse();
            if(tool=="pen") { stroke=new Polyline { Stroke=color,StrokeThickness=3,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,IsHitTestVisible=false }; stroke.Points.Add(point); stroke.Points.Add(new Point(point.X+.1,point.Y+.1)); canvas.Children.Add(stroke); }
            else if(tool=="rectangle") { shape=new Rectangle { Stroke=color,StrokeThickness=3,Fill=Brushes.Transparent,IsHitTestVisible=false }; canvas.Children.Add(shape); Canvas.SetLeft(shape,point.X); Canvas.SetTop(shape,point.Y); }
            else {
                shape=new Line { X1=point.X,Y1=point.Y,X2=point.X,Y2=point.Y,Stroke=color,StrokeThickness=3,StrokeStartLineCap=PenLineCap.Round,IsHitTestVisible=false };
                arrow1=new Line { Stroke=color,StrokeThickness=3,IsHitTestVisible=false }; arrow2=new Line { Stroke=color,StrokeThickness=3,IsHitTestVisible=false }; canvas.Children.Add(shape); canvas.Children.Add(arrow1); canvas.Children.Add(arrow2);
            }
        }
        void Update(Point p) {
            p.X=Math.Max(0,Math.Min(canvas.Width,p.X)); p.Y=Math.Max(0,Math.Min(canvas.Height,p.Y));
            if(tool=="pen") stroke.Points.Add(p);
            else if(tool=="rectangle") { Canvas.SetLeft(shape,Math.Min(start.X,p.X)); Canvas.SetTop(shape,Math.Min(start.Y,p.Y)); shape.Width=Math.Abs(p.X-start.X); shape.Height=Math.Abs(p.Y-start.Y); }
            else { var line=(Line)shape; line.X2=p.X; line.Y2=p.Y; double angle=Math.Atan2(p.Y-start.Y,p.X-start.X); arrow1.X1=arrow2.X1=p.X; arrow1.Y1=arrow2.Y1=p.Y; arrow1.X2=p.X-13*Math.Cos(angle-.5); arrow1.Y2=p.Y-13*Math.Sin(angle-.5); arrow2.X2=p.X-13*Math.Cos(angle+.5); arrow2.Y2=p.Y-13*Math.Sin(angle+.5); }
        }
        void Finish() {
            if(!drawing) return; drawing=false;
            if(tool=="pen") edits.Add(new UIElement[]{stroke});
            else if(tool=="rectangle") edits.Add(new UIElement[]{shape});
            else edits.Add(new UIElement[]{shape,arrow1,arrow2});
            canvas.ReleaseMouseCapture();
        }
        void Undo() { if(drawing) Finish(); if(edits.Count==0) return; foreach(var element in edits[edits.Count-1]) canvas.Children.Remove(element); edits.RemoveAt(edits.Count-1); }
        void Save() {
            Finish();
            try {
                // Render the composed image at the source's original pixel resolution.
                var output=Compose(canvas,original.PixelWidth,original.PixelHeight); library.Add(output);
                DialogResult=true; Close();
            } catch(Exception ex) { state.Text="未保存："+ex.Message+"。原图仍然保留。"; }
        }
        internal static BitmapSource Compose(Canvas surface,int width,int height) {
            var visual=new DrawingVisual();
            using(var context=visual.RenderOpen()) { context.DrawRectangle(new VisualBrush(surface),null,new Rect(0,0,width,height)); }
            var output=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); output.Render(visual); output.Freeze(); return output;
        }
    }
}
