using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using VisioFormula;
[assembly:TargetFramework(".NETFramework,Version=v4.8")]
public static class EditorTests {
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
    static readonly List<string> passed=new List<string>();static string root;
    static void Check(bool v,string name){if(!v)throw new Exception("FAIL: "+name);passed.Add(name);Console.WriteLine("PASS: "+name);}
    static EditState State(string text){int p=text.IndexOf('|');return new EditState{Text=text.Replace("|",""),Start=p<0?text.Length:p};}
    static string Show(EditState s){return s==null?"<no edit>":s.Text.Insert(s.Start,"|");}
    static void Expect(QuickInput q,string text,string key,string expected){var actual=q.Handle(State(text),key);Check(Show(actual)==expected,text+" / "+key+" -> "+expected+" (actual "+Show(actual)+")");}
    static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static void NativeType(FormulaEditor e,string text){foreach(char c in text)SendMessage(e.Handle,0x102,new IntPtr(c),IntPtr.Zero);}
    [STAThread]public static int Main(){
        root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","editor-tests");Directory.CreateDirectory(root);
        try{
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var options=new QuickOptions();var q=new QuickInput(options);Check(options.Parse().Count==44,"All 44 local shorthand rules included");
            foreach(var rule in options.Parse()){var next=q.Handle(State(rule.Key),"Space");Check(next!=null&&!next.Text.Contains("#tab")&&!next.Text.Contains("#cursor"),"Expand "+rule.Key+" without source markers");}
            Expect(q,"al","Space",@"\alpha|");Expect(q,@"\al","Space",@"\alpha|");Expect(q,"local","Space","<no edit>");Expect(q,@"\text{al|}","Space","<no edit>");Expect(q,"% al","Space","<no edit>");
            Expect(q,"sq","Space",@"\sqrt{|}");var bi=q.Handle(State("bi"),"Space");bi=bi.Replace(bi.Start,0,"n");var second=q.Handle(bi,"Tab");Check(Show(second)==@"\binom{n}{|}","Snippet tab stop tracks inserted text");second=second.Replace(second.Start,0,"k");Check(Show(q.Handle(second,"ShiftTab"))==@"\binom{|n}{k}","Shift Tab returns to previous snippet field");
            var numbered=new QuickInput(new QuickOptions{Rules=@"a:::#cursor X#tab2 Y#tab1;"});var ns=numbered.Handle(State("a"),"Space");ns=numbered.Handle(ns,"Tab");Check(ns.Start==ns.Text.Length,"Numbered tab order");
            var multi=new QuickInput(new QuickOptions{Rules="z:::\\begin{aligned}\n#cursor&=#tab1\n\\end{aligned};"});var ms=multi.Handle(State("z"),"Space");Check(ms.Text.Contains("\n")&&Show(multi.Handle(ms,"Tab")).Contains("&=|"),"Multiline snippets and tab stops");
            Expect(q,"1/2","Space",@"\frac{1}{2}|");Expect(q,"(a+b)/(c+d)","Space",@"\frac{a+b}{c+d}|");Expect(q,"x=1/2","Space",@"x=\frac{1}{2}|");Expect(q,"1/(a+","Space","<no edit>");Expect(q,"a / b","Space","<no edit>");
            Expect(q,"(a/b)","Space",@"\frac{a}{b}|");Expect(q,"(a+b/c+d)","Space",@"\frac{a+b}{c+d}|");
            Expect(q,"x_abc","Space",@"x_{abc}|");Expect(q,"x^12","Space",@"x^{12}|");Expect(q,"x^","Space","<no edit>");Expect(q,"x^{12}","Space","<no edit>");
            Expect(q,"","(","(|)");Expect(q,"(|)",")","()|");Expect(q,"{|}","Backspace","|");Expect(q,@"\frac{a|}{b}","Tab",@"\frac{a}{|b}");Expect(q,@"\su","m",@"\sum\limits|");
            Expect(q,"","Matrix","\\begin{pmatrix}\n|\n\\end{pmatrix}");Expect(q,@"\begin{pmatrix}1|\end{pmatrix}","Tab",@"\begin{pmatrix}1 & |\end{pmatrix}");Expect(q,@"\begin{pmatrix}1 & |\end{pmatrix}","Tab",@"\begin{pmatrix}1\end{pmatrix}|");
            Expect(q,@"\begin{pmatrix}1|\end{pmatrix}","Enter","\\begin{pmatrix}1 \\\\\n|\\end{pmatrix}");Expect(q,@"\begin{align*}x=|\end{align*}","Space",@"\begin{align*}x&= |\end{align*}");Expect(q,@"\begin{aligned}x&=|\end{aligned}","Space","<no edit>");
            var tab=new QuickInput(new QuickOptions{TabToExpand=true});Expect(tab,"al","Space","<no edit>");Expect(tab,"al","Tab",@"\alpha|");Expect(new QuickInput(new QuickOptions{Enabled=false}),"al","Space","<no edit>");
            bool bad=false;try{new QuickOptions{Rules="a:::x;\na:::y"}.Parse();}catch(ArgumentException){bad=true;}Check(bad,"Duplicate custom rules rejected without replacing settings");
            using(var host=new Form{Opacity=0,ShowInTaskbar=false,ClientSize=new Size(460,280)})using(var editor=new FormulaEditor{Dock=DockStyle.Fill,Font=new Font("Consolas",12)}){
                host.Controls.Add(editor);host.Show();editor.Focus();editor.LoadDocument("");NativeType(editor,"sq ");Check(editor.Text==@"\sqrt{}"&&editor.SelectionStart==6,"Native WM_CHAR expands shorthand into parameter");editor.HighlightNow();editor.UndoEdit();Check(editor.Text=="sq","One undo reverses expansion after highlighting");editor.RedoEdit();Check(editor.Text==@"\sqrt{}"&&editor.SelectionStart==6,"Redo restores expansion and cursor");
                NativeType(editor,"x");editor.HighlightNow();editor.UndoEdit();Check(editor.Text==@"\sqrt{}","Highlighting never enters undo history");editor.RedoEdit();Check(editor.Text==@"\sqrt{x}","Native typed character redo");
                editor.LoadDocument("bi",2);editor.Execute("Space");NativeType(editor,"n");editor.Execute("Tab");NativeType(editor,"k");editor.UndoEdit();editor.UndoEdit();Check(editor.Text==@"\binom{}{}","Undo field edits preserves clean snippet source");editor.RedoEdit();Check(editor.SelectionStart==editor.Text.Length-1&&editor.Execute("ShiftTab")&&editor.SelectionStart==7,"Redo restores snippet navigation");
                editor.LoadDocument("alpha",1,3);editor.InsertPlain("X\r\nY");editor.UndoEdit();Check(editor.Text=="alpha"&&editor.SelectionStart==1&&editor.SelectionLength==3,"Undo paste restores exact selection");editor.RedoEdit();Check(editor.Text=="aX\nYa","Plain text paste normalizes newlines");editor.UndoEdit();editor.InsertPlain("Q");Check(!editor.CanRedoEdit,"New branch discards redo history");
                editor.LoadDocument("al",2);SendMessage(editor.Handle,0x10D,IntPtr.Zero,IntPtr.Zero);Check(!editor.Execute("Space"),"IME composition suppresses shortcut expansion");editor.SelectedText="中文";SendMessage(editor.Handle,0x10E,IntPtr.Zero,IntPtr.Zero);editor.UndoEdit();Check(editor.Text=="al","IME composition is one undo operation");
                editor.LoadDocument(@"\frac{x_1}{2} % comment");editor.Select(3,2);int notifications=0;editor.TextChanged+=delegate{notifications++;};editor.HighlightNow();Check(editor.SelectionStart==3&&editor.SelectionLength==2&&notifications==0,"Highlighting preserves selection and emits no source change");
                host.Close();
            }
            string pref=Path.Combine(root,"preferences-"+Guid.NewGuid().ToString("N")+".json");var store=new SettingsStore(pref);store.Save(new Preferences{Font="times",Size=16,Zoom=200});var ws=new WorkspaceState(pref);
            ws.Capture(new Formula{Source=@"\alpha+1",Font="times",Size=16},3,2);ws.Save();ws.Archive();ws.Capture(new Formula{Source="",Font="times",Size=16},0,0);ws.Save();
            var reopened=new WorkspaceState(pref);Check(reopened.Data.Current.Formula.Source==""&&reopened.Data.History[0].Formula.Source==@"\alpha+1","Empty new document and previous draft survive restart");
            reopened.SaveQuick(new QuickOptions{Rules="zz:::\\zeta;",TabToExpand=true});Check(new WorkspaceState(pref).Quick.TabToExpand&&new WorkspaceState(pref).Quick.Rules.Contains("zz:::"),"Custom rules and trigger key survive restart");File.WriteAllText(reopened.DraftPath,"broken");Check(new WorkspaceState(pref).Data.Current.Formula.Source==@"\alpha+1","Draft backup recovers from damaged primary file");
            using(var form=new MainForm(store)){var source=Field<FormulaEditor>(form,"source");Check(source.Text==@"\alpha+1"&&Field<NumericUpDown>(form,"size").Value==16,"Main window restores draft text, style and cursor");form.GetType().GetField("target",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,new object());source.LoadDocument("a+b",3);form.GetType().GetMethod("NewFormula",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);Check(source.Text==""&&Field<object>(form,"target")==null&&Field<NumericUpDown>(form,"size").Value==16,"New formula disconnects Visio target, retains style, saves prior draft");Check(new WorkspaceState(pref).Data.History.Any(d=>d.Formula.Source=="a+b"),"New formula archives current contents before clearing");}
            using(var dialog=new QuickSettingsDialog(new QuickOptions()){Opacity=0,ShowInTaskbar=false}){dialog.Show();var ruleBox=Field<TextBox>(dialog,"rules");Check(ruleBox.Lines.Length==44,"Quick settings displays 44 editable lines, including Windows line endings");var round=(QuickOptions)dialog.GetType().GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(dialog,null);Check(round.Parse().Count==44,"Settings editor roundtrip preserves all rules");using(var bmp=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bmp,new Rectangle(Point.Empty,dialog.Size));bmp.Save(Path.Combine(root,"quick-settings.png"));}dialog.Close();}
            File.WriteAllText(Path.Combine(root,"results.json"),new JavaScriptSerializer().Serialize(new{passed=passed}));Console.WriteLine("TOTAL "+passed.Count);return 0;
        }catch(Exception ex){Console.WriteLine(ex);File.WriteAllText(Path.Combine(root,"failure.txt"),ex.ToString());return 1;}
    }
}
