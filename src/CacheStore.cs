using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace VisioFormula {
public sealed class ActionLease : IDisposable {
    Action release;
    public ActionLease(Action action){release=action;}
    public void Dispose(){var action=Interlocked.Exchange(ref release,null);if(action!=null)action();}
}
public sealed class CacheLease : IDisposable {
    readonly object gate=new object();FileStream stream;int references=1;bool ownerReleased;
    public readonly string Directory;
    internal CacheLease(string directory,FileStream file){Directory=directory;stream=file;}
    public IDisposable Retain(){lock(gate){if(stream==null)throw new ObjectDisposedException("CacheLease");references++;Touch();return new ActionLease(Release);}}
    public void Touch(){try{File.SetLastWriteTimeUtc(Path.Combine(Directory,".vf-cache"),DateTime.UtcNow);}catch(IOException){}catch(UnauthorizedAccessException){}}
    void Release(){lock(gate){if(--references==0&&stream!=null){stream.Dispose();stream=null;GC.SuppressFinalize(this);}}}
    public void Dispose(){lock(gate){if(ownerReleased)return;ownerReleased=true;Release();}}
    ~CacheLease(){if(stream!=null)stream.Dispose();}
}
public sealed class CacheSummary {
    public long Bytes,AvailableBytes,ProtectedBytes,RemovedBytes;public int Entries,ProtectedEntries,RemovedEntries,Skipped;
}
public sealed class CacheStore {
    public const string Marker="VisioFormula cache v1";
    public readonly string Root,LegacyRoot;readonly string mutexName;
    public static readonly CacheStore Default=new CacheStore(DefaultRoot(),Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cache"));
    static string DefaultRoot(){string custom=Environment.GetEnvironmentVariable("VISIOFORMULA_CACHE");return string.IsNullOrWhiteSpace(custom)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VisioFormula","Cache"):custom;}
    public CacheStore(string root,string legacy=null){Root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);LegacyRoot=legacy==null?null:Path.GetFullPath(legacy).TrimEnd(Path.DirectorySeparatorChar);using(var hash=SHA256.Create())mutexName="Local\\VisioFormulaCache_"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Root.ToUpperInvariant()))).Replace("-","");}
    IDisposable Enter(){var mutex=new Mutex(false,mutexName);bool held=false;try{try{held=mutex.WaitOne(10000);}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("缓存正忙，请稍后重试。");return new ActionLease(delegate{mutex.ReleaseMutex();mutex.Dispose();});}catch{mutex.Dispose();throw;}}
    static bool NoLinks(string path){try{for(var d=new DirectoryInfo(path);d!=null;d=d.Parent)if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)return false;return true;}catch{return false;}}
    public CacheLease Create(){using(Enter()){
        if(!NoLinks(Root))throw new IOException("缓存路径包含目录链接，请选择普通本地目录。");System.IO.Directory.CreateDirectory(Root);
        string dir=Path.Combine(Root,Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(dir);
        FileStream file=null;try{file=new FileStream(Path.Combine(dir,".lease"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.Read);File.WriteAllText(Path.Combine(dir,".vf-cache"),Marker);return new CacheLease(dir,file);}catch{if(file!=null)file.Dispose();throw;}
    }}
    sealed class Entry {public string Path;public long Size;public DateTime Used;public bool Legacy,ReadOnly;}
    static bool Allowed(string name,bool legacy){return Regex.IsMatch(name,@"^formula\.(tex|aux|log|dvi|svg|out)$",RegexOptions.IgnoreCase)||name=="latex-output.txt"||name=="dvisvgm-output.txt"||(!legacy&&(name==".vf-cache"||name==".lease"));}
    static Entry Inspect(string root,string path,bool legacy){
        if(!NoLinks(root)||!NoLinks(path)||!string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),root,StringComparison.OrdinalIgnoreCase))return null;
        Guid id;if(!Guid.TryParseExact(Path.GetFileName(path),"N",out id)||System.IO.Directory.GetDirectories(path).Length!=0)return null;
        var files=new DirectoryInfo(path).GetFiles();if(files.Any(f=>(f.Attributes&FileAttributes.ReparsePoint)!=0||!Allowed(f.Name,legacy)))return null;
        if(legacy){if(!files.Any(f=>f.Name=="formula.tex"||f.Name=="formula.svg"))return null;}
        else if(!File.Exists(Path.Combine(path,".lease"))||!File.Exists(Path.Combine(path,".vf-cache"))||File.ReadAllText(Path.Combine(path,".vf-cache"))!=Marker)return null;
        return new Entry{Path=path,Size=files.Sum(f=>f.Length),Legacy=legacy,ReadOnly=files.Any(f=>(f.Attributes&FileAttributes.ReadOnly)!=0),Used=legacy?files.Max(f=>f.LastWriteTimeUtc):File.GetLastWriteTimeUtc(Path.Combine(path,".vf-cache"))};
    }
    public bool LegacyMayBeRunning(){
        if(LegacyRoot==null)return false;string appDir=Path.GetDirectoryName(LegacyRoot);
        foreach(var p in Process.GetProcessesByName("VisioFormula"))using(p){try{if(p.Id!=Process.GetCurrentProcess().Id&&string.Equals(Path.GetDirectoryName(p.MainModule.FileName),appDir,StringComparison.OrdinalIgnoreCase))return true;}catch{return true;}}
        return false;
    }
    public CacheSummary Inspect(bool includeLegacy=true){return Operate(false,false,20,7,includeLegacy);}
    public CacheSummary Clean(bool all,int limitMB,int days,bool includeLegacy=false){if(limitMB<1||limitMB>10240||days<1||days>365)throw new ArgumentOutOfRangeException("缓存保留范围无效。");return Operate(true,all,limitMB,days,includeLegacy);}
    CacheSummary Operate(bool clean,bool all,int limitMB,int days,bool includeLegacy){using(Enter()){
        var summary=new CacheSummary();var entries=new List<Entry>();
        foreach(string root in includeLegacy&&LegacyRoot!=null&&!string.Equals(Root,LegacyRoot,StringComparison.OrdinalIgnoreCase)?new[]{Root,LegacyRoot}:new[]{Root}){
            bool legacy=string.Equals(root,LegacyRoot,StringComparison.OrdinalIgnoreCase);
            if(!NoLinks(root)){summary.Skipped++;continue;}if(!System.IO.Directory.Exists(root))continue;
            try{foreach(string dir in System.IO.Directory.GetDirectories(root)){try{var item=Inspect(root,dir,legacy);if(item==null){summary.Skipped++;continue;}entries.Add(item);summary.Bytes+=item.Size;summary.Entries++;}catch(IOException){summary.Skipped++;}catch(UnauthorizedAccessException){summary.Skipped++;}}}catch(IOException){summary.Skipped++;}catch(UnauthorizedAccessException){summary.Skipped++;}
        }
        long remaining=summary.Bytes;bool oldBusy=includeLegacy&&LegacyMayBeRunning();
        foreach(var entry in entries.OrderBy(e=>e.Used)){
            FileStream guard=null;try{
                if(entry.ReadOnly||(entry.Legacy&&oldBusy)){summary.ProtectedBytes+=entry.Size;summary.ProtectedEntries++;continue;}
                if(!entry.Legacy)guard=new FileStream(Path.Combine(entry.Path,".lease"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);
                else foreach(string file in System.IO.Directory.GetFiles(entry.Path))using(var check=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.None)){}
                summary.AvailableBytes+=entry.Size;
                if(!clean||(!all&&entry.Used>=DateTime.UtcNow.AddDays(-days)&&remaining<=limitMB*1024L*1024L))continue;
                // Revalidate absolute boundaries and every filename immediately before deletion; never recurse or follow links.
                string parent=entry.Legacy?LegacyRoot:Root;if(Inspect(parent,entry.Path,entry.Legacy)==null){summary.Skipped++;continue;}
                foreach(string file in System.IO.Directory.GetFiles(entry.Path).Where(f=>Path.GetFileName(f)!=".lease"&&Path.GetFileName(f)!=".vf-cache")){
                    long bytes=new FileInfo(file).Length;File.Delete(file);summary.RemovedBytes+=bytes;remaining-=bytes;
                }
                if(!entry.Legacy){string marker=Path.Combine(entry.Path,".vf-cache");long bytes=new FileInfo(marker).Length;File.Delete(marker);summary.RemovedBytes+=bytes;remaining-=bytes;}
                if(guard!=null){guard.Dispose();guard=null;}if(!entry.Legacy){string file=Path.Combine(entry.Path,".lease");long bytes=new FileInfo(file).Length;File.Delete(file);summary.RemovedBytes+=bytes;remaining-=bytes;}
                System.IO.Directory.Delete(entry.Path,false);summary.RemovedEntries++;
            }catch(IOException){summary.ProtectedBytes+=entry.Size;summary.ProtectedEntries++;}catch(UnauthorizedAccessException){summary.Skipped++;}finally{if(guard!=null)guard.Dispose();}
        }
        return summary;
    }}
}
}