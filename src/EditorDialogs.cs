using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace VisioFormula {
public sealed class QuickSettingsDialog : Form {
    readonly TextBox rules=new TextBox();readonly Dictionary<string,CheckBox> checks=new Dictionary<string,CheckBox>();readonly ComboBox trigger=new FontChoice(),matrix=new FontChoice();readonly Label error=new Label();
    public QuickOptions Result {get;private set;}
    string align;
    public QuickSettingsDialog(QuickOptions options){
        DialogTheme.Initialize(this,"快捷输入设置",new Size(580,616));MinimumSize=SizeFromClientSize(new Size(548,570));
        var layout=DialogTheme.Rows(48,128,8,36,10,-1,32,36);layout.Padding=new Padding(16);
        layout.Controls.Add(DialogTheme.Header("快捷输入","按你的输入习惯，调整自动补全和自定义规则。"),0,0);
        var optionsCard=DialogTheme.Card(12);var toggles=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=4,Margin=Padding.Empty};
        toggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));toggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));for(int i=0;i<4;i++)toggles.RowStyles.Add(new RowStyle(SizeType.Percent,25));int index=0;
        foreach(var pair in new[]{new[]{"Enabled","启用快捷输入"},new[]{"Highlight","语法高亮"},new[]{"Brackets","括号补全与跳转"},new[]{"Fractions","斜线转分式"},new[]{"Scripts","上下标补括号"},new[]{"SumLimits","求和追加 limits"},new[]{"Environments","矩阵 / 对齐 / 分段"},new[]{"AutoAlign","关系符自动对齐"}}){var c=new ThemeCheckBox{Text=pair[1],Dock=DockStyle.Fill};checks.Add(pair[0],c);toggles.Controls.Add(c,index%2,index/2);index++;}
        optionsCard.Controls.Add(toggles);layout.Controls.Add(optionsCard,0,1);
        var choices=DialogTheme.Columns(58,-1,16,56,-1);
        choices.Controls.Add(DialogTheme.Label("展开键",true),0,0);trigger.Items.AddRange(new object[]{"空格","Tab"});trigger.AccessibleName="快捷输入展开键";choices.Controls.Add(DialogTheme.Input(trigger),1,0);
        choices.Controls.Add(DialogTheme.Label("矩阵",true),3,0);matrix.Items.AddRange(new object[]{"pmatrix","bmatrix","matrix","Bmatrix","vmatrix","Vmatrix","smallmatrix"});matrix.AccessibleName="默认矩阵环境";choices.Controls.Add(DialogTheme.Input(matrix),4,0);layout.Controls.Add(choices,0,3);
        var rulesCard=DialogTheme.Card(12);var ruleLayout=DialogTheme.Rows(26,36,-1);ruleLayout.Controls.Add(DialogTheme.Label("自定义规则",false),0,0);
        var help=DialogTheme.Label("每条：缩写:::展开内容;   ·   规则之间换行，可展开多行\n#cursor 首次落点   ·   #tab / #tab1 后续落点",true);help.Font=new Font("Microsoft YaHei UI",8F);ruleLayout.Controls.Add(help,0,1);
        rules.Multiline=true;rules.AcceptsReturn=true;rules.AcceptsTab=true;rules.WordWrap=false;rules.ScrollBars=ScrollBars.Both;rules.Dock=DockStyle.Fill;rules.Margin=Padding.Empty;rules.Font=new Font("Consolas",10);rules.MaxLength=100000;rules.BorderStyle=BorderStyle.None;rules.BackColor=Color.White;rules.ForeColor=Theme.Ink;rules.AccessibleName="自定义快捷输入规则";
        var ruleMenu=new ThemeMenu();var undo=ruleMenu.Items.Add("撤销",null,delegate{rules.Undo();});ruleMenu.Items.Add(new ToolStripSeparator());ruleMenu.Items.Add("剪切",null,delegate{rules.Cut();});ruleMenu.Items.Add("复制",null,delegate{rules.Copy();});ruleMenu.Items.Add("粘贴",null,delegate{rules.Paste();});ruleMenu.Items.Add(new ToolStripSeparator());ruleMenu.Items.Add("全选",null,delegate{rules.SelectAll();});ruleMenu.Opening+=delegate{undo.Enabled=rules.CanUndo;};rules.ContextMenuStrip=ruleMenu;FormClosed+=delegate{ruleMenu.Dispose();};
        ruleLayout.Controls.Add(rules,0,2);rulesCard.Controls.Add(ruleLayout);rules.Enter+=delegate{rulesCard.Highlight=true;rulesCard.Invalidate();};rules.Leave+=delegate{rulesCard.Highlight=false;rulesCard.Invalidate();};layout.Controls.Add(rulesCard,0,5);
        error.Dock=DockStyle.Fill;error.ForeColor=Theme.Error;error.AutoEllipsis=true;error.TextAlign=ContentAlignment.MiddleLeft;error.Margin=Padding.Empty;layout.Controls.Add(error,0,6);
        var actions=DialogTheme.Columns(72,72,72,-1,76,8,76);
        var save=DialogTheme.Button("保存",delegate{try{Result=Read();Result.Parse();DialogResult=DialogResult.OK;}catch(Exception ex){error.Text=ex.Message;}},true);var cancel=DialogTheme.Button("取消",delegate{DialogResult=DialogResult.Cancel;},false);CancelButton=cancel;
        var import=DialogTheme.Button("导入",delegate{Import();},false);import.Quiet=true;var export=DialogTheme.Button("导出",delegate{Export();},false);export.Quiet=true;var reset=DialogTheme.Button("恢复默认",delegate{LoadOptions(new QuickOptions());error.Text="已载入默认值；点击保存后生效。";},false);reset.Quiet=true;
        actions.Controls.Add(import,0,0);actions.Controls.Add(export,1,0);actions.Controls.Add(reset,2,0);actions.Controls.Add(cancel,4,0);actions.Controls.Add(save,6,0);layout.Controls.Add(actions,0,7);
        Controls.Add(layout);LoadOptions(options);ResumeLayout(true);
    }
    void LoadOptions(QuickOptions q){foreach(var pair in checks)pair.Value.Checked=(bool)typeof(QuickOptions).GetField(pair.Key).GetValue(q);trigger.SelectedIndex=q.TabToExpand?1:0;matrix.SelectedItem=q.Matrix;align=q.Align;rules.Text=q.Rules.Replace("\r\n","\n").Replace("\n","\r\n");}
    QuickOptions Read(){var q=new QuickOptions{Rules=rules.Text,TabToExpand=trigger.SelectedIndex==1,Matrix=Convert.ToString(matrix.SelectedItem),Align=align};foreach(var pair in checks)typeof(QuickOptions).GetField(pair.Key).SetValue(q,pair.Value.Checked);return q;}
    void Import(){
        try{using(var d=new OpenFileDialog{Filter="规则或 Quick LaTeX 配置|*.txt;*.json|所有文件|*.*"})if(d.ShowDialog(this)==DialogResult.OK){
            string text=File.ReadAllText(d.FileName);QuickOptions q=Read();
            if(Path.GetExtension(d.FileName).Equals(".json",StringComparison.OrdinalIgnoreCase)){
                var map=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(text);
                if(map.ContainsKey("customShorthand_parameter")){
                    q.Rules=Convert.ToString(map["customShorthand_parameter"]);
                    if(map.ContainsKey("useTabtoComplete_toggle"))q.TabToExpand=Convert.ToBoolean(map["useTabtoComplete_toggle"]);
                    if(map.ContainsKey("addMatrixBlock_parameter"))q.Matrix=Convert.ToString(map["addMatrixBlock_parameter"]);
                }else q=new JavaScriptSerializer().Deserialize<QuickOptions>(text);
            }else q.Rules=text;
            q.Parse();LoadOptions(q);error.Text="已导入；点击保存后生效。";
        }}catch(Exception ex){error.Text=ex.Message;}
    }
    void Export(){try{var q=Read();q.Parse();using(var d=new SaveFileDialog{Filter="快捷输入设置|*.json",FileName="quick-input.json"})if(d.ShowDialog(this)==DialogResult.OK)File.WriteAllText(d.FileName,new JavaScriptSerializer().Serialize(q),System.Text.Encoding.UTF8);}catch(Exception ex){error.Text=ex.Message;}}
}
public sealed class DraftDialog : Form {
    public Draft Chosen;
    public DraftDialog(List<Draft> history){
        DialogTheme.Initialize(this,"恢复最近草稿",new Size(520,400));MinimumSize=SizeFromClientSize(new Size(420,300));
        var layout=DialogTheme.Rows(56,-1,12,36);layout.Padding=new Padding(16);layout.Controls.Add(DialogTheme.Header("最近草稿","选择一条草稿，继续编辑。"),0,0);
        var card=DialogTheme.Card(8);var list=new DraftList{Dock=DockStyle.Fill,AccessibleName="最近草稿"};foreach(var d in history)list.Items.Add(d);if(list.Items.Count>0)list.SelectedIndex=0;
        if(history.Count==0){var empty=DialogTheme.Label("还没有历史草稿\n点击主窗口的“新建”后，当前公式会保存在这里。",true);empty.TextAlign=ContentAlignment.MiddleCenter;card.Controls.Add(empty);}else card.Controls.Add(list);layout.Controls.Add(card,0,1);
        EventHandler choose=delegate{if(list.SelectedItem!=null){Chosen=(Draft)list.SelectedItem;DialogResult=DialogResult.OK;}};
        var actions=DialogTheme.Columns(-1,76,8,128);
        var cancel=DialogTheme.Button("取消",delegate{DialogResult=DialogResult.Cancel;},false);var restore=DialogTheme.Button("恢复选中草稿",choose,true);restore.Enabled=history.Count>0;CancelButton=cancel;AcceptButton=restore;list.DoubleClick+=choose;actions.Controls.Add(cancel,1,0);actions.Controls.Add(restore,3,0);layout.Controls.Add(actions,0,3);Controls.Add(layout);if(history.Count==0)list.Dispose();ResumeLayout(true);
    }
}
public sealed class BatchOptions {public bool WholePage,ChangeFont=true,ChangeSize;public string Font,ScopeKey;public double Size;}
public sealed class BatchDialog : Form {
    readonly ComboBox range=new FontChoice(),font=new FontChoice();readonly NumericUpDown size=new NumericUpDown{Minimum=4,Maximum=96,DecimalPlaces=1,BorderStyle=BorderStyle.None,ForeColor=Theme.Ink};readonly CheckBox changeFont=new ThemeCheckBox{Text="统一字体",Checked=true},changeSize=new ThemeCheckBox{Text="统一字号（pt）"};
    readonly Label progress=new Label();readonly Button apply=new ActionButton{Text="应用到公式",Primary=true},cancel=new ActionButton{Text="关闭"};readonly Func<bool,int> count;readonly Func<BatchOptions,Action<string>,CancellationToken,Task<int>> run;
    CancellationTokenSource cancellation;bool running;
    readonly Func<string> contextName,contextKey;string scopeKey;
    public int Updated {get;private set;}
    public BatchDialog(Formula current,Func<bool,int> count,Func<BatchOptions,Action<string>,CancellationToken,Task<int>> run,Func<string> contextName=null,Func<string> contextKey=null){
        this.contextName=contextName;this.contextKey=contextKey;
        this.count=count;this.run=run;DialogTheme.Initialize(this,"批量统一公式样式",new Size(440,370));MinimumSize=SizeFromClientSize(ClientSize);
        var layout=DialogTheme.Rows(48,36,10,96,10,40,-1,36);layout.Padding=new Padding(16);layout.Controls.Add(DialogTheme.Header("统一公式样式","将字体与字号应用到 Visio 中的公式。"),0,0);
        range.Items.AddRange(new object[]{"Visio 当前选中范围","Visio 当前整页"});range.SelectedIndex=0;range.AccessibleName="批量修改范围";layout.Controls.Add(DialogTheme.Input(range),0,1);
        var card=DialogTheme.Card(12);var fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,152));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));fields.RowStyles.Add(new RowStyle(SizeType.Percent,50));fields.RowStyles.Add(new RowStyle(SizeType.Percent,50));
        font.Items.AddRange(FontCatalog.All);font.SelectedIndex=FontCatalog.IndexOf(current.Font);font.Anchor=AnchorStyles.Left|AnchorStyles.Right;font.Margin=Padding.Empty;font.AccessibleName="统一后的字体";size.Value=(decimal)current.Size;size.Anchor=AnchorStyles.Left|AnchorStyles.Right;size.Margin=Padding.Empty;size.AccessibleName="统一后的字号（pt）";
        changeFont.Dock=changeSize.Dock=DockStyle.Fill;fields.Controls.Add(changeFont,0,0);fields.Controls.Add(font,1,0);fields.Controls.Add(changeSize,0,1);fields.Controls.Add(size,1,1);card.Controls.Add(fields);layout.Controls.Add(card,0,3);
        var hint=DialogTheme.Label("只换字体：保留原有缩放。统一字号：使用指定字号。\n源码、中心位置和旋转保持不变。",true);hint.Font=new Font("Microsoft YaHei UI",8F);layout.Controls.Add(hint,0,5);
        progress.Dock=DockStyle.Fill;progress.AutoEllipsis=true;progress.ForeColor=Theme.Accent;progress.Margin=Padding.Empty;progress.TextAlign=ContentAlignment.MiddleLeft;layout.Controls.Add(progress,0,6);
        var actions=DialogTheme.Columns(-1,76,8,128);cancel.Dock=apply.Dock=DockStyle.Fill;cancel.Margin=apply.Margin=Padding.Empty;actions.Controls.Add(cancel,1,0);actions.Controls.Add(apply,3,0);layout.Controls.Add(actions,0,7);Controls.Add(layout);
        range.SelectedIndexChanged+=delegate{RefreshCount();};changeFont.CheckedChanged+=delegate{RefreshCount();};changeSize.CheckedChanged+=delegate{RefreshCount();};apply.Click+=async delegate{await ApplyAsync();};cancel.Click+=delegate{if(running)cancellation.Cancel();else Close();};FormClosing+=delegate(object sender,FormClosingEventArgs e){if(running){e.Cancel=true;cancellation.Cancel();}};Shown+=delegate{RefreshCount();};ResumeLayout(true);
    }
    void RefreshCount(){try{int n=count(range.SelectedIndex==1);scopeKey=contextKey==null?null:contextKey();progress.Text=(contextName==null?"":contextName()+"\n")+"找到 "+n+" 个可编辑公式（含组内公式）";apply.Enabled=n>0&&(changeFont.Checked||changeSize.Checked);}catch(Exception ex){progress.Text=ex.Message;apply.Enabled=false;}font.Enabled=changeFont.Checked;size.Enabled=changeSize.Checked;}
    async Task ApplyAsync(){
        running=true;cancellation=new CancellationTokenSource();range.Enabled=font.Enabled=size.Enabled=changeFont.Enabled=changeSize.Enabled=apply.Enabled=false;cancel.Text="取消";
        try{Updated=await run(new BatchOptions{WholePage=range.SelectedIndex==1,ChangeFont=changeFont.Checked,ChangeSize=changeSize.Checked,Font=((MathFont)font.SelectedItem).Id,Size=(double)size.Value,ScopeKey=scopeKey},s=>progress.Text=s,cancellation.Token);progress.Text="已更新 "+Updated+" 个公式；Visio 中可一次撤销。";}
        catch(OperationCanceledException){progress.Text="已取消，Visio 公式未改变。";}
        catch(Exception ex){progress.Text="未完成："+ex.Message;}
        finally{running=false;cancellation.Dispose();cancellation=null;cancel.Text="关闭";range.Enabled=changeFont.Enabled=changeSize.Enabled=true;font.Enabled=changeFont.Checked;size.Enabled=changeSize.Checked;apply.Enabled=changeFont.Checked||changeSize.Checked;}
    }
}
}
