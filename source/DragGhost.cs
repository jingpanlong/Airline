using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Airline {
    // A separate click-through HWND keeps the image visible outside the shelf,
    // without consuming the mouse events intended for an OLE drop target.
    internal sealed class DragGhost : Window, IDisposable {
        readonly DispatcherTimer follow;
        readonly Func<Native.POINT> cursor;
        readonly Image picture;
        HwndSource source;
        bool disposed;
        internal bool IsFollowing { get { return follow.IsEnabled; } }
        internal Image Picture { get { return picture; } }
        public DragGhost(Shot shot) : this(shot,ReadCursor) { }
        internal DragGhost(Shot shot,Func<Native.POINT> getCursor) {
            cursor=getCursor;
            Title="晾图 · 拖动副本"; Width=196; Left=-30000; Top=-30000;
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true; Background=Brushes.Transparent; ShowInTaskbar=false;
            ShowActivated=false; Topmost=true; IsHitTestVisible=false;
            var image=Library.Load(shot.Path,400);
            double imageHeight=Math.Max(12,Math.Min(132,172.0*image.PixelHeight/image.PixelWidth)); Height=imageHeight+48;
            var card=new Border { Margin=new Thickness(6),Background=Brushes.White,Padding=new Thickness(6),CornerRadius=new CornerRadius(3),Opacity=.94,
                Effect=new DropShadowEffect { BlurRadius=10,ShadowDepth=3,Opacity=.2 } };
            var stack=new StackPanel(); card.Child=stack;
            picture=new Image { Source=image,Width=172,Height=imageHeight,Stretch=Stretch.Uniform }; stack.Children.Add(picture);
            var label=Ui.Text("副本  ·  原图保留",10,Ui.Green); label.Margin=new Thickness(3,6,0,1); stack.Children.Add(label); Content=card;
            SourceInitialized+=(s,e)=> {
                source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                Native.SetWindowLong(source.Handle,-20,Native.GetWindowLong(source.Handle,-20)|0x80|0x20|0x08000000);
                source.AddHook(Hook);
            };
            follow=new DispatcherTimer(DispatcherPriority.Input) { Interval=TimeSpan.FromMilliseconds(16) };
            follow.Tick+=(s,e)=>FollowCursor();
            Closed+=(s,e)=> { disposed=true; follow.Stop(); };
        }
        static Native.POINT ReadCursor() { Native.POINT point; Native.GetCursorPos(out point); return point; }
        IntPtr Hook(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled) {
            if(message==0x84) { handled=true; return new IntPtr(-1); } // HTTRANSPARENT
            if(message==0x21) { handled=true; return new IntPtr(3); } // MA_NOACTIVATE
            return IntPtr.Zero;
        }
        internal void Start() {
            Show(); FollowCursor(); UpdateLayout();
            // Present the initial image before OLE enters its native modal loop.
            // Subsequent movement uses SetWindowPos from GiveFeedback and does
            // not depend on WPF's dispatcher timer running inside that loop.
            Dispatcher.Invoke(DispatcherPriority.Render,new Action(()=>{}));
            follow.Start();
        }
        internal void FollowCursor() {
            if(disposed || source==null) return;
            var point=cursor();
            Native.RECT size; Native.GetWindowRect(source.Handle,out size);
            var monitor=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(point.X,point.Y)).Bounds;
            var position=Position(point,size,monitor);
            // Win32 positioning uses physical pixels, including negative multi-monitor coordinates.
            Native.SetWindowPos(source.Handle,new IntPtr(-1),position.X,position.Y,0,0,0x1|0x10|0x200);
        }
        internal static Native.POINT Position(Native.POINT point,Native.RECT window,System.Drawing.Rectangle monitor) {
            int x=point.X+16,y=point.Y+20,width=window.Right-window.Left,height=window.Bottom-window.Top;
            if(monitor.Contains(point.X,point.Y)) {
                if(x+width>monitor.Right) x=point.X-width-16;
                if(y+height>monitor.Bottom) y=point.Y-height-20;
                x=Math.Max(monitor.Left,x); y=Math.Max(monitor.Top,y);
            }
            return new Native.POINT { X=x,Y=y };
        }
        public void Dispose() { if(disposed) return; follow.Stop(); Close(); }
    }
}
