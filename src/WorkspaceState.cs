using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace VisioFormula {
public static class JsonFile {
    public static T Read<T>(string path) where T:class{
        foreach(string candidate in new[]{path,path+".bak"})try{if(File.Exists(candidate))return new JavaScriptSerializer{MaxJsonLength=4000000}.Deserialize<T>(File.ReadAllText(candidate));}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}
        return null;
    }
    public static void Write(string path,object value){
        Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,new JavaScriptSerializer{MaxJsonLength=4000000}.Serialize(value),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
public sealed class Draft {
    public Formula Formula;public int Start,Length;public DateTime SavedUtc=DateTime.UtcNow;
    public override string ToString(){string s=(Formula.Source??"").Replace('\n',' ').Replace('\r',' ');return SavedUtc.ToLocalTime().ToString("MM-dd HH:mm")+"   "+(s.Length>65?s.Substring(0,65)+"…":s);}
}
public sealed class DraftData {public int Version=1;public Draft Current;public List<Draft> History=new List<Draft>();}
public sealed class WorkspaceState {
    public readonly string DraftPath,QuickPath;public DraftData Data;public QuickOptions Quick;
    public WorkspaceState(string preferencesPath){
        // Use the preferences filename as namespace so test windows never touch real drafts.
        string basePath=Path.Combine(Path.GetDirectoryName(preferencesPath),Path.GetFileNameWithoutExtension(preferencesPath));
        DraftPath=basePath+".drafts.json";QuickPath=basePath+".quick-input.json";
        Data=JsonFile.Read<DraftData>(DraftPath)??new DraftData();if(Data.Version!=1)Data=new DraftData();
        if(Data.History==null)Data.History=new List<Draft>();Data.History.RemoveAll(d=>!Valid(d));if(!Valid(Data.Current))Data.Current=null;
        Quick=JsonFile.Read<QuickOptions>(QuickPath)??new QuickOptions();try{Quick.Parse();}catch{Quick=new QuickOptions();}
    }
    static bool Valid(Draft d){return d!=null&&d.Formula!=null&&d.Formula.Source!=null&&d.Formula.Source.Length<=50000&&FontCatalog.IndexOf(d.Formula.Font)>=0&&d.Formula.Size>=4&&d.Formula.Size<=96;}
    public void Capture(Formula f,int start,int length){Data.Current=new Draft{Formula=f.Clone(),Start=start,Length=length};}
    public void Archive(){
        Draft d=Data.Current;if(!Valid(d)||string.IsNullOrWhiteSpace(d.Formula.Source))return;
        Data.History.RemoveAll(x=>x.Formula.Source==d.Formula.Source&&x.Formula.Font==d.Formula.Font&&Math.Abs(x.Formula.Size-d.Formula.Size)<0.00001);
        Data.History.Insert(0,d);if(Data.History.Count>20)Data.History.RemoveRange(20,Data.History.Count-20);
    }
    public void Save(){JsonFile.Write(DraftPath,Data);}
    public void SaveQuick(QuickOptions q){q.Parse();JsonFile.Write(QuickPath,q);Quick=q;}
}
}
