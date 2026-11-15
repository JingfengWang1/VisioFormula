using System;
using System.Web.Script.Serialization;

namespace VisioFormula {
// A binding belongs to the originally loaded shape, never to the current selection.
public sealed class UpdateBinding {
    public readonly object Shape;public readonly string Label;
    Formula original;double[] geometry;string fingerprint;
    public bool Paused {get;private set;}public string Reason {get;private set;}
    static readonly string[] cells={"Width","Height","PinX","PinY","Angle"};
    public UpdateBinding(object shape){Shape=shape;dynamic s=shape;Label=(string)s.Document.Name+" / "+(string)s.ContainingPage.Name+" / 公式 "+(int)s.ID;Refresh();}
    public static bool SameContent(Formula a,Formula b){return a!=null&&b!=null&&a.Version==b.Version&&a.Source==b.Source&&a.Font==b.Font&&Math.Abs(a.Size-b.Size)<.00001;}
    public bool NeedsUpdate(Formula formula){return !SameContent(original,formula);}
    public void Refresh(){original=Bridge.Read((dynamic)Shape);fingerprint=new JavaScriptSerializer().Serialize(original);geometry=new double[cells.Length];for(int i=0;i<cells.Length;i++)geometry[i]=Bridge.Get((dynamic)Shape,cells[i]);Paused=false;Reason=null;}
    // 0: unchanged, 1: externally edited/undone, 2: shape/document no longer exists.
    public int Check(){
        try{
            Formula now=Bridge.Read((dynamic)Shape);bool changed=new JavaScriptSerializer().Serialize(now)!=fingerprint;
            for(int i=0;i<cells.Length&&!changed;i++)changed=Math.Abs(Bridge.Get((dynamic)Shape,cells[i])-geometry[i])>1e-7*Math.Max(1,Math.Abs(geometry[i]));
            if(changed){Pause("Visio 中的公式已被修改或撤销；请重新读回，或手动原位更新后继续。");return 1;}
            return 0;
        }catch{Pause("目标公式已删除或文档已关闭；请重新选择公式。");return 2;}
    }
    public void Pause(string reason){Paused=true;Reason=reason;}
}
}