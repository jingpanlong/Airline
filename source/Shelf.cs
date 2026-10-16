using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace Airline {
    internal sealed class Shelf : Window {
        readonly Library library;
        readonly bool demo;
        readonly bool diagnostics;
        Grid root, pane, collapsed;
        Canvas clothes;
        TextBlock count, status;
        Button trash, previous, next;
        Button menuButton;
        Button captureButton;
        HotkeyService hotkeys;
        ContextMenu activeMenu;
        readonly Dictionary<string,ImagePreview> previews=new Dictionary<string,ImagePreview>(StringComparer.OrdinalIgnoreCase);
        AboutWindow about;
        SettingsWindow settings;
        StartupNotice startupNotice;
        readonly StartupRegistration startup=new StartupRegistration();
        DispatcherTimer hover, clipboardRetry;
        Forms.NotifyIcon tray;
        HwndSource source;
        bool expanded, busy, capturing, shuttingDown;
        bool fullscreenHidden;
        DateTime manualRevealUntil=DateTime.MinValue;
        uint ignoredSequence, pendingSequence;
        int retries, page, capacity=6;
        DateTime lastInside=DateTime.Now;
        double screenTop, screenLeft;
        const double ShelfHeight=338;
        public Shelf(Library data,bool demoMode,bool diagnosticsMode=false) {
            library=data; demo=demoMode; diagnostics=diagnosticsMode;
            Title="晾图 · Airline"; Width=1280; Height=ShelfHeight;
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true; Background=Brushes.Transparent;
            ShowInTaskbar=false; ShowActivated=false; Topmost=true;
            FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");
            Build();
            SourceInitialized+=(s,e)=>InitializeNative();
            Closing+=(s,e)=> { if(!shuttingDown && !demo) { e.Cancel=true; Collapse(); } };
            Closed+=(s,e)=> { foreach(var preview in previews.Values.ToArray()) preview.Close(); previews.Clear(); if(about!=null) about.Close(); if(settings!=null) settings.Close(); if(startupNotice!=null) startupNotice.Close(); };
            PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Escape && !busy) { CloseMenus(); Collapse(); e.Handled=true; } };
            if(demo) { expanded=true; collapsed.Visibility=Visibility.Collapsed; pane.Visibility=Visibility.Visible; DrawShots(); }
        }
        void Build() {
            root=new Grid(); Content=root;
            collapsed=new Grid { Background=Brushes.Transparent };
            collapsed.Children.Add(new Border { Height=2, VerticalAlignment=VerticalAlignment.Top, Background=Ui.B("#A4B39D") });
            collapsed.Children.Add(new Border { Width=54,Height=5,VerticalAlignment=VerticalAlignment.Top, Background=Ui.Green,CornerRadius=new CornerRadius(0,0,3,3) });
            root.Children.Add(collapsed);
            pane=new Grid { Background=Ui.Paper,Visibility=Visibility.Collapsed,RenderTransform=new TranslateTransform() };
            pane.RowDefinitions.Add(new RowDefinition { Height=new GridLength(68) });
            pane.RowDefinitions.Add(new RowDefinition { Height=new GridLength(223) });
            pane.RowDefinitions.Add(new RowDefinition { Height=new GridLength(47) });
            root.Children.Add(pane);
            var header=new Grid { Margin=new Thickness(32,0,28,0) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var brand=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
            var logo=new Grid { Width=30,Height=28,Margin=new Thickness(0,0,12,0) };
            logo.Children.Add(new Line { X1=0,Y1=7,X2=30,Y2=7,Stroke=Ui.Green,StrokeThickness=1 });
            logo.Children.Add(new Border { Width=13,Height=15,Background=Ui.B("#DCE4D7"),BorderBrush=Ui.Green,BorderThickness=new Thickness(1),RenderTransform=new RotateTransform(-9),RenderTransformOrigin=new Point(.5,0),Margin=new Thickness(0,7,0,0) });
            brand.Children.Add(logo); brand.Children.Add(Ui.Text("晾图",21,Ui.Ink));
            brand.Children.Add(Ui.Text("  /  AIRLINE",10,Ui.Muted)); header.Children.Add(brand);
            count=Ui.Text("",12,Ui.Muted); count.Margin=new Thickness(26,0,0,0); Grid.SetColumn(count,1); header.Children.Add(count);
            var actions=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
            captureButton=Ui.Button("＋  截一张",()=>Capture()); captureButton.Name="CaptureButton"; captureButton.Background=Ui.B("#E5E9DF"); captureButton.ToolTip=library.CaptureShortcut.Display+" · 框选屏幕区域"; actions.Children.Add(captureButton);
            var folder=Ui.Button("资料夹 ↗",()=>OpenFolder()); folder.Name="FolderButton"; actions.Children.Add(folder);
            var settingsButton=Ui.Button("设置",()=>OpenSettings()); settingsButton.Name="SettingsButton"; actions.Children.Add(settingsButton);
            var aboutButton=Ui.Button("关于",()=>OpenAbout()); aboutButton.Name="AboutButton"; actions.Children.Add(aboutButton);
            menuButton=Ui.Button("•••",()=>OpenMenu()); menuButton.Name="MenuButton"; menuButton.ToolTip="设置与退出"; actions.Children.Add(menuButton);
            Grid.SetColumn(actions,2); header.Children.Add(actions); pane.Children.Add(header);
            clothes=new Canvas { ClipToBounds=false }; Grid.SetRow(clothes,1); pane.Children.Add(clothes);
            var footer=new Grid { Margin=new Thickness(32,0,26,0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var footerLeft=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
            var dot=new Ellipse { Width=5,Height=5,Fill=Ui.Green,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0) }; footerLeft.Children.Add(dot);
            status=Ui.Text("单击复制  ·  长按标注  ·  双击预览  ·  拖走分享",11,Ui.Muted); footerLeft.Children.Add(status); footer.Children.Add(footerLeft);
            var right=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
            previous=Ui.Button("‹",()=> { page--; DrawShots(); }); next=Ui.Button("›",()=> { page++; DrawShots(); });
            right.Children.Add(previous); right.Children.Add(next);
            trash=Ui.Button("♲  放手不要",()=>Status("把截图拖到这里，就送进回收站")); trash.AllowDrop=true;
            trash.DragOver+=(s,e)=> { if(e.Data.GetDataPresent("Airline.Shot")) { e.Effects=DragDropEffects.Move; trash.Background=Ui.B("#EBDCD5"); } else e.Effects=DragDropEffects.None; e.Handled=true; };
            trash.DragLeave+=(s,e)=>trash.Background=Brushes.Transparent;
            trash.Drop+=(s,e)=> { var shot=e.Data.GetData("Airline.Shot") as Shot; if(shot!=null) { Delete(shot); e.Effects=DragDropEffects.Move; } trash.Background=Brushes.Transparent; e.Handled=true; };
            right.Children.Add(trash); Grid.SetColumn(right,1); footer.Children.Add(right); Grid.SetRow(footer,2); pane.Children.Add(footer);
            pane.Children.Add(new Border { Height=1,Background=Ui.Line,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false });
        }
        void InitializeNative() {
            Native.ToolWindow(this); if(demo) return;
            source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); source.AddHook(Hook);
            var transform=source.CompositionTarget.TransformFromDevice;
            var bounds=Forms.Screen.PrimaryScreen.Bounds;
            var origin=transform.Transform(new Point(bounds.Left,bounds.Top));
            var extent=transform.Transform(new Point(bounds.Width,bounds.Height));
            screenTop=origin.Y; screenLeft=origin.X; Left=screenLeft; Top=screenTop; Width=extent.X;
            capacity=Math.Max(1,(int)((Width-64)/194));
            hotkeys=new HotkeyService(new NativeHotkeyBackend(source.Handle),library.CaptureShortcut,library.RevealShortcut); hotkeys.Start();
            bool hotkey=hotkeys.CaptureRegistered,reveal=hotkeys.RevealRegistered;
            bool listener=Native.AddClipboardFormatListener(source.Handle);
            ignoredSequence=Native.GetClipboardSequenceNumber();
            clipboardRetry=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(90) };
            clipboardRetry.Tick+=(s,e)=>ReadClipboard();
            hover=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(70) }; hover.Tick+=(s,e)=>Hover(); hover.Start();
            tray=new Forms.NotifyIcon { Text="晾图 · "+library.CaptureShortcut.Display+" 截图",Icon=System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),Visible=true };
            tray.MouseClick+=(s,e)=> { if(e.Button==Forms.MouseButtons.Left) Dispatcher.BeginInvoke(new Action(()=>Reveal())); };
            var menu=new Forms.ContextMenuStrip();
            menu.Items.Add("截一张",null,(s,e)=>Dispatcher.BeginInvoke(new Action(()=>Capture())));
            menu.Items.Add("展开晾图绳",null,(s,e)=>Dispatcher.BeginInvoke(new Action(()=>Reveal())));
            menu.Items.Add("设置 / 开机自启动",null,(s,e)=>Dispatcher.BeginInvoke(new Action(()=>OpenSettings())));
            menu.Items.Add("导出全部截图…",null,(s,e)=>Dispatcher.BeginInvoke(new Action(()=>ExportAll())));
            menu.Items.Add("打开截图资料夹",null,(s,e)=>OpenFolder()); menu.Items.Add("关于晾图",null,(s,e)=>Dispatcher.BeginInvoke(new Action(()=>OpenAbout()))); menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出晾图",null,(s,e)=>Quit()); tray.ContextMenuStrip=menu;
            Height=6; collapsed.Visibility=Visibility.Visible; pane.Visibility=Visibility.Collapsed;
            if(!hotkey || !reveal || !listener) {
                if(!diagnostics) tray.ShowBalloonTip(5000,"晾图已启动",(!hotkey?"截图快捷键被占用，请用托盘菜单截一张。\n":"")+(!listener?"剪贴板监听不可用；内置截图仍可使用。":""),Forms.ToolTipIcon.Warning);
            }
            Dispatcher.BeginInvoke(new Action(()=>ShowStartupNotice(false)));
        }
        IntPtr Hook(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled) {
            if((uint)message==Native.RunningMessage) { Dispatcher.BeginInvoke(new Action(()=>ShowStartupNotice(true))); handled=true; return new IntPtr(1); }
            if(message==0x312) { if(hotkeys==null || !hotkeys.Suspended) { if(w.ToInt32()==1) Capture(); else Reveal(); } handled=true; }
            if(message==0x31D && library.WatchClipboard && !capturing) {
                uint sequence=Native.GetClipboardSequenceNumber();
                if(sequence!=ignoredSequence) { pendingSequence=sequence; retries=0; clipboardRetry.Stop(); clipboardRetry.Start(); }
            }
            if(message==0x7E || message==0x2E0) Dispatcher.BeginInvoke(new Action(()=>Reposition()));
            return IntPtr.Zero;
        }
        void Reposition() {
            if(source==null) return;
            var t=source.CompositionTarget.TransformFromDevice; var b=Forms.Screen.PrimaryScreen.Bounds;
            var p=t.Transform(new Point(b.Left,b.Top)); var size=t.Transform(new Point(b.Width,b.Height));
            screenLeft=p.X; screenTop=p.Y; Left=p.X; Top=p.Y; Width=size.X; capacity=Math.Max(1,(int)((Width-64)/194)); DrawShots();
        }
        void ReadClipboard() {
            if(!library.WatchClipboard || pendingSequence==ignoredSequence) { clipboardRetry.Stop(); return; }
            try {
                uint now=Native.GetClipboardSequenceNumber();
                if(now==ignoredSequence) { clipboardRetry.Stop(); return; }
                if(now!=pendingSequence) { pendingSequence=now; retries=0; }
                if(ImageOnClipboard()) {
                    var image=LoadClipboardImage();
                    // Only mark consumed after the PNG has been durably stored.
                    if(image!=null && now==Native.GetClipboardSequenceNumber()) { library.Add(image); ignoredSequence=now; page=0; DrawShots(); Status("新截图已经挂好"); }
                } else ignoredSequence=now;
                clipboardRetry.Stop();
            } catch(System.Runtime.InteropServices.COMException) { if(++retries>=20) { clipboardRetry.Stop(); Status("剪贴板暂时忙碌，请在托盘中重新截一张"); } }
            catch(Exception ex) { clipboardRetry.Stop(); Error(ex); }
        }
        // WeChat (and many tools) put screenshots on the clipboard in formats that
        // System.Windows.Clipboard.GetImage() renders as a blank BitmapSource. Read
        // through System.Windows.Forms.Clipboard instead, which decodes DIB/PNG
        // reliably, then convert to a WPF BitmapSource via an HBITMAP.
        static bool ImageOnClipboard() {
            try {
                if(System.Windows.Clipboard.ContainsImage()) return true;
                var dao=Forms.Clipboard.GetDataObject();
                if(dao!=null && (dao.GetDataPresent("Png")||dao.GetDataPresent("image/png")||dao.GetDataPresent(System.Windows.DataFormats.Dib)||dao.GetDataPresent(System.Windows.DataFormats.Bitmap))) return true;
            } catch { }
            return false;
        }
        static BitmapSource LoadClipboardImage() {
            BitmapSource result=null;
            try {
                using(var src=Forms.Clipboard.GetImage()) {
                    if(src!=null) {
                        var bitmap=src as System.Drawing.Bitmap ?? new System.Drawing.Bitmap(src);
                        try { result=ToBitmapSource(bitmap); }
                        finally { if(!ReferenceEquals(bitmap,src)) bitmap.Dispose(); }
                    }
                }
            } catch { }
            if(result==null) { try { var wpf=System.Windows.Clipboard.GetImage(); if(wpf!=null) result=wpf; } catch { } }
            return result;
        }
        static BitmapSource ToBitmapSource(System.Drawing.Bitmap bitmap) {
            if(bitmap==null) return null;
            var handle=bitmap.GetHbitmap();
            try { var source=System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(handle,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions()); source.Freeze(); return source; }
            finally { Native.DeleteObject(handle); }
        }
        void Hover() {
            if(busy || IsMenuOpen || capturing) return;
            if(ApplyFullscreenPolicy(Native.IsOtherFullscreen(),DateTime.UtcNow) || !IsVisible) return;
            Native.POINT p; if(!Native.GetCursorPos(out p)) return;
            Native.RECT window; if(!Native.GetWindowRect(source.Handle,out window)) return;
            var screen=Forms.Screen.PrimaryScreen.Bounds;
            double scale=source.CompositionTarget.TransformToDevice.M22;
            // Compare cursor and HWND bounds in physical pixels. This remains correct after a DPI change.
            bool atEdge=p.X>=screen.Left && p.X<screen.Right && p.Y>=screen.Top && p.Y<screen.Top+Math.Max(2,6*scale);
            bool inside=p.X>=window.Left && p.X<window.Right && p.Y>=window.Top && p.Y<window.Bottom+14*scale;
            UpdateHover(atEdge,inside,DateTime.UtcNow);
        }
        internal void UpdateHover(bool atEdge,bool inside,DateTime now) {
            if(busy || IsMenuOpen || capturing) return;
            if(atEdge) { Expand(); lastInside=now; }
            if(expanded) {
                if(inside) lastInside=now;
                else if((now-lastInside).TotalMilliseconds>480) Collapse();
            }
        }
        public void Expand() {
            if(capturing || busy) return; lastInside=DateTime.UtcNow;
            if(expanded) return; expanded=true; Height=ShelfHeight; pane.Visibility=Visibility.Visible; collapsed.Visibility=Visibility.Collapsed; DrawShots();
            ((TranslateTransform)pane.RenderTransform).BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(-ShelfHeight,0,TimeSpan.FromMilliseconds(220)) { EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } });
        }
        internal void Reveal() {
            if(capturing || busy) return;
            manualRevealUntil=DateTime.UtcNow.AddSeconds(10); fullscreenHidden=false;
            if(!IsVisible) Show(); Expand();
        }
        internal bool ApplyFullscreenPolicy(bool fullscreen,DateTime now) {
            if(library.AvoidFullscreen && fullscreen && now>=manualRevealUntil) {
                CollapseImmediately(); if(IsVisible) Hide(); fullscreenHidden=true; return true;
            }
            if(fullscreenHidden) { fullscreenHidden=false; Show(); }
            return false;
        }
        void Collapse() {
            if(busy || IsMenuOpen || !expanded) return; expanded=false;
            var animation=new DoubleAnimation(0,-ShelfHeight,TimeSpan.FromMilliseconds(180)) { EasingFunction=new CubicEase { EasingMode=EasingMode.EaseIn } };
            animation.Completed+=(s,e)=> { if(!expanded) { pane.Visibility=Visibility.Collapsed; collapsed.Visibility=Visibility.Visible; Height=6; } };
            ((TranslateTransform)pane.RenderTransform).BeginAnimation(TranslateTransform.YProperty,animation);
        }
        internal bool IsExpanded { get { return expanded; } }
        internal bool IsBusy { get { return busy; } }
        internal bool IsMenuOpen { get { return activeMenu!=null && activeMenu.IsOpen; } }
        void CollapseImmediately() {
            expanded=false;
            var transform=(TranslateTransform)pane.RenderTransform;
            transform.BeginAnimation(TranslateTransform.YProperty,null); transform.Y=0;
            pane.Visibility=Visibility.Collapsed; collapsed.Visibility=Visibility.Visible; Height=6;
        }
        internal void CloseMenus() {
            if(activeMenu!=null) activeMenu.IsOpen=false;
            activeMenu=null;
        }
        void TrackMenu(ContextMenu menu) {
            menu.Opened+=(s,e)=>activeMenu=menu;
            menu.Closed+=(s,e)=> { if(activeMenu==menu) activeMenu=null; lastInside=DateTime.UtcNow; };
        }
        T RunBlocking<T>(Func<T> action) {
            bool wasBusy=busy;
            CloseMenus(); Mouse.Capture(null); CollapseImmediately(); busy=true;
            try { return action(); }
            finally { busy=wasBusy; lastInside=DateTime.UtcNow; }
        }
        internal bool? ShowOwnedDialog(Window dialog) {
            return RunBlocking(()=> { dialog.Owner=this; dialog.Topmost=true; return dialog.ShowDialog(); });
        }
        internal void ShowMessage(string text,MessageBoxImage icon) {
            RunBlocking(()=>MessageBox.Show(this,text,"晾图 · Airline",MessageBoxButton.OK,icon));
        }
        public void DrawShots() {
            if(clothes==null) return; clothes.Children.Clear();
            int pages=Math.Max(1,(library.Shots.Count+capacity-1)/capacity); page=Math.Max(0,Math.Min(page,pages-1));
            count.Text=library.Shots.Count==0?"让截图在这里歇一会儿":library.Shots.Count+" 张正在晾着";
            previous.Visibility=next.Visibility=pages>1?Visibility.Visible:Visibility.Collapsed;
            previous.IsEnabled=page>0; next.IsEnabled=page<pages-1;
            if(pages>1) count.Text+="   /   "+(page+1)+" · "+pages;
            var cord=new System.Windows.Shapes.Path { Stroke=Ui.B("#A8AD9D"),StrokeThickness=1.2,Data=Geometry.Parse("M 0,18 Q "+(Width/2).ToString(System.Globalization.CultureInfo.InvariantCulture)+",38 "+Width.ToString(System.Globalization.CultureInfo.InvariantCulture)+",18") };
            clothes.Children.Add(cord);
            if(library.Shots.Count==0) {
                var empty=new StackPanel { Width=560,HorizontalAlignment=HorizontalAlignment.Center };
                empty.Children.Add(Ui.Text("截下来，晾起来。",30,Ui.Ink));
                var description=Ui.Text("不挤桌面，也不打断手头的事。",13,Ui.Muted); description.Margin=new Thickness(0,12,0,16); empty.Children.Add(description);
                var button=Ui.Button("截第一张    "+library.CaptureShortcut.Display,()=>Capture()); button.HorizontalAlignment=HorizontalAlignment.Left; button.Background=Ui.B("#E5E9DF"); empty.Children.Add(button);
                Canvas.SetLeft(empty,Math.Max(32,(Width-560)/2)); Canvas.SetTop(empty,68); clothes.Children.Add(empty); return;
            }
            var items=library.Shots.Skip(page*capacity).Take(capacity).ToArray();
            double gap=Math.Min(210,(Width-64)/Math.Max(1,items.Length)), start=(Width-gap*items.Length)/2;
            for(int i=0;i<items.Length;i++) {
                var card=Card(items[i],i); Canvas.SetLeft(card,start+i*gap+(gap-170)/2); Canvas.SetTop(card,26); clothes.Children.Add(card);
            }
        }
        FrameworkElement Card(Shot shot,int index) {
            var holder=new HangingCard(shot,index,()=>library.AnimateShots);
            holder.ToolTip="单击复制 · 长按 0.55 秒标注 · 双击预览\n拖到 App 或文件夹发送副本；拖到回收站移除";
            var context=new ContextMenu();
            AddMenu(context,"复制图片",()=>Copy(shot)); AddMenu(context,"标注…",()=>Edit(shot)); AddMenu(context,"预览",()=>OpenPreview(shot)); AddMenu(context,"另存为…",()=>SaveAs(shot));
            context.Items.Add(new Separator()); AddMenu(context,"放入回收站",()=>Delete(shot));
            TrackMenu(context); holder.ContextMenu=context;
            var hold=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(550) };
            var click=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(Forms.SystemInformation.DoubleClickTime+15) };
            Point down=new Point(); bool pressed=false,held=false,dragged=false;
            click.Tick+=(s,e)=> { click.Stop(); Copy(shot); };
            hold.Tick+=(s,e)=> { hold.Stop(); if(pressed && !dragged) { held=true; pressed=false; holder.ReleaseMouseCapture(); Edit(shot); } };
            holder.MouseLeftButtonDown+=(s,e)=> {
                if(e.ClickCount>=2) { hold.Stop(); click.Stop(); held=true; pressed=false; OpenPreview(shot); e.Handled=true; return; }
                down=e.GetPosition(holder); pressed=true; held=false; dragged=false; holder.CaptureMouse(); hold.Start(); e.Handled=true;
            };
            holder.MouseMove+=(s,e)=> {
                if(!pressed || e.LeftButton!=MouseButtonState.Pressed) return;
                var p=e.GetPosition(holder);
                if(Math.Abs(p.X-down.X)>SystemParameters.MinimumHorizontalDragDistance || Math.Abs(p.Y-down.Y)>SystemParameters.MinimumVerticalDragDistance) {
                    hold.Stop(); click.Stop(); dragged=true; pressed=false; holder.ReleaseMouseCapture(); Drag(shot,holder);
                }
            };
            holder.MouseLeftButtonUp+=(s,e)=> { hold.Stop(); bool shouldCopy=pressed && !held && !dragged; holder.ReleaseMouseCapture(); if(shouldCopy) { click.Stop(); click.Start(); } pressed=false; e.Handled=true; };
            holder.LostMouseCapture+=(s,e)=> { hold.Stop(); pressed=false; };
            // An off-page card must not keep a pending gesture alive.
            holder.Unloaded+=(s,e)=> { hold.Stop(); click.Stop(); };
            return holder;
        }
        static void AddMenu(ContextMenu menu,string title,Action action) { var item=new MenuItem { Header=title }; item.Click+=(s,e)=>action(); menu.Items.Add(item); }
        internal void OpenMenu() {
            CloseMenus();
            var menu=new ContextMenu { PlacementTarget=menuButton,Placement=PlacementMode.Bottom,StaysOpen=false };
            AddMenu(menu,"设置 / 开机自启动…",()=>OpenSettings());
            AddMenu(menu,"导出全部截图…",()=>ExportAll());
            AddMenu(menu,"关于晾图",()=>OpenAbout()); menu.Items.Add(new Separator());
            AddMenu(menu,"使用说明",()=>ShowMessage("鼠标靠近主屏幕顶边，晾图绳就会展开。离开后自动收起。\n\n"+library.CaptureShortcut.Display+"：区域截图\n"+library.RevealShortcut.Display+"：展开晾图绳\nWin+Shift+S：Windows 截图复制后自动接住\n\n单击复制；长按标注（另挂一份）；双击预览。\n拖到 App / 文件夹：传递副本，原图保留。\n拖到右下角的回收区或 Windows 回收站：移除。\n\n每张截图都保存到本机资料夹，退出和重启不会清空。\n剪贴板监听可在设置中关闭，快捷键也可在设置中修改。\n\n当前版本在主屏幕顶边展开，区域截图支持多屏。",MessageBoxImage.Information));
            menu.Items.Add(new Separator()); AddMenu(menu,"退出晾图",()=>Quit());
            TrackMenu(menu); activeMenu=menu; menu.IsOpen=true;
        }
        internal StartupNotice ActiveStartupNotice { get { return startupNotice; } }
        internal void ShowStartupNotice(bool repeat) {
            if(diagnostics) return;
            if(!repeat && !library.NotifyStartup) return;
            if(startupNotice!=null) startupNotice.Close();
            var notice=new StartupNotice(library.Shots.Count,()=>Reveal(),repeat,demo,library.CaptureShortcut.Display);
            startupNotice=notice; notice.Closed+=(s,e)=> { if(startupNotice==notice) startupNotice=null; }; notice.Show();
        }
        internal AboutWindow OpenAbout() {
            CloseMenus(); CollapseImmediately();
            if(about!=null) { if(about.WindowState==WindowState.Minimized) about.WindowState=WindowState.Normal; about.Activate(); return about; }
            var window=new AboutWindow(); if(demo) { window.WindowStartupLocation=WindowStartupLocation.Manual; window.Left=Left; window.Top=Top; }
            about=window; window.Closed+=(s,e)=>about=null; window.Show(); return window;
        }
        internal SettingsWindow OpenSettings() {
            CloseMenus(); CollapseImmediately();
            if(settings!=null) { if(settings.WindowState==WindowState.Minimized) settings.WindowState=WindowState.Normal; settings.Activate(); return settings; }
            var window=new SettingsWindow(library,startup,()=> { if(!library.WatchClipboard && clipboardRetry!=null) clipboardRetry.Stop(); DrawShots(); },TrySetShortcuts,()=>hotkeys==null?new HotkeyService.Scope(()=>{}):hotkeys.Suspend());
            if(demo) { window.WindowStartupLocation=WindowStartupLocation.Manual; window.Left=Left; window.Top=Top; }
            settings=window; window.Closed+=(s,e)=>settings=null; window.Show(); return window;
        }
        sealed class PreviewHotkeyBackend : IHotkeyBackend { public bool Register(int id,Hotkey value) { return true; } public void Unregister(int id) { } }
        internal string TrySetShortcuts(Hotkey capture,Hotkey reveal) {
            var oldCapture=library.CaptureShortcut; var oldReveal=library.RevealShortcut;
            var service=hotkeys;
            if(service==null) { service=new HotkeyService(new PreviewHotkeyBackend(),oldCapture,oldReveal); service.Start(); }
            string error=service.Apply(capture,reveal,()=> { library.CaptureShortcut=capture; library.RevealShortcut=reveal; library.SaveSettings(); });
            if(error!=null) { library.CaptureShortcut=oldCapture; library.RevealShortcut=oldReveal; }
            else { captureButton.ToolTip=capture.Display+" · 框选屏幕区域"; if(tray!=null) tray.Text="晾图 · "+capture.Display+" 截图"; }
            return error;
        }
        async void ExportAll() {
            if(busy || capturing) return;
            if(library.Shots.Count==0) { Status("暂时没有截图可以导出"); return; }
            string destination;
            using(var picker=new Forms.FolderBrowserDialog { Description="选择保存位置，晾图会创建一个新的导出文件夹",ShowNewFolderButton=true }) {
                if(RunBlocking(()=>picker.ShowDialog(new Native.HandleOwner(new WindowInteropHelper(this).Handle)))!=Forms.DialogResult.OK) return;
                destination=picker.SelectedPath;
            }
            var snapshot=library.Shots.ToArray(); busy=true;
            try {
                string folder=await Task.Run(()=>Library.Export(snapshot,destination));
                Status("已导出 "+snapshot.Length+" 张，原图保留");
                Process.Start(new ProcessStartInfo("explorer.exe","\""+folder+"\"") { UseShellExecute=true });
                ShowMessage("已导出 "+snapshot.Length+" 张截图，原图仍在绳上。\n\n"+folder,MessageBoxImage.Information);
            } catch(Exception ex) { Error(ex); }
            finally { busy=false; lastInside=DateTime.UtcNow; }
        }
        void Copy(Shot shot) {
            try { Clipboard.SetImage(Library.Load(shot.Path,0)); ignoredSequence=Native.GetClipboardSequenceNumber(); Status("已复制，去粘贴吧"); }
            catch(Exception ex) { Error(ex); }
        }
        internal DragGhost CurrentDragGhost { get; private set; }
        internal void Drag(Shot shot,FrameworkElement element) {
            busy=true;
            try {
                string staged=library.Stage(shot);
                var data=new DataObject(); data.SetData(DataFormats.FileDrop,new string[]{staged});
                data.SetData("Preferred DropEffect",new MemoryStream(BitConverter.GetBytes((int)DragDropEffects.Copy)));
                data.SetData("Airline.Shot",shot);
                DragDropEffects effect;
                using(var ghost=new DragGhost(shot)) {
                    CurrentDragGhost=ghost;
                    GiveFeedbackEventHandler feedback=(s,e)=> { ghost.FollowCursor(); e.UseDefaultCursors=true; };
                    element.GiveFeedback+=feedback;
                    try { ghost.Start(); effect=DragDrop.DoDragDrop(element,data,DragDropEffects.Copy|DragDropEffects.Move); }
                    finally { element.GiveFeedback-=feedback; }
                }
                if(effect!=DragDropEffects.None && library.Shots.Contains(shot) && IsRecycleTarget(data)) {
                    // The shell recycled the disposable copy. Its surviving original can now be removed.
                    if(!File.Exists(staged)) { File.Delete(shot.Path); library.Shots.Remove(shot); DrawShots(); Status("已放入 Windows 回收站"); }
                    else Delete(shot);
                } else if(effect!=DragDropEffects.None && library.Shots.Contains(shot)) Status("副本已送出，原图还在绳上");
                // Preserve exported paths for receivers that read the file after DoDragDrop returns.
                // Outbox copies expire on startup after seven days; Images never expires automatically.
            } catch(Exception ex) { Error(ex); }
            finally { CurrentDragGhost=null; busy=false; lastInside=DateTime.UtcNow; }
        }
        internal static bool IsRecycleTarget(DataObject data) {
            if(!data.GetDataPresent("TargetCLSID")) return false;
            var value=data.GetData("TargetCLSID"); byte[] bytes=value as byte[];
            var stream=value as MemoryStream; if(stream!=null) bytes=stream.ToArray();
            return bytes!=null && bytes.Length>=16 && new Guid(bytes.Take(16).ToArray())==new Guid("645FF040-5081-101B-9F08-00AA002F954E");
        }
        void Delete(Shot shot) {
            try {
                if(!library.Shots.Contains(shot)) return;
                if(File.Exists(shot.Path) && !Native.Recycle(shot.Path,new WindowInteropHelper(this).Handle)) { Status("未移除：回收站操作取消或失败"); return; }
                library.Shots.Remove(shot); DrawShots(); Status("已放入回收站，可以在那里找回来");
            } catch(Exception ex) { Error(ex); }
        }
        internal ImagePreview OpenPreview(Shot shot) {
            if(busy || capturing) return null;
            try {
                CloseMenus(); Mouse.Capture(null); CollapseImmediately();
                ImagePreview preview;
                if(previews.TryGetValue(shot.Path,out preview)) {
                    if(preview.WindowState==WindowState.Minimized) preview.WindowState=WindowState.Normal;
                    preview.Activate(); return preview;
                }
                // A normal modeless window: the topmost shelf remains usable even
                // while the preview is open or minimized. Owning it by the topmost
                // shelf would also promote the preview above other applications.
                preview=new ImagePreview(shot) { Topmost=false };
                if(demo) { preview.WindowStartupLocation=WindowStartupLocation.Manual; preview.Left=Left; preview.Top=Top; preview.ShowActivated=false; }
                previews.Add(shot.Path,preview);
                preview.Closed+=(s,e)=> { previews.Remove(shot.Path); lastInside=DateTime.UtcNow; };
                try { preview.Show(); }
                catch { previews.Remove(shot.Path); throw; }
                return preview;
            } catch(Exception ex) { Error(ex); return null; }
        }
        void Edit(Shot shot) {
            try { if(ShowOwnedDialog(new Editor(shot,library))==true) { page=0; DrawShots(); Status("标注已另挂一份，原图也在"); } }
            catch(Exception ex) { Error(ex); }
        }
        void SaveAs(Shot shot) {
            try { var dialog=new Microsoft.Win32.SaveFileDialog { Filter="PNG 图片|*.png",FileName=shot.Name,OverwritePrompt=true }; if(RunBlocking(()=>dialog.ShowDialog(this))==true && !string.Equals(dialog.FileName,shot.Path,StringComparison.OrdinalIgnoreCase)) { File.Copy(shot.Path,dialog.FileName,true); Status("已保存副本，原图还在"); } }
            catch(Exception ex) { Error(ex); }
        }
        void Capture() {
            if(capturing || busy || demo) return; if(startupNotice!=null) startupNotice.Close(); CloseMenus(); CollapseImmediately(); capturing=true; clipboardRetry.Stop();
            var foreground=Native.GetForegroundWindow(); Hide();
            var delay=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(180) };
            delay.Tick+=(s,e)=> {
                delay.Stop();
                try {
                    var capture=new CaptureOverlay();
                    if(capture.ShowDialog()==true && capture.Result!=null) {
                        library.Add(capture.Result); page=0; DrawShots(); Status("已挂好，原图已经保存在本机");
                    }
                } catch(Exception ex) { Error(ex); }
                finally {
                    capturing=false; CollapseImmediately(); Show(); lastInside=DateTime.UtcNow;
                    Native.SetForegroundWindow(foreground);
                }
            }; delay.Start();
        }
        void OpenFolder() {
            try { CloseMenus(); CollapseImmediately(); Process.Start(new ProcessStartInfo("explorer.exe","\""+library.Images+"\"") { UseShellExecute=true }); }
            catch(Exception ex) { Error(ex); }
        }
        void Status(string text) { status.Text=text; }
        void Error(Exception ex) { ShowMessage("操作没有完成，已保存的截图仍然保留。\n\n"+ex.Message,MessageBoxImage.Warning); }
        internal void Quit() {
            shuttingDown=true; if(hover!=null) hover.Stop(); if(clipboardRetry!=null) clipboardRetry.Stop();
            if(source!=null) { if(hotkeys!=null) hotkeys.Stop(); Native.RemoveClipboardFormatListener(source.Handle); source.RemoveHook(Hook); }
            if(tray!=null) { tray.Visible=false; tray.Dispose(); } Close(); Application.Current.Shutdown();
        }
    }
}
