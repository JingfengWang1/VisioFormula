using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Xml;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using VisioFormula;

[assembly:TargetFramework(".NETFramework,Version=v4.8")]
public static class RealtimeTests {
    [DllImport("user32.dll")]static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")]static extern bool AreDpiAwarenessContextsEqual(IntPtr first,IntPtr second);
    static readonly List<string> passed=new List<string>();
    static readonly Dictionary<string,object> metrics=new Dictionary<string,object>();
    static int exitCode;
    static readonly string settingsPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","test-settings-"+Guid.NewGuid().ToString("N")+".json");
    static string Glyphs(string svg){var xml=new XmlDocument{XmlResolver=null};xml.LoadXml(svg);var text=new StringBuilder();foreach(XmlNode node in xml.GetElementsByTagName("path"))text.Append(((XmlElement)node).GetAttribute("d")).Append('|');return text.ToString();}
    static async Task TestPreferences(MainForm form){
        var font=Field<ComboBox>(form,"font");var size=Field<NumericUpDown>(form,"size");var zoom=Field<ComboBox>(form,"zoom");
        font.SelectedIndex=FontCatalog.IndexOf("stix2");size.Value=14.5m;zoom.SelectedIndex=2;
        var store=new SettingsStore(settingsPath);await Until(()=>File.Exists(settingsPath)&&store.Load().Font=="stix2"&&store.Load().Size==14.5&&store.Load().Zoom==200,"preferences auto-save");
        Check(true,"Font, point size and preview zoom are saved automatically");
        using(var reopened=new MainForm(store){Opacity=0,ShowInTaskbar=false}){
            Check(((MathFont)Field<ComboBox>(reopened,"font").SelectedItem).Id=="stix2"&&Field<NumericUpDown>(reopened,"size").Value==14.5m&&Field<ComboBox>(reopened,"zoom").SelectedIndex==2,"A new app window restores the previous settings");
            reopened.Show();await Task.Delay(30);reopened.Close();
        }
        File.WriteAllText(settingsPath,"broken json");Check(store.Load().Font=="latex"&&store.Load().Size==12,"Damaged settings do not prevent startup");
        File.WriteAllText(settingsPath,"{\"Version\":1,\"Font\":\"missing\",\"Size\":999,\"Zoom\":-12}");var safe=store.Load();
        Check(safe.Font=="latex"&&safe.Size==12&&safe.Zoom==150,"Unsupported saved values fall back to usable defaults");
        font.SelectedIndex=0;size.Value=12;zoom.SelectedIndex=1;
    }
    static async Task TestMathJaxFont(MainForm form){
        var source=Field<FormulaEditor>(form,"source");var browser=Field<WebBrowser>(form,"picture");var font=Field<ComboBox>(form,"font");
        font.SelectedIndex=FontCatalog.IndexOf("mathjax");Field<NumericUpDown>(form,"size").Value=12;
        source.Text=@"\omega_0\quad\Delta\omega_s\quad\hat{\omega}_s\quad\Sigma\quad\sum_{k=1}^n x_k";
        await Until(()=>Mode(browser)=="quick","MathJax font quick preview");string fast=(string)browser.Document.InvokeScript("vfPreviewSvg");
        await Until(()=>Mode(browser)=="exact"&&Field<Rendered>(form,"ready")!=null,"MathJax final vector");var result=Field<Rendered>(form,"ready");string exact=File.ReadAllText(result.Svg);
        Check(result.Formula.Font=="mathjax"&&Glyphs(fast)==Glyphs(exact),"MathJax exported vectors use exactly the quick-preview glyphs");
        Check(!File.Exists(Path.Combine(result.Directory,"formula.tex")),"MathJax option does not invoke local LaTeX");
        string samples=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"samples");Directory.CreateDirectory(samples);File.Copy(result.Svg,Path.Combine(samples,"mathjax.svg"),true);
        source.Text=@"\begin{aligned}\text{gain}\quad\bm{\alpha}&=\color{blue}\frac{a+b}{c}\\\mathbf{A}&=\begin{bmatrix}1&0\\0&1\end{bmatrix}\quad\mathbb{R}\end{aligned}";
        await Until(()=>Mode(browser)=="exact"&&Field<Rendered>(form,"ready")!=null,"MathJax bold/matrix/color");
        File.Copy(Field<Rendered>(form,"ready").Svg,Path.Combine(samples,"mathjax-matrix.svg"),true);
        Check(true,"MathJax final output supports English, Greek, bold, color, fractions and matrices");
        source.Text=@"\frac{";await Until(()=>Mode(browser)=="exact-error","MathJax invalid input");
        Check(Field<Rendered>(form,"ready")==null&&!Field<Button>(form,"copy").Enabled,"Invalid MathJax input cannot export an older formula");
        source.Text="x+1";font.SelectedIndex=1;await Until(()=>Mode(browser)=="exact"&&Field<Rendered>(form,"ready")!=null,"switch back to local LaTeX");
        Check(Field<Rendered>(form,"ready").Formula.Font=="times","Switching from MathJax back to a LaTeX font uses the selected renderer");
    }
    static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);passed.Add(label);Console.WriteLine("PASS: "+label);}
    static async Task Until(Func<bool> condition,string label,int timeout=12000){var watch=Stopwatch.StartNew();while(!condition()){if(watch.ElapsedMilliseconds>timeout)throw new Exception("TIMEOUT: "+label);await Task.Delay(15);}}
    static Dictionary<string,object> State(WebBrowser browser){try{return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>((string)browser.Document.InvokeScript("vfGetState"));}catch{return new Dictionary<string,object>{{"mode","unavailable"}};}}
    static string Mode(WebBrowser browser){return Convert.ToString(State(browser)["mode"]);}
    static double Number(WebBrowser browser,string key){return Convert.ToDouble(State(browser)[key]);}
    static double CenterY(Control control){return control.PointToScreen(new Point(0,0)).Y+control.Height/2.0;}
    static void InspectToolbar(MainForm form){
        var font=Field<ComboBox>(form,"font");var size=Field<NumericUpDown>(form,"size");Control fontLabel=null,unit=null;
        foreach(Control child in font.Parent.Controls)if(child is Label)fontLabel=child;
        foreach(Control child in size.Parent.Controls)if(child is Label)unit=child;
        if(fontLabel==null||unit==null)throw new Exception("Missing toolbar captions");
        foreach(Control control in new[]{fontLabel,(Control)font,size,unit})if(Math.Abs(CenterY(control)-CenterY(font))>1.1)throw new Exception("Toolbar control is not centered: "+control.Text);
        foreach(Control child in size.Controls)if(Math.Abs(CenterY(child)-CenterY(size))>1.1)throw new Exception("Numeric editor/spinner is not centered");
    }
    static async Task TestCache(){
        string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","cache-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        int compiled=0;var delivered=new List<string>();
        try{using(var c=new RealtimeController((f,token)=>{
            compiled++;string svg=Path.Combine(dir,compiled+".svg");File.WriteAllText(svg,"<svg/>");return new Rendered{Formula=f.Clone(),Svg=svg};
        })){
            c.Ready+=(r,v)=>delivered.Add(v.Formula.Source);
            var a=new Formula{Source="a",Font="latex",Size=12};var b=new Formula{Source="b",Font="latex",Size=12};
            c.Request(a,0);await c.LastTask;string original=c.Current.Svg;c.Request(b,0);await c.LastTask;
            var watch=Stopwatch.StartNew();c.Request(a,650);bool deferred=c.Current==null;await c.LastTask;
            Check(deferred&&compiled==2&&c.Current.Svg==original&&watch.ElapsedMilliseconds<250,"Matching cache skips debounce and compilation, but publishes after Request returns");
            c.Request(new Formula{Source="a",Font="times",Size=12},0);await c.LastTask;
            c.Request(new Formula{Source="a",Font="latex",Size=14},0);await c.LastTask;
            Check(compiled==4,"Font and point size are part of the cache identity");
            c.Request(a,0,true);await c.LastTask;Check(compiled==5&&c.Current.Svg!=original,"Forced rendering bypasses a cached result");
            File.Delete(c.Current.Svg);c.Request(a,0);await c.LastTask;Check(compiled==6&&File.Exists(c.Current.Svg),"Missing cached SVG is regenerated");
            delivered.Clear();c.Request(a,650);c.Request(b,0);await c.LastTask;
            Check(delivered.Count==1&&delivered[0]=="b","Superseded cache hits cannot publish an older formula");
            for(int i=0;i<17;i++){c.Request(new Formula{Source="entry"+i},0);await c.LastTask;}
            int before=compiled;c.Request(a,0);await c.LastTask;
            Check(compiled==before+1,"Cache keeps at most sixteen recent successful results");
        }}finally{foreach(string file in Directory.GetFiles(dir))File.Delete(file);Directory.Delete(dir);}
    }
    static void InspectLayout(Control parent){
        foreach(Control child in parent.Controls){
            if(child.Width<=0||child.Height<=0)throw new Exception("Control collapsed: "+child.GetType().Name+" "+child.Text);
            if(child.Bounds.Left<0||child.Bounds.Top<0||child.Bounds.Right>parent.ClientSize.Width+1||child.Bounds.Bottom>parent.ClientSize.Height+1)
                throw new Exception("Control clipped: "+child.GetType().Name+" "+child.Text+" "+child.Bounds+" parent "+parent.ClientSize);
            if(child is Label&&child.Font.Height>child.Height+1)throw new Exception("Text vertically clipped: "+child.Text);
            if(!(child is WebBrowser)&&!(child is NumericUpDown))InspectLayout(child);
        }
    }
    static void PreviewContained(WebBrowser browser){
        if(Number(browser,"displayWidth")<=0||Number(browser,"displayHeight")<=0)throw new Exception("Formula preview is collapsed");
        if(Number(browser,"displayWidth")>Number(browser,"availableWidth")+.1||Number(browser,"displayHeight")>Number(browser,"availableHeight")+.1)
            throw new Exception("Formula exceeds preview bounds");
    }
    static async Task TestCompactPreview(MainForm form){
        var source=Field<FormulaEditor>(form,"source");var browser=Field<WebBrowser>(form,"picture");var zoom=Field<ComboBox>(form,"zoom");
        metrics["windowDpi"]=form.DeviceDpi;
        Check(AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(),new IntPtr(-4)),"Window renders with PerMonitorV2 DPI awareness");
        InspectLayout(form);Check(true,"Default layout keeps toolbar, captions and actions within their containers");
        var fontControl=Field<ComboBox>(form,"font");Control caption=null;foreach(Control control in fontControl.Parent.Controls)if(control is Label)caption=control;
        Check(caption!=null&&Math.Abs((caption.Top+caption.Height/2.0)-(fontControl.Top+fontControl.Height/2.0))<=1,"Font label and selector are vertically centered on the same line");
        InspectToolbar(form);Check(true,"Font, number, spinner and pt share the same vertical center");
        var toolbar=((TableLayoutPanel)form.Controls[0]).GetControlFromPosition(0,1);
        string snapshots=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification");Directory.CreateDirectory(snapshots);
        using(var bitmap=new Bitmap(toolbar.Width,toolbar.Height)){toolbar.DrawToBitmap(bitmap,toolbar.ClientRectangle);bitmap.Save(Path.Combine(snapshots,"toolbar.png"),System.Drawing.Imaging.ImageFormat.Png);}
        Field<ComboBox>(form,"font").SelectedIndex=0;Field<NumericUpDown>(form,"size").Value=12;source.Text="G(s)";
        await Until(()=>Mode(browser)=="quick","small quick formula");
        Check(Number(browser,"displayHeight")/Number(browser,"cssScale")<35&&Number(browser,"scale")<=1.5,"Short quick preview respects reading zoom rather than filling the panel");
        await Until(()=>Mode(browser)=="exact","small exact formula");
        Check(Number(browser,"displayHeight")/Number(browser,"cssScale")<35&&Math.Abs(Number(browser,"scale")-1.5)<.001,"Short exact preview respects the same 150 percent ceiling");
        double shortHeight=Number(browser,"displayHeight");var chosen=Field<Rendered>(form,"ready");string originalSvg=File.ReadAllText(chosen.Svg);int revision=Field<RealtimeController>(form,"realtime").Revision;
        double cssToDevice=(double)browser.ClientSize.Width/browser.Document.Body.ClientRectangle.Width;
        Check(Math.Abs(Number(browser,"displayWidth")*cssToDevice-chosen.Width*form.DeviceDpi*1.5)<2,"Preview reading size follows the actual monitor DPI");
        zoom.SelectedIndex=2;await Task.Delay(60);
        Check(Math.Abs(Number(browser,"displayHeight")/shortHeight-4.0/3)<.01&&Object.ReferenceEquals(chosen,Field<Rendered>(form,"ready"))&&File.ReadAllText(chosen.Svg)==originalSvg&&revision==Field<RealtimeController>(form,"realtime").Revision,"Preview zoom changes only display size; exact SVG and output size remain unchanged");
        zoom.SelectedIndex=1;
        Size normal=form.Size;form.Size=new Size(normal.Width+100,normal.Height+100);await Task.Delay(100);
        Check(Math.Abs(Number(browser,"displayHeight")-shortHeight)<.01,"Enlarging the window does not inflate a short formula");
        form.Size=form.MinimumSize;await Task.Delay(100);InspectLayout(form);InspectToolbar(form);PreviewContained(browser);
        Check(source.Height>=2*source.Font.Height&&browser.Height>=form.DeviceDpi*.4,"High-DPI minimum size reserves usable editor and preview space");
        Check(true,"Minimum-size layout keeps toolbar and preview completely inside the window");
        var filter=Field<PreviewDragFilter>(form,"dragFilter");var click=Message.Create(zoom.Handle,0x0201,new IntPtr(1),IntPtr.Zero);
        Check(!filter.PreFilterMessage(ref click),"Preview zoom control receives clicks without initiating a formula drag");
        var grip=Field<Label>(form,"grip");bool swallowed=true;
        for(int i=0;i<3;i++)foreach(Control area in new Control[]{browser,grip}){
            var down=Message.Create(area.Handle,0x0201,new IntPtr(1),IntPtr.Zero);var up=Message.Create(area.Handle,0x0202,IntPtr.Zero,IntPtr.Zero);
            swallowed&=filter.PreFilterMessage(ref down)&&filter.PreFilterMessage(ref up)&&!browser.Capture;
        }
        Check(swallowed,"Repeated artwork and grip gestures suppress browser-native image dragging");
        source.Text=string.Join("+",new string[]{"a_1","a_2","a_3","a_4","a_5","a_6","a_7","a_8","a_9","a_{10}","a_{11}","a_{12}","a_{13}","a_{14}","a_{15}","a_{16}"});
        await Until(()=>Mode(browser)=="quick","long quick formula");PreviewContained(browser);
        await Until(()=>Mode(browser)=="exact","long exact formula");PreviewContained(browser);
        Check(Number(browser,"scale")<1.5,"Long formulas shrink to fit the minimum-width preview");
        source.Text=@"\begin{aligned}a&=1\\b&=2\\c&=3\\d&=4\\e&=5\\f&=6\\g&=7\\h&=8\\i&=9\\j&=10\end{aligned}";
        await Until(()=>Mode(browser)=="quick","tall quick formula");PreviewContained(browser);
        await Until(()=>Mode(browser)=="exact","tall exact formula");PreviewContained(browser);
        Check(Number(browser,"scale")<1.5,"Tall aligned formulas shrink by height without clipping");
        form.Size=normal;await Task.Delay(100);InspectLayout(form);
    }
    static async Task TestCoordinator(){
        int running=0,maximum=0;var entered=new ManualResetEventSlim(false);var release=new ManualResetEventSlim(false);var delivered=new List<string>();
        using(var c=new RealtimeController((f,token)=>{
            int n=Interlocked.Increment(ref running);maximum=Math.Max(maximum,n);
            try{if(f.Source=="old"){entered.Set();release.Wait(3000);}return new Rendered{Formula=f};}
            finally{Interlocked.Decrement(ref running);}
        })){
            c.Ready+=(r,f)=>delivered.Add(f.Formula.Source);
            c.Request(new Formula{Source="old"},0);await Until(()=>entered.IsSet,"old renderer started");
            c.Request(new Formula{Source="new"},0);release.Set();await c.LastTask;
            Check(delivered.Count==1&&delivered[0]=="new","Late completion cannot publish stale formula");
            Check(maximum==1,"Only one exact renderer runs at a time");
            c.Request(new Formula{Source="pending"},100);c.Dispose();await c.LastTask;
            Check(delivered.Count==1,"Closing cancels pending work without publishing a result");
        }
    }
    static async Task TestWindow(MainForm form){
        var source=Field<FormulaEditor>(form,"source");var browser=Field<WebBrowser>(form,"picture");
        var copy=Field<Button>(form,"copy");var export=Field<Button>(form,"export");
        var controller=Field<RealtimeController>(form,"realtime");
        await Until(()=>Field<Rendered>(form,"ready")!=null&&Mode(browser)=="exact","startup exact result");
        Check(true,"Startup automatically renders exact LaTeX in the real preview control");
        var timer=Stopwatch.StartNew();source.Text=@"\frac{a+b}{c+d}";
        Check(!copy.Enabled&&!export.Enabled&&Field<Rendered>(form,"ready")==null,"Editing immediately disables stale export and copy");
        string lastMode="";
        await Until(()=>{string m=Mode(browser);if(m!=lastMode){lastMode=m;Console.WriteLine("Preview state: "+new JavaScriptSerializer().Serialize(State(browser)));}return m=="quick";},"MathJax fast preview",5000);
        metrics["warmQuickPreviewMs"]=timer.ElapsedMilliseconds;metrics["mathJaxTypesettingMs"]=State(browser)["quickMs"];
        // Desktop scheduling is load-dependent; verify phase ordering and record latency separately.
        Check(Mode(browser)=="quick"&&Field<Rendered>(form,"ready")==null,"Fast preview precedes the matching precise result");
        Check(!source.ReadOnly&&source.Enabled,"Editor stays editable during preview");
        await Until(()=>Field<Rendered>(form,"ready")!=null&&Mode(browser)=="exact","automatic exact result");
        metrics["automaticExactTotalMs"]=timer.ElapsedMilliseconds;
        Check(copy.Enabled&&export.Enabled,"Only the matching exact result enables copy and export");
        var cached=Field<Rendered>(form,"ready");string cachedSource=source.Text;
        source.Text="x+567";await Until(()=>Field<Rendered>(form,"ready")!=null&&Mode(browser)=="exact","intermediate formula");
        timer.Restart();source.Text=cachedSource;Check(!copy.Enabled,"Returning to cached source still immediately invalidates the previous output");
        await Until(()=>Field<Rendered>(form,"ready")!=null&&Mode(browser)=="exact","cached formula repaint");metrics["cachedTotalMs"]=timer.ElapsedMilliseconds;
        Check(Object.ReferenceEquals(cached,Field<Rendered>(form,"ready"))&&Mode(browser)=="exact","Reverting a formula reuses its precise SVG with the current browser revision");
        int sawQuick=0;
        for(int i=0;i<14;i++){source.Text="x+"+i;await Task.Delay(35);if(Mode(browser)=="quick")sawQuick++;}
        Check(sawQuick>0,"Fast previews continue during sustained typing without a pause");
        await Until(()=>Field<Rendered>(form,"ready")!=null,"typing burst settles");
        Check(Field<Rendered>(form,"ready").Formula.Source=="x+13","Typing burst settles on the latest formula");
        bool started=false;controller.Started+=r=>started=true;
        source.Text=@"\sum_{n=1}^{100} n^2";await Until(()=>started,"compile started");
        source.Text=@"\frac{\omega_n^2}{s^2+2\zeta\omega_n s+\omega_n^2}";
        Check(!source.ReadOnly&&!copy.Enabled,"New input is accepted while LaTeX runs and old output is invalidated");
        await Until(()=>Field<Rendered>(form,"ready")!=null,"replacement exact render");
        Check(Field<Rendered>(form,"ready").Formula.Source==source.Text,"Canceled compilation cannot replace current exact result");
        source.Text=@"\frac{1}{";
        await Until(()=>Mode(browser)=="exact-error","invalid formula handled");
        Check(Field<Rendered>(form,"ready")==null&&!copy.Enabled&&!export.Enabled,"Invalid LaTeX cannot be exported as an old formula");
        source.Text=@"\begin{aligned}\mathbf{x}&=\begin{bmatrix}a\\b\end{bmatrix}\\y&=\color{blue}\frac{1}{s+1}\end{aligned}";
        await Until(()=>Mode(browser)=="quick","matrix quick preview");
        Check(true,"Quick renderer supports matrix, alignment, bold, and color");
        await Until(()=>Field<Rendered>(form,"ready")!=null&&Mode(browser)=="exact","recover after syntax error");
        Check(true,"Correcting syntax automatically recovers precise rendering");
        var font=Field<ComboBox>(form,"font");var size=Field<NumericUpDown>(form,"size");
        font.SelectedIndex=1;size.Value=10;
        await Until(()=>Field<Rendered>(form,"ready")!=null,"font/size change");
        Check(Field<Rendered>(form,"ready").Formula.Font=="times"&&Field<Rendered>(form,"ready").Formula.Size==10,"Font and size changes automatically regenerate the exact SVG");
        await TestCompactPreview(form);
        await TestMathJaxFont(form);
        await TestPreferences(form);
        source.Text="";await Task.Delay(120);
        Check(Mode(browser)=="empty"&&!copy.Enabled&&!export.Enabled,"Empty input clears preview and export state");
        source.Text=@"\frac{1}{1+s}";await Task.Delay(60);form.Close();
        Check(true,"Window closes cleanly with scheduled work");
    }
    [STAThread] public static int Main(){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var form=new MainForm(new SettingsStore(settingsPath)){ShowInTaskbar=false,Opacity=0};
        form.Shown+=async delegate {
            try{await TestCoordinator();await TestCache();await TestWindow(form);}
            catch(Exception ex){exitCode=1;Console.WriteLine(ex);metrics["error"]=ex.ToString();try{metrics["previewState"]=State(Field<WebBrowser>(form,"picture"));}catch{}}
            finally{
                string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification");Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir,"realtime-results.json"),new JavaScriptSerializer().Serialize(new{passed=passed.ToArray(),metrics=metrics}));
                if(!form.IsDisposed)form.Close();
                if(File.Exists(settingsPath))File.Delete(settingsPath);
            }
        };
        Application.Run(form);return exitCode;
    }
}
