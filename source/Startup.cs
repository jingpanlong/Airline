using System;
using Microsoft.Win32;

namespace Airline {
    internal static class Product {
        internal const string Version="0.3.0";
        internal const string Author="微信公众号：景同志\n小红书：景同学";
        internal static string Executable { get { return System.Reflection.Assembly.GetExecutingAssembly().Location; } }
    }
    internal interface IStartupStore { string Read(); void Write(string command); void Delete(); }
    internal sealed class RegistryStartupStore : IStartupStore {
        const string Key="Software\\Microsoft\\Windows\\CurrentVersion\\Run",Name="Airline.Screenline";
        public string Read() { using(var key=Registry.CurrentUser.OpenSubKey(Key)) return key==null?null:key.GetValue(Name) as string; }
        public void Write(string command) { using(var key=Registry.CurrentUser.CreateSubKey(Key)) key.SetValue(Name,command,RegistryValueKind.String); }
        public void Delete() { using(var key=Registry.CurrentUser.OpenSubKey(Key,true)) if(key!=null) key.DeleteValue(Name,false); }
    }
    internal sealed class StartupRegistration {
        readonly IStartupStore store;
        internal readonly string Command;
        internal StartupRegistration() : this(new RegistryStartupStore(),Product.Executable) { }
        internal StartupRegistration(IStartupStore backend,string executable) { store=backend; Command="\""+executable+"\" --autostart"; }
        internal bool Enabled { get { return !string.IsNullOrWhiteSpace(store.Read()); } }
        internal bool CurrentLocation { get { return string.Equals(store.Read(),Command,StringComparison.OrdinalIgnoreCase); } }
        internal void Set(bool enabled) {
            if(enabled && Command.Length>260) throw new InvalidOperationException("程序路径太长，请移到更短的文件夹后开启自启动。");
            if(enabled) store.Write(Command); else store.Delete();
        }
    }
}
