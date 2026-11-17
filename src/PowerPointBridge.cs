using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml;
using ComData=System.Runtime.InteropServices.ComTypes.IDataObject;

namespace VisioFormula {
// Natural sizes keep the shared Formula protocol's inch units. PowerPoint geometry is in points.
public static class PowerPointMetadata {
    const int ChunkSize=900,MaxChunks=1024;
    const string Prefix="VF_PPT_",Kind="SVG_FILL_1";
    static string Tag(dynamic shape,string key){return (string)shape.Tags[Prefix+key];}
    public static bool IsFormula(object shape){try{return Tag((dynamic)shape,"KIND")==Kind;}catch{return false;}}
    public static string Identity(object shape){return Tag((dynamic)shape,"ID");}
    public static string Encode(Formula f){return BitConverter.ToString(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(f))).Replace("-","");}
    public static Formula Decode(string data){
        if(string.IsNullOrEmpty(data)||data.Length%2!=0||data.Length>ChunkSize*MaxChunks)throw new Exception("PowerPoint 公式数据长度无效。");
        var bytes=new byte[data.Length/2];for(int i=0;i<bytes.Length;i++)bytes[i]=byte.Parse(data.Substring(i*2,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture);
        var f=new JavaScriptSerializer().Deserialize<Formula>(new UTF8Encoding(false,true).GetString(bytes));Validate(f);return f;
    }
    public static void Validate(Formula f){
        if(f==null||f.Version!=1||!Finite(f.Size)||!Finite(f.BaseWidth)||!Finite(f.BaseHeight)||f.BaseWidth<=0||f.BaseHeight<=0)throw new Exception("不支持的 PowerPoint 公式数据或尺寸。");
        Renderer.Validate(f);FontCatalog.Get(f.Font);
    }
    static bool Finite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value);}
    static string Hash(string data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.ASCII.GetBytes(data.ToUpperInvariant()))).Replace("-","");}
    public static void Write(object target,Formula f){
        Validate(f);dynamic s=target;string data=Encode(f);int count=(data.Length+ChunkSize-1)/ChunkSize;if(count>MaxChunks)throw new Exception("公式数据过长。");
        int previous;int.TryParse(Tag(s,"COUNT"),out previous);previous=Math.Min(Math.Max(0,previous),MaxChunks);
        for(int i=0;i<count;i++)s.Tags.Add(Prefix+"DATA_"+i,data.Substring(i*ChunkSize,Math.Min(ChunkSize,data.Length-i*ChunkSize)));
        for(int i=count;i<previous;i++)s.Tags.Delete(Prefix+"DATA_"+i);
        s.Tags.Add(Prefix+"HASH",Hash(data));s.Tags.Add(Prefix+"COUNT",count.ToString(CultureInfo.InvariantCulture));
        s.Tags.Add(Prefix+"VERSION","1");s.Tags.Add(Prefix+"KIND",Kind);if(string.IsNullOrEmpty(Tag(s,"ID")))s.Tags.Add(Prefix+"ID",Guid.NewGuid().ToString("N").ToUpperInvariant());
    }
    public static Formula Read(object target){
        dynamic s=target;if(!IsFormula(target)||Tag(s,"VERSION")!="1")throw new Exception("选中的图形不是本工具生成的 PowerPoint 公式。请选中完整公式。");
        int count;if(!int.TryParse(Tag(s,"COUNT"),out count)||count<1||count>MaxChunks)throw new Exception("PowerPoint 公式数据块数无效。");
        var data=new StringBuilder();for(int i=0;i<count;i++){string part=Tag(s,"DATA_"+i);if(string.IsNullOrEmpty(part)||part.Length>ChunkSize)throw new Exception("PowerPoint 公式源码不完整。");data.Append(part);}
        string encoded=data.ToString();if(!string.Equals(Hash(encoded),Tag(s,"HASH"),StringComparison.OrdinalIgnoreCase))throw new Exception("PowerPoint 公式源码校验失败。");return Decode(encoded);
    }
}
public sealed class PowerPointTarget {
    internal readonly object Slide,Presentation,Application;internal readonly int ShapeID;readonly string identity;
    public readonly string Label;
    public PowerPointTarget(object shape){
        dynamic s=shape;PowerPointMetadata.Read(shape);if((int)s.Type!=1)throw new Exception("此原型仅编辑本工具创建的独立公式图形。");
        Slide=s.Parent;dynamic slide=Slide;Presentation=slide.Parent;Application=s.Application;ShapeID=(int)s.Id;identity=PowerPointMetadata.Identity(shape);Guid token;if(!Guid.TryParseExact(identity,"N",out token))throw new Exception("PowerPoint 公式标识不完整，请重新插入。");
        Label="PowerPoint / "+(string)((dynamic)Presentation).Name+" / 幻灯片 "+(int)slide.SlideIndex+" / 公式 "+ShapeID;
        // Group children are deliberately excluded until the prototype's single-object path is accepted.
        bool grouped=false;try{object parent=s.ParentGroup;grouped=parent!=null;}catch(COMException){}catch(UnauthorizedAccessException){}if(grouped)throw new Exception("PowerPoint 原型暂不编辑组合内部的公式；请先取消外层组合。");
        Resolve();
    }
    public object Resolve(){
        try{dynamic slide=Slide;for(int i=1;i<=slide.Shapes.Count;i++){dynamic s=slide.Shapes[i];if((int)s.Id==ShapeID){if(PowerPointMetadata.Identity((object)s)!=identity)break;return s;}}}
        catch(COMException){throw new Exception("目标公式已删除或演示文稿已关闭，请重新读回公式。");}
        throw new Exception("目标公式已删除、重建或放入组合，请重新读回公式。");
    }
}
public sealed class PowerPointBridge : IFormulaHost,IDisposable {
    dynamic app,stage;IDisposable stagePin;readonly bool followActive;
    [DllImport("ole32.dll")]static extern int OleGetClipboard(out ComData data);
    public PowerPointBridge(){followActive=true;Refresh();}
    public PowerPointBridge(object application){if(application==null)throw new ArgumentNullException("application");app=application;}
    public string Name {get{return "PowerPoint";}}
    void Refresh(){if(!followActive)return;object current;try{current=Marshal.GetActiveObject("PowerPoint.Application");}catch(COMException){throw new Exception("请先打开桌面版 PowerPoint 和要编辑的演示文稿。");}if((object)app!=null&&!object.Equals((object)app,current))CloseStage();app=current;}
    public object Selected(){
        Refresh();dynamic window=app.ActiveWindow;if(window==null)throw new Exception("请先在 PowerPoint 中打开幻灯片并选中一个公式。");
        dynamic selection=window.Selection;if((int)selection.Type!=2||(int)selection.ShapeRange.Count!=1)throw new Exception("请在 PowerPoint 中只选中一个完整公式图形。");
        return new PowerPointTarget((object)selection.ShapeRange[1]);
    }
    public Formula Read(object target){return PowerPointMetadata.Read(((PowerPointTarget)target).Resolve());}
    public object Insert(object slideObject,Rendered result,double left,double top){
        ValidateResult(result);dynamic slide=slideObject,shape=null;
        using(result.Pin())try{
            shape=slide.Shapes.AddShape(1,(float)left,(float)top,(float)(result.Width*72),(float)(result.Height*72));
            shape.Line.Visible=0;shape.Shadow.Visible=0;shape.Fill.UserPicture(result.Svg);shape.LockAspectRatio=-1;
            shape.Name="VF_Formula_"+shape.Id;Formula formula=result.Formula.Clone();formula.BaseWidth=result.Width;formula.BaseHeight=result.Height;
            PowerPointMetadata.Write((object)shape,formula);return shape;
        }catch{if((object)shape!=null)try{shape.Delete();}catch{}throw;}
    }
    static void ValidateResult(Rendered result){
        if(result==null||!File.Exists(result.Svg))throw new Exception("精确渲染结果已失效，请重新生成公式。");
        Formula f=result.Formula.Clone();f.BaseWidth=result.Width;f.BaseHeight=result.Height;PowerPointMetadata.Validate(f);
        var xml=new XmlDocument{XmlResolver=null};xml.Load(result.Svg);if(xml.DocumentElement==null||xml.DocumentElement.LocalName!="svg")throw new Exception("渲染结果不是有效的 SVG。");
    }
    object Stage(Rendered result){
        Refresh();ValidateResult(result);if((object)stage!=null)try{if((int)stage.Slides.Count<1)CloseStage();}catch(COMException){CloseStage();}catch(UnauthorizedAccessException){CloseStage();}if(stagePin!=null){stagePin.Dispose();stagePin=null;}stagePin=result.Pin();
        if((object)stage==null){stage=app.Presentations.Add(0);stage.Slides.Add(1,12);}
        // Render any earlier copied object before rebuilding the hidden source slide.
        NativeDrag.Flush();dynamic slide=stage.Slides[1];for(int i=slide.Shapes.Count;i>=1;i--)slide.Shapes[i].Delete();
        object inserted=Insert((object)slide,result,0,0);stage.Saved=-1;return inserted;
    }
    public void Copy(Rendered result){dynamic shape=Stage(result);Retry(delegate{shape.Copy();});}
    public ComData GetDragData(Rendered result){
        Copy(result);ComData data=null;Retry(delegate{Marshal.ThrowExceptionForHR(OleGetClipboard(out data));});
        // Keep Office's native data provider: its private formats include live tokens, not standalone byte arrays.
        var f=new System.Runtime.InteropServices.ComTypes.FORMATETC{cfFormat=(short)System.Windows.Forms.DataFormats.GetFormat("PowerPoint 12.0 Internal Shapes").Id,dwAspect=System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL|System.Runtime.InteropServices.ComTypes.TYMED.TYMED_ISTREAM};
        if(data==null||data.QueryGetData(ref f)!=0)throw new Exception("PowerPoint 未提供可携带源码的原生拖放数据，请重新打开演示文稿后重试。");return data;
    }
    public int Drag(Rendered result){ComData data=GetDragData(result);try{return NativeDrag.Begin(data);}finally{if(data!=null&&Marshal.IsComObject(data))Marshal.ReleaseComObject(data);}}
    static void Retry(Action action){for(int attempt=0;;attempt++)try{action();return;}catch(COMException e){if((e.HResult!=unchecked((int)0x800401D0)&&e.HResult!=unchecked((int)0x80010001)&&e.HResult!=unchecked((int)0x8001010A))||attempt>=9)throw;Thread.Sleep(80);}}
    public void Update(object targetObject,Rendered result,bool explicitSize){
        var target=(PowerPointTarget)targetObject;dynamic shape=target.Resolve();Formula old=PowerPointMetadata.Read((object)shape);ValidateResult(result);
        double w=(double)shape.Width,h=(double)shape.Height,sx=w/(old.BaseWidth*72),sy=h/(old.BaseHeight*72);
        if(!explicitSize&&Math.Abs(sx-sy)>Math.Max(sx,sy)*.015)throw new Exception("该公式被非等比拉伸过；请恢复等比缩放，或重新设定字号后更新。");
        double scale=explicitSize?1:(sx+sy)/2,cx=(double)shape.Left+w/2,cy=(double)shape.Top+h/2,angle=(double)shape.Rotation;
        int aspect=(int)shape.LockAspectRatio;double nw=result.Width*72*scale,nh=result.Height*72*scale;
        dynamic owner=target.Application,pres=target.Presentation,backup=null;
        if(pres.Windows.Count>0)pres.Windows[1].Activate();else if(followActive)throw new Exception("目标演示文稿没有可编辑窗口，请重新打开并读回公式。");
        using(result.Pin()){
            owner.StartNewUndoEntry();try{
                backup=shape.Duplicate()[1];backup.Visible=0;
                shape.Fill.UserPicture(result.Svg);shape.LockAspectRatio=0;shape.Width=(float)nw;shape.Height=(float)nh;shape.Left=(float)(cx-nw/2);shape.Top=(float)(cy-nh/2);shape.Rotation=(float)angle;shape.LockAspectRatio=aspect;
                Formula next=result.Formula.Clone();next.BaseWidth=result.Width;next.BaseHeight=result.Height;PowerPointMetadata.Write((object)shape,next);
                backup.Delete();backup=null;
            }catch(Exception error){
                if((object)backup!=null)try{backup.PickUp();shape.Apply();shape.LockAspectRatio=0;shape.Width=(float)w;shape.Height=(float)h;shape.Left=(float)(cx-w/2);shape.Top=(float)(cy-h/2);shape.Rotation=(float)angle;shape.LockAspectRatio=aspect;PowerPointMetadata.Write((object)shape,old);backup.Delete();backup=null;}
                catch(Exception rollback){throw new Exception("更新失败，自动恢复也未完成。请在目标 PowerPoint 文档撤销并重新读回公式。 "+error.Message+" / "+rollback.Message);}
                throw;
            }finally{owner.StartNewUndoEntry();}
        }
    }
    void CloseStage(){if((object)stage!=null)try{NativeDrag.Flush();stage.Saved=-1;stage.Close();}catch{}finally{stage=null;}if(stagePin!=null){stagePin.Dispose();stagePin=null;}}
    public void Dispose(){CloseStage();app=null;}
}
}