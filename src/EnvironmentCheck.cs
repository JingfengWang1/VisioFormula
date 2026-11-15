using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace VisioFormula {
public static class Toolchain {
    static readonly ConcurrentDictionary<string,string> versions=new ConcurrentDictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    public static IEnumerable<string> CandidateDirectories(string applicationDirectory){
        var dirs=new List<string>();string config=Path.Combine(applicationDirectory,"tex-bin.txt");
        if(File.Exists(config))try{dirs.Add(File.ReadAllText(config).Trim());}catch(IOException){}catch(UnauthorizedAccessException){}
        dirs.AddRange((Environment.GetEnvironmentVariable("PATH")??"").Split(';'));
        foreach(var drive in DriveInfo.GetDrives())try{if(drive.DriveType!=DriveType.Fixed||!drive.IsReady)continue;string root=Path.Combine(drive.RootDirectory.FullName,"texlive");if(Directory.Exists(root))foreach(string year in Directory.GetDirectories(root).OrderByDescending(x=>x))dirs.Add(Path.Combine(year,"bin","windows"));}catch(IOException){}catch(UnauthorizedAccessException){}
        foreach(string baseDir in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs"),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}){
            if(string.IsNullOrEmpty(baseDir))continue;dirs.Add(Path.Combine(baseDir,"MiKTeX","miktex","bin","x64"));dirs.Add(Path.Combine(baseDir,"MiKTeX","miktex","bin"));
        }
        return dirs.Where(d=>!string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.OrdinalIgnoreCase);
    }
    public static string Find(string name,string selected=null){return Resolve(name,selected,CandidateDirectories(AppDomain.CurrentDomain.BaseDirectory));}
    public static string Resolve(string name,string selected,IEnumerable<string> candidates){
        bool manual=!string.IsNullOrWhiteSpace(selected);
        foreach(string dir in manual?new[]{selected}:candidates)try{string file=Path.Combine(dir.Trim().Trim('"'),name+".exe");if(File.Exists(file))return Path.GetFullPath(file);}catch(ArgumentException){}catch(NotSupportedException){}
        throw new IOException((manual?"指定目录中":"本机")+"找不到 "+name+".exe；请在“环境检查”中选择 TeX 的 bin 目录。");
    }
    public static string Execute(string file,string arguments,string cwd,int timeoutMs,CancellationToken token,out int exitCode){
        token.ThrowIfCancellationRequested();var start=new ProcessStartInfo(file,arguments){WorkingDirectory=cwd,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var process=Process.Start(start)){
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();var watch=Stopwatch.StartNew();
            using(token.Register(delegate{try{if(!process.HasExited)process.Kill();}catch{}})){
                while(!process.WaitForExit(40)){token.ThrowIfCancellationRequested();if(watch.ElapsedMilliseconds>timeoutMs){try{process.Kill();}catch{}throw new TimeoutException(Path.GetFileNameWithoutExtension(file)+" 执行超时。");}}
            }
            token.ThrowIfCancellationRequested();exitCode=process.ExitCode;return stdout.Result+stderr.Result;
        }
    }
    public static string Version(string file,CancellationToken token){
        string found;if(versions.TryGetValue(file,out found))return found;int code;
        string text=Execute(file,"--version",Path.GetDirectoryName(file),5000,token,out code);if(code!=0)throw new IOException("工具无法正常启动："+Path.GetFileName(file));
        found=text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"版本未知";versions[file]=found;return found;
    }
    public static void Refresh(){versions.Clear();}
    public static string EngineArguments(string latex,CancellationToken token){return Version(latex,token).IndexOf("MiKTeX",StringComparison.OrdinalIgnoreCase)>=0?"--disable-installer ":"";}
}
public sealed class CheckResult {
    public string Name,State,Detail;public bool Problem;
    public CheckResult(string name,string state,string detail,bool problem=false){Name=name;State=state;Detail=detail;Problem=problem;}
}
public static class EnvironmentInspector {
    public static CheckResult Visio(){
        try{
            if(Type.GetTypeFromProgID("Visio.Application")==null)return new CheckResult("Visio","未安装","安装桌面版 Visio 后可拖放、读回与更新。",true);
            object obj;try{obj=Marshal.GetActiveObject("Visio.Application");}catch(COMException){return new CheckResult("Visio","尚未打开","已找到桌面 Visio；打开绘图后即可连接。");}
            dynamic app=obj;dynamic window=app.ActiveWindow;
            return new CheckResult("Visio",window==null?"已连接，未打开绘图":"已连接",window==null?"请打开一个绘图文档。":"版本 "+(string)app.Version+" · "+(string)window.Document.Name);
        }catch(Exception e){return new CheckResult("Visio","连接失败","请让两个程序以相同用户权限运行并重新打开绘图。 "+Short(e),true);}
    }
    static CheckResult Writable(string name,string dir){
        string probe=null;try{Directory.CreateDirectory(dir);probe=Path.Combine(dir,"vf-probe-"+Guid.NewGuid().ToString("N")+".tmp");File.WriteAllText(probe,"probe");return new CheckResult(name,"可写",dir);}catch(Exception e){return new CheckResult(name,"不可写",dir+" · "+Short(e),true);}finally{if(probe!=null)try{File.Delete(probe);}catch{}}
    }
    public static List<CheckResult> Basic(string texDirectory,string settingsPath,CacheStore cache,string assets,CancellationToken token){
        var rows=new List<CheckResult>();foreach(string name in new[]{"latex","dvisvgm"}){
            token.ThrowIfCancellationRequested();try{string file=Toolchain.Find(name,texDirectory);rows.Add(new CheckResult(name,"可用",file+"\n"+Toolchain.Version(file,token)));}catch(OperationCanceledException){throw;}catch(Exception e){rows.Add(new CheckResult(name,"不可用",Short(e),true));}
        }
        string[] required={"preview.html","tex-svg-full.js","polyfill.js"};bool resource=required.All(f=>File.Exists(Path.Combine(assets,f))&&new FileInfo(Path.Combine(assets,f)).Length>0);
        rows.Add(new CheckResult("离线 MathJax",resource?"资源齐全":"资源缺失",resource?"点击“测试渲染”验证内嵌浏览器与 SVG 导出。":"请复制完整 assets 文件夹。",!resource));
        rows.Add(Writable("设置与草稿",Path.GetDirectoryName(settingsPath)));rows.Add(Writable("渲染缓存",cache.Root));
        foreach(var font in FontCatalog.All)rows.Add(new CheckResult(font.Name,"待测试",font.Id=="mathjax"?"离线字体，无需 TeX。":"点击“测试渲染”检查实际字体和宏包。"));
        return rows;
    }
    public static string Short(Exception e){string text=e.Message;foreach(string line in text.Split('\n'))if(line.TrimStart().StartsWith("!"))return line.Trim().TrimStart('!').Trim();return text.Length>240?text.Substring(0,240)+"…":text;}
    public static string FontFailure(MathFont font,Exception e){return Short(e)+"\n请通过 TeX Live Manager 或 MiKTeX Console 检查/安装相关宏包："+(font.Preamble??"请检查完整离线资源及 Windows 浏览器组件。")+"。修复后重新测试。";}
}
}