using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using System.Runtime.Versioning;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using VisioFormula;
[assembly:TargetFramework(".NETFramework,Version=v4.8")]
public static class InteractiveTests {
    [STAThread]public static void Main(){
        string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","interactive-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("VISIOFORMULA_CACHE",Path.Combine(root,"cache"));File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"verification","interactive-current.txt"),root);
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        dynamic app=null,doc=null;var form=new MainForm(new SettingsStore(Path.Combine(root,"settings.json")));form.Text="Visio Formula 0.9.0 · 测试窗口";
        var timer=new System.Windows.Forms.Timer{Interval=700};
        try{
            app=Activator.CreateInstance(Type.GetTypeFromProgID("Visio.Application"));app.AlertResponse=7;app.Visible=true;doc=app.Documents.Add("");doc.SaveAs(Path.Combine(root,"VF-0.9-鼠标验证.vsdx"));
            try{var bounds=Screen.PrimaryScreen.WorkingArea;app.Window.WindowState=0;app.Window.SetWindowRect(bounds.Width/3,0,bounds.Width*2/3,bounds.Height);}catch(Exception e){File.WriteAllText(Path.Combine(root,"placement.txt"),e.Message);}
            var owned=new Bridge((object)app);typeof(MainForm).GetField("bridge",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(form,owned);
            timer.Tick+=delegate{try{var items=new List<object>();dynamic page=doc.Pages[1];for(int i=1;i<=page.Shapes.Count;i++){dynamic s=page.Shapes[i];bool formula=Bridge.IsFormula(s);items.Add(new{id=(int)s.ID,formula=formula,source=formula?Bridge.Read(s).Source:null,width=Bridge.Get(s,"Width"),height=Bridge.Get(s,"Height")});}File.WriteAllText(Path.Combine(root,"observed.json"),new JavaScriptSerializer().Serialize(new{shapes=items}));}catch{}};timer.Start();
            Application.Run(form);
        }finally{timer.Stop();timer.Dispose();if(doc!=null)try{doc.Save();doc.Close();}catch{}if(app!=null)try{app.Quit();}catch{}}
    }
}