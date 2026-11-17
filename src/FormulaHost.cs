using System;

namespace VisioFormula {
// The editor shares rendered formulas; each destination owns transfer and document operations.
public interface IFormulaHost {
    string Name {get;}
    object Selected();
    Formula Read(object target);
    void Update(object target,Rendered result,bool explicitSize);
    void Copy(Rendered result);
    int Drag(Rendered result);
}
public sealed class VisioHost : IFormulaHost {
    readonly Bridge bridge;
    public VisioHost(Bridge value){bridge=value;}
    public string Name {get{return "Visio";}}
    public object Selected(){return bridge.Selected();}
    public Formula Read(object target){return Bridge.Read((dynamic)target);}
    public void Update(object target,Rendered result,bool explicitSize){bridge.Update((dynamic)target,result,explicitSize);}
    public void Copy(Rendered result){bridge.Copy(result);}
    public int Drag(Rendered result){return bridge.Drag(result);}
}
}