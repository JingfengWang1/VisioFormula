using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VisioFormula {
internal static class Theme {
    internal static readonly Color Background=Color.FromArgb(244,247,248),Ink=Color.FromArgb(28,43,49),Muted=Color.FromArgb(111,125,132),Line=Color.FromArgb(220,228,231),Accent=Color.FromArgb(24,105,106),SoftAccent=Color.FromArgb(231,243,240),Error=Color.FromArgb(172,70,54);
    internal static GraphicsPath Round(RectangleF r,float radius) {
        var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
}
// Draw directly at the monitor's DPI rather than stretching cached bitmaps.
internal sealed class Surface : Panel {
    internal bool Highlight;
    internal Surface(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=Color.White;}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float s=DeviceDpi/96f;
        using(var path=Theme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),9*s))using(var fill=new SolidBrush(BackColor))using(var pen=new Pen(Highlight?Theme.Accent:Theme.Line,Math.Max(1,s))){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}base.OnPaint(e);
    }
}
internal sealed class ActionButton : Button {
    internal bool Primary,Quiet;bool hover,pressed;
    internal ActionButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;UseVisualStyleBackColor=false;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnPaint(PaintEventArgs e){
        Color parent=Parent==null?Theme.Background:Parent.BackColor;
        Color fill=!Enabled?(Quiet?parent:Color.FromArgb(234,239,241)):Primary?(pressed?Color.FromArgb(15,78,80):hover?Color.FromArgb(31,123,123):Theme.Accent):hover?Theme.SoftAccent:Quiet?parent:Color.White;
        Color ink=!Enabled?Color.FromArgb(156,166,171):Primary?Color.White:Theme.Ink;
        e.Graphics.Clear(parent);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float s=DeviceDpi/96f;
        using(var path=Theme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),7*s))using(var brush=new SolidBrush(fill)){e.Graphics.FillPath(brush,path);if(!Quiet&&!Primary&&Enabled)using(var pen=new Pen(Theme.Line,Math.Max(1,s)))e.Graphics.DrawPath(pen,path);}
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused&&ShowFocusCues){Rectangle r=ClientRectangle;r.Inflate(-(int)(4*s),-(int)(4*s));ControlPaint.DrawFocusRectangle(e.Graphics,r,ink,fill);}
    }
}
internal sealed class StatusBadge : Label {
    internal StatusBadge(){AutoSize=false;TextAlign=ContentAlignment.MiddleCenter;BackColor=Theme.SoftAccent;ForeColor=Theme.Accent;SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(Parent==null?Color.White:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=Theme.Round(new RectangleF(0,0,Width-1,Height-1),Math.Max(2,(Height-1)/2f)))using(var brush=new SolidBrush(BackColor))e.Graphics.FillPath(brush,path);
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
    }
}
internal sealed class FontChoice : ComboBox {
    bool hover;
    internal FontChoice(){DrawMode=DrawMode.OwnerDrawFixed;FlatStyle=FlatStyle.Flat;DropDownStyle=ComboBoxStyle.DropDownList;BackColor=Color.White;ForeColor=Theme.Ink;}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);ItemHeight=Font.Height+Math.Max(4,DeviceDpi/24);}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void OnSelectedIndexChanged(EventArgs e){Invalidate();base.OnSelectedIndexChanged(e);}
    // Keep the native combobox and list for keyboard/accessibility behavior,
    // but paint its closed face without the legacy bevel and square arrow.
    protected override void WndProc(ref Message m){
        base.WndProc(ref m);
        if(m.Msg==0x000F){using(var g=Graphics.FromHwnd(Handle))DrawFace(g);}
        else if((m.Msg==0x0318||m.Msg==0x0317)&&m.WParam!=IntPtr.Zero){using(var g=Graphics.FromHdc(m.WParam))DrawFace(g);}
    }
    void DrawFace(Graphics g){
        float s=DeviceDpi/96f;using(var background=new SolidBrush(BackColor))g.FillRectangle(background,ClientRectangle);g.SmoothingMode=SmoothingMode.AntiAlias;int arrow=(int)(25*s);
        var button=new RectangleF(Width-arrow,1,arrow-1,Math.Max(1,Height-2));
        if(hover||Focused||DroppedDown)using(var path=Theme.Round(button,4*s))using(var b=new SolidBrush(Theme.SoftAccent))g.FillPath(b,path);
        var text=new Rectangle((int)(3*s),0,Math.Max(0,Width-arrow-(int)(4*s)),Height);TextRenderer.DrawText(g,Text,Font,text,Enabled?Theme.Ink:Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        float cx=Width-arrow/2f,cy=Height/2f;using(var p=new Pen(Enabled?Theme.Muted:Theme.Line,1.25f*s)){p.StartCap=p.EndCap=LineCap.Round;g.DrawLines(p,new[]{new PointF(cx-3*s,cy-1.5f*s),new PointF(cx,cy+1.5f*s),new PointF(cx+3*s,cy-1.5f*s)});}
    }
    protected override void OnDrawItem(DrawItemEventArgs e){
        if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;
        using(var brush=new SolidBrush(selected?Theme.SoftAccent:Color.White))e.Graphics.FillRectangle(brush,e.Bounds);
        var b=e.Bounds;b.X+=Math.Max(3,DeviceDpi/24);b.Width-=Math.Max(3,DeviceDpi/24);
        TextRenderer.DrawText(e.Graphics,Convert.ToString(Items[e.Index]),Font,b,Theme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
}
// Shared by every popup, including the source and rules editors. Native menu
// keyboard handling and accessibility are retained; only the drawing changes.
internal sealed class ThemeMenu : ContextMenuStrip {
    internal ThemeMenu(){Font=new Font("Microsoft YaHei UI",9F);ForeColor=Theme.Ink;BackColor=Color.White;ShowImageMargin=false;ShowCheckMargin=false;Renderer=new MenuPainter();}
    protected override void OnOpening(System.ComponentModel.CancelEventArgs e){
        float s=DeviceDpi/96f;Padding=new Padding((int)(5*s));
        foreach(ToolStripItem item in Items)item.Padding=item is ToolStripSeparator?new Padding(0,(int)(3*s),0,(int)(3*s)):new Padding((int)(10*s),(int)(7*s),(int)(10*s),(int)(7*s));
        base.OnOpening(e);
    }
    sealed class MenuPainter : ToolStripProfessionalRenderer {
        internal MenuPainter(){RoundedEdges=false;}
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(Color.White);}
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){}
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){using(var p=new Pen(Theme.Line))e.Graphics.DrawRectangle(p,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);}
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){
            if(!e.Item.Selected||!e.Item.Enabled)return;float s=e.Graphics.DpiX/96f;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Theme.Round(new RectangleF(1,1,e.Item.Width-2,e.Item.Height-2),5*s))using(var b=new SolidBrush(Theme.SoftAccent))e.Graphics.FillPath(b,path);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){int inset=(int)(10*e.Graphics.DpiX/96f);var r=new Rectangle(inset,0,Math.Max(0,e.Item.Width-2*inset),e.Item.Height);TextRenderer.DrawText(e.Graphics,e.Text,e.TextFont,r,!e.Item.Enabled?Theme.Muted:e.Item.Selected?Theme.Accent:Theme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);}
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){int inset=(int)(10*e.Graphics.DpiX/96f);using(var p=new Pen(Theme.Line))e.Graphics.DrawLine(p,inset,e.Item.Height/2,e.Item.Width-inset,e.Item.Height/2);}
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e){e.ArrowColor=e.Item.Enabled?Theme.Accent:Theme.Muted;base.OnRenderArrow(e);}
    }
}
internal sealed class ThemeCheckBox : CheckBox {
    bool hover;
    internal ThemeCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);FlatStyle=FlatStyle.Flat;Cursor=Cursors.Hand;ForeColor=Theme.Ink;Margin=Padding.Empty;}
    public override Size GetPreferredSize(Size proposedSize){float s=DeviceDpi/96f;var t=TextRenderer.MeasureText(Text,Font);return new Size(t.Width+(int)(28*s),Math.Max(t.Height,(int)(24*s)));}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnCheckedChanged(EventArgs e){Invalidate();base.OnCheckedChanged(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void OnPaint(PaintEventArgs e){
        float s=DeviceDpi/96f;float side=16*s,x=2*s,y=(Height-side)/2;Color paper=Parent==null?Color.White:Parent.BackColor;
        e.Graphics.Clear(paper);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=Theme.Round(new RectangleF(x,y,side,side),4*s))using(var b=new SolidBrush(!Enabled?Theme.Line:Checked?Theme.Accent:hover?Theme.SoftAccent:Color.White))using(var p=new Pen(Enabled&&(Checked||hover)?Theme.Accent:Theme.Line,Math.Max(1,s))){e.Graphics.FillPath(b,path);e.Graphics.DrawPath(p,path);}
        if(Checked)using(var p=new Pen(Enabled?Color.White:Theme.Muted,1.7f*s)){p.StartCap=p.EndCap=LineCap.Round;e.Graphics.DrawLines(p,new[]{new PointF(x+3.5f*s,y+8*s),new PointF(x+6.5f*s,y+11*s),new PointF(x+12.5f*s,y+5*s)});}
        var text=new Rectangle((int)(26*s),0,Math.Max(0,Width-(int)(26*s)),Height);
        TextRenderer.DrawText(e.Graphics,Text,Font,text,Enabled?Theme.Ink:Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused&&ShowFocusCues){var r=ClientRectangle;r.Inflate(-1,-1);ControlPaint.DrawFocusRectangle(e.Graphics,r,Theme.Accent,paper);}
    }
}
internal static class DialogTheme {
    internal static void Initialize(Form f,string title,Size client){f.SuspendLayout();f.AutoScaleDimensions=new SizeF(96,96);f.AutoScaleMode=AutoScaleMode.Dpi;f.Font=new Font("Microsoft YaHei UI",9F);f.ForeColor=Theme.Ink;f.BackColor=Theme.Background;f.Text=title;f.ClientSize=client;f.StartPosition=FormStartPosition.CenterParent;f.ShowInTaskbar=false;f.MaximizeBox=false;f.MinimizeBox=false;f.ShowIcon=false;}
    internal static TableLayoutPanel Rows(params float[] heights){var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=heights.Length,Margin=Padding.Empty};foreach(float h in heights)p.RowStyles.Add(new RowStyle(h<0?SizeType.Percent:SizeType.Absolute,h<0?100:h));return p;}
    internal static TableLayoutPanel Columns(params float[] widths){var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=widths.Length,RowCount=1,Margin=Padding.Empty};p.RowStyles.Add(new RowStyle(SizeType.Percent,100));foreach(float w in widths)p.ColumnStyles.Add(new ColumnStyle(w<0?SizeType.Percent:SizeType.Absolute,w<0?100:w));return p;}
    internal static Label Label(string text,bool muted){return new Label{Text=text,Dock=DockStyle.Fill,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,ForeColor=muted?Theme.Muted:Theme.Ink,Margin=Padding.Empty};}
    internal static Control Header(string title,string description){var p=Rows(27,-1);var t=Label(title,false);t.Font=new Font("Microsoft YaHei UI",13F);p.Controls.Add(t,0,0);p.Controls.Add(Label(description,true),0,1);return p;}
    internal static Surface Card(int padding){return new Surface{Dock=DockStyle.Fill,Padding=new Padding(padding),Margin=Padding.Empty};}
    internal static Control Input(Control input){var c=Card(8);c.Padding=new Padding(10,4,8,4);c.Controls.Add(new CenteredInput(input));return c;}
    internal static ActionButton Button(string text,EventHandler action,bool primary){var b=new ActionButton{Text=text,Primary=primary,Dock=DockStyle.Fill,Margin=Padding.Empty};b.Click+=action;return b;}
}
internal sealed class CenteredInput : Panel {
    bool arranging;
    internal CenteredInput(Control input){Dock=DockStyle.Fill;Margin=Padding.Empty;input.Margin=Padding.Empty;Controls.Add(input);input.SizeChanged+=delegate{PerformLayout();};}
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(arranging||Controls.Count==0)return;arranging=true;try{var c=Controls[0];c.SetBounds(0,Math.Max(0,(ClientSize.Height-c.Height)/2),ClientSize.Width,c.Height);}finally{arranging=false;}}
}
internal sealed class DraftList : ListBox {
    internal DraftList(){DrawMode=DrawMode.OwnerDrawFixed;BorderStyle=BorderStyle.None;BackColor=Color.White;ForeColor=Theme.Ink;IntegralHeight=false;}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ItemHeight=(int)(66*DeviceDpi/96f);}
    protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);ItemHeight=(int)(66*DeviceDpi/96f);}
    protected override void OnDrawItem(DrawItemEventArgs e){
        if(e.Index<0)return;var d=(Draft)Items[e.Index];float s=DeviceDpi/96f;bool selected=(e.State&DrawItemState.Selected)!=0;e.Graphics.FillRectangle(Brushes.White,e.Bounds);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        var area=new RectangleF(e.Bounds.X+2*s,e.Bounds.Y+2*s,e.Bounds.Width-4*s,e.Bounds.Height-4*s);
        if(selected)using(var path=Theme.Round(area,6*s))using(var b=new SolidBrush(Theme.SoftAccent))e.Graphics.FillPath(b,path);
        string formula=(d.Formula.Source??"").Replace('\r',' ').Replace('\n',' ');
        var r=new Rectangle(e.Bounds.X+(int)(12*s),e.Bounds.Y+(int)(7*s),e.Bounds.Width-(int)(24*s),(int)(27*s));
        using(var f=new Font("Consolas",10F))TextRenderer.DrawText(e.Graphics,formula,f,r,Theme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        r.Y+=(int)(29*s);r.Height=(int)(20*s);string meta=d.SavedUtc.ToLocalTime().ToString("MM-dd HH:mm")+"   ·   "+FontCatalog.All[FontCatalog.IndexOf(d.Formula.Font)]+"   ·   "+d.Formula.Size.ToString("0.#")+" pt";
        TextRenderer.DrawText(e.Graphics,meta,Font,r,Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(selected&&Focused&&(e.State&DrawItemState.Focus)!=0){var focus=Rectangle.Round(area);focus.Inflate(-2,-2);ControlPaint.DrawFocusRectangle(e.Graphics,focus,Theme.Accent,Theme.SoftAccent);}
    }
}
}
