using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
namespace VisioFormula {
public sealed class Preferences {
    public int Version=1;
    public string Font="latex";
    public double Size=12;
    public int Zoom=150;
    public void Validate(){
        if(FontCatalog.IndexOf(Font)<0)Font="latex";
        if(double.IsNaN(Size)||double.IsInfinity(Size)||Size<4||Size>96)Size=12;
        if(Zoom!=0&&Zoom!=100&&Zoom!=150&&Zoom!=200&&Zoom!=300)Zoom=150;
    }
}
public sealed class SettingsStore {
    public readonly string FilePath;
    public SettingsStore():this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VisioFormula","settings.json")){}
    public SettingsStore(string path){FilePath=Path.GetFullPath(path);}
    public Preferences Load(){try{if(File.Exists(FilePath)){var p=new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(FilePath));if(p!=null&&p.Version==1){p.Validate();return p;}}}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}return new Preferences();}
    public void Save(Preferences preferences){
        preferences.Validate();Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        string temp=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{
            File.WriteAllText(temp,new JavaScriptSerializer().Serialize(preferences),new UTF8Encoding(false));
            if(File.Exists(FilePath))File.Replace(temp,FilePath,null);else File.Move(temp,FilePath);
        }finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
}
