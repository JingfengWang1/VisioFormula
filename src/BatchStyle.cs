using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;

namespace VisioFormula {
public sealed class FormulaTarget {
    public object Shape;public Formula Original;public string Fingerprint;public double[] Geometry;public int ID;
}
public sealed partial class Bridge {
    public string ContextKey(){RefreshActive();dynamic w=App.ActiveWindow;if(w==null||w.Page==null)throw new Exception("请先打开 Visio 绘图页。");return Convert.ToString(App.ProcessID,CultureInfo.InvariantCulture)+"/"+Convert.ToString(w.Document.ID,CultureInfo.InvariantCulture)+"/"+Convert.ToString(w.Page.ID,CultureInfo.InvariantCulture);}
    public string ContextName(){RefreshActive();dynamic w=App.ActiveWindow;if(w==null||w.Page==null)throw new Exception("请先打开 Visio 绘图页。");return (string)w.Document.Name+" / "+(string)w.Page.Name;}
    static readonly string[] GeometryCells={"Width","Height","PinX","PinY","Angle"};
    static string Fingerprint(dynamic s){
        return new JavaScriptSerializer().Serialize(Read(s));
    }
    public List<FormulaTarget> Collect(bool wholePage){
        RefreshActive();
        dynamic window=App.ActiveWindow;if(window==null||window.Page==null)throw new Exception("请先打开 Visio 绘图页。");
        dynamic shapes=wholePage?window.Page.Shapes:window.Selection;
        var result=new List<FormulaTarget>();var seen=new HashSet<int>();
        for(int i=1;i<=shapes.Count;i++)CollectShape(shapes[i],result,seen);
        return result;
    }
    static void CollectShape(dynamic shape,List<FormulaTarget> result,HashSet<int> seen){
        int id=shape.ID;if(!seen.Add(id))return;
        if(IsFormula(shape)){var geometry=new double[GeometryCells.Length];for(int j=0;j<geometry.Length;j++)geometry[j]=Get(shape,GeometryCells[j]);result.Add(new FormulaTarget{Shape=shape,ID=id,Original=Read(shape),Fingerprint=Fingerprint(shape),Geometry=geometry});return;}
        if(shape.Type==2)for(int i=1;i<=shape.Shapes.Count;i++)CollectShape(shape.Shapes[i],result,seen);
    }
    public void ApplyBatch(List<FormulaTarget> targets,List<Rendered> rendered,bool explicitSize){
        if(targets.Count==0||targets.Count!=rendered.Count)throw new ArgumentException("批量公式数量不一致。");
        dynamic owner=((dynamic)targets[0].Shape).Application;
        for(int i=0;i<targets.Count;i++){
            dynamic shape=targets[i].Shape;
            if(!SameApplication(owner,shape.Application))throw new Exception("批量公式来自不同的 Visio 实例，请重新选择范围。");
            if(Fingerprint(shape)!=targets[i].Fingerprint)throw new Exception("准备期间有公式被修改，请重新开始批量操作。");
            // Visio may recompute floating-point cells while processing idle messages.
            // Ignore subpixel rounding, but reject edits made during rendering.
            for(int j=0;j<GeometryCells.Length;j++)if(Math.Abs(Get(shape,GeometryCells[j])-targets[i].Geometry[j])>1e-7*Math.Max(1,Math.Abs(targets[i].Geometry[j])))throw new Exception("准备期间有公式被移动或缩放，请重新开始批量操作。");
            if(rendered[i]==null||!File.Exists(rendered[i].Svg))throw new Exception("批量渲染结果丢失，请重试。");
            var xml=new XmlDocument{XmlResolver=null};xml.Load(rendered[i].Svg);if(xml.DocumentElement==null||xml.DocumentElement.LocalName!="svg")throw new Exception("批量 SVG 文件无效，请重新生成。");
            if(!explicitSize){double sx=Get(shape,"Width")/targets[i].Original.BaseWidth,sy=Get(shape,"Height")/targets[i].Original.BaseHeight;if(Math.Abs(sx-sy)>Math.Max(sx,sy)*.015)throw new Exception("有公式被非等比拉伸，请先修正或勾选统一字号。");}
        }
        int scope=owner.BeginUndoScope("批量统一公式样式");
        try{for(int i=0;i<targets.Count;i++)Update(targets[i].Shape,rendered[i],explicitSize);owner.EndUndoScope(scope,true);}
        catch{owner.EndUndoScope(scope,false);throw;}
    }
}
}
