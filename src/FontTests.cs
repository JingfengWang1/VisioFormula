using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Xml;
using VisioFormula;
public static class FontTests {
    public static int Main(){
        string root=AppDomain.CurrentDomain.BaseDirectory,samples=Path.Combine(root,"samples"),verification=Path.Combine(root,"verification");Directory.CreateDirectory(samples);Directory.CreateDirectory(verification);
        var passed=new List<string>();
        try{
            foreach(MathFont font in FontCatalog.All){
                if(font.Id=="mathjax")continue;
                var f=new Formula{Font=font.Id,Size=12,Source=@"\omega_0\quad\Delta\omega_s\quad\hat{\omega}_s\quad\Sigma\quad\sum_{k=1}^n x_k"};
                Rendered sample=Renderer.Render(f);File.Copy(sample.Svg,Path.Combine(samples,font.Id+".svg"),true);
                var xml=new XmlDocument{XmlResolver=null};xml.Load(sample.Svg);
                if(xml.GetElementsByTagName("path").Count==0||xml.GetElementsByTagName("text").Count>0)throw new Exception(font.Name+" did not produce outlined glyphs");
                Rendered stress=Renderer.Render(new Formula{Font=font.Id,Size=10,Source=@"\begin{aligned}\text{gain}\quad\bm{\alpha}&=\color{blue}\frac{a+b}{c}\\\mathbf{A}&=\begin{bmatrix}1&0\\0&1\end{bmatrix}\quad\mathbb{R}\end{aligned}"});
                if(stress.Width<=0||stress.Height<=0)throw new Exception("Invalid dimensions");
                string label=font.Name+": outlined sample, English, Greek, bold, color, fraction and matrix";passed.Add(label);Console.WriteLine("PASS: "+label);
            }
            File.WriteAllText(Path.Combine(verification,"fonts-results.json"),new JavaScriptSerializer().Serialize(new{passed=passed}));return 0;
        }catch(Exception ex){Console.WriteLine(ex);File.WriteAllText(Path.Combine(verification,"fonts-error.txt"),ex.ToString());return 1;}
    }
}
