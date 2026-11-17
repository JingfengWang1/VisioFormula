using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;
using VisioFormula;
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
public static class PowerPointTests {
    static readonly List<string> passed=new List<string>();static string root;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);passed.Add(message);Console.WriteLine("PASS: "+message);}
    static bool Near(double a,double b){return Math.Abs(a-b)<.005;}
    static T Field<T>(object o,string key){return (T)o.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static void Inspect(Control p){foreach(Control c in p.Controls){if(c.Visible&&(c.Left<0||c.Top<0||c.Right>p.ClientSize.Width+1||c.Bottom>p.ClientSize.Height+1))throw new Exception("Clipped "+c.GetType().Name+" "+c.Text);if(!(c is TextBoxBase)&&!(c is ComboBox)&&!(c is NumericUpDown))Inspect(c);}}
    static string XmlPart(ZipArchive zip,string part){using(var reader=new StreamReader(zip.GetEntry(part).Open()))return reader.ReadToEnd();}
    static void Package(string path,int svgShapes){using(var zip=ZipFile.OpenRead(path)){
        string xml=XmlPart(zip,"ppt/slides/slide1.xml");Check(xml.Split(new[]{"svgBlip"},StringSplitOptions.None).Length-1>=svgShapes,"Saved slide retains SVG references for formula shapes");
        Check(zip.Entries.Any(e=>e.FullName.StartsWith("ppt/media/")&&e.FullName.EndsWith(".svg")),"SVG media is embedded inside pptx");
        Check(zip.Entries.Any(e=>e.FullName.StartsWith("ppt/tags/")),"Formula metadata is embedded inside pptx");
        string rels=XmlPart(zip,"ppt/slides/_rels/slide1.xml.rels");Check(!rels.Contains("TargetMode=\"External\""),"Formula artwork does not link to the local cache");
    }}
    static Rendered Ui(){
        var settings=new SettingsStore(Path.Combine(root,"ui-settings.json"));settings.Save(new Preferences{AutoUpdate=true});Rendered fixture=null;Exception failure=null;
        using(var form=new MainForm(settings){Opacity=0,ShowInTaskbar=false}){
            form.Shown+=async delegate{try{
                Inspect(form);form.Size=form.MinimumSize;form.PerformLayout();Inspect(form);
                var destination=Field<ComboBox>(form,"destination");Check(destination.SelectedIndex==0,"Existing settings default to Visio");
                var source=Field<FormulaEditor>(form,"source");string text=source.Text;
                typeof(MainForm).GetField("target",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,new object());destination.SelectedIndex=1;
                Check(Field<object>(form,"target")==null&&source.Text==text,"Changing destination detaches the old target and preserves input");
                Check(!Field<ToolStripItem>(form,"batchMenu").Enabled&&Field<Button>(form,"copy").Text.Contains("PowerPoint"),"PowerPoint mode labels transfer and disables batch styles");
                Check(Field<UpdateBinding>(form,"binding")==null,"PowerPoint prototype does not create an automatic-update binding");
                Check(settings.Load().Destination=="powerpoint"&&settings.Load().AutoUpdate,"Destination persists without altering the Visio automatic-update preference");
                destination.SelectedIndex=0;Check(Field<ToolStripItem>(form,"batchMenu").Enabled,"Returning to Visio restores batch styles");
                Field<ComboBox>(form,"font").SelectedIndex=FontCatalog.IndexOf("mathjax");source.Text=@"\text{Test}\quad\bm{\alpha}=\color{blue}\frac{1}{s+1}\quad\begin{bmatrix}1&0\\0&1\end{bmatrix}";
                var wait=System.Diagnostics.Stopwatch.StartNew();Rendered exact=null;while((exact=Field<Rendered>(form,"ready"))==null||exact.Formula.Font!="mathjax"){if(wait.ElapsedMilliseconds>15000)throw new Exception("MathJax fixture timed out; status="+Field<Label>(form,"status").Text);await System.Threading.Tasks.Task.Delay(25);}
                string svg=Path.Combine(root,"mathjax.svg");File.Copy(exact.Svg,svg);fixture=new Rendered{Formula=exact.Formula.Clone(),Svg=svg,Directory=root,Width=exact.Width,Height=exact.Height};Check(true,"MathJax fixture comes from the actual offline preview engine");
            }catch(Exception e){failure=e;}finally{form.Close();}};
            Application.Run(form);
        }
        if(failure!=null)throw failure;return fixture;
    }
    [STAThread]public static int Main(){
        root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","powerpoint-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Environment.SetEnvironmentVariable("VISIOFORMULA_CACHE",Path.Combine(root,"cache"));
        dynamic app=null,doc=null;bool own=false;PowerPointBridge bridge=null;Rendered first=null,second=null;
        try{
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Rendered mathjax=Ui();
            try{app=Marshal.GetActiveObject("PowerPoint.Application");}catch(COMException){app=Activator.CreateInstance(Type.GetTypeFromProgID("PowerPoint.Application"));own=true;}
            doc=app.Presentations.Add(0);dynamic slide=doc.Slides.Add(1,12);bridge=new PowerPointBridge((object)app);
            first=Renderer.Render(new Formula{Source=@"\alpha+\Gamma=\frac{1}{s+1}\quad\text{AbC}",Font="times",Size=12});dynamic shape=bridge.Insert((object)slide,first,100,120);var target=new PowerPointTarget((object)shape);
            int id=shape.Id,z=shape.ZOrderPosition;Check(bridge.Read(target).Source==first.Formula.Source,"Insert/read retains exact case-sensitive TeX source");
            Check(Near((double)shape.Width,first.Width*72)&&Near((double)shape.Height,first.Height*72),"Natural dimensions use inches-to-points conversion");
            var encoded=PowerPointMetadata.Encode(bridge.Read(target));Check(PowerPointMetadata.Decode(encoded.ToLowerInvariant()).Source==first.Formula.Source,"Metadata encoding survives host case normalization");
            dynamic meta=slide.Shapes.AddShape(1,0,0,1,1);var longFormula=bridge.Read(target);longFormula.Source="% 中文 AbC Γ\n"+new string('x',5000);PowerPointMetadata.Write((object)meta,longFormula);Check(PowerPointMetadata.Read((object)meta).Source==longFormula.Source,"Chunked metadata roundtrips long Unicode source");
            string old=(string)meta.Tags["VF_PPT_DATA_0"];meta.Tags.Add("VF_PPT_DATA_0","0"+old.Substring(1));bool rejected=false;try{PowerPointMetadata.Read((object)meta);}catch{rejected=true;}Check(rejected,"Corrupt formula metadata is rejected");meta.Delete();
            dynamic pasteSlide=doc.Slides.Add(2,12);for(int i=0;i<3;i++){
                var data=bridge.GetDragData(first);Check(data!=null,"Native drag data provider acquired, attempt "+(i+1));
                dynamic pasted=pasteSlide.Shapes.Paste()[1];Check(PowerPointMetadata.Read((object)pasted).Source==first.Formula.Source,"Native paste retains formula metadata, attempt "+(i+1));
                if(Marshal.IsComObject(data))Marshal.ReleaseComObject(data);
            }
            dynamic staging=Field<object>(bridge,"stage");staging.Saved=-1;staging.Close();bridge.Copy(first);dynamic recovered=pasteSlide.Shapes.Paste()[1];Check(PowerPointMetadata.Read((object)recovered).Source==first.Formula.Source,"Transfer recovers after its own staging presentation is closed");
            bridge.Dispose();bridge=new PowerPointBridge((object)app);dynamic afterClose=pasteSlide.Shapes.Paste()[1];Check(PowerPointMetadata.Read((object)afterClose).Source==first.Formula.Source,"Copied formula remains readable after staging presentation closes");
            dynamic fontSlide=doc.Slides.Add(3,12);int fontIndex=0;foreach(var font in FontCatalog.All){using(var rendered=font.Id=="mathjax"?mathjax:Renderer.Render(new Formula{Font=font.Id,Source=mathjax.Formula.Source})){dynamic item=bridge.Insert((object)fontSlide,rendered,30,30+fontIndex++*60);Check(PowerPointMetadata.Read((object)item).Font==font.Id&&Near((double)item.Width,rendered.Width*72),"PowerPoint embeds "+font.Id+" with matching metadata and physical size");}}
            string initial=Path.Combine(root,"initial.pptx");doc.SaveAs(initial,24);Package(initial,1);doc.Close();doc=null;first.Dispose();first=null;CacheStore.Default.Clean(true,20,7);
            doc=app.Presentations.Open(initial,0,0,0);fontSlide=doc.Slides[3];Check((int)fontSlide.Shapes.Count==7&&Enumerable.Range(1,7).All(i=>PowerPointMetadata.Read((object)fontSlide.Shapes[i]).Font==FontCatalog.All[i-1].Id),"All seven font identities survive PowerPoint save/reopen");slide=doc.Slides[1];shape=slide.Shapes[1];target=new PowerPointTarget((object)shape);Check(bridge.Read(target).Source.Contains(@"\Gamma")&&bridge.Read(target).Font=="times","Save, reopen and read back work without the original cache");
            shape.LockAspectRatio=0;shape.Width=(float)((double)shape.Width*1.4);shape.Height=(float)((double)shape.Height*1.4);shape.LockAspectRatio=-1;shape.Rotation=25f;double cx=(double)shape.Left+(double)shape.Width/2,cy=(double)shape.Top+(double)shape.Height/2;
            dynamic effect=slide.TimeLine.MainSequence.AddEffect(shape,1);int animations=slide.TimeLine.MainSequence.Count;id=shape.Id;z=shape.ZOrderPosition;
            second=Renderer.Render(new Formula{Source=@"\frac{\alpha+\Gamma}{s^2+2s+1}",Font="stix2",Size=12});bridge.Update(target,second,false);shape=target.Resolve();
            Check((int)shape.Id==id&&(int)shape.ZOrderPosition==z,"In-place update keeps the shape ID and stacking order");
            Check(Near((double)shape.Left+(double)shape.Width/2,cx)&&Near((double)shape.Top+(double)shape.Height/2,cy)&&Near((double)shape.Rotation,25),"In-place update keeps center and rotation");
            Check(Near((double)shape.Width,second.Width*72*1.4)&&Near((double)shape.Height,second.Height*72*1.4),"Content changes keep the existing uniform scale");
            Check(bridge.Read(target).Source==second.Formula.Source&&bridge.Read(target).Font=="stix2","In-place update commits matching source and font metadata");
            Check((int)slide.Shapes.Count==1&&(int)slide.TimeLine.MainSequence.Count==animations&&(int)slide.TimeLine.MainSequence[1].Shape.Id==id,"Update removes backup shapes and preserves the original animation target");
            using(var sizeResult=Renderer.Render(new Formula{Source="x+1",Size=18})){bridge.Update(target,sizeResult,true);Check(Near((double)((dynamic)target.Resolve()).Width,sizeResult.Width*72)&&bridge.Read(target).Size==18,"Explicit font size resets the imported natural dimensions");}
            shape=target.Resolve();shape.LockAspectRatio=0;shape.Width=(float)((double)shape.Width*2);rejected=false;try{bridge.Update(target,second,false);}catch{rejected=true;}Check(rejected&&bridge.Read(target).Source=="x+1","Nonuniform scaling is rejected before changing formula content");
            shape.Width=(float)((double)shape.Width/2);shape.LockAspectRatio=-1;
            string invalid=Path.Combine(root,"invalid.svg");File.WriteAllText(invalid,"<not-svg/>");rejected=false;try{bridge.Update(target,new Rendered{Formula=second.Formula,Width=second.Width,Height=second.Height,Svg=invalid},false);}catch{rejected=true;}Check(rejected&&bridge.Read(target).Source=="x+1","Invalid SVG is rejected without modifying the target");
            string final=Path.Combine(root,"updated.pptx");doc.SaveAs(final,24);Package(final,1);doc.Close();doc=null;doc=app.Presentations.Open(final,0,0,0);shape=doc.Slides[1].Shapes[1];Check(PowerPointMetadata.Read((object)shape).Size==18,"Updated metadata survives a second save/reopen");
            target=new PowerPointTarget((object)shape);shape.Delete();rejected=false;try{target.Resolve();}catch{rejected=true;}Check(rejected,"Deleted targets are rejected instead of updating another shape");
            File.WriteAllText(Path.Combine(root,"results.json"),new JavaScriptSerializer().Serialize(new{passed=passed,manualPending=new[]{"Physical mouse drag/drop","PowerPoint Ctrl+Z/Ctrl+Y grouping","Visual fidelity and cross-window behavior"}}));Console.WriteLine("RESULTS: "+root);return 0;
        }catch(Exception e){Console.Error.WriteLine(e);Console.Error.WriteLine("RESULTS: "+root);return 1;}
        finally{if(bridge!=null)bridge.Dispose();if(first!=null)first.Dispose();if(second!=null)second.Dispose();if(doc!=null)try{doc.Saved=-1;doc.Close();}catch{}if(own&&app!=null)try{if(app.Presentations.Count==0)app.Quit();}catch{}}
    }
}