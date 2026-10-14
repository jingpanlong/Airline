using System;
using System.Windows.Input;

namespace Airline {
    internal sealed class Hotkey : IEquatable<Hotkey> {
        internal readonly uint Modifiers,Key;
        internal Hotkey(uint modifiers,uint key) { Modifiers=modifiers; Key=key; }
        internal bool Valid { get { return (Modifiers&3)!=0 && (Modifiers&~7u)==0 && ((Key>=0x30 && Key<=0x39) || (Key>=0x41 && Key<=0x5A) || (Key>=0x70 && Key<=0x87)); } }
        internal string Token { get { return Modifiers+":"+Key; } }
        internal string Display { get { return ((Modifiers&2)!=0?"Ctrl + ":"")+((Modifiers&1)!=0?"Alt + ":"")+((Modifiers&4)!=0?"Shift + ":"")+(Key>=0x30 && Key<=0x39?((char)Key).ToString():KeyInterop.KeyFromVirtualKey((int)Key).ToString()); } }
        public bool Equals(Hotkey other) { return other!=null && other.Modifiers==Modifiers && other.Key==Key; }
        public override bool Equals(object other) { return Equals(other as Hotkey); }
        public override int GetHashCode() { return (int)(Modifiers*257+Key); }
        internal static Hotkey Parse(string token,Hotkey fallback) {
            uint modifiers,key; string[] parts=token.Split(':');
            if(parts.Length!=2 || !uint.TryParse(parts[0],out modifiers) || !uint.TryParse(parts[1],out key)) return fallback;
            var value=new Hotkey(modifiers,key); return value.Valid?value:fallback;
        }
    }
    internal interface IHotkeyBackend { bool Register(int id,Hotkey shortcut); void Unregister(int id); }
    internal sealed class NativeHotkeyBackend : IHotkeyBackend {
        readonly IntPtr handle; internal NativeHotkeyBackend(IntPtr h) { handle=h; }
        public bool Register(int id,Hotkey shortcut) { return Native.RegisterHotKey(handle,id,0x4000|shortcut.Modifiers,shortcut.Key); }
        public void Unregister(int id) { Native.UnregisterHotKey(handle,id); }
    }
    internal sealed class HotkeyService {
        readonly IHotkeyBackend backend;
        bool captureRegistered,revealRegistered;
        internal Hotkey Capture,Reveal;
        internal bool Suspended { get; private set; }
        internal bool CaptureRegistered { get { return captureRegistered; } }
        internal bool RevealRegistered { get { return revealRegistered; } }
        internal HotkeyService(IHotkeyBackend registration,Hotkey capture,Hotkey reveal) { backend=registration; Capture=capture; Reveal=reveal; }
        internal void Start() { captureRegistered=backend.Register(1,Capture); revealRegistered=backend.Register(2,Reveal); }
        internal void Stop() { if(captureRegistered) backend.Unregister(1); if(revealRegistered) backend.Unregister(2); captureRegistered=revealRegistered=false; }
        internal string Apply(Hotkey capture,Hotkey reveal,Action persist) {
            if(!capture.Valid || !reveal.Valid) return "请使用 Ctrl / Alt 配合字母、数字或 F1–F24。";
            if(capture.Equals(reveal)) return "截图和展开不能使用同一个快捷键。";
            if(Suspended) return "请先结束快捷键录制。";
            var oldCapture=Capture; var oldReveal=Reveal; bool oldC=captureRegistered,oldR=revealRegistered; Stop();
            try {
                captureRegistered=backend.Register(1,capture); if(!captureRegistered) throw new InvalidOperationException("截图快捷键被其他程序占用，请换一个组合。");
                revealRegistered=backend.Register(2,reveal); if(!revealRegistered) throw new InvalidOperationException("展开快捷键被其他程序占用，请换一个组合。");
                persist(); Capture=capture; Reveal=reveal; return null;
            } catch(Exception ex) {
                Stop(); Capture=oldCapture; Reveal=oldReveal;
                if(oldC) captureRegistered=backend.Register(1,oldCapture); if(oldR) revealRegistered=backend.Register(2,oldReveal);
                return ex.Message+((oldC && !captureRegistered)||(oldR && !revealRegistered)?" 原快捷键恢复失败，请重新应用。":" 原快捷键保持不变。");
            }
        }
        internal IDisposable Suspend() {
            bool oldC=captureRegistered,oldR=revealRegistered; Stop(); Suspended=true;
            return new Scope(()=> { Suspended=false; if(oldC) captureRegistered=backend.Register(1,Capture); if(oldR) revealRegistered=backend.Register(2,Reveal); });
        }
        internal sealed class Scope : IDisposable { Action finish; internal Scope(Action done) { finish=done; } public void Dispose() { var done=finish; finish=null; if(done!=null) done(); } }
    }
}
