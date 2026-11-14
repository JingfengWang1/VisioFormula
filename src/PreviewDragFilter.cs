using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VisioFormula {
// All mouse gestures over the preview belong to the formula tool. MSHTML is
// only a renderer; it never receives a left-button down and cannot drag pixels.
public sealed class PreviewDragFilter : IMessageFilter {
    readonly Control surface;
    readonly Control[] additionalSurfaces;
    readonly Action startDrag;
    Point origin;
    bool armed;
    [DllImport("user32.dll")] static extern bool IsChild(IntPtr parent,IntPtr child);
    public PreviewDragFilter(Control surface,Action startDrag,params Control[] additionalSurfaces) { this.surface=surface; this.startDrag=startDrag; this.additionalSurfaces=additionalSurfaces; }
    public bool PreFilterMessage(ref Message message) {
        if(surface.IsDisposed || !surface.IsHandleCreated)return false;
        bool inside=message.HWnd==surface.Handle || IsChild(surface.Handle,message.HWnd);
        foreach(Control area in additionalSurfaces)if(!area.IsDisposed&&area.IsHandleCreated&&(message.HWnd==area.Handle||IsChild(area.Handle,message.HWnd)))inside=true;
        if(message.Msg==0x0201 && inside) { // WM_LBUTTONDOWN
            origin=Cursor.Position; armed=true; surface.Capture=true; return true;
        }
        if(message.Msg==0x0202 && (inside || armed)) { // WM_LBUTTONUP
            armed=false; surface.Capture=false; return true;
        }
        if(message.Msg==0x0200 && armed) { // WM_MOUSEMOVE
            if((message.WParam.ToInt64()&1)==0) { armed=false; surface.Capture=false; return true; }
            Point p=Cursor.Position;
            if(Math.Abs(p.X-origin.X)>SystemInformation.DragSize.Width/2 || Math.Abs(p.Y-origin.Y)>SystemInformation.DragSize.Height/2) {
                armed=false; surface.Capture=false;
                // Start OLE after this input message has returned, not from an
                // MSHTML event callback. This avoids nested browser drag loops.
                surface.BeginInvoke(startDrag);
            }
            return true;
        }
        if(message.Msg==0x0100 && message.WParam.ToInt32()==27 && armed) {
            armed=false; surface.Capture=false; return true;
        }
        if(message.Msg==0x0215 && armed) armed=false; // WM_CAPTURECHANGED
        return false;
    }
}
}
