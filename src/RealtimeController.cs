using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace VisioFormula {
// Requests originate on the UI thread. Await captures that context, so published
// events also run there. The semaphore limits the external compiler to one job.
public sealed class RealtimeController : IDisposable {
    readonly Func<Formula,CancellationToken,Task<Rendered>> compile;
    readonly SemaphoreSlim serial=new SemaphoreSlim(1,1);
    sealed class CacheEntry { public string Key;public Rendered Result; }
    readonly LinkedList<CacheEntry> cache=new LinkedList<CacheEntry>();
    const int CacheLimit=16;
    CancellationTokenSource active;
    bool disposed;
    public int Revision { get; private set; }
    public Rendered Current { get; private set; }
    public Task LastTask { get; private set; }
    public event Action<int> Started;
    public event Action<int,Rendered> Ready;
    public event Action<int,Exception> Failed;
    public RealtimeController() : this(new Func<Formula,CancellationToken,Rendered>(Renderer.Render)) {}
    public RealtimeController(Func<Formula,CancellationToken,Rendered> compile) { this.compile=(f,token)=>Task.Run(()=>compile(f,token),token); }
    public RealtimeController(Func<Formula,CancellationToken,Task<Rendered>> compile) { this.compile=compile; }
    public static int DelayFor(Formula formula){return formula.Font=="mathjax"?140:250;}
    public int Request(Formula formula,int delayMs,bool force=false) {
        if(disposed)throw new ObjectDisposedException("RealtimeController");
        CancelActive(); Current=null; int rev=++Revision;
        if(string.IsNullOrWhiteSpace(formula.Source)) { LastTask=Task.FromResult(0); return rev; }
        active=new CancellationTokenSource();
        LastTask=Run(formula.Clone(),rev,delayMs,active,force);
        return rev;
    }
    void CancelActive() { if(active!=null) { try{active.Cancel();}catch(ObjectDisposedException){} active=null; } }
    bool CurrentRequest(int rev,CancellationToken token) { return !disposed && rev==Revision && !token.IsCancellationRequested; }
    static string Key(Formula f){return f.Version+"\n"+f.Font+"\n"+f.Size.ToString("R",CultureInfo.InvariantCulture)+"\n"+f.Source;}
    Rendered TakeCached(Formula f,bool force){
        string key=Key(f);
        for(var node=cache.First;node!=null;node=node.Next)if(node.Value.Key==key){
            var entry=node.Value;cache.Remove(node);
            if(force||!File.Exists(entry.Result.Svg))return null;
            cache.AddFirst(entry);return entry.Result;
        }
        return null;
    }
    void Remember(Formula f,Rendered result){
        if(result==null||string.IsNullOrEmpty(result.Svg)||!File.Exists(result.Svg))return;
        string key=Key(f);for(var node=cache.First;node!=null;node=node.Next)if(node.Value.Key==key){cache.Remove(node);break;}
        cache.AddFirst(new CacheEntry{Key=key,Result=result});if(cache.Count>CacheLimit)cache.RemoveLast();
    }
    async Task Run(Formula f,int rev,int delayMs,CancellationTokenSource cancellation,bool force) {
        CancellationToken token=cancellation.Token;
        bool entered=false;
        try {
            // Never publish synchronously from Request: the window must first set
            // the browser's revision, including when a cache entry is available.
            await Task.Yield();
            if(!CurrentRequest(rev,token))return;
            Rendered cached=TakeCached(f,force);
            if(cached!=null){Current=cached;if(Ready!=null)Ready(rev,cached);return;}
            if(delayMs>0)await Task.Delay(delayMs,token);
            await serial.WaitAsync(token); entered=true;
            if(!CurrentRequest(rev,token))return;
            if(Started!=null)Started(rev);
            Rendered result=await compile(f,token);
            // The revision check also rejects old results from renderers that
            // completed too late to observe cancellation.
            if(!CurrentRequest(rev,token))return;
            Remember(f,result);
            Current=result;
            if(Ready!=null)Ready(rev,result);
        } catch(OperationCanceledException) {
        } catch(Exception ex) {
            if(CurrentRequest(rev,token)) { Current=null; if(Failed!=null)Failed(rev,ex); }
        } finally { if(entered)serial.Release(); cancellation.Dispose(); }
    }
    public void Dispose() { disposed=true; Revision++; Current=null; CancelActive();cache.Clear(); }
}
}
