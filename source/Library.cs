using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Airline {
    [Serializable] internal sealed class Shot {
        public string Path;
        public string Name { get { return System.IO.Path.GetFileName(Path); } }
        public DateTime Created { get { return File.GetCreationTime(Path); } }
    }
    internal sealed class Library {
        public readonly string Root, Images, Outbox;
        public readonly List<Shot> Shots = new List<Shot>();
        public bool WatchClipboard=true;
        public bool NotifyStartup=true, AvoidFullscreen=true;
        public bool AnimateShots=true;
        public Hotkey CaptureShortcut=new Hotkey(3,0x53),RevealShortcut=new Hotkey(3,0x4C);
        public Library(string root) {
            Root=root; Images=System.IO.Path.Combine(root,"Images"); Outbox=System.IO.Path.Combine(root,"Outbox");
            Directory.CreateDirectory(Images); Directory.CreateDirectory(Outbox);
            Shots.AddRange(Directory.GetFiles(Images,"*.png").OrderByDescending(p=>p).Select(p=>new Shot { Path=p }));
            string config=System.IO.Path.Combine(Root,"settings.txt");
            if(File.Exists(config)) {
                var lines=File.ReadAllLines(config);
                WatchClipboard=!lines.Contains("clipboard=off"); NotifyStartup=!lines.Contains("startup-notice=off"); AvoidFullscreen=!lines.Contains("fullscreen=off");
                AnimateShots=!lines.Contains("sway=off");
                foreach(string line in lines) { if(line.StartsWith("capture-hotkey=")) CaptureShortcut=Hotkey.Parse(line.Substring(15),CaptureShortcut); if(line.StartsWith("reveal-hotkey=")) RevealShortcut=Hotkey.Parse(line.Substring(14),RevealShortcut); }
                if(CaptureShortcut.Equals(RevealShortcut)) { CaptureShortcut=new Hotkey(3,0x53); RevealShortcut=new Hotkey(3,0x4C); }
            }
            foreach(string p in Directory.GetFiles(Outbox,"*.png")) {
                try { if(File.GetLastWriteTimeUtc(p)<DateTime.UtcNow.AddDays(-7)) File.Delete(p); } catch { }
            }
        }
        public void SaveSettings() {
            string path=System.IO.Path.Combine(Root,"settings.txt"),temp=path+".tmp";
            File.WriteAllLines(temp,new[]{WatchClipboard?"clipboard=on":"clipboard=off",NotifyStartup?"startup-notice=on":"startup-notice=off",AvoidFullscreen?"fullscreen=on":"fullscreen=off",AnimateShots?"sway=on":"sway=off","capture-hotkey="+CaptureShortcut.Token,"reveal-hotkey="+RevealShortcut.Token});
            if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
        }
        internal static string Export(Shot[] shots,string destination) {
            string folder=System.IO.Path.Combine(destination,"晾图导出_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,6));
            Directory.CreateDirectory(folder);
            foreach(var shot in shots) File.Copy(shot.Path,System.IO.Path.Combine(folder,shot.Name),false);
            return folder;
        }
        public Shot Add(BitmapSource image) {
            string name="Airline_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N").Substring(0,6)+".png";
            string path=System.IO.Path.Combine(Images,name), temp=path+".part";
            try {
                Encode(image,temp); File.Move(temp,path);
                var shot=new Shot { Path=path }; Shots.Insert(0,shot); return shot;
            } finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        public string Stage(Shot shot) {
            string path=System.IO.Path.Combine(Outbox,Guid.NewGuid().ToString("N")+"_"+shot.Name);
            File.Copy(shot.Path,path); File.SetLastWriteTimeUtc(path,DateTime.UtcNow); return path;
        }
        public static BitmapImage Load(string path, int width) {
            var bitmap=new BitmapImage();
            using(var stream=File.OpenRead(path)) {
                bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad;
                if(width>0) bitmap.DecodePixelWidth=width;
                bitmap.StreamSource=stream; bitmap.EndInit(); bitmap.Freeze();
            }
            return bitmap;
        }
        public static void Encode(BitmapSource image,string path) {
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using(var f=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)) encoder.Save(f);
        }
    }
}
