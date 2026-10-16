using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Airline {
    internal sealed class StartupNotice : Window {
        readonly DispatcherTimer timer;
        public StartupNotice(int shots,Action reveal,bool repeat=false,bool offscreen=false,string captureDisplay="Ctrl + Alt + S") {
            Title="晾图 · 启动提示"; Width=380; Height=196; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true; Background=Brushes.Transparent; ShowInTaskbar=false; ShowActivated=false; Topmost=true;
            var card=new Border { Margin=new Thickness(9),Padding=new Thickness(20,16,18,15),Background=Ui.Paper,CornerRadius=new CornerRadius(12),BorderBrush=Ui.Line,BorderThickness=new Thickness(1),Effect=new DropShadowEffect { BlurRadius=15,ShadowDepth=3,Opacity=.18 } }; Content=card;
            var panel=new StackPanel(); card.Child=panel;
            var header=new DockPanel(); var close=Ui.Button("×",()=>Close()); close.Padding=new Thickness(7,0,7,0); DockPanel.SetDock(close,Dock.Right); header.Children.Add(close);
            var title=Ui.Text(repeat?"晾图正在运行":"晾图已启动",18,Ui.Ink); title.Name="NoticeTitle"; title.FontWeight=FontWeights.SemiBold; header.Children.Add(title); panel.Children.Add(header);
            var message=Ui.Text("鼠标移到屏幕顶边，就能展开晾图绳。",12,Ui.Muted); message.Margin=new Thickness(0,10,0,5); panel.Children.Add(message);
            panel.Children.Add(Ui.Text(captureDisplay+" 截图  ·  已保留 "+shots+" 张",11,Ui.Green));
            var button=Ui.Button("展开晾图绳  ↑",()=> { Close(); reveal(); }); button.Name="RevealButton"; button.Background=Ui.B("#E5E9DF"); button.HorizontalAlignment=HorizontalAlignment.Left; button.Margin=new Thickness(0,14,0,0); panel.Children.Add(button);
            SourceInitialized+=(s,e)=> {
                var h=new WindowInteropHelper(this).Handle; Native.ToolWindow(this);
                Native.SetWindowLong(h,-20,Native.GetWindowLong(h,-20)|0x08000000);
                Native.ExcludeFromCapture(this);
                var scale=HwndSource.FromHwnd(h).CompositionTarget.TransformFromDevice;
                var area=System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                var corner=scale.Transform(new Point(area.Right,area.Bottom));
                Left=offscreen?-30000:corner.X-Width-12; Top=offscreen?-30000:corner.Y-Height-12;
            };
            timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(6) }; timer.Tick+=(s,e)=> { timer.Stop(); Close(); };
            MouseEnter+=(s,e)=>timer.Stop(); MouseLeave+=(s,e)=> { timer.Stop(); timer.Start(); };
            Loaded+=(s,e)=>timer.Start(); Closed+=(s,e)=>timer.Stop();
        }
    }
    internal sealed class AboutWindow : Window {
        internal static Action<string> OpenLink=url=> { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute=true }); } catch(Exception) { } };
        public AboutWindow() {
            Title="关于晾图"; Width=510; Height=672; ResizeMode=ResizeMode.NoResize; Background=Ui.Paper; WindowStartupLocation=WindowStartupLocation.CenterScreen;
            var panel=new StackPanel { Margin=new Thickness(32,24,32,22) }; Content=panel;
            var kicker=Ui.Text("AIRLINE  /  WINDOWS",11,Ui.Green); kicker.Margin=new Thickness(0,0,0,18); panel.Children.Add(kicker);
            var name=Ui.Text("晾图",34,Ui.Ink); name.FontWeight=FontWeights.SemiBold; panel.Children.Add(name);
            var version=Ui.Text("v"+Product.Version+"    一根细线，接住每一个灵感。",12,Ui.Muted); version.Margin=new Thickness(0,8,0,20); panel.Children.Add(version);
            var intro=Ui.Text("截图像衣服一样，挂在屏幕顶边。\n靠近时展开，离开时安静收起。\n单击复制，长按标注，双击独立预览。\n拖动发送副本，原图继续留在绳上。",13,Ui.Ink); intro.LineHeight=25; panel.Children.Add(intro);
            var privacy=Ui.Text("本机保存  ·  不上传截图  ·  无需账户",11,Ui.Green); privacy.Margin=new Thickness(0,16,0,18); panel.Children.Add(privacy);
            panel.Children.Add(new Border { Height=1,Background=Ui.Line,Margin=new Thickness(0,0,0,19) });
            var download=Ui.Text("下载与更新",16,Ui.Ink); download.FontWeight=FontWeights.SemiBold; panel.Children.Add(download);
            var link=new System.Windows.Documents.Hyperlink { NavigateUri=new Uri(Product.Repository), Foreground=Ui.Green };
            link.Inlines.Add(Product.RepositoryLabel);
            link.RequestNavigate+=(s,e)=> { OpenLink(e.Uri.ToString()); e.Handled=true; };
            var linkBlock=new TextBlock { FontSize=13, Margin=new Thickness(0,11,0,7), TextWrapping=TextWrapping.Wrap };
            linkBlock.Name="RepositoryLink"; linkBlock.Inlines.Add(link); panel.Children.Add(linkBlock);
            var downloadHint=Ui.Text("点击打开 GitHub 仓库，获取最新版本、使用说明与更新。",11,Ui.Muted); downloadHint.TextWrapping=TextWrapping.Wrap; panel.Children.Add(downloadHint);
            panel.Children.Add(new Border { Height=1,Background=Ui.Line,Margin=new Thickness(0,19,0,19) });
            var contact=Ui.Text("联系作者",16,Ui.Ink); contact.FontWeight=FontWeights.SemiBold; panel.Children.Add(contact);
            var author=Ui.Text(Product.Author,14,Ui.Green); author.Name="AuthorContacts"; author.LineHeight=28; author.Margin=new Thickness(0,10,0,15); panel.Children.Add(author);
            var hint=Ui.Text("在对应平台搜索以上名称，即可找到作者。",11,Ui.Muted); panel.Children.Add(hint);
            KeyDown+=(s,e)=> { if(e.Key==System.Windows.Input.Key.Escape) Close(); };
        }
    }
    internal sealed class SettingsWindow : Window {
        readonly TextBlock status;
        bool updating;
        Hotkey captureDraft,revealDraft;
        Button captureKey,revealKey;
        IDisposable recordingPause;
        int recording;
        public SettingsWindow(Library data,StartupRegistration startup,Action changed,Func<Hotkey,Hotkey,string> applyShortcuts=null,Func<IDisposable> suspendShortcuts=null) {
            Title="晾图 · 设置"; Width=590; Height=800; MinWidth=550; MinHeight=480; ResizeMode=ResizeMode.CanResize; Background=Ui.Paper; WindowStartupLocation=WindowStartupLocation.CenterScreen;
            var panel=new StackPanel { Margin=new Thickness(28,22,28,20) }; Content=new ScrollViewer { Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
            var title=Ui.Text("按你的习惯晾图",24,Ui.Ink); title.Margin=new Thickness(0,0,0,16); panel.Children.Add(title);
            status=Ui.Text("设置会保存在本机。",11,Ui.Green); status.TextWrapping=TextWrapping.Wrap;
            bool enabled=false; try { enabled=startup.Enabled; if(enabled && !startup.CurrentLocation) status.Text="启动项指向旧位置。关闭后重新开启，将更新到本版本。"; } catch(Exception ex) { status.Text="无法读取自启动项："+ex.Message; }
            var start=Switch(panel,"开机自启动","登录 Windows 后自动运行；默认关闭。",enabled);
            start.Name="StartupSwitch";
            start.Click+=(s,e)=> {
                if(updating) return;
                try { startup.Set(start.IsChecked==true); status.Text=start.IsChecked==true?"已开启。请保留当前程序所在的文件夹。":"已关闭开机自启动。"; }
                catch(Exception ex) { updating=true; start.IsChecked=!(start.IsChecked==true); updating=false; status.Text="设置未生效："+ex.Message; }
            };
            var notice=Switch(panel,"启动时显示右下角提示","每次打开都告诉你已经启动，不抢键盘焦点。",data.NotifyStartup);
            notice.Name="NoticeSwitch"; notice.Click+=(s,e)=>Save(data,changed,notice,data.NotifyStartup,()=>data.NotifyStartup=notice.IsChecked==true);
            var clipboard=Switch(panel,"接住剪贴板中的图片","可配合 Win + Shift + S；也会接住其他复制的图片。",data.WatchClipboard);
            clipboard.Click+=(s,e)=>Save(data,changed,clipboard,data.WatchClipboard,()=>data.WatchClipboard=clipboard.IsChecked==true);
            var fullscreen=Switch(panel,"全屏时自动避让","全屏时隐藏细线；用下方的展开快捷键临时显示。",data.AvoidFullscreen);
            fullscreen.Click+=(s,e)=>Save(data,changed,fullscreen,data.AvoidFullscreen,()=>data.AvoidFullscreen=fullscreen.IsChecked==true);
            var sway=Switch(panel,"截图轻轻飘动","围绕夹子缓慢摆动，悬停时停下并高亮。",data.AnimateShots);
            sway.Click+=(s,e)=> { bool previous=data.AnimateShots; try { data.AnimateShots=sway.IsChecked==true; data.SaveSettings(); changed(); status.Text="设置已保存。"; } catch(Exception ex) { data.AnimateShots=previous; sway.IsChecked=previous; status.Text="设置未保存："+ex.Message; } };
            panel.Children.Add(new Border { Height=1,Background=Ui.Line,Margin=new Thickness(0,12,0,12) });
            panel.Children.Add(Ui.Text("快捷键",16,Ui.Ink)); captureDraft=data.CaptureShortcut; revealDraft=data.RevealShortcut;
            captureKey=Ui.Button(captureDraft.Display,()=>BeginRecording(1,suspendShortcuts)); captureKey.Name="CaptureKeyButton";
            revealKey=Ui.Button(revealDraft.Display,()=>BeginRecording(2,suspendShortcuts)); revealKey.Name="RevealKeyButton";
            KeyRow(panel,"区域截图",captureKey); KeyRow(panel,"展开晾图绳",revealKey);
            var keyActions=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,7,0,7) };
            var apply=Ui.Button("应用快捷键",()=> { StopRecording(); string error=applyShortcuts==null?"请通过晾图绳打开设置。":applyShortcuts(captureDraft,revealDraft); if(error==null) status.Text="快捷键已应用，下次启动仍然有效。"; else { captureDraft=data.CaptureShortcut; revealDraft=data.RevealShortcut; UpdateKeyButtons(); status.Text=error; } }); apply.Name="ApplyKeysButton"; apply.Background=Ui.B("#E5E9DF"); keyActions.Children.Add(apply);
            keyActions.Children.Add(Ui.Button("恢复默认",()=> { StopRecording(); captureDraft=new Hotkey(3,0x53); revealDraft=new Hotkey(3,0x4C); UpdateKeyButtons(); status.Text="已填入默认组合，请点击应用。"; })); panel.Children.Add(keyActions);
            var shortcutHint=Ui.Text("点击组合键按钮后按键录制，Ctrl / Alt 可配合 Shift。\n支持字母、数字和 F1–F24；Esc 取消录制。",11,Ui.Muted); shortcutHint.LineHeight=21; shortcutHint.Margin=new Thickness(0,3,0,15); panel.Children.Add(shortcutHint); panel.Children.Add(status);
            PreviewKeyDown+=(s,e)=> {
                if(recording!=0) {
                    e.Handled=true;
                    var key=e.Key==System.Windows.Input.Key.System?e.SystemKey:e.Key;
                    if(key==System.Windows.Input.Key.Escape) { StopRecording(); status.Text="已取消录制。"; return; }
                    var candidate=new Hotkey((uint)System.Windows.Input.Keyboard.Modifiers,(uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key));
                    RecordShortcut(candidate);
                } else if(e.Key==System.Windows.Input.Key.Escape) Close();
            };
            Deactivated+=(s,e)=>StopRecording(); Closed+=(s,e)=>StopRecording();
        }
        void BeginRecording(int target,Func<IDisposable> suspend) {
            StopRecording(); recording=target; if(suspend!=null) recordingPause=suspend();
            var button=target==1?captureKey:revealKey; button.Content="按下组合键…"; button.Focus(); status.Text="正在录制，原有快捷键暂时停用。Esc 取消。";
        }
        internal bool IsRecording { get { return recording!=0; } }
        internal void RecordShortcut(Hotkey candidate) { if(recording==0 || !candidate.Valid) return; if(recording==1) captureDraft=candidate; else revealDraft=candidate; StopRecording(); status.Text="组合键已录制，请点击应用。"; }
        void StopRecording() { recording=0; if(recordingPause!=null) { recordingPause.Dispose(); recordingPause=null; } UpdateKeyButtons(); }
        void UpdateKeyButtons() { if(captureKey!=null) captureKey.Content=captureDraft.Display; if(revealKey!=null) revealKey.Content=revealDraft.Display; }
        static void KeyRow(StackPanel panel,string title,Button button) { var row=new DockPanel { Margin=new Thickness(0,6,0,0) }; DockPanel.SetDock(button,Dock.Right); row.Children.Add(button); row.Children.Add(Ui.Text(title,13,Ui.Ink)); panel.Children.Add(row); }
        void Save(Library data,Action changed,CheckBox toggle,bool previous,Action set) {
            bool oldNotice=data.NotifyStartup,oldClipboard=data.WatchClipboard,oldFullscreen=data.AvoidFullscreen;
            try { set(); data.SaveSettings(); changed(); status.Text="设置已保存。"; }
            catch(Exception ex) { data.NotifyStartup=oldNotice; data.WatchClipboard=oldClipboard; data.AvoidFullscreen=oldFullscreen; toggle.IsChecked=previous; status.Text="设置未保存："+ex.Message; }
        }
        static CheckBox Switch(StackPanel panel,string label,string description,bool value) {
            var box=Ui.Switch(label,value); panel.Children.Add(box);
            var hint=Ui.Text(description,11,Ui.Muted); hint.Margin=new Thickness(0,0,0,9); hint.TextWrapping=TextWrapping.Wrap; panel.Children.Add(hint); return box;
        }
    }
}
