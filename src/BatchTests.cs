using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using VisioFormula;
[assembly:TargetFramework(".NETFramework,Version=v4.8")]
public static class BatchTests {
    static readonly List<string> passed=new List<string>();static Bridge bridge;static dynamic doc,ownedApp;static int code;static string root;
    static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);passed.Add(name);Console.WriteLine("PASS: "+name);}
    static bool Near(double a,double b){return Math.Abs(a-b)<0.00001;}
    static List<Rendered> RenderAll(List<FormulaTarget> targets,string font,double? size){return targets.Select(t=>{var f=t.Original.Clone();if(font!=null)f.Font=font;if(size.HasValue)f.Size=size.Value;return Renderer.Render(f);}).ToList();}
    static string State(List<FormulaTarget> targets){return string.Join("\n",targets.Select(t=>new JavaScriptSerializer().Serialize(Bridge.Read(t.Shape))+"|"+Bridge.Get(t.Shape,"Width")+"|"+Bridge.Get(t.Shape,"Height")+"|"+Bridge.Get(t.Shape,"PinX")+"|"+Bridge.Get(t.Shape,"PinY")));}
    static void Inspect(Control p){foreach(Control c in p.Controls){if(c.Visible&&(c.Width<=0||c.Height<=0||c.Left<0||c.Top<0||c.Right>p.ClientSize.Width+1||c.Bottom>p.ClientSize.Height+1))throw new Exception("Clipped "+c.GetType().Name+" "+c.Text+" "+c.Bounds+" in "+p.ClientSize);if(!(c is NumericUpDown)&&!(c is ComboBox)&&!(c is TextBoxBase))Inspect(c);}}
    static async Task Run(MainForm form){
        ownedApp=Activator.CreateInstance(Type.GetTypeFromProgID("Visio.Application"));bridge=new Bridge((object)ownedApp);bridge.App.Visible=false;doc=bridge.App.Documents.Add("");dynamic page=doc.Pages[1];
        var a=Renderer.Render(new Formula{Source=@"\alpha+\frac{1}{s+1}",Size=12});var b=Renderer.Render(new Formula{Source=@"x_1+x_2",Font="times",Size=16});
        dynamic first=page.Drop(bridge.GetDragData(a),2,5),second=page.Drop(bridge.GetDragData(b),4,5),nested=page.Drop(bridge.GetDragData(a),3,3),rect=page.DrawRectangle(2,2,4,4);
        int firstID=first.ID,secondID=second.ID,nestedID=nested.ID,rectID=rect.ID;
        dynamic selection=page.CreateSelection(0);selection.Select(nested,2);selection.Select(rect,2);dynamic group=selection.Group();int groupID=group.ID;
        Bridge.Set(first,"Width",a.Width*1.5);Bridge.Set(first,"Height",a.Height*1.5);Bridge.Set(first,"Angle",.23);
        bridge.App.ActiveWindow.DeselectAll();bridge.App.ActiveWindow.Select(group,2);var within=bridge.Collect(false);Check(within.Count==1&&within[0].ID==nestedID,"Selected outer group finds nested formula, ignores ordinary shapes");
        bridge.App.ActiveWindow.DeselectAll();bridge.App.ActiveWindow.Select(first,2);bridge.App.ActiveWindow.Select(second,2);Check(bridge.Collect(false).Count==2,"Selection scope finds exactly selected formulas");
        var all=bridge.Collect(true);Check(all.Count==3,"Whole page includes nested formulas once");string original=State(all);double x=Bridge.Get(first,"PinX"),y=Bridge.Get(first,"PinY");
        var outputs=RenderAll(all,"stix2",null);bridge.ApplyBatch(all,outputs,false);
        Check(all.All(t=>Bridge.Read(t.Shape).Font=="stix2"&&Bridge.Read(t.Shape).Source==t.Original.Source),"Font batch preserves all sources and writes selected font");
        int fi=all.FindIndex(t=>t.ID==firstID);Check(Near(Bridge.Get(first,"Width"),outputs[fi].Width*1.5)&&Near(Bridge.Get(first,"PinX"),x)&&Near(Bridge.Get(first,"PinY"),y)&&Near(Bridge.Get(first,"Angle"),.23),"Font-only batch preserves scale, center and rotation");
        Check((int)first.ID==firstID&&(int)second.ID==secondID&&(int)nested.ID==nestedID&&(int)group.ID==groupID,"Batch preserves shape and enclosing group identities");
        bridge.App.Undo();Check(State(all)==original,"One Visio undo restores entire batch, including source and geometry");bridge.App.Redo();Check(all.All(t=>Bridge.Read(t.Shape).Font=="stix2"),"One redo reapplies entire batch");
        all=bridge.Collect(true);outputs=RenderAll(all,null,10);bridge.ApplyBatch(all,outputs,true);Check(all.All(t=>Bridge.Read(t.Shape).Size==10&&Bridge.Read(t.Shape).Font=="stix2"),"Size-only batch sets size without changing font");
        for(int i=0;i<all.Count;i++)Check(Near(Bridge.Get(all[i].Shape,"Width"),outputs[i].Width)&&Near(Bridge.Get(all[i].Shape,"Height"),outputs[i].Height),"Uniform physical size for shape "+all[i].ID);
        all=bridge.Collect(true);outputs=RenderAll(all,"times",11);string before=State(all);string broken=Path.Combine(root,"invalid-"+Guid.NewGuid().ToString("N")+".svg");File.WriteAllText(broken,"<invalid>");var good=outputs[1];outputs[1]=new Rendered{Formula=good.Formula,Svg=broken,Width=1,Height=1};bool rejected=false;
        try{bridge.ApplyBatch(all,outputs,true);}catch{rejected=true;}Check(rejected&&State(all)==before,"Damaged SVG is rejected before any Visio mutation");outputs[1]=good;
        dynamic locked=all[1].Shape;string oldFormula=locked.CellsU["User.VFData0"].FormulaU;locked.CellsU["User.VFData0"].FormulaU="GUARD("+oldFormula+")";all=bridge.Collect(true);before=State(all);rejected=false;
        try{bridge.ApplyBatch(all,outputs,true);}catch{rejected=true;}Check(rejected&&State(all)==before,"Guarded metadata failure rolls back already updated formulas");locked.CellsU["User.VFData0"].FormulaForceU=oldFormula;
        all=bridge.Collect(true);outputs=RenderAll(all,"times",11);Bridge.Set(first,"PinX",x+.3);string changed=State(all);rejected=false;try{bridge.ApplyBatch(all,outputs,true);}catch{rejected=true;}Check(rejected&&State(all)==changed,"Concurrent Visio changes reject stale batch before any mutation");
        // Run the actual asynchronous UI path, including the embedded MathJax exporter.
        var bridgeField=typeof(MainForm).GetField("bridge",BindingFlags.Instance|BindingFlags.NonPublic);bridgeField.SetValue(form,bridge);
        var method=typeof(MainForm).GetMethod("RunBatch",BindingFlags.Instance|BindingFlags.NonPublic);var opts=new BatchOptions{WholePage=true,ChangeFont=true,Font="mathjax",ChangeSize=true,Size=13};
        Check(bridge.ContextName().Contains((string)doc.Name)&&bridge.ContextKey().Contains("/"),"Batch identifies target document and page");opts.ScopeKey="changed-page";bool wrongPage=false;try{await (Task<int>)method.Invoke(form,new object[]{opts,(Action<string>)(s=>{}),CancellationToken.None});}catch(Exception){wrongPage=true;}Check(wrongPage,"Changed document/page refuses batch before writing");opts.ScopeKey=bridge.ContextKey();var cancel=new CancellationTokenSource();cancel.Cancel();string unchanged=State(bridge.Collect(true));bool canceled=false;
        try{await (Task<int>)method.Invoke(form,new object[]{opts,(Action<string>)(s=>{}),cancel.Token});}catch(OperationCanceledException){canceled=true;}Check(canceled&&State(bridge.Collect(true))==unchanged,"Canceled UI batch leaves every formula untouched");cancel.Dispose();
        int count=await (Task<int>)method.Invoke(form,new object[]{opts,(Action<string>)(s=>Console.WriteLine(s)),CancellationToken.None});Check(count==3&&bridge.Collect(true).All(t=>t.Original.Font=="mathjax"&&t.Original.Size==13),"UI batch renders MathJax and commits all formulas");
        string saved=Path.Combine(root,"batch-verified.vsdx");doc.SaveAs(saved);doc.Close();doc=null;doc=bridge.App.Documents.Open(saved);all=bridge.Collect(true);Check(all.Count==3&&all.All(t=>t.Original.Font=="mathjax"&&t.Original.Size==13),"Batch styles and sources survive document save/reopen");
        dynamic reopened=doc.Pages[1].Shapes.ItemFromID[firstID];bridge.Update(reopened,Renderer.Render(new Formula{Source="z+1",Size=13}),false);Check(Bridge.Read(reopened).Source=="z+1"&&(int)reopened.ID==firstID,"Reopened batch formula remains editable in place");
        using(var settings=new QuickSettingsDialog(new QuickOptions()){Opacity=0,ShowInTaskbar=false}){settings.Show();await Task.Delay(50);Inspect(settings);using(var bmp=new Bitmap(settings.Width,settings.Height)){settings.DrawToBitmap(bmp,new Rectangle(Point.Empty,settings.Size));bmp.Save(Path.Combine(root,"quick-settings.png"));}settings.Size=settings.MinimumSize;await Task.Delay(50);Inspect(settings);settings.Close();Check(true,"Quick settings layout fits at default and minimum size");}
        using(var batch=new BatchDialog(new Formula(),whole=>3,(o,p,t)=>Task.FromResult(3),()=>"测试绘图.vsdx / Page-1",()=>"1/1"){Opacity=0,ShowInTaskbar=false}){batch.Show();await Task.Delay(50);Inspect(batch);using(var bmp=new Bitmap(batch.Width,batch.Height)){batch.DrawToBitmap(bmp,new Rectangle(Point.Empty,batch.Size));bmp.Save(Path.Combine(root,"batch-dialog.png"));}batch.Close();Check(true,"Batch dialog layout fits without clipped controls");}
        doc.Saved=true;doc.Close();doc=null;bridgeField.SetValue(form,null);dynamic app=bridge.App;bridge.Dispose();app.Quit();ownedApp=null;bridge=null;
    }
    [STAThread]public static int Main(){
        root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","batch");Directory.CreateDirectory(root);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var form=new MainForm(new SettingsStore(Path.Combine(root,"test-"+Guid.NewGuid().ToString("N")+".json"))){Opacity=0,ShowInTaskbar=false};
        form.Shown+=async delegate{try{await Run(form);}catch(Exception ex){code=1;Console.WriteLine(ex);File.WriteAllText(Path.Combine(root,"failure.txt"),ex.ToString());}finally{if(doc!=null)try{doc.Saved=true;doc.Close();}catch{}if(bridge!=null)bridge.Dispose();if(ownedApp!=null)try{ownedApp.Quit();}catch{}File.WriteAllText(Path.Combine(root,"results.json"),new JavaScriptSerializer().Serialize(new{passed=passed}));form.Close();}};
        Application.Run(form);return code;
    }
}
