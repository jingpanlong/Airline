using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Airline {
    internal static class FeaturePreview {
        sealed class EmptyStartupStore : IStartupStore { public string Read() { return null; } public void Write(string text) { } public void Delete() { } }
        static void Text(DrawingContext context,string text,double x,double y,double size,Brush brush) {
            context.DrawText(new FormattedText(text,System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,brush,1),new Point(x,y));
        }
        static void Draw(DrawingContext context,Window window,double x,double y,double width,double height) {
            var panel=(FrameworkElement)window.Content; window.Content=null;
            var frame=new System.Windows.Controls.Grid { Width=width,Height=height,Background=window is StartupNotice?Brushes.Transparent:Ui.Paper }; frame.Children.Add(panel);
            frame.Measure(new Size(width,height)); frame.Arrange(new Rect(0,0,width,height)); frame.UpdateLayout();
            context.DrawRoundedRectangle(Brushes.White,null,new Rect(x-1,y-1,width+2,height+2),8,8);
            var image=new RenderTargetBitmap((int)width,(int)height,96,96,PixelFormats.Pbgra32); image.Render(frame);
            context.DrawImage(image,new Rect(x,y,width,height));
        }
        internal static int Render(string output) {
            try {
                var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
                var data=new Library(Path.Combine(Environment.CurrentDirectory,"work","feature-demo-"+Guid.NewGuid().ToString("N")));
                var visual=new DrawingVisual(); using(var c=visual.RenderOpen()) {
                    c.DrawRectangle(Ui.B("#ECEDE5"),null,new Rect(0,0,1520,950));
                    Text(c,"晾图  v"+Product.Version+"   /   小工具，也有明确的反馈。",28,23,25,Ui.Ink);
                    Text(c,"关于与作者",28,70,12,Ui.Green); Text(c,"偏好设置",559,70,12,Ui.Green); Text(c,"每次启动都会告诉你",1140,70,12,Ui.Green);
                    Draw(c,new AboutWindow(),24,98,510,570);
                    Draw(c,new SettingsWindow(data,new StartupRegistration(new EmptyStartupStore(),Product.Executable),()=>{}),551,98,550,790);
                    Draw(c,new StartupNotice(12,()=>{}),1113,98,380,196);
                    Text(c,"开机自启动默认关闭",1140,344,17,Ui.Ink); Text(c,"开启后，登录 Windows 自动运行。",1140,379,13,Ui.Muted);
                    Text(c,"原图仍然保留",1140,448,17,Ui.Ink); Text(c,"导出全部截图，只发送一份副本。",1140,483,13,Ui.Muted);
                    Text(c,"全屏时安静退开",1140,552,17,Ui.Ink); Text(c,"Ctrl + Alt + L 随时临时展开。",1140,587,13,Ui.Muted);
                    Text(c,"截图轻轻摆动",1140,681,17,Ui.Ink); Text(c,"鼠标悬停会暂停，并显示选中高亮。",1140,716,13,Ui.Muted);
                    Text(c,"快捷键可以修改",1140,786,17,Ui.Ink); Text(c,"点击录制，按下组合键，再点击应用。",1140,821,13,Ui.Muted);
                    Text(c,"原生 WPF 控件渲染  ·  启动提示中的张数为演示数据",28,916,11,Ui.Muted);
                }
                var image=new RenderTargetBitmap(1520,950,96,96,PixelFormats.Pbgra32); image.Render(visual);
                if(File.Exists(output)) File.Delete(output); Library.Encode(image,output); app.Shutdown(); return 0;
            } catch(Exception ex) { File.WriteAllText(output+".error.txt",ex.ToString()); return 1; }
        }
    }
}
