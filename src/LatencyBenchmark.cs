using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using VisioFormula;
[assembly:TargetFramework(".NETFramework,Version=v4.8")]
public static class LatencyBenchmark {
    static T Field<T>(object instance,string name){return (T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance);}
    static string Mode(WebBrowser browser){try{return Convert.ToString(new JavaScriptSerializer().Deserialize<Dictionary<string,object>>((string)browser.Document.InvokeScript("vfGetState"))["mode"]);}catch{return "";}}
    static async Task Until(Func<bool> condition){var watch=Stopwatch.StartNew();while(!condition()){if(watch.ElapsedMilliseconds>15000)throw new Exception("Timed out");await Task.Delay(10);}}
    [STAThread]public static int Main(string[] args){
        int code=0;string output=args[0],settingsPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","benchmark-"+Guid.NewGuid().ToString("N")+".json");var rows=new List<object>();
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var form=new MainForm(new SettingsStore(settingsPath)){Opacity=0,ShowInTaskbar=false};
        form.Shown+=async delegate{
            try{
                var source=Field<FormulaEditor>(form,"source");var font=Field<ComboBox>(form,"font");var browser=Field<WebBrowser>(form,"picture");var controller=Field<RealtimeController>(form,"realtime");
                await Until(()=>Field<Rendered>(form,"ready")!=null&&Mode(browser)=="exact");
                var watch=new Stopwatch();long started=0;controller.Started+=delegate{started=watch.ElapsedMilliseconds;};
                foreach(string id in new[]{"latex","times","mathjax"})for(int i=0;i<3;i++){
                    font.SelectedIndex=FontCatalog.IndexOf(id);started=0;watch.Restart();source.Text=@"G(s)=\frac{1}{s^2+2\zeta\omega_n s+\omega_n^2}+"+(i+1);
                    long quick=-1;await Until(()=>{if(Mode(browser)=="quick"&&quick<0)quick=watch.ElapsedMilliseconds;return Field<Rendered>(form,"ready")!=null&&Mode(browser)=="exact";});
                    long total=watch.ElapsedMilliseconds;rows.Add(new{font=id,run=i+1,quickMs=quick,waitMs=started,renderMs=total-started,totalMs=total});Console.WriteLine(id+" "+(i+1)+": wait="+started+" render="+(total-started)+" total="+total);
                }
                var chosen=Field<Rendered>(form,"ready");string original=source.Text;
                source.Text="x+987";await Until(()=>Field<Rendered>(form,"ready")!=null&&Mode(browser)=="exact");
                watch.Restart();source.Text=original;await Until(()=>Field<Rendered>(form,"ready")!=null&&Mode(browser)=="exact");
                rows.Add(new{font="mathjax-repeat",totalMs=watch.ElapsedMilliseconds});
                File.WriteAllText(output,new JavaScriptSerializer().Serialize(rows));
            }catch(Exception ex){code=1;Console.WriteLine(ex);}
            finally{form.Close();if(File.Exists(settingsPath))File.Delete(settingsPath);}
        };Application.Run(form);return code;
    }
}
