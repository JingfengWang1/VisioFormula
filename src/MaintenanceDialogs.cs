using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VisioFormula {
public sealed class AppSettingsDialog : Form {
    readonly CheckBox autoUpdate=new ThemeCheckBox{Text="自动更新已关联的 Visio 公式",Dock=DockStyle.Fill};
    public bool AutoUpdate {get{return autoUpdate.Checked;}}
    public AppSettingsDialog(bool enabled,Action<IWin32Window> quick,Action<IWin32Window> environment,Action<IWin32Window> cache){
        DialogTheme.Initialize(this,"设置",new Size(490,300));MinimumSize=SizeFromClientSize(new Size(440,300));
        var rows=DialogTheme.Rows(52,38,64,12,38,-1,36);rows.Padding=new Padding(16);
        rows.Controls.Add(DialogTheme.Header("设置","调整公式同步、输入习惯与本机环境。"),0,0);autoUpdate.Checked=enabled;rows.Controls.Add(autoUpdate,0,1);
        rows.Controls.Add(DialogTheme.Label("读回 Visio 公式后，停顿约 600 ms 且精确结果就绪时自动同步。\n每次同步可在 Visio 撤销；目标被修改或撤销后暂停。\n重开软件需要重新读回目标公式。",true),0,2);
        var links=DialogTheme.Columns(-1,8,-1,8,-1);links.Controls.Add(DialogTheme.Button("快捷输入…",delegate{if(quick!=null)quick(this);},false),0,0);links.Controls.Add(DialogTheme.Button("环境检查…",delegate{if(environment!=null)environment(this);},false),2,0);links.Controls.Add(DialogTheme.Button("缓存管理…",delegate{if(cache!=null)cache(this);},false),4,0);rows.Controls.Add(links,0,4);
        var actions=DialogTheme.Columns(-1,76,8,76);var cancel=DialogTheme.Button("取消",delegate{DialogResult=DialogResult.Cancel;},false);var save=DialogTheme.Button("保存",delegate{DialogResult=DialogResult.OK;},true);actions.Controls.Add(cancel,1,0);actions.Controls.Add(save,3,0);rows.Controls.Add(actions,0,6);CancelButton=cancel;AcceptButton=save;Controls.Add(rows);ResumeLayout(true);
    }
}
public sealed class CacheDialog : Form {
    readonly CacheStore store;readonly CheckBox enabled=new ThemeCheckBox{Text="自动清理",Dock=DockStyle.Fill},legacy=new ThemeCheckBox{Text="包含此程序旁的旧版缓存",Dock=DockStyle.Fill};
    readonly NumericUpDown limit=new NumericUpDown{Minimum=1,Maximum=10240,BorderStyle=BorderStyle.None},days=new NumericUpDown{Minimum=1,Maximum=365,BorderStyle=BorderStyle.None};
    readonly Label summary=DialogTheme.Label("正在统计…",false),message=DialogTheme.Label("",true);readonly Button clean,save,close;bool working;
    public bool AutoClean {get{return enabled.Checked;}}public int LimitMB {get{return (int)limit.Value;}}public int Days {get{return (int)days.Value;}}
    public CacheDialog(CacheStore cache,Preferences options){
        store=cache;DialogTheme.Initialize(this,"缓存管理",new Size(520,410));MinimumSize=SizeFromClientSize(new Size(470,410));
        var rows=DialogTheme.Rows(52,50,32,40,32,44,-1,36);rows.Padding=new Padding(16);rows.Controls.Add(DialogTheme.Header("缓存管理","清理可重新生成的编译文件，保留正在使用的结果。"),0,0);rows.Controls.Add(summary,0,1);
        enabled.Checked=options.AutoClean;limit.Value=options.CacheLimitMB;days.Value=options.CacheDays;rows.Controls.Add(enabled,0,2);
        var settings=DialogTheme.Columns(76,72,38,24,60,64,32);settings.Controls.Add(DialogTheme.Label("容量目标",true),0,0);settings.Controls.Add(DialogTheme.Input(limit),1,0);settings.Controls.Add(DialogTheme.Label("MB",true),2,0);settings.Controls.Add(DialogTheme.Label("保留",true),4,0);settings.Controls.Add(DialogTheme.Input(days),5,0);settings.Controls.Add(DialogTheme.Label("天",true),6,0);rows.Controls.Add(settings,0,3);
        rows.Controls.Add(legacy,0,4);rows.Controls.Add(DialogTheme.Label("使用中的缓存可能暂时超过容量目标。\n草稿、设置、导出文件和 Visio 文档不属于缓存清理范围。",true),0,5);rows.Controls.Add(message,0,6);
        var actions=DialogTheme.Columns(88,8,88,-1,64,8,64);clean=DialogTheme.Button("立即清理",async delegate{await RefreshStats(true);},false);var open=DialogTheme.Button("打开目录",delegate{try{Directory.CreateDirectory(store.Root);Process.Start(new ProcessStartInfo("explorer.exe","\""+store.Root+"\""){UseShellExecute=true});}catch(Exception e){message.Text=e.Message;}},false);close=DialogTheme.Button("取消",delegate{DialogResult=DialogResult.Cancel;},false);save=DialogTheme.Button("保存",delegate{DialogResult=DialogResult.OK;},true);actions.Controls.Add(clean,0,0);actions.Controls.Add(open,2,0);actions.Controls.Add(close,4,0);actions.Controls.Add(save,6,0);rows.Controls.Add(actions,0,7);
        legacy.CheckedChanged+=async delegate{await RefreshStats(false);};Shown+=async delegate{await RefreshStats(false);};FormClosing+=delegate(object sender,FormClosingEventArgs e){if(working)e.Cancel=true;};CancelButton=close;AcceptButton=save;Controls.Add(rows);ResumeLayout(true);
    }
    static string FormatSize(long bytes){return (bytes/1024d/1024d).ToString("0.00")+" MB";}
    async Task RefreshStats(bool remove){if(working)return;working=true;clean.Enabled=save.Enabled=close.Enabled=legacy.Enabled=false;bool old=legacy.Checked;int mb=LimitMB,retention=Days;try{
        CacheSummary removed=null;if(remove)removed=await Task.Run(()=>store.Clean(true,mb,retention,old));var report=await Task.Run(()=>store.Inspect(old));
        summary.Text="共 "+FormatSize(report.Bytes)+" · "+report.Entries+" 项\n可清理 "+FormatSize(report.AvailableBytes)+" · 使用中 "+FormatSize(report.ProtectedBytes);
        message.Text=(removed==null?"目录："+store.Root:"已释放 "+FormatSize(removed.RemovedBytes)+"，清理 "+removed.RemovedEntries+" 项。")+(report.Skipped>0?"\n跳过 "+report.Skipped+" 个无法确认或无法访问的目录。":"");
    }catch(Exception e){message.Text="清理未完成："+e.Message;}finally{working=false;clean.Enabled=save.Enabled=close.Enabled=legacy.Enabled=true;}}
}
public sealed class EnvironmentDialog : Form {
    readonly string settingsPath;readonly Func<CancellationToken,Task<Rendered>> mathjax;readonly List<CheckResult> results=new List<CheckResult>();
    readonly TextBox report=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,BackColor=Color.White,Font=new Font("Microsoft YaHei UI",9F)};
    readonly Label path=DialogTheme.Label("",true),message=DialogTheme.Label("",true);readonly Button detect,test,choose,automatic,copy,close;CancellationTokenSource cancellation;bool working;
    public string TexDirectory {get;private set;}public bool PathChanged {get;private set;}
    public EnvironmentDialog(string selected,string settings,Func<CancellationToken,Task<Rendered>> renderMathJax){
        TexDirectory=selected;settingsPath=settings;mathjax=renderMathJax;DialogTheme.Initialize(this,"环境检查",new Size(650,530));MinimumSize=SizeFromClientSize(new Size(520,420));
        var rows=DialogTheme.Rows(52,40,36,10,-1,32,36);rows.Padding=new Padding(16);rows.Controls.Add(DialogTheme.Header("环境检查","检查本机工具、字体与存储；完整测试按需运行。"),0,0);rows.Controls.Add(path,0,1);
        var location=DialogTheme.Columns(120,8,108,-1);choose=DialogTheme.Button("选择 TeX 路径",delegate{using(var dialog=new FolderBrowserDialog{Description="选择包含 latex.exe 和 dvisvgm.exe 的 bin 目录",SelectedPath=TexDirectory})if(dialog.ShowDialog(this)==DialogResult.OK){try{Toolchain.Find("latex",dialog.SelectedPath);Toolchain.Find("dvisvgm",dialog.SelectedPath);TexDirectory=dialog.SelectedPath;PathChanged=true;PathLabel();message.Text="目录已选择；关闭窗口后保存，点击测试确认字体可用。";}catch(Exception e){message.Text=e.Message;}}},false);automatic=DialogTheme.Button("恢复自动查找",delegate{TexDirectory="";PathChanged=true;Toolchain.Refresh();PathLabel();message.Text="将使用自动查找；关闭窗口后保存。";},false);location.Controls.Add(choose,0,0);location.Controls.Add(automatic,2,0);rows.Controls.Add(location,0,2);
        var card=DialogTheme.Card(12);card.Controls.Add(report);rows.Controls.Add(card,0,4);rows.Controls.Add(message,0,5);
        var actions=DialogTheme.Columns(88,8,88,8,108,-1,76);detect=DialogTheme.Button("重新检测",async delegate{await Check(false);},false);test=DialogTheme.Button("测试渲染",async delegate{await Check(true);},true);copy=DialogTheme.Button("复制诊断信息",delegate{try{Clipboard.SetText("Visio Formula 0.10.0 prototype\r\n"+path.Text+"\r\n"+report.Text,TextDataFormat.UnicodeText);message.Text="诊断信息已复制，包含本机路径。";}catch(Exception e){message.Text="剪贴板忙，请稍后重试。 "+e.Message;}},false);close=DialogTheme.Button("关闭",delegate{if(working){cancellation.Cancel();message.Text="正在取消检查…";}else Close();},false);actions.Controls.Add(detect,0,0);actions.Controls.Add(test,2,0);actions.Controls.Add(copy,4,0);actions.Controls.Add(close,6,0);rows.Controls.Add(actions,0,6);
        Shown+=async delegate{await Check(false);};FormClosing+=delegate(object sender,FormClosingEventArgs e){if(working){cancellation.Cancel();e.Cancel=true;}};CancelButton=close;Controls.Add(rows);PathLabel();ResumeLayout(true);
    }
    void PathLabel(){path.Text=string.IsNullOrWhiteSpace(TexDirectory)?"TeX 路径：自动查找（兼容 tex-bin.txt）":"TeX 路径："+TexDirectory;}
    void Display(){var text=new StringBuilder();foreach(var row in results)text.AppendLine(row.Name+"  ·  "+row.State).AppendLine(row.Detail).AppendLine();report.Text=text.ToString();}
    async Task Check(bool fonts){if(working)return;working=true;detect.Enabled=test.Enabled=choose.Enabled=automatic.Enabled=copy.Enabled=false;close.Text="取消检查";cancellation=new CancellationTokenSource();var token=cancellation.Token;try{
        message.Text="正在检查…";Toolchain.Refresh();results.Clear();results.Add(EnvironmentInspector.Visio());results.Add(EnvironmentInspector.PowerPoint());Display();var basic=await Task.Run(()=>EnvironmentInspector.Basic(TexDirectory,settingsPath,CacheStore.Default,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets"),token));results.AddRange(basic);Display();
        if(fonts)foreach(var font in FontCatalog.All){token.ThrowIfCancellationRequested();message.Text="正在测试 "+font.Name+"…";var row=results.Last(r=>r.Name==font.Name);try{
            using(var rendered=font.Id=="mathjax"?await mathjax(token):await Task.Run(()=>Renderer.Render(new Formula{Source=@"\text{test}\quad\bm{\alpha}=\frac{1}{s+1}\quad\color{blue}\begin{bmatrix}1&0\\0&1\end{bmatrix}",Font=font.Id,Size=12},token,TexDirectory),token)){
                if(rendered==null||!File.Exists(rendered.Svg)||rendered.Width<=0||rendered.Height<=0)throw new IOException("未生成有效的 SVG。");row.State="渲染通过";row.Detail="分式、矩阵、英文、粗体和颜色已生成矢量结果。";row.Problem=false;
            }
        }catch(OperationCanceledException){throw;}catch(Exception e){row.State="渲染失败";row.Detail=EnvironmentInspector.FontFailure(font,e);row.Problem=true;}Display();}
        message.Text=results.Any(r=>r.Problem)?"检查完成，请按对应条目的提示修复。":"检查完成。";
    }catch(OperationCanceledException){message.Text="已取消检查；已完成的结果保留。";}catch(Exception e){message.Text="检查失败："+EnvironmentInspector.Short(e);}finally{cancellation.Dispose();cancellation=null;working=false;detect.Enabled=test.Enabled=choose.Enabled=automatic.Enabled=copy.Enabled=true;close.Text="关闭";}}
}
}