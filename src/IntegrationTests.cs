using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using VisioFormula;
using System.Runtime.InteropServices;
using System.Xml;

public static class IntegrationTests {
    [DllImport("user32.dll",SetLastError=true)] static extern bool OpenClipboard(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool CloseClipboard();
    static List<string> passed=new List<string>();
    static void Check(bool condition,string name) { if(!condition)throw new Exception("FAIL: "+name); passed.Add(name); Console.WriteLine("PASS: "+name); }
    static bool Near(double a,double b) { return Math.Abs(a-b)<0.00001; }
    [STAThread] public static int Main(string[] args) {
        bool skipClipboard=Array.IndexOf(args,"--without-clipboard")>=0;
        string output=Path.GetFullPath(args.Length>0?args[0]:"."); Directory.CreateDirectory(output);
        dynamic doc=null,ownedApp=null; Bridge bridge=null;
        try {
            var f=new Formula { Source=@"\begin{aligned}\mathbf{v}&=\begin{bmatrix}v_d\\v_q\end{bmatrix}\\G(s)&=\color{blue}\frac{1}{s+1}\end{aligned}",Size=12 };
            Rendered first=Renderer.Render(f); Check(File.Exists(first.Svg),"LaTeX to SVG: matrix, alignment, bold, color");
            ownedApp=Activator.CreateInstance(Type.GetTypeFromProgID("Visio.Application"));bridge=new Bridge((object)ownedApp);ownedApp.Visible=false; doc=bridge.App.Documents.Add(""); dynamic page=doc.Pages[1];
            dynamic staged=bridge.Stage(first);
            Check(staged.Type==2 && staged.Shapes.Count==1,"Native wrapper contains one SVG group");
            dynamic original=page.Drop(staged,4.0,5.0); int id=original.ID;
            Check(Bridge.Read(original).Source==f.Source,"Native Visio drop preserves source payload");
            bool repeated=true;
            // Exercise the exact IDataObject factory used by the drag source,
            // including rebuilding the staging shape for every subsequent drag.
            for(int attempt=0;attempt<5;attempt++) {
                object data=bridge.GetDragData(first);
                dynamic dropped=page.Drop(data,1.5+attempt,8.0);
                repeated &= Bridge.IsFormula(dropped) && Bridge.Read(dropped).Source==f.Source
                    && Near(Bridge.Get(dropped,"Width"),first.Width) && Near(Bridge.Get(dropped,"Height"),first.Height);
            }
            Check(repeated,"Five consecutive IDataObject transfers preserve source and physical size");
            if(!skipClipboard){
            bool locked=false;for(int attempt=0;attempt<30&&!locked;attempt++){locked=OpenClipboard(IntPtr.Zero);if(!locked)System.Threading.Thread.Sleep(100);}
            Check(locked,"Clipboard lock acquired for drag independence test");
            try {
                object data=bridge.GetDragData(first);
                dynamic dropped=page.Drop(data,2.0,2.0);
                Check(Bridge.Read(dropped).Source==f.Source,"IDataObject transfer succeeds while system clipboard is locked");
            } finally { CloseClipboard(); }
            }
            string initial=Path.Combine(output,"01-original.vsdx"); doc.SaveAs(initial); doc.Close(); doc=null;
            // Remove only this test's generated rendering directory. Recovery must use the document.
            string cache=Path.GetFullPath(Renderer.Cache)+Path.DirectorySeparatorChar;
            string generated=Path.GetFullPath(first.Directory);
            if(!generated.StartsWith(cache,StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe test cache path");
            Directory.Delete(generated,true);
            doc=bridge.App.Documents.Open(initial); page=doc.Pages[1]; original=page.Shapes.ItemFromID[id];
            Formula recovered=Bridge.Read(original);
            Check(recovered.Source==f.Source && recovered.Size==12,"Save, close, delete render cache, reopen and recover source/settings");
            double ow=Bridge.Get(original,"Width"),oh=Bridge.Get(original,"Height");
            Bridge.Set(original,"Width",ow*1.6); Bridge.Set(original,"Height",oh*1.6);
            Bridge.Set(original,"PinX",3.1); Bridge.Set(original,"PinY",4.2); Bridge.Set(original,"Angle",0.2);
            dynamic copy=original.Duplicate(); int duplicateID=copy.ID;
            string duplicateSource=Bridge.Read(copy).Source;
            var next=new Formula { Source=@"\begin{aligned}\mathbf{v}&=\begin{bmatrix}v_d\\v_q\end{bmatrix}\\G(s)&=\color{red}\frac{\omega_n^2}{s^2+2\zeta\omega_n s+\omega_n^2}\end{aligned}",Size=12 };
            Rendered second=Renderer.Render(next); bridge.Update(original,second,false);
            Check((int)original.ID==id,"In-place update preserves outer shape ID");
            Check(Near(Bridge.Get(original,"PinX"),3.1)&&Near(Bridge.Get(original,"PinY"),4.2)&&Near(Bridge.Get(original,"Angle"),0.2),"Update preserves position and rotation");
            Check(Near(Bridge.Get(original,"Width"),second.Width*1.6)&&Near(Bridge.Get(original,"Height"),second.Height*1.6),"Update preserves 1.6x scale, adapts width and height to content");
            Check(original.Shapes.Count==1,"Update replaces old SVG artwork without duplicate children");
            dynamic art=original.Shapes[1];
            Check(Near(Bridge.Get(art,"Width"),Bridge.Get(original,"Width"))&&Near(Bridge.Get(art,"Height"),Bridge.Get(original,"Height")),"New artwork fills outer group at intended size");
            Check(Bridge.Read(copy).Source==duplicateSource,"Editing a duplicate does not change the other formula");
            bridge.App.Undo();
            Check(Bridge.Read(original).Source==f.Source && Near(Bridge.Get(original,"Width"),ow*1.6),"One undo restores previous source and geometry together");
            bridge.App.Redo();
            Check(Bridge.Read(original).Source==next.Source && Near(Bridge.Get(original,"Width"),second.Width*1.6),"Redo restores updated source and geometry together");
            if(!skipClipboard){bridge.Copy(second); page.Paste();
            dynamic pasted=bridge.App.ActiveWindow.Selection[1];
            Check(Bridge.Read(pasted).Source==next.Source,"Native clipboard paste preserves editable source");}
            var tf=new Formula { Source=@"\text{gain}\quad\boldsymbol{\alpha}=\frac{a+b}{c}",Font="times",Size=10 };
            Rendered times=Renderer.Render(tf); dynamic timesStage=bridge.Stage(times); dynamic timesShape=page.Drop(timesStage,5.8,7.8);
            Check(Bridge.Read(timesShape).Font=="times","Times / NewTX render and metadata");
            var fontIDs=new Dictionary<string,int>();
            int fontNumber=0;
            foreach(MathFont definition in FontCatalog.All){
                string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"samples",definition.Id+".svg");
                if(!File.Exists(path))throw new Exception("Run test-fonts.ps1 and test-realtime.ps1 before integration tests.");
                var xml=new XmlDocument{XmlResolver=null};xml.Load(path);var root=xml.DocumentElement;
                Formula stored=new JavaScriptSerializer().Deserialize<Formula>(xml.GetElementsByTagName("metadata")[0].InnerText);
                var vector=new Rendered{Formula=stored,Svg=path,Width=double.Parse(root.GetAttribute("width").Replace("pt",""),CultureInfo.InvariantCulture)/72,Height=double.Parse(root.GetAttribute("height").Replace("pt",""),CultureInfo.InvariantCulture)/72};
                dynamic added=page.Drop(bridge.GetDragData(vector),4,10.5-fontNumber*.4);fontIDs.Add(definition.Id,(int)added.ID);fontNumber++;
                Check(Bridge.Read(added).Font==definition.Id&&Near(Bridge.Get(added,"Width"),vector.Width),definition.Name+" imports with its font identity and physical size");
                if(definition.Id=="mathjax"){
                    int beforeID=added.ID;bridge.Update(added,vector,false);
                    Check((int)added.ID==beforeID&&Bridge.Read(added).Font=="mathjax","MathJax vector supports in-place update");
                }
            }
            string finished=Path.Combine(output,"02-updated.vsdx"); doc.SaveAs(finished); doc.Close(); doc=null;
            bridge.Dispose(); bridge=new Bridge(false);bridge.App=ownedApp;
            doc=bridge.App.Documents.Open(finished); page=doc.Pages[1]; original=page.Shapes.ItemFromID[id];
            Check(Bridge.Read(original).Source==next.Source,"Updated source survives another save/reopen and new bridge instance");
            Check(Bridge.Read(page.Shapes.ItemFromID[duplicateID]).Source==f.Source,"Independent duplicate survives save/reopen");
            bool allFonts=true;foreach(var entry in fontIDs)allFonts &= Bridge.Read(page.Shapes.ItemFromID[entry.Value]).Font==entry.Key;
            Check(allFonts,"All seven font identities survive document save and reopen");
            page.Export(Path.Combine(output,"verified-page.svg"));
            var report=new { passed=passed.ToArray(),shapeID=id,source=Bridge.Read(original).Source,width=Bridge.Get(original,"Width"),height=Bridge.Get(original,"Height"),skipped=skipClipboard?new[]{"Clipboard lock/transfer and copy/paste: desktop clipboard unavailable"}:new string[0],nativeMouseDrag="API IDataObject transfer and gesture filtering verified; physical mouse drop not repeated" };
            File.WriteAllText(Path.Combine(output,"integration-results.json"),new JavaScriptSerializer().Serialize(report));
            doc.Saved=true; doc.Close(); doc=null;
            return 0;
        } catch(Exception ex) { Console.WriteLine(ex); File.WriteAllText(Path.Combine(output,"test-error.txt"),ex.ToString()); return 1; }
        finally { if(doc!=null){try{doc.Saved=true;doc.Close();}catch{}} if(bridge!=null)bridge.Dispose();if(ownedApp!=null)try{ownedApp.Quit();}catch{} }
    }
}
