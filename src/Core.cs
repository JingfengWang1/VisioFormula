using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml;
using System.Windows.Forms;
using ComData = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace VisioFormula {
public sealed class Formula {
    public int Version = 1;
    public string Source = @"G(s)=\frac{1}{s^2+2\zeta\omega_n s+\omega_n^2}";
    public string Font = "latex";
    public double Size = 12;
    public double BaseWidth;
    public double BaseHeight;
    public Formula Clone() { return (Formula)MemberwiseClone(); }
}
public sealed class Rendered {
    public Formula Formula;
    public string Directory, Svg, Png;
    public double Width, Height;
}
public static class Renderer {
    public static readonly string Cache = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
    public static string FindTool(string name) {
        var dirs = new List<string>();
        string config = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tex-bin.txt");
        if (File.Exists(config)) dirs.Add(File.ReadAllText(config).Trim());
        dirs.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'));
        foreach (string drive in new [] { "D:\\", "C:\\" }) {
            string root = Path.Combine(drive, "texlive");
            if (System.IO.Directory.Exists(root)) {
                var years = System.IO.Directory.GetDirectories(root); Array.Sort(years); Array.Reverse(years);
                foreach (string year in years) dirs.Add(Path.Combine(year, "bin", "windows"));
            }
        }
        foreach (string dir in dirs) { try { string p = Path.Combine(dir.Trim('"'), name + ".exe"); if(File.Exists(p)) return p; } catch {} }
        throw new Exception("找不到 " + name + "。请将 TeX 的 bin 路径写入程序旁的 tex-bin.txt。");
    }
    static void Run(string tool, string args, string cwd,CancellationToken token) {
        token.ThrowIfCancellationRequested();
        var psi = new ProcessStartInfo(FindTool(tool), args) { WorkingDirectory=cwd, UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
        using (var p = Process.Start(psi)) {
            Task<string> stdout=p.StandardOutput.ReadToEndAsync(), stderr=p.StandardError.ReadToEndAsync();
            var clock=Stopwatch.StartNew();
            using(token.Register(delegate { try { if(!p.HasExited)p.Kill(); } catch {} })) {
                while(!p.WaitForExit(40)) {
                    token.ThrowIfCancellationRequested();
                    if(clock.ElapsedMilliseconds>30000) { p.Kill(); throw new Exception(tool + " 编译超时。"); }
                }
            }
            token.ThrowIfCancellationRequested();
            string log=stdout.Result+stderr.Result;
            File.WriteAllText(Path.Combine(cwd, tool + "-output.txt"),log,Encoding.UTF8);
            if(p.ExitCode!=0) throw new Exception(log.Length>3000 ? log.Substring(log.Length-3000) : log);
        }
    }
    public static string Body(string source) {
        string s=source.Trim();
        if(s.StartsWith("$$") && s.EndsWith("$$") && s.Length>=4) s=s.Substring(2,s.Length-4);
        else if(s.StartsWith("$") && s.EndsWith("$") && s.Length>=2) s=s.Substring(1,s.Length-2);
        else if((s.StartsWith(@"\[") && s.EndsWith(@"\]")) || (s.StartsWith(@"\(") && s.EndsWith(@"\)"))) s=s.Substring(2,s.Length-4);
        s=s.Replace(@"\begin{align*}",@"\begin{aligned}").Replace(@"\end{align*}",@"\end{aligned}")
           .Replace(@"\begin{align}",@"\begin{aligned}").Replace(@"\end{align}",@"\end{aligned}")
           .Replace(@"\begin{equation*}","").Replace(@"\end{equation*}","")
           .Replace(@"\begin{equation}","").Replace(@"\end{equation}","");
        return s;
    }
    public static Rendered Render(Formula f) { return Render(f,CancellationToken.None); }
    public static Rendered Render(Formula f,CancellationToken token) {
        token.ThrowIfCancellationRequested();
        Validate(f);
        MathFont mathFont=FontCatalog.Get(f.Font);
        if(mathFont.Id=="mathjax")throw new Exception("MathJax 字体需要由工具的内嵌渲染器生成。");
        string dir=Path.Combine(Cache,Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(dir);
        string size=f.Size.ToString("0.###",CultureInfo.InvariantCulture);
        string tex="\\documentclass[border=0pt]{standalone}\n\\usepackage{amsmath}\n"+mathFont.Preamble+"\n\\usepackage{bm,xcolor}\n"+
            "\\begin{document}\n\\fontsize{"+size+"}{"+(f.Size*1.2).ToString("0.###",CultureInfo.InvariantCulture)+"}\\selectfont\n$\\displaystyle "+Body(f.Source)+"$\n\\end{document}\n";
        File.WriteAllText(Path.Combine(dir,"formula.tex"),tex,new UTF8Encoding(false));
        Run("latex","-interaction=nonstopmode -halt-on-error -no-shell-escape formula.tex",dir,token);
        Run("dvisvgm","--no-fonts=1 --exact-bbox --bbox=min --output=formula.svg formula.dvi",dir,token);
        return StoreSvg(f,dir,File.ReadAllText(Path.Combine(dir,"formula.svg")),token);
    }
    public static void Validate(Formula f){
        if(string.IsNullOrWhiteSpace(f.Source)) throw new Exception("请输入公式。");
        if(f.Source.Length>50000) throw new Exception("公式源码超过第一版的 50,000 字符限制。");
        if(f.Size<4 || f.Size>96) throw new Exception("字号应在 4–96 pt 之间。");
    }
    public static Rendered RenderMathJax(Formula f,string content,CancellationToken token){
        token.ThrowIfCancellationRequested();Validate(f);
        var xml=new XmlDocument{XmlResolver=null};xml.LoadXml(content);var root=xml.DocumentElement;
        string[] box=root.GetAttribute("viewBox").Split(new[]{' ',','},StringSplitOptions.RemoveEmptyEntries);
        if(box.Length!=4)throw new Exception("MathJax 未返回有效的 SVG 尺寸。");
        double width=double.Parse(box[2],CultureInfo.InvariantCulture)/1000*f.Size,height=double.Parse(box[3],CultureInfo.InvariantCulture)/1000*f.Size;
        if(width<=0||height<=0)throw new Exception("公式没有可导出的图形。");
        root.SetAttribute("width",width.ToString("R",CultureInfo.InvariantCulture)+"pt");root.SetAttribute("height",height.ToString("R",CultureInfo.InvariantCulture)+"pt");
        root.SetAttribute("color","black");root.RemoveAttribute("style");
        string dir=Path.Combine(Cache,Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(dir);
        return StoreSvg(f,dir,root.OuterXml,token);
    }
    static Rendered StoreSvg(Formula f,string dir,string content,CancellationToken token){
        string svg=Path.Combine(dir,"formula.svg");
        var xml=new XmlDocument { XmlResolver=null }; xml.LoadXml(content);
        var root=xml.DocumentElement;
        double width=double.Parse(root.GetAttribute("width").Replace("pt",""),CultureInfo.InvariantCulture)/72;
        double height=double.Parse(root.GetAttribute("height").Replace("pt",""),CultureInfo.InvariantCulture)/72;
        var metadata=xml.CreateElement("metadata",root.NamespaceURI); metadata.SetAttribute("id","VisioFormula");
        metadata.InnerText=new JavaScriptSerializer().Serialize(f); root.AppendChild(metadata); xml.Save(svg);
        token.ThrowIfCancellationRequested();
        return new Rendered { Formula=f.Clone(), Directory=dir, Svg=svg, Png=Path.Combine(dir,"formula.png"), Width=width, Height=height };
    }
}
public sealed partial class Bridge : IDisposable {
    public dynamic App;
    dynamic stage;
    readonly bool followActive;
    public Bridge(bool create) {
        followActive=true;
        try { App=Marshal.GetActiveObject("Visio.Application"); }
        catch { if(!create) throw new Exception("请先打开 Visio 和要编辑的文档。"); App=Activator.CreateInstance(Type.GetTypeFromProgID("Visio.Application")); App.Visible=true; }
    }
    // Explicit binding is used by isolated tests; it never attaches to a user's
    // running Visio instance or creates a second, unowned application.
    public Bridge(object application){if(application==null)throw new ArgumentNullException("application");App=application;}
    public void RefreshActive(){
        if(!followActive)return;
        try{App=Marshal.GetActiveObject("Visio.Application");}
        catch(COMException){throw new Exception("请先打开 Visio，并点击要编辑的绘图窗口，再重试。");}
    }
    static bool SameApplication(dynamic a,dynamic b){return (int)a.ProcessID==(int)b.ProcessID;}
    public static double Get(dynamic shape,string cell) { return (double)shape.CellsU[cell].ResultIU; }
    public static void Set(dynamic shape,string cell,double value) { shape.CellsU[cell].FormulaForceU=value.ToString("R",CultureInfo.InvariantCulture); }
    static void Text(dynamic shape,string name,string value) {
        string cell="User."+name;
        if(shape.CellExistsU[cell,(short)0]==0) shape.AddNamedRow((short)242,name,(short)0);
        shape.CellsU[cell].FormulaU="\""+value.Replace("\"","\"\"")+"\"";
    }
    public static bool IsFormula(dynamic s) { return s.CellExistsU["User.VFCount",(short)0]!=0; }
    public static void Write(dynamic s,Formula f) {
        string data=Convert.ToBase64String(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(f)));
        int count=(data.Length+999)/1000;
        for(int i=0;i<count;i++) Text(s,"VFData"+i,data.Substring(i*1000,Math.Min(1000,data.Length-i*1000)));
        Text(s,"VFCount",count.ToString(CultureInfo.InvariantCulture)); Text(s,"VFVersion","1");
    }
    public static Formula Read(dynamic s) {
        if(!IsFormula(s)) throw new Exception("选中的图形不是本工具生成的公式。请选中整个公式组。");
        int count=int.Parse((string)s.CellsU["User.VFCount"].ResultStr[""],CultureInfo.InvariantCulture);
        if(count<1 || count>1000) throw new Exception("公式数据长度无效。");
        var data=new StringBuilder(); for(int i=0;i<count;i++) data.Append((string)s.CellsU["User.VFData"+i].ResultStr[""]);
        var f=new JavaScriptSerializer().Deserialize<Formula>(Encoding.UTF8.GetString(Convert.FromBase64String(data.ToString())));
        if(f.Version!=1 || f.BaseWidth<=0 || f.BaseHeight<=0) throw new Exception("不支持的公式数据版本或尺寸。");
        return f;
    }
    public dynamic Selected() {
        RefreshActive();
        dynamic window=App.ActiveWindow;
        if(window==null)throw new Exception("请先在 Visio 中打开绘图页，并选中一个公式。");
        int count=window.Selection.Count;
        if(count!=1)throw new Exception("《"+(string)window.Document.Name+"》"+(count==0?"尚未选中图形，请先选中一个完整公式。":"选中了 "+count+" 个图形，请只选中一个公式。"));
        dynamic s=window.Selection[1];
        Read(s); return s;
    }
    public dynamic Stage(Rendered r) {
        RefreshActive();
        if((object)stage!=null){bool same=false;try{same=SameApplication(stage.Application,App);}catch(COMException){}catch(InvalidComObjectException){}if(!same)CloseStage();}
        if(stage==null) stage=App.Documents.AddEx("",0,64|128|256);
        dynamic page=stage.Pages[1];
        for(int i=page.Shapes.Count;i>=1;i--) page.Shapes[i].Delete();
        dynamic art=page.Import(r.Svg);
        Set(art,"Width",r.Width); Set(art,"Height",r.Height);
        Set(art,"PinX",r.Width/2); Set(art,"PinY",r.Height/2);
        dynamic boundary=page.DrawRectangle(0,0,r.Width,r.Height);
        boundary.CellsU["LinePattern"].FormulaU="0"; boundary.CellsU["FillPattern"].FormulaU="0";
        dynamic selection=page.CreateSelection(0); selection.Select(art,2); selection.Select(boundary,2);
        dynamic outer=selection.Group();
        boundary.Delete();
        outer.NameU="VF_Formula";
        Formula f=r.Formula.Clone(); f.BaseWidth=Get(outer,"Width"); f.BaseHeight=Get(outer,"Height");
        Write(outer,f);
        outer.CellsU["LockAspect"].FormulaU="1";
        outer.CellsU["SelectMode"].FormulaU="0";
        return outer;
    }
    public void Copy(Rendered r) {
        dynamic shape=Stage(r);
        for(int attempt=0;;attempt++) {
            try { shape.Copy(0); return; }
            catch(COMException ex) { if(ex.HResult!=unchecked((int)0x800401D0)||attempt>=7)throw; System.Threading.Thread.Sleep(80); }
        }
    }
    public ComData GetDragData(Rendered r) {
        object shape=Stage(r);
        return (ComData)shape;
    }
    public int Drag(Rendered r) { return NativeDrag.Begin(GetDragData(r)); }
    public void Update(dynamic target,Rendered r,bool explicitSize) {
        Formula old=Read(target);
        double width=Get(target,"Width"),height=Get(target,"Height");
        double sx=width/old.BaseWidth,sy=height/old.BaseHeight;
        if(!explicitSize && Math.Abs(sx-sy)>Math.Max(sx,sy)*0.015) throw new Exception("该公式被非等比拉伸过。请先等比缩放，或在工具中重新设定字号后更新。");
        double scale=explicitSize ? 1 : (sx+sy)/2;
        double x=Get(target,"PinX"),y=Get(target,"PinY"),angle=Get(target,"Angle");
        // The loaded target can belong to a different instance than the latest
        // selection/drag connection. Its own application must own the undo scope.
        dynamic owner=target.Application;
        int scope=owner.BeginUndoScope("更新 LaTeX 公式");
        try {
            int oldCount=target.Shapes.Count;
            dynamic art=target.Import(r.Svg);
            double nw=r.Width*scale,nh=r.Height*scale;
            Set(target,"Width",nw); Set(target,"Height",nh);
            for(int i=oldCount;i>=1;i--) target.Shapes[i].Delete();
            Set(art,"Width",nw); Set(art,"Height",nh);
            art.CellsU["PinX"].FormulaForceU="Sheet."+target.ID+"!Width*0.5";
            art.CellsU["PinY"].FormulaForceU="Sheet."+target.ID+"!Height*0.5";
            art.CellsU["LocPinX"].FormulaForceU="Width*0.5";
            art.CellsU["LocPinY"].FormulaForceU="Height*0.5";
            Set(target,"PinX",x); Set(target,"PinY",y); Set(target,"Angle",angle);
            Formula next=r.Formula.Clone(); next.BaseWidth=r.Width; next.BaseHeight=r.Height; Write(target,next);
            owner.EndUndoScope(scope,true);
        } catch { owner.EndUndoScope(scope,false); throw; }
    }
    void CloseStage(){
        if((object)stage!=null) { try { NativeDrag.Flush(); stage.Saved=true; stage.Close(); } catch {} finally{stage=null;} }
    }
    public void Dispose() {
        CloseStage();
        App=null;
    }
}
[ComVisible(true),Guid("00000121-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDropSource {
    [PreserveSig] int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escape,int keys);
    [PreserveSig] int GiveFeedback(int effect);
}
[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
public sealed class DropSource : IDropSource {
    public int QueryContinueDrag(bool escape,int keys) { if(escape) return 0x40101; return (keys&1)==0 ? 0x40100 : 0; }
    public int GiveFeedback(int effect) { return 0x40102; }
}
public static class NativeDrag {
    [DllImport("ole32.dll")] static extern int OleFlushClipboard();
    [DllImport("ole32.dll")] static extern int DoDragDrop(ComData data,IDropSource source,int allowed,out int effect);
    public static int Begin(ComData data) { int effect; int hr=DoDragDrop(data,new DropSource(),1,out effect); if(hr<0) Marshal.ThrowExceptionForHR(hr); return effect; }
    public static void Flush() { OleFlushClipboard(); }
}
}
