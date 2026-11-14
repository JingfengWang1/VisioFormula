using System;
using System.IO;
using System.Xml;
namespace VisioFormula {
public static class PreviewHtml {
    public static string CreateShell() {
        string assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets");
        string html=File.ReadAllText(Path.Combine(assets,"preview.html"));
        return html.Replace("{{POLYFILL}}",File.ReadAllText(Path.Combine(assets,"polyfill.js")).Replace("</script","<\\/script"))
                   .Replace("{{MATHJAX}}",File.ReadAllText(Path.Combine(assets,"tex-svg-full.js")).Replace("</script","<\\/script"));
    }
    public static string DisplaySvg(string svg) {
        var xml=new XmlDocument{XmlResolver=null};xml.LoadXml(svg);
        var root=xml.DocumentElement;
        root.SetAttribute("width","100%");root.SetAttribute("height","100%");
        root.SetAttribute("preserveAspectRatio","xMidYMid meet");return root.OuterXml;
    }
}
}
