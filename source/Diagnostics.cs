using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Airline {
    internal static class RuntimeTest {
        public static int Run(string root) {
            Directory.CreateDirectory(root); var log=new StringBuilder(); int exit=1;
            var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
            try {
                var data=new Library(Path.Combine(root,"runtime-"+Guid.NewGuid().ToString("N")));
                data.WatchClipboard=false; data.NotifyStartup=false; data.AvoidFullscreen=false; data.SaveSettings();
                var shelf=new Shelf(data,false,true); app.MainWindow=shelf;
                shelf.Show(); shelf.Left=-30000; shelf.Top=-30000;
                var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(350) };
                int stage=0;
                timer.Tick+=(s,e)=> {
                    try {
                        if(stage++==0) { shelf.Expand(); return; }
                        if(stage==2) {
                            if(!shelf.IsExpanded || !shelf.IsEnabled) throw new Exception("Native shelf did not expand with input enabled");
                            shelf.UpdateHover(false,false,DateTime.UtcNow.AddSeconds(2)); return;
                        }
                        if(shelf.IsExpanded || shelf.Height!=6 || !Native.IsWindowEnabled(new WindowInteropHelper(shelf).Handle)) throw new Exception("Native shelf did not collapse and remain enabled");
                        log.AppendLine("PASS Production initialization, HWND hook, tray, native hover timer, expand/collapse, and enabled input");
                        IntPtr reply; var acknowledgement=Native.SendMessageTimeout(new WindowInteropHelper(shelf).Handle,Native.RunningMessage,IntPtr.Zero,IntPtr.Zero,2,500,out reply);
                        if(acknowledgement==IntPtr.Zero || reply!=new IntPtr(1)) throw new Exception("Repeated-launch notification message was not acknowledged");
                        log.AppendLine("PASS Repeated-launch notification message acknowledgement");
                        log.AppendLine("Clipboard monitoring was disabled and no user screenshots were read or captured."); exit=0;
                    } catch(Exception ex) { log.AppendLine("FAIL "+ex); }
                    timer.Stop(); shelf.Quit();
                }; timer.Start(); app.Run();
            } catch(Exception ex) { log.AppendLine("FAIL "+ex); app.Shutdown(); }
            File.WriteAllText(Path.Combine(root,"runtime-results.txt"),log.ToString()); return exit;
        }
    }
    internal static class SelfTest {
        public static int Run(string root) {
            var log=new StringBuilder(); int checks=0;
            Action<bool,string> check=(condition,name)=> { if(!condition) throw new Exception(name); checks++; log.AppendLine("PASS "+name); };
            Directory.CreateDirectory(root);
            try {
                string isolated=Path.Combine(root,"run-"+Guid.NewGuid().ToString("N"));
                var library=new Library(isolated);
                check(library.Shots.Count==0,"New library is empty");
                var image=Preview.Sample(0);
                var first=library.Add(image); var second=library.Add(image);
                check(first.Path!=second.Path && library.Shots.Count==2,"Identical screenshots remain separate items");
                check(Directory.GetFiles(library.Images,"*.part").Length==0,"Successful atomic save leaves no partial file");
                var loaded=Library.Load(first.Path,0);
                check(loaded.PixelWidth==image.PixelWidth && loaded.PixelHeight==image.PixelHeight,"PNG preserves original dimensions");
                byte[] before=new byte[image.PixelWidth*image.PixelHeight*4],after=new byte[before.Length];
                image.CopyPixels(before,image.PixelWidth*4,0); loaded.CopyPixels(after,loaded.PixelWidth*4,0);
                check(before.SequenceEqual(after),"PNG round-trip preserves pixel data");
                string stage=library.Stage(first); string export=Path.Combine(isolated,"saved-copy.png"); File.Move(stage,export);
                check(File.Exists(first.Path) && File.Exists(export),"Moving an outgoing copy leaves original in library");
                var reopened=new Library(isolated); check(reopened.Shots.Count==2,"Restart restores all screenshots");
                reopened.WatchClipboard=false; reopened.SaveSettings(); check(!new Library(isolated).WatchClipboard,"Clipboard preference persists");
                reopened.NotifyStartup=false; reopened.AvoidFullscreen=false; reopened.SaveSettings();
                var configured=new Library(isolated); check(!configured.NotifyStartup && !configured.AvoidFullscreen && !configured.WatchClipboard,"All three preferences persist without resetting one another");
                File.WriteAllText(Path.Combine(isolated,"settings.txt"),"clipboard=off");
                configured=new Library(isolated); check(!configured.WatchClipboard && configured.NotifyStartup && configured.AvoidFullscreen,"Legacy settings migrate with sensible new-feature defaults");
                reopened.NotifyStartup=true; reopened.AvoidFullscreen=true; reopened.SaveSettings();
                string exported=Library.Export(reopened.Shots.ToArray(),Path.Combine(isolated,"exports"));
                check(Directory.GetFiles(exported,"*.png").Length==2 && File.Exists(first.Path) && File.ReadAllBytes(Path.Combine(exported,first.Name)).SequenceEqual(File.ReadAllBytes(first.Path)),"Bulk export copies all PNGs losslessly and retains originals");
                string exportedAgain=Library.Export(reopened.Shots.ToArray(),Path.Combine(isolated,"exports"));
                check(exported!=exportedAgain,"Repeated exports create separate folders without overwriting");
                var startupStore=new MemoryStartupStore(); var startup=new StartupRegistration(startupStore,"C:\\Program Files\\晾图\\Airline.exe");
                check(!startup.Enabled && startupStore.Writes==0,"Startup is off until user explicitly enables it");
                startup.Set(true); check(startup.Enabled && startup.CurrentLocation && startupStore.Value=="\"C:\\Program Files\\晾图\\Airline.exe\" --autostart","Startup switch enables a correctly quoted executable path");
                startup.Set(false); check(!startup.Enabled && startupStore.Value==null,"Startup switch removes only its own value on disable");
                bool rejected=false; try { new StartupRegistration(new MemoryStartupStore(),"C:\\"+new string('x',270)+"\\Airline.exe").Set(true); } catch(InvalidOperationException) { rejected=true; }
                check(rejected,"Startup rejects command lines beyond Windows Run key limit");
                CheckHotkeys(check);
                reopened.CaptureShortcut=new Hotkey(7,0x4B); reopened.RevealShortcut=new Hotkey(3,0x4A); reopened.AnimateShots=false; reopened.SaveSettings();
                var shortcutsRestored=new Library(isolated);
                check(shortcutsRestored.CaptureShortcut.Equals(reopened.CaptureShortcut) && shortcutsRestored.RevealShortcut.Equals(reopened.RevealShortcut) && !shortcutsRestored.AnimateShots,"Custom shortcuts and sway preference persist across restart");
                reopened.AnimateShots=true; reopened.SaveSettings();
                check(Native.CoversScreen(new Native.RECT { Left=0,Top=0,Right=1920,Bottom=1080 },new System.Drawing.Rectangle(0,0,1920,1080)) && !Native.CoversScreen(new Native.RECT { Left=0,Top=0,Right=1920,Bottom=1040 },new System.Drawing.Rectangle(0,0,1920,1080)),"Fullscreen detection distinguishes monitor bounds from work area");
                // Simulate a write failure. No source items should be lost or partially added.
                int originalCount=reopened.Shots.Count; bool failed=false;
                try { reopened.Add(null); } catch { failed=true; }
                check(failed && reopened.Shots.Count==originalCount && Directory.GetFiles(reopened.Images,"*.part").Length==0,"Failed write preserves library and removes partial data");
                var selection=CaptureOverlay.Selection(new Point(300,200),new Point(-8,40),240,180);
                check(selection==new Rect(0,40,240,140),"Selection clamps reverse drags to desktop bounds");
                var trash=new DataObject(); trash.SetData("TargetCLSID",new MemoryStream(new Guid("645FF040-5081-101B-9F08-00AA002F954E").ToByteArray()));
                check(Shelf.IsRecycleTarget(trash),"Shell recycle-bin identity is recognized");
                var folder=new DataObject(); folder.SetData("TargetCLSID",new MemoryStream(Guid.NewGuid().ToByteArray()));
                check(!Shelf.IsRecycleTarget(folder) && !Shelf.IsRecycleTarget(new DataObject()),"Other drag targets do not delete originals");
                var shelf=new Shelf(reopened,true); var panel=(FrameworkElement)shelf.Content; panel.Measure(new Size(1280,338)); panel.Arrange(new Rect(0,0,1280,338)); panel.UpdateLayout();
                check(panel.ActualWidth==1280,"Native shelf content lays out at target width");
                IntPtr handle=new System.Windows.Interop.WindowInteropHelper(shelf).EnsureHandle();
                check(handle!=IntPtr.Zero && (Native.GetWindowLong(handle,-20)&0x80)!=0,"Native WPF window initializes as a tool window");
                shelf.Close();
                var annotation=new Canvas { Width=350,Height=230,Background=Brushes.White };
                var mark=new System.Windows.Shapes.Rectangle { Width=20,Height=20,Fill=Brushes.Red }; Canvas.SetLeft(mark,10); Canvas.SetTop(mark,10); annotation.Children.Add(mark);
                annotation.Measure(new Size(350,230)); annotation.Arrange(new Rect(0,0,350,230)); annotation.UpdateLayout();
                var annotated=Editor.Compose(annotation,700,460); byte[] red=new byte[4]; annotated.CopyPixels(new Int32Rect(30,30,1,1),red,4,0);
                check(annotated.PixelWidth==700 && annotated.PixelHeight==460 && red[2]==255 && red[1]==0,"Annotation export maps display coordinates to original pixels");
                CheckInteractions(reopened,first,check);
                log.AppendLine("\n"+checks+" checks passed. No real user screenshots or clipboard were accessed.");
                File.WriteAllText(Path.Combine(root,"test-results.txt"),log.ToString()); Console.Write(log.ToString()); return 0;
            } catch(Exception ex) { log.AppendLine("FAIL "+ex); File.WriteAllText(Path.Combine(root,"test-results.txt"),log.ToString()); Console.Write(log.ToString()); return 1; }
        }
        sealed class MemoryStartupStore : IStartupStore {
            internal string Value; internal int Writes; internal bool FailWrites;
            public string Read() { return Value; } public void Write(string command) { if(FailWrites) throw new UnauthorizedAccessException("Test access denied"); Value=command; Writes++; } public void Delete() { Value=null; }
        }
        sealed class MemoryHotkeys : IHotkeyBackend {
            internal readonly System.Collections.Generic.Dictionary<int,Hotkey> Keys=new System.Collections.Generic.Dictionary<int,Hotkey>();
            internal readonly System.Collections.Generic.HashSet<Hotkey> Blocked=new System.Collections.Generic.HashSet<Hotkey>();
            public bool Register(int id,Hotkey value) { if(Blocked.Contains(value) || Keys.Values.Any(key=>key.Equals(value))) return false; Keys[id]=value; return true; }
            public void Unregister(int id) { Keys.Remove(id); }
        }
        static void CheckHotkeys(Action<bool,string> check) {
            var oldC=new Hotkey(3,0x53); var oldR=new Hotkey(3,0x4C); var backend=new MemoryHotkeys(); var service=new HotkeyService(backend,oldC,oldR); service.Start();
            check(service.CaptureRegistered && service.RevealRegistered,"Both initial shortcuts register");
            string duplicate=service.Apply(oldC,oldC,()=>{}); check(duplicate!=null && backend.Keys[1].Equals(oldC) && backend.Keys[2].Equals(oldR),"Duplicate shortcuts are rejected without disturbing registrations");
            var newC=new Hotkey(7,0x58); var newR=new Hotkey(3,0x59); backend.Blocked.Add(newR);
            string conflict=service.Apply(newC,newR,()=>{}); check(conflict!=null && backend.Keys[1].Equals(oldC) && backend.Keys[2].Equals(oldR),"Shortcut conflict rolls both registrations back"); backend.Blocked.Clear();
            string saveError=service.Apply(newC,newR,()=> { throw new IOException("Failed to persist"); });
            check(saveError!=null && service.Capture.Equals(oldC) && backend.Keys[2].Equals(oldR),"Preference write failure restores original shortcuts");
            bool saved=false; check(service.Apply(oldR,oldC,()=>saved=true)==null && saved && backend.Keys[1].Equals(oldR) && backend.Keys[2].Equals(oldC),"Capture and reveal combinations can be swapped atomically");
            using(service.Suspend()) check(service.Suspended && backend.Keys.Count==0,"Recording temporarily unregisters shortcuts to prevent accidental capture");
            check(!service.Suspended && backend.Keys.Count==2,"Ending recording restores shortcuts");
            check(!new Hotkey(0,0x53).Valid && !new Hotkey(4,0x53).Valid && new Hotkey(6,0x70).Valid,"Shortcut validation requires Ctrl or Alt and accepts function keys");
            check(Hotkey.Parse("999:0",oldC).Equals(oldC),"Malformed stored shortcuts fall back safely"); service.Stop();
        }
        static void Pump(int milliseconds) {
            var frame=new DispatcherFrame();
            var timer=new DispatcherTimer(DispatcherPriority.Background) { Interval=TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick+=(s,e)=> { timer.Stop(); frame.Continue=false; }; timer.Start(); Dispatcher.PushFrame(frame);
        }
        static T Find<T>(DependencyObject parent,string name) where T:FrameworkElement {
            var found=parent as T; if(found!=null && found.Name==name) return found;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) { var child=Find<T>(VisualTreeHelper.GetChild(parent,i),name); if(child!=null) return child; }
            return null;
        }
        static void CheckInteractions(Library library,Shot shot,Action<bool,string> check) {
            // Use real HWNDs and the real WPF dispatcher, off screen, with isolated test images.
            var shelf=new Shelf(library,true) { Left=-30000,Top=-30000,ShowActivated=false };
            try {
                shelf.Show(); Pump(260); shelf.UpdateLayout();
                var hanging=Find<HangingCard>(shelf,"HangingScreenshot");
                check(hanging.SwayActive,"Displayed screenshots gently sway around their peg");
                hanging.SetSelected(true); Pump(180);
                check(hanging.Selected && !hanging.SwayActive && Find<Border>(hanging,"ShotPaper").BorderBrush==Ui.Green && Panel.GetZIndex(hanging)==10,"Hovered screenshot pauses sway, gains outline, and lifts above others");
                var hoverPreview=new RenderTargetBitmap(1280,338,96,96,PixelFormats.Pbgra32); hoverPreview.Render((Visual)shelf.Content); Library.Encode(hoverPreview,Path.Combine(library.Root,"hover-preview.png"));
                hanging.SetSelected(false); Pump(180); check(hanging.SwayActive && !hanging.Selected && Panel.GetZIndex(hanging)==0,"Leaving screenshot restores sway and normal visual state");
                foreach(string name in new[]{"CaptureButton","FolderButton","MenuButton","SettingsButton","AboutButton"}) {
                    var button=Find<Button>(shelf,name);
                    var point=button.TranslatePoint(new Point(button.ActualWidth/2,button.ActualHeight/2),shelf);
                    DependencyObject hit=shelf.InputHitTest(point) as DependencyObject;
                    while(hit!=null && hit!=button) hit=VisualTreeHelper.GetParent(hit);
                    check(hit==button && button.IsEnabled,"Header button accepts hit testing: "+name);
                }
                var now=DateTime.UtcNow; shelf.UpdateHover(false,true,now);
                shelf.UpdateHover(false,false,now.AddMilliseconds(200)); check(shelf.IsExpanded,"Brief pointer departure keeps shelf open");
                shelf.UpdateHover(false,false,now.AddMilliseconds(600)); Pump(230);
                check(!shelf.IsExpanded && shelf.Height==6,"Pointer departure collapses native shelf after animation");
                check(!hanging.SwayActive,"Hidden shelf stops screenshot animation clocks");
                shelf.UpdateHover(true,true,DateTime.UtcNow); Pump(270); check(shelf.IsExpanded,"Top edge reopens shelf after collapse");
                Find<Button>(shelf,"MenuButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(80);
                check(shelf.IsMenuOpen,"Header menu click opens an anchored context menu");
                shelf.CloseMenus(); Pump(80);
                now=DateTime.UtcNow; shelf.UpdateHover(false,false,now.AddMilliseconds(600)); Pump(230);
                check(!shelf.IsMenuOpen && !shelf.IsExpanded,"Closing menu releases auto-hide suppression");
                foreach(Window dialog in new Window[]{new Editor(shot,library)}) {
                    shelf.Expand(); Pump(250);
                    dialog.WindowStartupLocation=WindowStartupLocation.Manual; dialog.Left=-30000; dialog.Top=-30000; dialog.ShowActivated=false;
                    bool inspected=false,correct=false;
                    var closer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(140) };
                    closer.Tick+=(s,e)=> {
                        closer.Stop(); inspected=true;
                        var ownerHandle=new WindowInteropHelper(shelf).Handle;
                        correct=dialog.Owner==shelf && dialog.Topmost && !shelf.IsExpanded && shelf.Height==6 && shelf.IsBusy && !Native.IsWindowEnabled(ownerHandle);
                        dialog.Close();
                    };
                    closer.Start(); shelf.ShowOwnedDialog(dialog);
                    check(inspected && correct,"Modal dialog has owner, stays above shelf, and hides drawer: "+dialog.GetType().Name);
                    check(!shelf.IsBusy && Native.IsWindowEnabled(new WindowInteropHelper(shelf).Handle),"Closing dialog restores native window input: "+dialog.GetType().Name);
                    shelf.Expand(); Pump(250); now=DateTime.UtcNow; shelf.UpdateHover(false,false,now.AddMilliseconds(600)); Pump(230);
                    check(!shelf.IsExpanded && shelf.Height==6,"Auto-hide still works after modal closes: "+dialog.GetType().Name);
                }
                var preview=shelf.OpenPreview(shot); Pump(100);
                check(preview!=null && preview.IsVisible && !preview.Topmost && !shelf.IsBusy && Native.IsWindowEnabled(new WindowInteropHelper(shelf).Handle),"Modeless preview does not disable or lock shelf");
                shelf.UpdateHover(true,true,DateTime.UtcNow); Pump(250);
                check(shelf.IsExpanded && preview.IsVisible,"Shelf opens at top edge while preview remains open");
                now=DateTime.UtcNow; shelf.UpdateHover(false,false,now.AddMilliseconds(600)); Pump(230);
                check(!shelf.IsExpanded && preview.IsVisible,"Shelf auto-hides while preview remains open");
                preview.WindowState=WindowState.Minimized; shelf.UpdateHover(true,true,DateTime.UtcNow); Pump(250);
                check(shelf.IsExpanded && !shelf.IsBusy,"Minimized preview does not lock shelf");
                var reused=shelf.OpenPreview(shot);
                check(object.ReferenceEquals(preview,reused) && preview.WindowState!=WindowState.Minimized,"Opening same screenshot reuses and restores preview");
                preview.Close(); shelf.UpdateHover(true,true,DateTime.UtcNow); Pump(250);
                check(shelf.IsExpanded && Native.IsWindowEnabled(new WindowInteropHelper(shelf).Handle),"Preview close leaves shelf responsive");

                Native.POINT cursor=new Native.POINT { X=-29000,Y=-29000 }; DragGhost floating;
                using(floating=new DragGhost(shot,()=>cursor)) {
                    floating.Start(); Pump(80);
                    check(floating.IsVisible && floating.IsFollowing && floating.Picture.Source!=null,"Drag image contains original screenshot and follows cursor");
                    int style=Native.GetWindowLong(new WindowInteropHelper(floating).Handle,-20);
                    check((style&0x20)!=0 && (style&0x08000000)!=0 && !floating.ShowActivated && !floating.IsHitTestVisible,"Drag image is transparent to clicks and cannot activate");
                    cursor=new Native.POINT { X=-28000,Y=-27000 }; Pump(80);
                    Native.RECT bounds; Native.GetWindowRect(new WindowInteropHelper(floating).Handle,out bounds);
                    check(bounds.Left==cursor.X+16 && bounds.Top==cursor.Y+20,"Drag image follows physical cursor coordinates via timer");
                    var surface=(FrameworkElement)floating.Content; surface.UpdateLayout();
                    var image=new RenderTargetBitmap((int)Math.Ceiling(floating.Width),(int)Math.Ceiling(floating.Height),96,96,PixelFormats.Pbgra32); image.Render(surface);
                    byte[] pixels=new byte[image.PixelWidth*image.PixelHeight*4]; image.CopyPixels(pixels,image.PixelWidth*4,0);
                    check(pixels.Where((value,index)=>index%4==3).Any(alpha=>alpha>0),"Drag image actually renders visible pixels");
                    Library.Encode(image,Path.Combine(library.Root,"drag-ghost.png"));
                }
                check(!floating.IsVisible && !floating.IsFollowing,"Drag image closes and timer stops after disposal");
                var placed=DragGhost.Position(new Native.POINT { X=1919,Y=1079 },new Native.RECT { Right=196,Bottom=180 },new System.Drawing.Rectangle(0,0,1920,1080));
                check(placed.X>=0 && placed.X+196<=1920 && placed.Y>=0 && placed.Y+180<=1080,"Drag image flips before clipping at screen right and bottom edges");
                bool dragInspected=false,dragVisible=false;
                var source=Find<Button>(shelf,"CaptureButton");
                QueryContinueDragEventHandler cancel=(s,e)=> { dragInspected=true; dragVisible=shelf.CurrentDragGhost!=null && shelf.CurrentDragGhost.IsVisible && shelf.CurrentDragGhost.Picture.Source!=null; e.Action=DragAction.Cancel; e.Handled=true; };
                source.QueryContinueDrag+=cancel;
                // OLE waits for an input message before querying the source. Post Esc
                // only to the isolated test HWND; do not synthesize global user input.
                IntPtr testHandle=new WindowInteropHelper(shelf).Handle;
                using(var cancelTimer=new System.Threading.Timer(unused=>Native.PostMessage(testHandle,0x100,new IntPtr(27),IntPtr.Zero),null,150,150)) {
                    try { shelf.Drag(shot,source); } finally { source.QueryContinueDrag-=cancel; }
                }
                check(dragInspected && dragVisible,"Actual OLE drag starts with a visible screenshot floating above source");
                check(shelf.CurrentDragGhost==null && !shelf.IsBusy && File.Exists(shot.Path),"Canceled OLE drag removes ghost, releases busy state, and retains original");
                var about=shelf.OpenAbout(); Pump(80);
                check(Find<TextBlock>(about,"AuthorContacts").Text=="微信公众号：景同志\n小红书：景同学" && !shelf.IsBusy,"About contains exact author contacts and does not lock shelf");
                var linkBlock=Find<TextBlock>(about,"RepositoryLink");
                var link=linkBlock==null?null:linkBlock.Inlines.OfType<System.Windows.Documents.Hyperlink>().FirstOrDefault();
                check(link!=null && link.NavigateUri.ToString()==Product.Repository && link.Inlines.OfType<System.Windows.Documents.Run>().Any(run=>run.Text==Product.RepositoryLabel),"About shows the GitHub repository address");
                string opened=null; var previousOpen=AboutWindow.OpenLink;
                try { AboutWindow.OpenLink=url=>opened=url; if(link!=null) link.RaiseEvent(new System.Windows.Navigation.RequestNavigateEventArgs(link.NavigateUri,"about")); }
                finally { AboutWindow.OpenLink=previousOpen; }
                check(opened==Product.Repository,"About repository link opens the GitHub address without side effects");
                check(!TextContents(about).Any(text=>text.Contains("Tendedero") || text.Contains("灵感来自")),"About does not contain inspiration attribution");
                shelf.UpdateHover(true,true,DateTime.UtcNow); Pump(250); check(shelf.IsExpanded && about.IsVisible,"About remains open while shelf can expand"); about.Close();
                using(var noSideEffects=new FakeSettingsScope()) {
                    var settings=new SettingsWindow(library,noSideEffects.Startup,()=>{}) { WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000,ShowActivated=false };
                    settings.Show(); Pump(80); var start=Find<CheckBox>(settings,"StartupSwitch"); start.IsChecked=true; start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    check(noSideEffects.Startup.Enabled,"Settings checkbox invokes startup registration");
                    start.IsChecked=false; start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); check(!noSideEffects.Startup.Enabled,"Settings checkbox disables startup registration"); settings.Close();
                }
                var failingStore=new MemoryStartupStore { FailWrites=true };
                var failureSettings=new SettingsWindow(library,new StartupRegistration(failingStore,"C:\\Test\\Airline.exe"),()=>{}) { WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000,ShowActivated=false };
                failureSettings.Show(); var failStart=Find<CheckBox>(failureSettings,"StartupSwitch"); failStart.IsChecked=true; failStart.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                check(failStart.IsChecked==false && failingStore.Value==null,"Startup registration failure reverts switch and leaves startup off");
                string blocker=Path.Combine(library.Root,"settings.txt.tmp"); Directory.CreateDirectory(blocker);
                var failNotice=Find<CheckBox>(failureSettings,"NoticeSwitch"); failNotice.IsChecked=false; failNotice.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                check(library.NotifyStartup && failNotice.IsChecked==true,"Preference write failure restores both model and visible switch"); Directory.Delete(blocker); failureSettings.Close();
                var keyBackend=new MemoryHotkeys(); var keyService=new HotkeyService(keyBackend,library.CaptureShortcut,library.RevealShortcut); keyService.Start();
                var keySettings=new SettingsWindow(library,new StartupRegistration(new MemoryStartupStore(),"C:\\Test\\Airline.exe"),()=>{},(capture,reveal)=>keyService.Apply(capture,reveal,()=> { library.CaptureShortcut=capture; library.RevealShortcut=reveal; library.SaveSettings(); }),()=>keyService.Suspend()) { WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000,ShowActivated=false };
                keySettings.Show(); Find<Button>(keySettings,"CaptureKeyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(keySettings.IsRecording && keyService.Suspended,"Settings shortcut button enters recording without triggering screenshot");
                var recorded=new Hotkey(7,0x58); keySettings.RecordShortcut(recorded); Find<Button>(keySettings,"ApplyKeysButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(!keySettings.IsRecording && keyBackend.Keys[1].Equals(recorded) && new Library(library.Root).CaptureShortcut.Equals(recorded),"Recorded shortcut applies and persists from settings button"); keySettings.Close(); keyService.Stop();
                int reveals=0; var notice=new StartupNotice(library.Shots.Count,()=>reveals++,false,true); notice.Show(); Pump(80);
                check(notice.IsVisible && !notice.ShowActivated && notice.Topmost,"Startup notice is visible above windows without stealing activation");
                Find<Button>(notice,"RevealButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); check(reveals==1 && !notice.IsVisible,"Startup notice button invokes reveal and dismisses notice");
                shelf.ShowStartupNotice(false); var firstNotice=shelf.ActiveStartupNotice;
                shelf.ShowStartupNotice(false); var secondNotice=shelf.ActiveStartupNotice;
                check(firstNotice!=secondNotice && !firstNotice.IsVisible && secondNotice.IsVisible,"Startup notice is shown on every launch, rather than just the first run");
                shelf.ShowStartupNotice(true);
                check(Find<TextBlock>(shelf.ActiveStartupNotice,"NoticeTitle").Text=="晾图正在运行","Repeated-launch notification explicitly says the app is running"); shelf.ActiveStartupNotice.Close();
                shelf.ApplyFullscreenPolicy(true,DateTime.UtcNow); check(!shelf.IsVisible && !shelf.IsExpanded,"Fullscreen policy hides shelf and edge strip");
                shelf.ApplyFullscreenPolicy(false,DateTime.UtcNow); check(shelf.IsVisible && shelf.Height==6,"Leaving fullscreen restores collapsed shelf");
                shelf.ApplyFullscreenPolicy(true,DateTime.UtcNow); shelf.Reveal(); check(shelf.IsVisible && shelf.IsExpanded && !shelf.ApplyFullscreenPolicy(true,DateTime.UtcNow),"Explicit reveal overrides fullscreen avoidance temporarily");
            } finally { shelf.CloseMenus(); shelf.Close(); }
        }
        sealed class FakeSettingsScope : IDisposable {
            internal readonly StartupRegistration Startup=new StartupRegistration(new MemoryStartupStore(),"C:\\Test\\Airline.exe");
            public void Dispose() { }
        }
        static System.Collections.Generic.IEnumerable<string> TextContents(DependencyObject root) {
            var text=root as TextBlock; if(text!=null) yield return text.Text;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(string child in TextContents(VisualTreeHelper.GetChild(root,i))) yield return child;
        }
    }
    internal static class Preview {
        static void Text(DrawingContext context,string text,double x,double y,double size,Brush color) {
            var formatted=new FormattedText(text,System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,color,1.0);
            context.DrawText(formatted,new Point(x,y));
        }
        public static BitmapSource Sample(int index) {
            var visual=new DrawingVisual();
            using(var c=visual.RenderOpen()) {
                Brush bg=Ui.B(new[]{"#ECEFE8","#F0E5D7","#263C36","#EFE9E2","#E4E9EB","#E8EAD9"}[index%6]); c.DrawRectangle(bg,null,new Rect(0,0,700,460));
                if(index==0) {
                    Text(c,"STUDIO / NOTES",38,28,18,Ui.Green); Text(c,"Small things,",38,118,49,Ui.Ink); Text(c,"well considered.",38,178,49,Ui.Ink);
                    c.DrawRectangle(Ui.B("#D3DBC9"),null,new Rect(38,288,624,110)); Text(c,"把日常留一点空白。",62,322,22,Ui.Green);
                } else if(index==1) {
                    c.DrawEllipse(Ui.B("#B5825E"),null,new Point(463,139),64,64);
                    var mountain=Geometry.Parse("M 0,390 L 195,143 340,312 457,218 700,437 700,460 0,460 Z"); c.DrawGeometry(Ui.B("#788673"),null,mountain);
                    c.DrawGeometry(Ui.B("#435B50"),null,Geometry.Parse("M 0,460 L 193,333 328,390 478,292 700,434 700,460 Z")); Text(c,"A quieter afternoon.",34,33,23,Ui.Ink);
                } else if(index==2) {
                    Text(c,"PALETTE / 03",40,30,18,Ui.B("#B9CDB6")); Text(c,"Soft by nature",40,104,41,Ui.Paper);
                    string[] colors={"#F7F5EF","#C3CFB5","#7F9A7A","#D5AD86"}; for(int i=0;i<4;i++) { c.DrawRoundedRectangle(Ui.B(colors[i]),null,new Rect(40+i*160,218,140,168),12,12); Text(c,colors[i],51+i*160,351,16,Ui.Ink); }
                } else if(index==3) {
                    Text(c,"我的一周",40,30,30,Ui.Ink); Text(c,"留意那些微小的进展。",40,85,18,Ui.Muted);
                    string[] tasks={"整理灵感","散步，看看树","完成一个小设计"}; for(int i=0;i<3;i++) { c.DrawRoundedRectangle(Brushes.White,null,new Rect(40,147+i*88,620,66),8,8); c.DrawEllipse(Ui.Green,null,new Point(69,180+i*88),7,7); Text(c,tasks[i],94,165+i*88,23,Ui.Ink); }
                } else if(index==4) {
                    Text(c,"FIELD / JOURNAL",38,29,18,Ui.Muted); Text(c,"Room to think.",38,102,46,Ui.Ink);
                    for(int i=0;i<4;i++) c.DrawLine(new Pen(Ui.B("#ADBFB3"),2),new Point(40,222+i*47),new Point(656,222+i*47));
                    c.DrawRoundedRectangle(Ui.B("#BCCCBF"),null,new Rect(376,215,266,154),7,7); Text(c,"Ideas grow here.",392,266,22,Ui.Green);
                } else {
                    Text(c,"WEEKEND READING",38,31,18,Ui.Green); c.DrawRectangle(Ui.B("#899A76"),null,new Rect(38,98,190,315)); c.DrawRectangle(Ui.B("#D1AE82"),null,new Rect(260,129,181,284)); c.DrawRectangle(Ui.B("#B4BDA2"),null,new Rect(473,79,187,334));
                    Text(c,"SLOW",62,151,26,Brushes.White); Text(c,"LIVING",62,193,26,Brushes.White); Text(c,"THE",283,184,24,Ui.Ink); Text(c,"EVERYDAY",276,221,18,Ui.Ink); Text(c,"FIELD",495,124,25,Ui.Ink); Text(c,"NOTES",495,163,25,Ui.Ink);
                }
            }
            var bitmap=new RenderTargetBitmap(700,460,96,96,PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
        }
        public static int Render(string output) {
            try {
                var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
                string root=Path.Combine(Environment.CurrentDirectory,"work","preview-data-"+Guid.NewGuid().ToString("N"));
                var library=new Library(root); for(int i=5;i>=0;i--) library.Add(Sample(i));
                var shelf=new Shelf(library,true) { Width=1440 }; shelf.DrawShots();
                var panel=(FrameworkElement)shelf.Content; panel.Measure(new Size(1440,338)); panel.Arrange(new Rect(0,0,1440,338)); panel.UpdateLayout();
                var visual=new DrawingVisual();
                using(var c=visual.RenderOpen()) {
                    c.DrawRectangle(Ui.B("#ECEDE5"),null,new Rect(0,0,1440,930));
                    Text(c,"AIRLINE     /     晾图     /     WINDOWS",70,43,13,Ui.Green);
                    Text(c,"把截图，晾在屏幕边上。",70,99,48,Ui.Ink);
                    Text(c,"一根细线，接住每一个灵感。鼠标靠近，才轻轻展开。",73,173,17,Ui.Muted);
                    Text(c,"原生桌面小工具   ·   本机保存   ·   无需账户",1060,179,12,Ui.Muted);
                    c.PushTransform(new TranslateTransform(0,254)); c.DrawRectangle(new VisualBrush(panel),null,new Rect(0,0,1440,338)); c.Pop();
                    Text(c,"离开时，安静收起。",73,654,21,Ui.Ink);
                    Text(c,"不挤桌面，不抢注意力。截图退出后仍然保留。",73,696,14,Ui.Muted);
                    c.DrawLine(new Pen(Ui.B("#A4B39D"),2),new Point(814,679),new Point(1370,679)); c.DrawRoundedRectangle(Ui.Green,null,new Rect(1061,678,54,5),2,2);
                    Text(c,"鼠标靠近屏幕顶边  ↑",993,706,12,Ui.Muted);
                    string[] titles={"01  单击复制","02  长按标注","03  双击预览","04  拖走分享"};
                    string[] captions={"点一下，接着去粘贴。","标注另挂一份，保留原图。","放大看看刚刚的发现。","送出副本，原图还在绳上。"};
                    for(int i=0;i<4;i++) { double x=73+i*340; c.DrawLine(new Pen(Ui.Line,1),new Point(x,782),new Point(x+289,782)); Text(c,titles[i],x,807,17,Ui.Ink); Text(c,captions[i],x,846,13,Ui.Muted); }
                    Text(c,"界面中的截图为演示素材",1215,900,10,Ui.Muted);
                }
                var image=new RenderTargetBitmap(1440,930,96,96,PixelFormats.Pbgra32); image.Render(visual);
                if(File.Exists(output)) File.Delete(output); Library.Encode(image,output); app.Shutdown(); return 0;
            } catch(Exception ex) { File.WriteAllText(output+".error.txt",ex.ToString()); return 1; }
        }
    }
}
