using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VisioFormula {
public sealed partial class MainForm {
    Preferences preferences;UpdateBinding binding;IDisposable readyPin;ToolStripItem settingsMenu;
    readonly Stopwatch editClock=Stopwatch.StartNew(),bindingClock=Stopwatch.StartNew();
    readonly System.Windows.Forms.Timer syncTimer=new System.Windows.Forms.Timer{Interval=100};
    readonly System.Windows.Forms.Timer maintenanceTimer=new System.Windows.Forms.Timer{Interval=60000};
    readonly CancellationTokenSource lifetime=new CancellationTokenSource();int maintenanceDepth;bool cleaning;
    void SetReady(Rendered value){var pin=value==null?null:value.Pin();var old=readyPin;ready=value;readyPin=pin;if(old!=null)old.Dispose();}
    void ClearTarget(){target=null;loadedSize=0;binding=null;RefreshBindingUi();}
    void RefreshBindingUi(){
        editorMode.Text=binding==null?"LaTeX":preferences.AutoUpdate?(binding.Paused?"同步暂停":"自动更新"):"编辑公式";
        tips.SetToolTip(editorMode,binding==null?"TeX 公式源码":binding.Label+(binding.Paused?"\n"+binding.Reason:"\n仅更新此公式；更换目标请点击“编辑选中公式”。"));
    }
    void InitializeMaintenance(){syncTimer.Tick+=delegate{TryAutoUpdate();};maintenanceTimer.Tick+=async delegate{await CleanIdle();};}
    void StartMaintenance(){syncTimer.Start();if(string.Equals(settings.FilePath,new SettingsStore().FilePath,StringComparison.OrdinalIgnoreCase)){maintenanceTimer.Start();StartupCheck();}}
    void TryAutoUpdate(){
        if(closing||busy||maintenanceDepth>0||!preferences.AutoUpdate||binding==null)return;
        bool canWrite=!binding.Paused&&ready!=null&&object.ReferenceEquals(ready,realtime.Current)&&editClock.ElapsedMilliseconds>=600&&UpdateBinding.SameContent(ready.Formula,Current());
        if(canWrite||bindingClock.ElapsedMilliseconds>=500){bindingClock.Restart();bool alreadyPaused=binding.Paused;int state=binding.Check();if(state!=0){string reason=binding.Reason;if(state==2)ClearTarget();RefreshBindingUi();if(!alreadyPaused||state==2)SetStatus(reason,true);return;}}
        if(canWrite&&binding.NeedsUpdate(ready.Formula))ApplyUpdate(true);
    }
    async void StartupCheck(){
        try{
            if(!preferences.EnvironmentChecked){var visio=EnvironmentInspector.Visio();var rows=await Task.Run(()=>EnvironmentInspector.Basic(preferences.TexDirectory,settings.FilePath,CacheStore.Default,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets"),lifetime.Token));
                if(closing)return;bool issue=visio.Problem||rows.Any(r=>r.Problem);settingsMenu.Text=issue?"设置（环境需检查）…":"设置…";preferences.EnvironmentChecked=true;SaveSettings();if(issue)SetStatus("部分环境未就绪，可在“更多 → 设置 → 环境检查”查看。",true);
            }
            await Task.Delay(1800,lifetime.Token);await CleanIdle();
        }catch(OperationCanceledException){}catch(Exception e){if(!closing){Log(e);settingsMenu.Text="设置（环境需检查）…";}}
    }
    async Task CleanIdle(){
        if(closing||busy||maintenanceDepth>0||cleaning||!preferences.AutoClean||editClock.ElapsedMilliseconds<1500)return;
        cleaning=true;int limit=preferences.CacheLimitMB,days=preferences.CacheDays;try{await Task.Run(()=>CacheStore.Default.Clean(false,limit,days));}catch(Exception e){Log(e);}finally{cleaning=false;}
    }
    void ConfigureApplication(){if(busy)return;maintenanceDepth++;try{
        using(var dialog=new AppSettingsDialog(preferences.AutoUpdate,owner=>ConfigureInputOwned(owner),owner=>ShowEnvironment(owner),owner=>ShowCache(owner)))if(dialog.ShowDialog(this)==DialogResult.OK){preferences.AutoUpdate=dialog.AutoUpdate;SaveSettings();RefreshBindingUi();SetStatus(preferences.AutoUpdate?"自动更新已开启；通过“编辑选中公式”关联目标。":"自动更新已关闭；可手动原位更新。");}
    }finally{maintenanceDepth--;}}
    void ConfigureInputOwned(IWin32Window owner){using(var dialog=new QuickSettingsDialog(workspace.Quick))if(dialog.ShowDialog(owner)==DialogResult.OK)try{workspace.SaveQuick(dialog.Result);source.Configure(dialog.Result);SetStatus("快捷输入设置已保存");}catch(Exception e){Error(e);}}
    void ShowCache(IWin32Window owner){if(busy)return;maintenanceDepth++;try{using(var dialog=new CacheDialog(CacheStore.Default,preferences))if(dialog.ShowDialog(owner)==DialogResult.OK){preferences.AutoClean=dialog.AutoClean;preferences.CacheLimitMB=dialog.LimitMB;preferences.CacheDays=dialog.Days;SaveSettings();}}finally{maintenanceDepth--;}}
    async void ShowEnvironment(IWin32Window owner){if(busy)return;maintenanceDepth++;busy=true;Toggle();try{
        Task previous=realtime.LastTask;quickTimer.Stop();realtime.Request(new Formula{Source=""},0);if(previous!=null)await previous;if(closing)return;SetReady(null);
        using(var dialog=new EnvironmentDialog(preferences.TexDirectory,settings.FilePath,token=>RenderExactAsync(new Formula{Font="mathjax",Source=@"\text{test}\quad\bm{\alpha}=\frac{1}{s+1}\quad\color{blue}\begin{bmatrix}1&0\\0&1\end{bmatrix}"},token))){dialog.ShowDialog(owner);if(dialog.PathChanged){preferences.TexDirectory=dialog.TexDirectory;Toolchain.Refresh();realtime.ClearCache();}preferences.EnvironmentChecked=true;SaveSettings();settingsMenu.Text="设置…";}
    }catch(Exception e){Error(e);}finally{maintenanceDepth--;busy=false;if(!closing){Toggle();Schedule(false);}}}
}
}