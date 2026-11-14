using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VisioFormula {
public sealed class QuickOptions {
    public bool Enabled=true,Highlight=true,TabToExpand=false,Brackets=true,Fractions=true,Scripts=true,SumLimits=true,Environments=true,AutoAlign=true;
    public string Matrix="pmatrix",Align="align*",Rules=DefaultRules;
    public const string DefaultRules=@"bi:::\binom{#cursor}{#tab};
sq:::\sqrt{};
bb:::\mathbb{};
bf:::\mathbf{};
rm:::\mathrm{};
te:::\text{};
inf:::\infty;
cd:::\cdot;
qu:::\quad;
ti:::\times;
al:::\alpha;
be:::\beta;
ga:::\gamma;
Ga:::\Gamma;
de:::\delta;
De:::\Delta;
ep:::\epsilon;
ze:::\zeta;
et:::\eta;
th:::\theta;
Th:::\Theta;
io:::\iota;
ka:::\kappa;
la:::\lambda;
La:::\Lambda;
mu:::\mu;
nu:::\nu;
xi:::\xi;
Xi:::\Xi;
pi:::\pi;
Pi:::\Pi;
rh:::\rho;
si:::\sigma;
Si:::\Sigma;
ta:::\tau;
up:::\upsilon;
Up:::\Upsilon;
ph:::\phi;
Ph:::\Phi;
ch:::\chi;
ps:::\psi;
Ps:::\Psi;
om:::\omega;
Om:::\Omega";
    public List<Snippet> Parse(){
        if(Rules==null||Rules.Length>100000)throw new ArgumentException("快捷规则过长或为空。");
        var result=new List<Snippet>();var used=new HashSet<string>(StringComparer.Ordinal);
        foreach(string part in Regex.Split(Rules.Replace("\r\n","\n"),@";\s*\n")){
            string line=part.Trim().TrimEnd(';');if(line.Length==0)continue;
            int separator=line.IndexOf(":::",StringComparison.Ordinal);
            if(separator<=0)throw new ArgumentException("每条规则应写成：缩写:::展开内容;，规则之间换行。");
            string key=line.Substring(0,separator).Trim(),value=line.Substring(separator+3);
            if(key.Length>40||key.Any(char.IsWhiteSpace)||value.Length==0)throw new ArgumentException("缩写不能包含空白，展开内容不能为空："+key);
            if(!used.Add(key))throw new ArgumentException("缩写重复："+key);
            if(Regex.Matches(value,"#cursor").Count>1)throw new ArgumentException("每条规则只能有一个 #cursor："+key);
            result.Add(new Snippet{Key=key,Value=value});
        }
        if(!new[]{"matrix","pmatrix","bmatrix","Bmatrix","vmatrix","Vmatrix","smallmatrix"}.Contains(Matrix))throw new ArgumentException("不支持的矩阵环境。");
        if(Align!="align*"&&Align!="aligned")throw new ArgumentException("对齐环境应为 align* 或 aligned。");
        return result.OrderByDescending(x=>x.Key.Length).ToList();
    }
}
public sealed class Snippet { public string Key,Value; }
public sealed class EditState {
    public string Text=""; public int Start,Length,TabIndex=-1;public List<int> Tabs=new List<int>();
    public EditState Clone(){return new EditState{Text=Text,Start=Start,Length=Length,TabIndex=TabIndex,Tabs=new List<int>(Tabs)};}
    public EditState Replace(int start,int length,string value,int caret=-1){
        var n=Clone();n.Text=Text.Remove(start,length).Insert(start,value);n.Start=caret<0?start+value.Length:caret;n.Length=0;
        int end=start+length,delta=value.Length-length;
        for(int i=0;i<n.Tabs.Count;i++){
            if(n.Tabs[i]==start&&i==n.TabIndex)continue;
            if(n.Tabs[i]>=end)n.Tabs[i]+=delta;else if(n.Tabs[i]>start)n.Tabs[i]=start;
        }
        return n;
    }
}
// Pure text transformations: preview and Visio never receive snippet markers.
public sealed class QuickInput {
    public QuickOptions Options {get;private set;}
    readonly List<Snippet> rules;
    public QuickInput(QuickOptions options){Options=options;rules=options.Parse();}
    public static bool Escaped(string text,int pos){int n=0;while(--pos>=0&&text[pos]=='\\')n++;return n%2!=0;}
    public static bool TextContext(string text,int pos){
        var stack=new Stack<bool>();bool inside=false,comment=false;
        for(int i=0;i<pos;i++){
            if(text[i]=='\n'){comment=false;continue;}if(comment)continue;
            if(text[i]=='%'&&!Escaped(text,i)){comment=true;continue;}
            if(Escaped(text,i))continue;
            if(text[i]=='{'){bool isText=Regex.IsMatch(text.Substring(0,i),@"\\(?:text\w*|mbox|hbox)\s*$");stack.Push(inside);inside|=isText;}
            else if(text[i]=='}'&&stack.Count>0)inside=stack.Pop();
        }
        return inside||comment;
    }
    static string EnvironmentAt(string text,int pos){
        var stack=new Stack<string>();string clean=Regex.Replace(text.Substring(0,pos),@"(?<!\\)%[^\n]*","");
        foreach(Match m in Regex.Matches(clean,@"\\(begin|end)\{([^}]+)\}")){
            if(m.Groups[1].Value=="begin")stack.Push(m.Groups[2].Value);else if(stack.Count>0&&stack.Peek()==m.Groups[2].Value)stack.Pop();
        }
        return stack.Count==0?"":stack.Peek();
    }
    EditState Expand(EditState s){
        if(s.Length>0)return null;
        foreach(var rule in rules){
            int p=s.Start-rule.Key.Length;if(p<0||string.CompareOrdinal(s.Text,p,rule.Key,0,rule.Key.Length)!=0)continue;
            if(p>0&&char.IsLetter(s.Text[p-1]))continue;
            int remove=rule.Key.Length;
            if(p>0&&s.Text[p-1]=='\\'&&rule.Value.StartsWith("\\",StringComparison.Ordinal)&&!Escaped(s.Text,p-1)){p--;remove++;}
            var text=new StringBuilder();var stops=new List<KeyValuePair<int,int>>();int cursor=-1,last=0,ordinal=100000;
            foreach(Match marker in Regex.Matches(rule.Value,@"#cursor|#tab(\d*)")){
                text.Append(rule.Value.Substring(last,marker.Index-last));
                if(marker.Value=="#cursor")cursor=text.Length;
                else{int order;if(marker.Groups[1].Value.Length==0)order=ordinal++;else if(!int.TryParse(marker.Groups[1].Value,out order))order=ordinal++;stops.Add(new KeyValuePair<int,int>(order,text.Length));}
                last=marker.Index+marker.Length;
            }
            text.Append(rule.Value.Substring(last));string inserted=text.ToString();
            if(cursor<0)cursor=inserted.EndsWith("{}",StringComparison.Ordinal)?inserted.Length-1:inserted.Length;
            var n=s.Replace(p,remove,inserted,p+cursor);n.Tabs.Clear();n.Tabs.Add(n.Start);
            n.Tabs.AddRange(stops.OrderBy(t=>t.Key).Select(t=>p+t.Value));n.TabIndex=0;return n;
        }
        return null;
    }
    static int MatchingOpen(string text,int close){
        string opens="({[",closes=")}]";int kind=closes.IndexOf(text[close]);if(kind<0)return -1;
        int depth=0;for(int i=close;i>=0;i--){if(Escaped(text,i))continue;if(text[i]==closes[kind])depth++;else if(text[i]==opens[kind]&&--depth==0)return i;}return -1;
    }
    static bool Balanced(string text){var stack=new Stack<char>();string opens="({[",closes=")}]";for(int i=0;i<text.Length;i++){if(Escaped(text,i))continue;int o=opens.IndexOf(text[i]),c=closes.IndexOf(text[i]);if(o>=0)stack.Push(text[i]);else if(c>=0&&(stack.Count==0||stack.Pop()!=opens[c]))return false;}return stack.Count==0;}
    static string Unwrap(string s){return s.Length>=2&&s[0]=='('&&s[s.Length-1]==')'&&MatchingOpen(s,s.Length-1)==0?s.Substring(1,s.Length-2):s;}
    static EditState Fraction(EditState s){
        if(s.Start<2)return null;int line=s.Text.LastIndexOf('\n',s.Start-1)+1;
        int slash=s.Text.LastIndexOf('/',s.Start-1);if(slash<line||slash==0||Escaped(s.Text,slash))return null;
        if(slash>0&&slash+1<s.Text.Length&&s.Text[slash-1]==' '&&s.Text[slash+1]==' ')return null;
        if(")}]".IndexOf(s.Text[s.Start-1])>=0){int open=MatchingOpen(s.Text,s.Start-1);if(open>=line&&open<slash){string n=s.Text.Substring(open+1,slash-open-1),d=s.Text.Substring(slash+1,s.Start-slash-2);if(n.Length>0&&d.Length>0&&Balanced(n)&&Balanced(d))return s.Replace(open,s.Start-open,@"\frac{"+n+"}{"+d+"}");}}
        string den=s.Text.Substring(slash+1,s.Start-slash-1);if(den.Length==0||!Balanced(den))return null;
        int start=slash-1;
        if(s.Text[start]==')'){start=MatchingOpen(s.Text,start);if(start<line)return null;}
        else{
            var stack=new Stack<char>();string opens="({[",closes=")}]";
            for(;start>=line;start--){char c=s.Text[start];if(Escaped(s.Text,start))continue;int close=closes.IndexOf(c),open=opens.IndexOf(c);
                if(close>=0)stack.Push(opens[close]);else if(open>=0){if(stack.Count==0)break;if(stack.Pop()!=c)return null;}
                else if(stack.Count==0&&(char.IsWhiteSpace(c)||"=><,/&^_$".IndexOf(c)>=0))break;
            }
            start++;
        }
        if(start>=slash)return null;string num=s.Text.Substring(start,slash-start);if(!Balanced(num))return null;
        return s.Replace(start,s.Start-start,@"\frac{"+Unwrap(num)+"}{"+Unwrap(den)+"}");
    }
    static EditState ScriptBraces(EditState s){
        if(s.Start==0)return null;string before=s.Text.Substring(0,s.Start);Match m=Regex.Match(before,@"(?<!\\)([_^])([^{}\s_^]+)$");
        if(!m.Success||!Balanced(m.Groups[2].Value))return null;
        if(m.Groups[1].Value=="_"&&m.Groups[2].Length>9)return null;
        return s.Replace(m.Index,m.Length,m.Groups[1].Value+"{"+Unwrap(m.Groups[2].Value)+"}");
    }
    public EditState Handle(EditState s,string key){
        if(!Options.Enabled)return null;
        int pos=s.Start;bool text=TextContext(s.Text,pos);
        if(!text&&(key=="Space"||key=="ShiftSpace"||key=="Tab")){
            if(key=="ShiftSpace"||(Options.TabToExpand?key=="Tab":key=="Space")){var expand=Expand(s);if(expand!=null)return expand;}
        }
        if(key=="Tab"||key=="ShiftTab"){
            if(s.TabIndex>=0){int index=s.TabIndex+(key=="Tab"?1:-1);if(index>=0&&index<s.Tabs.Count){var n=s.Clone();n.TabIndex=index;n.Start=n.Tabs[index];n.Length=0;return n;}}
            if(!text&&Options.Environments&&key=="Tab"){
                string env=EnvironmentAt(s.Text,pos);if(env=="cases"||env.EndsWith("matrix",StringComparison.Ordinal)){
                    if(pos>=3&&s.Text.Substring(pos-3,3)==" & "){
                        int end=s.Text.IndexOf(@"\end{"+env+"}",pos,StringComparison.Ordinal);
                        if(end>=0&&string.IsNullOrWhiteSpace(s.Text.Substring(pos,end-pos)))return s.Replace(pos-3,3,"",end+env.Length+6-3);
                    }
                    return s.Replace(pos,s.Length," & ");
                }
            }
            if(Options.Brackets){
                if(key=="Tab")for(int i=pos;i<s.Text.Length;i++){if(s.Text[i]=='\n')break;if("})]".IndexOf(s.Text[i])>=0&&!Escaped(s.Text,i)){var n=s.Clone();n.Start=i+1;if(i+1<s.Text.Length&&s.Text[i+1]=='{')n.Start++;n.Length=0;n.Tabs.Clear();n.TabIndex=-1;return n;}}
                else for(int i=pos-2;i>=0;i--){if(s.Text[i]=='\n')break;if("{([".IndexOf(s.Text[i])>=0&&!Escaped(s.Text,i)){var n=s.Clone();n.Start=i+1;n.Length=0;return n;}}
            }
            return null;
        }
        if(text)return null;
        if(key=="Space"&&s.Length==0){
            // A fraction in an exponent must be completed before wrapping the exponent.
            if(Options.Scripts){var script=ScriptBraces(s);if(script!=null){int slash=s.Text.LastIndexOf('/',Math.Max(0,pos-1));int power=Math.Max(s.Text.LastIndexOf('^',Math.Max(0,pos-1)),s.Text.LastIndexOf('_',Math.Max(0,pos-1)));if(slash<power)return script;}}
            if(Options.Fractions){var f=Fraction(s);if(f!=null)return f;}
            if(Options.Scripts){var script=ScriptBraces(s);if(script!=null)return script;}
            if(Options.AutoAlign){string env=EnvironmentAt(s.Text,pos);if(env=="align*"||env=="aligned"||env=="align"){
                Match m=Regex.Match(s.Text.Substring(0,pos),@"(?:\\(?:le|ge|neq|approx)|[=><])$");if(m.Success&&(m.Index==0||s.Text[m.Index-1]!='&'))return s.Replace(m.Index,m.Length,"&"+m.Value+" ");
            }}
        }
        if(Options.Environments){
            string env=key=="Align"?Options.Align:key=="Matrix"?Options.Matrix:key=="Cases"?"cases":null;
            if(env!=null){string selected=s.Text.Substring(pos,s.Length);string begin=@"\begin{"+env+"}\n",end="\n"+@"\end{"+env+"}";return s.Replace(pos,s.Length,begin+selected+end,pos+begin.Length+selected.Length);}
            if(key=="Enter"){env=EnvironmentAt(s.Text,pos);if(env=="cases"||env=="aligned"||env=="align*"||env=="align"||env.EndsWith("matrix",StringComparison.Ordinal))return s.Replace(pos,s.Length," \\\\\n");}
        }
        if(key.Length==1&&Options.Brackets){
            string opens="({[",closes=")}]";int open=opens.IndexOf(key[0]),close=closes.IndexOf(key[0]);
            if(open>=0&&!Escaped(s.Text,pos)){
                if(s.Length>0){var n=s.Replace(pos,s.Length,key+s.Text.Substring(pos,s.Length)+closes[open],pos+1);n.Length=s.Length;return n;}
                if(pos==s.Text.Length||char.IsWhiteSpace(s.Text[pos])||closes.IndexOf(s.Text[pos])>=0)return s.Replace(pos,0,key+closes[open],pos+1);
            }
            if(close>=0&&s.Length==0&&pos<s.Text.Length&&s.Text[pos]==key[0]&&!Escaped(s.Text,pos)){var n=s.Clone();n.Start++;return n;}
        }
        if(key=="Backspace"&&Options.Brackets&&s.Length==0&&pos>0&&pos<s.Text.Length){int i="({[".IndexOf(s.Text[pos-1]);if(i>=0&&")}]"[i]==s.Text[pos]&&!Escaped(s.Text,pos-1))return s.Replace(pos-1,2,"");}
        if(key=="m"&&Options.SumLimits&&s.Length==0&&pos>=3&&s.Text.Substring(pos-3,3)==@"\su"&&!Escaped(s.Text,pos-3))return s.Replace(pos,0,@"m\limits");
        return null;
    }
}
}
