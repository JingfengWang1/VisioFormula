using System;
namespace VisioFormula {
public sealed class MathFont {
    public readonly string Id,Name,Description,Preamble;
    public MathFont(string id,string name,string description,string preamble){Id=id;Name=name;Description=description;Preamble=preamble;}
    public override string ToString(){return Name;}
}
public static class FontCatalog {
    // Stable IDs are stored in Visio documents; keep the original latex/times IDs.
    public static readonly MathFont[] All={
        new MathFont("latex","Latin Modern","经典 LaTeX 数学字形，适合搭配 LaTeX 正文。",@"\usepackage{amssymb,lmodern}"),
        new MathFont("times","Times · NewTX","Times 风格，适合搭配 Times New Roman 正文。",@"\usepackage{newtxtext,newtxmath}"),
        new MathFont("stix2","STIX Two","STIX Two 衬线数学字体，可与 Times 风格正文比较搭配。",@"\usepackage{stix2}"),
        new MathFont("palatino","Palatino · NewPX","Palatino 风格，适合搭配 Palatino / TeX Gyre Pagella。",@"\usepackage{newpxtext,newpxmath}"),
        new MathFont("libertinus","Libertinus","Libertinus 文字与数学配套，适合统一采用该字族的图表。",@"\usepackage{libertinus-type1,libertinust1math}"),
        new MathFont("sans","Sans · NewTXsf","无衬线数学字形，可搭配 Helvetica / Arial 风格图中文字。",@"\usepackage{helvet}\renewcommand{\familydefault}{\sfdefault}\usepackage{newtxsf}"),
        new MathFont("mathjax","MathJax · TeX","快速预览同款字形，直接生成 MathJax 矢量结果，无需本机 LaTeX。",null)
    };
    public static int IndexOf(string id){for(int i=0;i<All.Length;i++)if(All[i].Id==id)return i;return -1;}
    public static MathFont Get(string id){int i=IndexOf(id);if(i<0)throw new Exception("当前版本不支持该公式字体："+id+"。请升级工具后重试。");return All[i];}
}
}
