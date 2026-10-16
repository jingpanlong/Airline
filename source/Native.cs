using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace Airline {
    internal static class Native {
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT point);
        [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h, out RECT rect);
        [DllImport("user32.dll")] internal static extern bool IsWindowEnabled(IntPtr h);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr h, uint message, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] internal static extern bool AddClipboardFormatListener(IntPtr h);
        [DllImport("user32.dll")] internal static extern bool RemoveClipboardFormatListener(IntPtr h);
        [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h, out uint process);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetClassName(IntPtr h,StringBuilder name,int length);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindow(string className,string title);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string message);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr SendMessageTimeout(IntPtr h,uint message,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
        internal static readonly uint RunningMessage=RegisterWindowMessage("Airline.Screenline.RunningNotice.v1");
        internal static bool IsOtherFullscreen() {
            IntPtr foreground=GetForegroundWindow(); if(foreground==IntPtr.Zero) return false;
            var className=new StringBuilder(128); GetClassName(foreground,className,128);
            string name=className.ToString(); if(name=="Progman" || name=="WorkerW" || name=="Shell_TrayWnd" || name=="Shell_SecondaryTrayWnd" || name=="#32769") return false;
            uint process; GetWindowThreadProcessId(foreground,out process);
            if(process==(uint)System.Diagnostics.Process.GetCurrentProcess().Id) return false;
            if(IsZoomed(foreground) && (GetWindowLong(foreground,-16)&0x00C00000)!=0) return false;
            RECT rect; if(!GetWindowRect(foreground,out rect)) return false;
            return CoversScreen(rect,System.Windows.Forms.Screen.PrimaryScreen.Bounds);
        }
        internal static bool CoversScreen(RECT rect,System.Drawing.Rectangle screen) { return rect.Left<=screen.Left && rect.Top<=screen.Top && rect.Right>=screen.Right && rect.Bottom>=screen.Bottom; }
        internal sealed class HandleOwner : System.Windows.Forms.IWin32Window {
            public IntPtr Handle { get; private set; }
            internal HandleOwner(IntPtr handle) { Handle=handle; }
        }
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr h, int index);
        [DllImport("user32.dll")] internal static extern int SetWindowLong(IntPtr h, int index, int value);
        [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
        [DllImport("user32.dll")] internal static extern bool SetWindowDisplayAffinity(IntPtr h, uint affinity);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr h);
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] internal struct SHFILEOPSTRUCT {
            public IntPtr hwnd; public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
        }
        [DllImport("shell32.dll", CharSet=CharSet.Unicode)] internal static extern int SHFileOperation(ref SHFILEOPSTRUCT op);
        internal static bool Recycle(string path, IntPtr owner) {
            var op = new SHFILEOPSTRUCT { hwnd=owner, wFunc=3, pFrom=path+"\0\0", fFlags=0x40|0x10|0x400 };
            return SHFileOperation(ref op)==0 && !op.fAnyOperationsAborted;
        }
        internal static void ToolWindow(Window w) {
            var h = new WindowInteropHelper(w).Handle;
            SetWindowLong(h, -20, GetWindowLong(h,-20)|0x80);
            int pref=1; try { DwmSetWindowAttribute(h, 33, ref pref, 4); } catch { }
        }
    }
}
