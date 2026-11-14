using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace VisioFormula {
public sealed class FormulaEditor : RichTextBox {
    readonly List<EditState> undo=new List<EditState>(),redo=new List<EditState>();
    readonly Timer colorTimer=new Timer{Interval=90};
    EditState current=new EditState(),compositionStart;
    bool changing,coloring,composing;QuickInput input=new QuickInput(new QuickOptions());
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,ref Point p);
    public FormulaEditor(){
        DetectUrls=false;AcceptsTab=true;WordWrap=true;ScrollBars=RichTextBoxScrollBars.Vertical;BorderStyle=BorderStyle.None;HideSelection=false;MaxLength=50000;AllowDrop=false;
        colorTimer.Tick+=delegate{colorTimer.Stop();HighlightNow();};
        var menu=new ThemeMenu();
        var u=menu.Items.Add("撤销  Ctrl+Z",null,delegate{UndoEdit();});var r=menu.Items.Add("重做  Ctrl+Y",null,delegate{RedoEdit();});menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("剪切",null,delegate{Cut();});menu.Items.Add("复制",null,delegate{Copy();});menu.Items.Add("粘贴纯文本",null,delegate{PastePlain();});menu.Items.Add("全选",null,delegate{SelectAll();});
        menu.Opening+=delegate{u.Enabled=CanUndoEdit;r.Enabled=CanRedoEdit;};ContextMenuStrip=menu;
    }
    public bool CanUndoEdit{get{return undo.Count>0;}}public bool CanRedoEdit{get{return redo.Count>0;}}
    public bool IsComposing{get{return composing;}}
    public void Configure(QuickOptions options){input=new QuickInput(options);QueueColor();}
    public EditState Snapshot(){var s=current.Clone();s.Text=Text;s.Start=SelectionStart;s.Length=SelectionLength;return s;}
    public void LoadDocument(string text,int start=0,int length=0){undo.Clear();redo.Clear();Restore(new EditState{Text=Normalize(text),Start=start,Length=length});}
    static string Normalize(string s){return (s??"").Replace("\r\n","\n").Replace('\r','\n');}
    void Push(EditState before){undo.Add(before.Clone());if(undo.Count>300)undo.RemoveAt(0);redo.Clear();}
    void Restore(EditState state){
        changing=true;try{current=state.Clone();if(Text!=state.Text)Text=state.Text;Select(Math.Max(0,Math.Min(TextLength,state.Start)),Math.Max(0,Math.Min(state.Length,TextLength-Math.Max(0,Math.Min(TextLength,state.Start)))));current.Start=SelectionStart;current.Length=SelectionLength;ClearUndo();}
        finally{changing=false;}QueueColor();
    }
    public void Apply(EditState state){
        if(ReadOnly)return;var before=Snapshot();if(state.Text!=before.Text)Push(before);Restore(state);
    }
    public void InsertPlain(string text){var s=Snapshot();text=Normalize(text);if(s.Text.Length-s.Length+text.Length>MaxLength)return;Apply(s.Replace(s.Start,s.Length,text));}
    public void UndoEdit(){if(ReadOnly||composing||undo.Count==0)return;var s=undo[undo.Count-1];undo.RemoveAt(undo.Count-1);redo.Add(Snapshot());Restore(s);}
    public void RedoEdit(){if(ReadOnly||composing||redo.Count==0)return;var s=redo[redo.Count-1];redo.RemoveAt(redo.Count-1);undo.Add(Snapshot());Restore(s);}
    public bool Execute(string key){if(ReadOnly||composing)return false;var next=input.Handle(Snapshot(),key);if(next==null)return false;Apply(next);return true;}
    void PastePlain(){try{if(Clipboard.ContainsText())InsertPlain(Clipboard.GetText());}catch(ExternalException){System.Media.SystemSounds.Beep.Play();}}
    protected override void OnTextChanged(EventArgs e){
        if(coloring)return;
        if(!changing){var previous=current.Clone();string text=Text;
            int prefix=0;while(prefix<previous.Text.Length&&prefix<text.Length&&previous.Text[prefix]==text[prefix])prefix++;
            int tail=0;while(tail<previous.Text.Length-prefix&&tail<text.Length-prefix&&previous.Text[previous.Text.Length-tail-1]==text[text.Length-tail-1])tail++;
            if(!composing)Push(previous);
            current=previous.Replace(prefix,previous.Text.Length-prefix-tail,text.Substring(prefix,text.Length-prefix-tail));current.Start=SelectionStart;current.Length=SelectionLength;
            ClearUndo();QueueColor();
        }
        base.OnTextChanged(e);
    }
    protected override void OnSelectionChanged(EventArgs e){if(!coloring&&!changing&&current.Text==Text){current.Start=SelectionStart;current.Length=SelectionLength;}base.OnSelectionChanged(e);}
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
        if(!composing){
            if(keyData==(Keys.Control|Keys.B)||keyData==(Keys.Control|Keys.I)||keyData==(Keys.Control|Keys.U))return true;
            if(keyData==(Keys.Control|Keys.Z)){UndoEdit();return true;}
            if(keyData==(Keys.Control|Keys.Y)||keyData==(Keys.Control|Keys.Shift|Keys.Z)){RedoEdit();return true;}
            if(keyData==(Keys.Control|Keys.V)||keyData==(Keys.Shift|Keys.Insert)){PastePlain();return true;}
            string action=keyData==(Keys.Alt|Keys.Shift|Keys.A)?"Align":keyData==(Keys.Alt|Keys.Shift|Keys.M)?"Matrix":keyData==(Keys.Alt|Keys.Shift|Keys.C)?"Cases":keyData==Keys.Tab?"Tab":keyData==(Keys.Shift|Keys.Tab)?"ShiftTab":keyData==Keys.Enter?"Enter":keyData==Keys.Back?"Backspace":null;
            if(action!=null&&Execute(action))return true;
            if(keyData==Keys.Tab){InsertPlain("  ");return true;}
            if(keyData==(Keys.Shift|Keys.Tab))return true;
            if(keyData==(Keys.Shift|Keys.Space)){if(!Execute("ShiftSpace"))InsertPlain(" ");return true;}
        }
        return base.ProcessCmdKey(ref msg,keyData);
    }
    protected override void OnKeyPress(KeyPressEventArgs e){if(!composing&&!char.IsControl(e.KeyChar)&&Execute(e.KeyChar==' '?"Space":e.KeyChar.ToString()))e.Handled=true;base.OnKeyPress(e);}
    protected override void WndProc(ref Message m){
        if(m.Msg==0x10D){composing=true;compositionStart=Snapshot();colorTimer.Stop();}
        if(m.Msg==0x302&&!changing){PastePlain();return;}
        if(m.Msg==0x304||m.Msg==0xC7){UndoEdit();return;} // EM_UNDO / WM_UNDO
        base.WndProc(ref m);
        if(m.Msg==0x10E){composing=false;if(compositionStart!=null&&compositionStart.Text!=Text)Push(compositionStart);compositionStart=null;current.Text=Text;current.Start=SelectionStart;current.Length=SelectionLength;ClearUndo();QueueColor();}
    }
    void QueueColor(){if(IsDisposed||composing)return;colorTimer.Stop();colorTimer.Start();}
    public void HighlightNow(){
        if(composing||coloring||!IsHandleCreated)return;int start=SelectionStart,length=SelectionLength;Point scroll=new Point();SendMessage(Handle,0x4DD,IntPtr.Zero,ref scroll);coloring=true;SendMessage(Handle,0xB,IntPtr.Zero,IntPtr.Zero);
        try{
            SelectAll();SelectionColor=Theme.Ink;
            if(input.Options.Highlight){
                foreach(Match m in Regex.Matches(Text,@"%[^\n]*|\\(?:[a-zA-Z]+\*?|.)|[{}\[\]()]|[\^_&]|\d+(?:\.\d+)?")){
                    if(m.Value[0]=='%'&&QuickInput.Escaped(Text,m.Index))continue;
                    Select(m.Index,m.Length);char c=m.Value[0];SelectionColor=c=='%'?Theme.Muted:c=='\\'?Theme.Accent:"{}[]()".IndexOf(c)>=0?Color.FromArgb(145,85,53):"^_&".IndexOf(c)>=0?Color.FromArgb(133,79,151):Color.FromArgb(56,105,160);
                }
            }
            Select(start,length);if(length==0)SelectionColor=Theme.Ink;ClearUndo();SendMessage(Handle,0x4DE,IntPtr.Zero,ref scroll);
        }finally{SendMessage(Handle,0xB,new IntPtr(1),IntPtr.Zero);coloring=false;Invalidate();}
    }
    protected override void Dispose(bool disposing){if(disposing){colorTimer.Dispose();if(ContextMenuStrip!=null)ContextMenuStrip.Dispose();}base.Dispose(disposing);}
}
}
