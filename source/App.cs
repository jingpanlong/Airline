using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Airline {
    internal static class Program {
        [STAThread] public static int Main(string[] args) {
            AppContext.SetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges",false);
            if(args.Length>0 && args[0]=="--self-test") return SelfTest.Run(args.Length>1?args[1]:Path.Combine(Path.GetTempPath(),"Airline-test-"+Guid.NewGuid().ToString("N")));
            if(args.Length>1 && args[0]=="--render-preview") return Preview.Render(args[1]);
            if(args.Length>1 && args[0]=="--runtime-test") return RuntimeTest.Run(args[1]);
            if(args.Length>1 && args[0]=="--render-features") return FeaturePreview.Render(args[1]);
            bool first;
            using(var mutex=new Mutex(true,"Local\\Airline.Screenline.1",out first)) {
                if(!first) {
                    IntPtr existing=Native.FindWindow(null,"晾图 · Airline"),reply;
                    if(existing==IntPtr.Zero || Native.SendMessageTimeout(existing,Native.RunningMessage,IntPtr.Zero,IntPtr.Zero,2,600,out reply)==IntPtr.Zero || reply!=new IntPtr(1))
                        MessageBox.Show("晾图已经在运行。鼠标移到主屏幕顶边，或点击通知区域的晾图图标。","晾图");
                    return 0;
                }
                var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
                app.DispatcherUnhandledException+=(s,e)=> {
                    string text="这次操作没有完成，已保存的截图仍然保留。\n\n"+e.Exception.Message;
                    var owner=app.MainWindow as Shelf;
                    if(owner!=null) owner.ShowMessage(text,MessageBoxImage.Warning);
                    else MessageBox.Show(text,"晾图",MessageBoxButton.OK,MessageBoxImage.Warning);
                    e.Handled=true;
                };
                try {
                    var library=new Library(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Airline"));
                    var shelf=new Shelf(library,false); app.MainWindow=shelf; shelf.Show(); app.Run(); return 0;
                } catch(Exception ex) { MessageBox.Show("无法启动晾图：\n"+ex.Message,"晾图"); return 1; }
            }
        }
    }
}
