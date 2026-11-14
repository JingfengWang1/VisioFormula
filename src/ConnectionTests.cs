using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using VisioFormula;
public static class ConnectionTests {
    static List<string> passed=new List<string>();static string root;
    static void Check(bool yes,string name){if(!yes)throw new Exception("FAIL: "+name);passed.Add(name);Console.WriteLine("PASS: "+name);}
    static bool Same(dynamic a,dynamic b){return (int)a.ProcessID==(int)b.ProcessID;}
    static void Activate(dynamic app,dynamic window){
        window.Activate();Console.WriteLine("ACTIVATE: "+window.Document.Name);var clock=Stopwatch.StartNew();
        while(clock.ElapsedMilliseconds<90000){try{if(Same(Marshal.GetActiveObject("Visio.Application"),app))return;}catch(COMException){}Application.DoEvents();Thread.Sleep(50);}
        throw new Exception("Visio did not register the activated test instance");
    }
    static void CloseDocument(dynamic d){if((object)d==null)return;try{d.Saved=true;d.Close();}catch{}}
    [STAThread]public static int Main(){
        root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","connection");Directory.CreateDirectory(root);Console.OutputEncoding=System.Text.Encoding.UTF8;
        dynamic a=null,b=null,da=null,db=null;Bridge pa=null,pb=null,live=null;
        try{
            Application.EnableVisualStyles();
            var ra=Renderer.Render(new Formula{Source=@"a+\alpha",Size=12});var rb=Renderer.Render(new Formula{Source=@"b+\beta",Font="times",Size=10});
            string runDir=Path.Combine(root,"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(runDir);
            a=Activator.CreateInstance(Type.GetTypeFromProgID("Visio.Application"));a.AlertResponse=7;a.Visible=true;da=a.Documents.Add("");da.SaveAs(Path.Combine(runDir,"VF-connection-A.vsdx"));dynamic wa=a.ActiveWindow;pa=new Bridge((object)a);
            b=Activator.CreateInstance(Type.GetTypeFromProgID("Visio.Application"));b.AlertResponse=7;b.Visible=true;db=b.Documents.Add("");db.SaveAs(Path.Combine(runDir,"VF-connection-B.vsdx"));dynamic wb=b.ActiveWindow;pb=new Bridge((object)b);
            dynamic sa=da.Pages[1].Drop(pa.GetDragData(ra),3,5),sb=db.Pages[1].Drop(pb.GetDragData(rb),4,4);int aid=sa.ID,bid=sb.ID;
            wa.DeselectAll();wa.Select(sa,2);wb.DeselectAll();wb.Select(sb,2);
            Activate(a,wa);live=new Bridge(false);Check(Same(live.App,a),"Initial connection uses activated instance A");
            wa.DeselectAll();Activate(b,wb);Check(Same(live.App,a)&&wa.Selection.Count==0,"Reproduced cached A with no selection while B has a selected formula");
            dynamic picked=live.Selected();Check(Same(picked.Application,b)&&(int)picked.ID==bid&&Bridge.Read(picked).Source==rb.Formula.Source,"Read selection refreshes to instance B and recovers its source");string keyB=live.ContextKey();
            wa.Select(sa,2);Activate(a,wa);picked=live.Selected();Check(Same(picked.Application,a)&&(int)picked.ID==aid,"Switching back reads the selected formula from A");string keyA=live.ContextKey();Check(keyA!=keyB&&keyA.Split('/')[0]!=keyB.Split('/')[0],"Batch context includes unique Visio instance identity");
            dynamic stagedA=live.Stage(ra);Check(Same(stagedA.Application,a),"Drag staging follows A");Activate(b,wb);dynamic stagedB=live.Stage(rb);Check(Same(stagedB.Application,b),"Drag staging reconnects to B without retaining A's staging document");
            double x=Bridge.Get(sa,"PinX"),y=Bridge.Get(sa,"PinY");var next=Renderer.Render(new Formula{Source=@"\frac{a+1}{s+1}",Size=12});live.Update(sa,next,false);
            Check(Bridge.Read(sa).Source==next.Formula.Source&&(int)sa.ID==aid&&Math.Abs(Bridge.Get(sa,"PinX")-x)<1e-7&&Math.Abs(Bridge.Get(sa,"PinY")-y)<1e-7,"Updating loaded A target preserves identity and center while connection points to B");
            a.Undo();Check(Bridge.Read(sa).Source==ra.Formula.Source,"Single undo belongs to target A even after switching to B");a.Redo();Check(Bridge.Read(sa).Source==next.Formula.Source,"Redo restores the update in A");Check(Bridge.Read(sb).Source==rb.Formula.Source,"Unrelated formula in B stays unchanged");
            var targets=pa.Collect(true);live.ApplyBatch(targets,new List<Rendered>{ra},false);Check(Bridge.Read(sa).Source==ra.Formula.Source,"Batch transaction follows target application's ownership");a.Undo();Check(Bridge.Read(sa).Source==next.Formula.Source,"Batch undo remains in target A");
            var mixed=pa.Collect(true);mixed.AddRange(pb.Collect(true));bool rejected=false;try{live.ApplyBatch(mixed,new List<Rendered>{ra,rb},false);}catch(Exception e){rejected=e.Message.Contains("不同");}Check(rejected&&Bridge.Read(sa).Source==next.Formula.Source&&Bridge.Read(sb).Source==rb.Formula.Source,"Mixed-instance batch rejected before changes");
            Activate(b,wb);wb.DeselectAll();rejected=false;try{live.Selected();}catch(Exception e){rejected=e.Message.Contains((string)db.Name)&&e.Message.Contains("尚未选中");}Check(rejected,"No-selection message names the connected document");
            dynamic plain=db.Pages[1].DrawRectangle(1,1,2,2);wb.Select(sb,2);wb.Select(plain,2);rejected=false;try{live.Selected();}catch(Exception e){rejected=e.Message.Contains("2 个");}Check(rejected,"Multiple-selection message reports the actual count");
            wb.DeselectAll();wb.Select(sb,2);live.Selected();pb.Dispose();pb=null;CloseDocument(db);db=null;b.Quit();b=null;wa.DeselectAll();wa.Select(sa,2);Activate(a,wa);picked=live.Selected();Check(Same(picked.Application,a),"Closed former instance reconnects to remaining A");
            dynamic recoveredStage=live.Stage(ra);Check(Same(recoveredStage.Application,a),"Disconnected staging document can be replaced after former instance closes");
            da.SaveAs(Path.Combine(root,"connection-verified.vsdx"));CloseDocument(da);da=null;da=a.Documents.Open(Path.Combine(root,"connection-verified.vsdx"));wa=a.ActiveWindow;sa=da.Pages[1].Shapes.ItemFromID[aid];wa.DeselectAll();wa.Select(sa,2);Activate(a,wa);picked=live.Selected();Check(Bridge.Read(picked).Source==next.Formula.Source,"Saved/reopened selected formula reads back correctly");
            live.Update(picked,ra,false);Check((int)picked.ID==aid&&Bridge.Read(picked).Source==ra.Formula.Source,"Reopened selected formula updates in place");
            File.WriteAllText(Path.Combine(root,"results.json"),new JavaScriptSerializer().Serialize(new{passed=passed}));return 0;
        }catch(Exception e){Console.WriteLine(e);File.WriteAllText(Path.Combine(root,"failure.txt"),e.ToString());return 1;}
        finally{if(live!=null)live.Dispose();if(pa!=null)pa.Dispose();if(pb!=null)pb.Dispose();CloseDocument(da);CloseDocument(db);if((object)a!=null)try{a.Quit();}catch{}if((object)b!=null)try{b.Quit();}catch{}}
    }
}
