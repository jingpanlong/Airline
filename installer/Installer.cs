using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("晾图 安装程序")]
[assembly: AssemblyProduct("晾图 / Airline")]
[assembly: AssemblyCompany("景同志 / 景同学")]
[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]

namespace AirlineSetup {
    internal static class Package {
        internal const string Version="0.3.0",Id="Airline.Screenline",RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run",UninstallKey="Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Airline.Screenline";
        internal static string Self { get { return Assembly.GetExecutingAssembly().Location; } }
        internal static byte[] Payload() { using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Airline.Payload")) using(var memory=new MemoryStream()) { if(stream==null) throw new InvalidOperationException("缺少程序文件。"); stream.CopyTo(memory); return memory.ToArray(); } }
        internal static string Hash(byte[] bytes) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",""); }
        internal static void AtomicWrite(string path,byte[] bytes) { string temporary=path+".installing"; try { File.WriteAllBytes(temporary,bytes); if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path); } finally { if(File.Exists(temporary)) File.Delete(temporary); } }
        internal static string Normalize(string root) {
            string full=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            if(string.Equals(full,Path.GetPathRoot(full).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("请选择一个专用程序文件夹，不能直接安装在磁盘根目录。");
            return full;
        }
        internal static bool Running(string executable) {
            foreach(var process in Process.GetProcesses()) {
                try { if(string.Equals(process.MainModule.FileName,executable,StringComparison.OrdinalIgnoreCase)) return true; } catch { } finally { process.Dispose(); }
            } return false;
        }
        internal static void Install(string target,bool register,bool desktop,bool autostart) {
            string root=Normalize(target),app=Path.Combine(root,"Airline.exe");
            if(Running(app)) throw new InvalidOperationException("安装位置的晾图正在运行。请先从托盘退出，再点击安装。");
            string marker=Path.Combine(root,"install-info.txt");
            if(File.Exists(app) && (!File.Exists(marker) || File.ReadAllLines(marker).FirstOrDefault()!=Id)) throw new InvalidOperationException("这个目录已有同名程序但没有安装记录，请选择新的专用文件夹。");
            if(autostart && ("\""+app+"\" --autostart").Length>260) throw new InvalidOperationException("启动路径太长，请选择更短的安装位置。");
            Directory.CreateDirectory(root); byte[] payload=Payload(); AtomicWrite(app,payload);
            AtomicWrite(Path.Combine(root,"Uninstall.exe"),File.ReadAllBytes(Self));
            File.WriteAllLines(Path.Combine(root,"install-info.txt"),new[]{Id,Version,Hash(payload)},Encoding.UTF8);
            if(!register) return;
            using(var key=Registry.CurrentUser.CreateSubKey(UninstallKey)) {
                key.SetValue("DisplayName","晾图 / Airline"); key.SetValue("DisplayVersion",Version); key.SetValue("Publisher","景同志 / 景同学");
                key.SetValue("InstallLocation",root); key.SetValue("DisplayIcon",app); key.SetValue("UninstallString","\""+Path.Combine(root,"Uninstall.exe")+"\" --uninstall");
                key.SetValue("NoModify",1,RegistryValueKind.DWord); key.SetValue("NoRepair",1,RegistryValueKind.DWord);
            }
            using(var key=Registry.CurrentUser.CreateSubKey(RunKey)) { if(autostart) key.SetValue(Id,"\""+app+"\" --autostart"); else key.DeleteValue(Id,false); }
            string programs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"晾图"); Directory.CreateDirectory(programs);
            CreateShortcut(Path.Combine(programs,"晾图.lnk"),app,""); CreateShortcut(Path.Combine(programs,"卸载晾图.lnk"),Path.Combine(root,"Uninstall.exe"),"--uninstall");
            if(desktop) CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"晾图.lnk"),app,"");
        }
        static void CreateShortcut(string path,string target,string arguments) {
            object shell=null,shortcut=null;
            try {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                shortcut=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
                var type=shortcut.GetType(); type.InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{target});
                type.InvokeMember("Arguments",BindingFlags.SetProperty,null,shortcut,new object[]{arguments});
                type.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,shortcut,new object[]{Path.GetDirectoryName(target)});
                type.InvokeMember("Description",BindingFlags.SetProperty,null,shortcut,new object[]{"晾图 · 截图晾在屏幕顶边"}); type.InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);
            } finally { if(shortcut!=null) Marshal.FinalReleaseComObject(shortcut); if(shell!=null) Marshal.FinalReleaseComObject(shell); }
        }
        static void RemoveShortcut(string path,string target) {
            if(!File.Exists(path)) return; object shell=null,shortcut=null;
            try {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); shortcut=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
                string actual=shortcut.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,shortcut,null) as string;
                if(string.Equals(actual,target,StringComparison.OrdinalIgnoreCase)) File.Delete(path);
            } finally { if(shortcut!=null) Marshal.FinalReleaseComObject(shortcut); if(shell!=null) Marshal.FinalReleaseComObject(shell); }
        }
        internal static void Uninstall(string target,bool register,bool deleteSelf) {
            string root=Normalize(target),marker=Path.Combine(root,"install-info.txt"),app=Path.Combine(root,"Airline.exe"),uninstall=Path.Combine(root,"Uninstall.exe");
            if(!File.Exists(marker) || File.ReadAllLines(marker).FirstOrDefault()!=Id) throw new InvalidOperationException("这个目录没有有效的晾图安装记录，未删除任何文件。");
            if(Running(app)) throw new InvalidOperationException("晾图正在运行，请先从托盘退出后再卸载。");
            if(register) {
                string programs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"晾图");
                RemoveShortcut(Path.Combine(programs,"晾图.lnk"),app); RemoveShortcut(Path.Combine(programs,"卸载晾图.lnk"),uninstall);
                RemoveShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"晾图.lnk"),app);
                if(Directory.Exists(programs) && !Directory.EnumerateFileSystemEntries(programs).Any()) Directory.Delete(programs);
                using(var key=Registry.CurrentUser.OpenSubKey(RunKey,true)) if(key!=null && string.Equals(key.GetValue(Id) as string,"\""+app+"\" --autostart",StringComparison.OrdinalIgnoreCase)) key.DeleteValue(Id,false);
                bool matches; using(var key=Registry.CurrentUser.OpenSubKey(UninstallKey)) matches=key!=null && string.Equals(key.GetValue("InstallLocation") as string,root,StringComparison.OrdinalIgnoreCase);
                if(matches) Registry.CurrentUser.DeleteSubKey(UninstallKey,false);
            }
            if(File.Exists(app)) File.Delete(app); File.Delete(marker);
            if(deleteSelf && File.Exists(uninstall)) File.Delete(uninstall);
            if(deleteSelf && !Directory.EnumerateFileSystemEntries(root).Any()) Directory.Delete(root);
            // The separate %LOCALAPPDATA%\Airline image library is never touched.
        }
        internal static void DelayedSelfCleanup(string root) {
            root=Normalize(root); string uninstall=Path.GetFullPath(Path.Combine(root,"Uninstall.exe"));
            if(!string.Equals(uninstall,Path.GetFullPath(Self),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("卸载程序位置不匹配。");
            string script=Path.Combine(Path.GetTempPath(),"Airline-cleanup-"+Guid.NewGuid().ToString("N")+".ps1");
            string text="Wait-Process -Id "+Process.GetCurrentProcess().Id+" -ErrorAction SilentlyContinue\r\n"+
                "Remove-Item -LiteralPath '"+uninstall.Replace("'","''")+"' -Force -ErrorAction SilentlyContinue\r\n"+
                "if ((Test-Path -LiteralPath '"+root.Replace("'","''")+"') -and -not (Get-ChildItem -LiteralPath '"+root.Replace("'","''")+"' -Force)) { Remove-Item -LiteralPath '"+root.Replace("'","''")+"' -ErrorAction SilentlyContinue }\r\n"+
                "Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue\r\n";
            File.WriteAllText(script,text,new UTF8Encoding(true));
            Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \""+script+"\"") { UseShellExecute=false,CreateNoWindow=true });
        }
        internal static bool FrameworkSupported() {
            using(var key=Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full")) { var release=key==null?null:key.GetValue("Release"); return release is int && (int)release>=461808; }
        }
        internal static int Test(string folder) {
            Directory.CreateDirectory(folder); var log=new StringBuilder(); int checks=0;
            Action<bool,string> check=(ok,name)=> { if(!ok) throw new Exception(name); checks++; log.AppendLine("PASS "+name); };
            try {
                byte[] payload=Payload(); check(payload.Length>50000 && payload[0]=='M' && payload[1]=='Z',"Embedded application is a Windows executable");
                string target=Path.Combine(folder,"installed-"+Guid.NewGuid().ToString("N")); Install(target,false,false,false);
                check(Hash(File.ReadAllBytes(Path.Combine(target,"Airline.exe")))==Hash(payload),"Installed application exactly matches embedded payload");
                check(File.Exists(Path.Combine(target,"Uninstall.exe")) && File.ReadAllLines(Path.Combine(target,"install-info.txt"))[0]==Id,"Uninstaller and installation marker are created");
                Install(target,false,false,false); check(Hash(File.ReadAllBytes(Path.Combine(target,"Airline.exe")))==Hash(payload),"Repeated installation safely replaces application");
                string keep=Path.Combine(target,"user-file.txt"); File.WriteAllText(keep,"keep"); string images=Path.Combine(folder,"existing-library.txt"); File.WriteAllText(images,"screenshots remain");
                Uninstall(target,false,true); check(!File.Exists(Path.Combine(target,"Airline.exe")) && !File.Exists(Path.Combine(target,"Uninstall.exe")),"Uninstall removes only installed binaries");
                check(File.Exists(keep) && File.Exists(images),"Uninstall preserves unrelated files and screenshot library");
                bool rejected=false; try { Uninstall(folder,false,true); } catch(InvalidOperationException) { rejected=true; } check(rejected,"Uninstall rejects a directory with no installation marker");
                log.AppendLine("\n"+checks+" installer checks passed. Real registry and user shortcuts were not modified.");
                File.WriteAllText(Path.Combine(folder,"installer-results.txt"),log.ToString()); return 0;
            } catch(Exception ex) { log.AppendLine("FAIL "+ex); File.WriteAllText(Path.Combine(folder,"installer-results.txt"),log.ToString()); return 1; }
        }
    }
    internal sealed class SetupForm : Form {
        readonly TextBox directory;
        readonly CheckBox desktop,startup,launch;
        readonly Label status;
        readonly Button install;
        readonly bool uninstall;
        bool previewMode;
        protected override bool ShowWithoutActivation { get { return previewMode; } }
        internal void RenderPreview(string output) {
            previewMode=true; StartPosition=FormStartPosition.Manual; Location=new Point(-30000,-30000); ShowInTaskbar=false;
            Show(); Application.DoEvents(); PerformLayout();
            using(var bitmap=new Bitmap(Width,Height)) { DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height)); bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png); } Close();
        }
        internal SetupForm(bool remove) {
            uninstall=remove; Text=remove?"卸载晾图":"安装晾图 / Airline"; ClientSize=new Size(600,440); Font=new Font("Microsoft YaHei UI",9); BackColor=Color.FromArgb(247,245,239); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen;
            Icon=Icon.ExtractAssociatedIcon(Package.Self);
            Controls.Add(new Label { Text=remove?"让程序退场，截图留下。":"把截图，晾在屏幕边上。",Font=new Font(Font.FontFamily,20,FontStyle.Bold),ForeColor=Color.FromArgb(48,58,53),Bounds=new Rectangle(28,28,544,48) });
            Controls.Add(new Label { Text="晾图 v"+Package.Version+"  ·  当前用户安装  ·  无需管理员权限",ForeColor=Color.FromArgb(85,114,96),Bounds=new Rectangle(30,86,540,24) });
            Controls.Add(new Label { Text=remove?"卸载位置":"安装位置",Bounds=new Rectangle(30,130,520,22) });
            string initial=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Airline");
            if(remove) initial=Path.GetDirectoryName(Package.Self);
            else using(var key=Registry.CurrentUser.OpenSubKey(Package.UninstallKey)) if(key!=null && key.GetValue("InstallLocation") is string) initial=(string)key.GetValue("InstallLocation");
            directory=new TextBox { Text=initial,Bounds=new Rectangle(30,158,440,28),ReadOnly=remove }; Controls.Add(directory);
            var browse=new Button { Text="浏览…",Bounds=new Rectangle(482,157,85,29),Enabled=!remove }; browse.Click+=(s,e)=> { using(var picker=new FolderBrowserDialog { SelectedPath=directory.Text,Description="选择晾图专用安装文件夹" }) if(picker.ShowDialog(this)==DialogResult.OK) directory.Text=picker.SelectedPath; }; Controls.Add(browse);
            desktop=new CheckBox { Text="创建桌面快捷方式",Bounds=new Rectangle(30,210,220,25),Checked=false,Visible=!remove }; Controls.Add(desktop);
            startup=new CheckBox { Text="登录 Windows 时自动运行",Bounds=new Rectangle(30,243,270,25),Checked=false,Visible=!remove }; Controls.Add(startup);
            if(!remove) using(var key=Registry.CurrentUser.OpenSubKey(Package.RunKey)) startup.Checked=key!=null && key.GetValue(Package.Id)!=null;
            launch=new CheckBox { Text="安装完成后打开晾图",Bounds=new Rectangle(30,276,240,25),Checked=true,Visible=!remove }; Controls.Add(launch);
            status=new Label { Text=remove?"只移除程序、它的快捷方式和安装记录。\n已保存截图和用户设置将保留。":"自动创建开始菜单入口，可从 Windows 应用列表卸载。\n建议先退出正在运行的旧版晾图。",ForeColor=Color.FromArgb(100,110,101),Bounds=new Rectangle(30,320,540,46) }; Controls.Add(status);
            install=new Button { Text=remove?"卸载":"安装",BackColor=Color.FromArgb(85,114,96),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Bounds=new Rectangle(450,380,118,36) }; install.FlatAppearance.BorderSize=0; install.Click+=(s,e)=>Perform(); Controls.Add(install);
            var cancel=new Button { Text="取消",Bounds=new Rectangle(345,380,94,36) }; cancel.Click+=(s,e)=>Close(); Controls.Add(cancel);
        }
        void Perform() {
            install.Enabled=false;
            try {
                if(uninstall) {
                    Package.Uninstall(directory.Text,true,false); Package.DelayedSelfCleanup(directory.Text);
                    MessageBox.Show(this,"晾图已卸载。截图和设置仍然保留。","晾图",MessageBoxButtons.OK,MessageBoxIcon.Information); Close();
                } else {
                    if(!Package.FrameworkSupported()) throw new InvalidOperationException("需要 .NET Framework 4.7.2 或更高版本，请先安装系统运行组件。");
                    Package.Install(directory.Text,true,desktop.Checked,startup.Checked);
                    status.Text="安装完成。已有截图保留。\n如果旧版仍在运行，请先退出旧版再打开新版。"; install.Text="完成";
                    if(launch.Checked) Process.Start(new ProcessStartInfo(Path.Combine(Package.Normalize(directory.Text),"Airline.exe")) { UseShellExecute=true });
                    MessageBox.Show(this,"晾图安装完成。可从开始菜单打开。","晾图",MessageBoxButtons.OK,MessageBoxIcon.Information); Close();
                }
            } catch(Exception ex) { status.Text=ex.Message; install.Enabled=true; MessageBox.Show(this,ex.Message,"未完成",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        }
    }
    internal static class Program {
        [STAThread] static int Main(string[] args) {
            if(args.Length>1 && args[0]=="--self-test") return Package.Test(args[1]);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>1 && args[0]=="--render-preview") {
                using(var form=new SetupForm(false)) form.RenderPreview(args[1]); return 0;
            }
            bool remove=args.Contains("--uninstall") || string.Equals(Path.GetFileName(Package.Self),"Uninstall.exe",StringComparison.OrdinalIgnoreCase);
            if(remove && !string.Equals(Path.GetFileName(Package.Self),"Uninstall.exe",StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("请通过已安装版本的卸载入口运行。","晾图"); return 1; }
            Application.Run(new SetupForm(remove)); return 0;
        }
    }
}
