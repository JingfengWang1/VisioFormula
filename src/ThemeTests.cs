using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using VisioFormula;
[assembly:TargetFramework(".NETFramework,Version=v4.8")]
public static class ThemeTests {
    [DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    static readonly List<string> passed=new List<string>();static string root;
    static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static void Check(bool value,string text){if(!value)throw new Exception(text);passed.Add(text);Console.WriteLine("PASS: "+text);}
    static IEnumerable<Control> All(Control c){foreach(Control child in c.Controls){yield return child;foreach(Control nested in All(child))yield return nested;}}
    static void Inspect(Control p){foreach(Control c in p.Controls){if(c.Visible&&(c.Width<=0||c.Height<=0||c.Left<0||c.Top<0||c.Right>p.ClientSize.Width+1||c.Bottom>p.ClientSize.Height+1))throw new Exception("Clipped "+c.GetType().Name+" "+c.Text+" "+c.Bounds+" in "+p.ClientSize);if(!(c is NumericUpDown)&&!(c is ComboBox)&&!(c is TextBoxBase))Inspect(c);}}
    static void Snapshot(Control f,string name){using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(Point.Empty,f.Size));bmp.Save(Path.Combine(root,name+".png"));}}
    static void Open(Form f){f.Opacity=0;f.ShowInTaskbar=false;f.Show();Application.DoEvents();Inspect(f);}
    [STAThread]public static int Main(string[] args){
        bool dpi96=args.Contains("--96");if(dpi96)SetThreadDpiAwarenessContext(new IntPtr(-1));
        root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","theme-"+(dpi96?"96":"native"));Directory.CreateDirectory(root);
        try{
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var settings=new QuickSettingsDialog(new QuickOptions())){
                Open(settings);Check(true,"Settings default layout fits at "+settings.DeviceDpi+" DPI");Snapshot(settings,"settings");
                var rules=Field<TextBox>(settings,"rules");Check(rules.Lines.Length==44,"Rules remain editable on 44 separate lines");
                Check(All(settings).OfType<CheckBox>().Count(c=>c.GetType().Name=="ThemeCheckBox")==8,"All eight settings share themed checkboxes");
                Check(rules.ContextMenuStrip.GetType().Name=="ThemeMenu","Rules editor uses shared popup theme");
                settings.Size=settings.MinimumSize;Application.DoEvents();Inspect(settings);Check(rules.Height>=rules.Font.Height*2,"Minimum settings size keeps at least two visible rule lines");Snapshot(settings,"settings-minimum");
                rules.Text="z:::\\zeta;";All(settings).OfType<Button>().Single(b=>b.Text=="取消").PerformClick();Check(settings.DialogResult==DialogResult.Cancel&&settings.Result==null,"Cancel discards unsaved rule edits");
            }
            using(var settings=new QuickSettingsDialog(new QuickOptions())){
                Open(settings);var box=Field<TextBox>(settings,"rules");box.Text="z:::\\zeta;";All(settings).OfType<Button>().Single(b=>b.Text=="保存").PerformClick();Check(settings.DialogResult==DialogResult.OK&&settings.Result.Parse().Count==1,"Save returns valid edited configuration");
            }
            using(var settings=new QuickSettingsDialog(new QuickOptions())){
                Open(settings);Field<TextBox>(settings,"rules").Text="z:::a;\nz:::b;";All(settings).OfType<Button>().Single(b=>b.Text=="保存").PerformClick();Check(settings.DialogResult!=DialogResult.OK&&!string.IsNullOrEmpty(Field<Label>(settings,"error").Text),"Invalid rules stay open with an error");settings.Close();
            }
            var history=new List<Draft>{new Draft{Formula=new Formula{Source=@"G(s)=\frac{1}{s^2+2\zeta\omega_n s+\omega_n^2}",Font="times",Size=12}},new Draft{Formula=new Formula{Source=@"\begin{pmatrix}a&b\\c&d\end{pmatrix}"}},new Draft{Formula=new Formula{Source=@"\phi_a,\phi_b,\phi_c\in[0,2\pi)"}}};
            using(var drafts=new DraftDialog(history)){
                Open(drafts);Snapshot(drafts,"drafts");drafts.Size=drafts.MinimumSize;Application.DoEvents();Inspect(drafts);Check(true,"Draft list fits default and minimum sizes");Snapshot(drafts,"drafts-minimum");
                var list=All(drafts).OfType<ListBox>().Single();list.SelectedIndex=1;((Button)drafts.AcceptButton).PerformClick();Check(drafts.Chosen==history[1],"Restore returns the selected draft");
            }
            using(var empty=new DraftDialog(new List<Draft>())){Open(empty);Check(!((Button)empty.AcceptButton).Enabled,"Empty history disables restore");Snapshot(empty,"drafts-empty");empty.Close();}
            BatchOptions applied=null;
            using(var batch=new BatchDialog(new Formula(),whole=>whole?5:3,(o,p,t)=>{applied=o;return Task.FromResult(3);},()=>"测试绘图.vsdx / Page-1",()=>"test/1")){
                Open(batch);Check(true,"Batch controls and target summary fit");Snapshot(batch,"batch");
                Field<CheckBox>(batch,"changeFont").Checked=false;Check(!Field<ComboBox>(batch,"font").Enabled&&!Field<Button>(batch,"apply").Enabled,"Batch disables inactive fields and empty action");
                Field<CheckBox>(batch,"changeSize").Checked=true;Field<NumericUpDown>(batch,"size").Value=16;Field<Button>(batch,"apply").PerformClick();Application.DoEvents();
                Check(applied!=null&&!applied.ChangeFont&&applied.ChangeSize&&applied.Size==16&&applied.ScopeKey=="test/1","Styled batch dialog passes unchanged options to the operation");Check(batch.Updated==3,"Batch completion feedback preserved");batch.Close();
            }
            using(var host=new Form{ClientSize=new Size(400,300),Opacity=0,ShowInTaskbar=false})using(var editor=new FormulaEditor{Dock=DockStyle.Fill}){
                host.Controls.Add(editor);host.Show();editor.LoadDocument("al",2);editor.Execute("Space");
                var menu=editor.ContextMenuStrip;menu.Opacity=0;menu.Show(host,new Point(10,10));Application.DoEvents();Check(menu.GetType().Name=="ThemeMenu"&&!menu.ShowImageMargin,"Source popup uses shared theme without legacy icon gutter");
                Check(menu.Items[0].Enabled&&!menu.Items[1].Enabled,"Popup undo and redo enabled states preserved");Snapshot(menu,"source-menu");menu.Close();menu.Items[0].PerformClick();Check(editor.Text=="al","Themed popup undo command works");
                using(var main=new MainForm(new SettingsStore(Path.Combine(root,"menu-settings.json")))){var more=Field<ContextMenuStrip>(main,"toolsMenu");more.Opacity=0;more.Show(host,new Point(10,10));Application.DoEvents();Snapshot(more,"more-menu");Check(more.Items.Cast<ToolStripItem>().Where(i=>!(i is ToolStripSeparator)).All(i=>i.Height>=editor.DeviceDpi*.3),"Actual main menu rows provide DPI-scaled click targets");more.Close();}host.Close();
            }
            File.WriteAllText(Path.Combine(root,"results.json"),new JavaScriptSerializer().Serialize(new{passed=passed.Count,checks=passed}));return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
