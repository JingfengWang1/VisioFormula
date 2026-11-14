using System;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Windows.Forms;

[assembly:TargetFramework(".NETFramework,Version=v4.8")]
namespace VisioFormula {
public sealed class MainForm : Form {
    FormulaEditor source=new FormulaEditor(); ComboBox font=new FontChoice(); NumericUpDown size=new NumericUpDown();
    WebBrowser picture=new WebBrowser(); Label status=new Label();
    Button edit=new ActionButton{Quiet=true},update=new ActionButton{Quiet=true},copy=new ActionButton{Primary=true},export=new ActionButton();
    Button newFormula=new ActionButton{Quiet=true},more=new ActionButton{Quiet=true};ContextMenuStrip toolsMenu=new ThemeMenu();
    StatusBadge badge=new StatusBadge(); Label grip=new Label(),editorMode=new Label(); ComboBox zoom=new FontChoice();
    readonly ToolTip tips=new ToolTip{AutoPopDelay=15000,InitialDelay=450,ReshowDelay=150};
    Rendered ready; Bridge bridge; dynamic target; double loadedSize;
    bool busy,applying,closing,browserReady;
    PreviewDragFilter dragFilter;
    readonly RealtimeController realtime;
    readonly SettingsStore settings;
    readonly WorkspaceState workspace;
    readonly System.Windows.Forms.Timer draftTimer=new System.Windows.Forms.Timer { Interval=450 };
    readonly System.Windows.Forms.Timer quickTimer=new System.Windows.Forms.Timer { Interval=80 };
    readonly System.Windows.Forms.Timer settingsTimer=new System.Windows.Forms.Timer { Interval=350 };
    public MainForm():this(new SettingsStore()){}
    public MainForm(SettingsStore settings) {
        this.settings=settings;Preferences initial=settings.Load();workspace=new WorkspaceState(settings.FilePath);Draft draft=workspace.Data.Current;if(draft!=null){initial.Font=draft.Formula.Font;initial.Size=draft.Formula.Size;}realtime=new RealtimeController(new Func<Formula,CancellationToken,Task<Rendered>>(RenderExactAsync));
        SuspendLayout();
        AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        Font=new Font("Microsoft YaHei UI",9F);ForeColor=Theme.Ink;BackColor=Theme.Background;
        Text="Visio Formula 0.8.2";ClientSize=new Size(432,590);MinimumSize=new Size(360,460);
        StartPosition=FormStartPosition.Manual;Location=new Point(30,60);DoubleBuffered=true;
        string iconPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","formula.ico");if(File.Exists(iconPath))Icon=new Icon(iconPath);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=9,Padding=new Padding(16,8,16,6),Margin=Padding.Empty};
        foreach(var row in new[]{new RowStyle(SizeType.Absolute,48),new RowStyle(SizeType.Absolute,40),new RowStyle(SizeType.Absolute,10),new RowStyle(SizeType.Percent,44),new RowStyle(SizeType.Absolute,10),new RowStyle(SizeType.Percent,56),new RowStyle(SizeType.Absolute,10),new RowStyle(SizeType.Absolute,36),new RowStyle(SizeType.Absolute,30)})layout.RowStyles.Add(row);
        var header=Row(3);
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,54));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,54));
        header.Controls.Add(new Label{Text="Formula",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI Semibold",19F),Margin=Padding.Empty},0,0);
        Setup(newFormula,"新建",delegate{NewFormula();});Setup(more,"更多",delegate{toolsMenu.Show(more,new Point(0,more.Height));});header.Controls.Add(newFormula,1,0);header.Controls.Add(more,2,0);
        toolsMenu.Items.Add("快捷输入设置…",null,delegate{ConfigureInput();});toolsMenu.Items.Add("恢复最近草稿…",null,delegate{RestoreDraft();});toolsMenu.Items.Add(new ToolStripSeparator());toolsMenu.Items.Add("批量统一公式样式…",null,delegate{BatchStyles();});

        var options=Row(3);
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,10));options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,112));
        var fontBox=new Surface{Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=new Padding(9,5,6,5)};
        var fontRow=Row(2);
        fontRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,36));fontRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));fontRow.Controls.Add(Caption("字体"),0,0);
        font.AccessibleName="数学字体";font.Anchor=AnchorStyles.Left|AnchorStyles.Right;font.Margin=Padding.Empty;font.Items.AddRange(FontCatalog.All);font.SelectedIndex=FontCatalog.IndexOf(initial.Font);font.TabIndex=1;font.DropDownWidth=240;
        fontRow.Controls.Add(font,1,0);fontBox.Controls.Add(fontRow);
        var sizeBox=new Surface{Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=new Padding(10,5,8,5)};
        var sizeRow=Row(2);
        sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,22));
        size.AccessibleName="公式字号（pt）";size.Minimum=4;size.Maximum=96;size.DecimalPlaces=1;size.Value=(decimal)initial.Size;size.BorderStyle=BorderStyle.None;size.Anchor=AnchorStyles.Left|AnchorStyles.Right;size.Margin=Padding.Empty;size.ForeColor=Theme.Ink;size.TabIndex=2;
        sizeRow.Controls.Add(size,0,0);sizeRow.Controls.Add(Caption("pt"),1,0);sizeBox.Controls.Add(sizeRow);options.Controls.Add(fontBox,0,0);options.Controls.Add(sizeBox,2,0);

        var editor=new Surface{Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=new Padding(12,6,12,12)};
        var editorLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};
        editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var editHeader=Row(3);
        editHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));editHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));editHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,76));
        editorMode.Text="LaTeX";editorMode.Dock=DockStyle.Fill;editorMode.TextAlign=ContentAlignment.MiddleLeft;editorMode.ForeColor=Theme.Muted;editorMode.Margin=Padding.Empty;
        Setup(edit,"编辑选中公式",delegate {ReadSelected();});Setup(update,"原位更新",delegate{UpdateSelected();});edit.TabIndex=3;update.TabIndex=4;
        editHeader.Controls.Add(editorMode,0,0);editHeader.Controls.Add(edit,1,0);editHeader.Controls.Add(update,2,0);
        source.Multiline=true;source.WordWrap=true;source.BorderStyle=BorderStyle.None;
        source.AccessibleName="LaTeX 源码";source.Font=new Font("Consolas",12F);source.ForeColor=Theme.Ink;source.BackColor=Color.White;source.Dock=DockStyle.Fill;source.Margin=new Padding(0,8,0,0);source.Configure(workspace.Quick);source.LoadDocument(draft==null?new Formula().Source:draft.Formula.Source,draft==null?0:draft.Start,draft==null?0:draft.Length);source.TabIndex=0;
        source.Enter+=delegate{editor.Highlight=true;editor.Invalidate();};source.Leave+=delegate{editor.Highlight=false;editor.Invalidate();};
        editorLayout.Controls.Add(editHeader,0,0);editorLayout.Controls.Add(source,0,1);editor.Controls.Add(editorLayout);

        picture.Dock=DockStyle.Fill;picture.Margin=Padding.Empty;picture.IsWebBrowserContextMenuEnabled=false;picture.AllowWebBrowserDrop=false;picture.ScriptErrorsSuppressed=true;picture.WebBrowserShortcutsEnabled=false;picture.TabStop=false;
        picture.Navigating+=delegate(object s,WebBrowserNavigatingEventArgs e){if(e.Url!=null&&e.Url.ToString()!="about:blank")e.Cancel=true;};
        picture.DocumentCompleted+=delegate{
            if(closing)return;
            try{picture.Document.InvokeScript("vfGetState");browserReady=true;SyncViewport();Script("vfZoom",PreviewZoom());Script("vfSetRevision",realtime.Revision);if(ready!=null)PaintExact(ready,realtime.Revision);else PaintQuick();}
            catch(Exception ex){Log(ex);}
        };
        picture.SizeChanged+=delegate{QueueViewportSync();};picture.DpiChangedAfterParent+=delegate{QueueViewportSync();};
        var preview=new Surface{Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=new Padding(8,6,8,6)};
        var previewLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};
        previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,28));previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,30));
        var previewHeader=Row(3);previewHeader.Margin=new Padding(4,0,4,0);
        previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,80));previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,82));
        previewHeader.Controls.Add(Caption("预览"),0,0);
        zoom.AccessibleName="预览缩放";zoom.Font=new Font("Segoe UI",8F);zoom.Dock=DockStyle.Fill;zoom.Margin=new Padding(0,1,8,0);zoom.Items.AddRange(new object[]{"100%","150%","200%","300%","适应"});zoom.SelectedIndex=Array.IndexOf(new[]{100,150,200,300,0},initial.Zoom);zoom.TabIndex=7;previewHeader.Controls.Add(zoom,1,0);
        badge.Dock=DockStyle.Fill;badge.Margin=new Padding(0,3,0,3);badge.Font=new Font("Microsoft YaHei UI",8F);previewHeader.Controls.Add(badge,2,0);
        grip.Dock=DockStyle.Fill;grip.TextAlign=ContentAlignment.MiddleCenter;grip.ForeColor=Theme.Accent;grip.Margin=Padding.Empty;
        previewLayout.Controls.Add(previewHeader,0,0);previewLayout.Controls.Add(picture,0,1);previewLayout.Controls.Add(grip,0,2);preview.Controls.Add(previewLayout);
        // The zoom selector must receive ordinary clicks. Only the artwork and drag grip initiate a drag.
        dragFilter=new PreviewDragFilter(picture,TryDrag,grip);Application.AddMessageFilter(dragFilter);

        var outputs=Row(3);
        outputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));outputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,10));outputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
        Setup(copy,"复制到 Visio",delegate{try{if(ready==null||busy)return;EnsureBridge();bridge.Copy(ready);SetStatus("已复制 · 在 Visio 中按 Ctrl+V");}catch(Exception ex){Error(ex);}});
        Setup(export,"导出 SVG",delegate{
            if(ready==null||busy)return;Rendered chosen=ready;
            try{using(var d=new SaveFileDialog{Filter="SVG 矢量图|*.svg",FileName="formula.svg"})if(d.ShowDialog()==DialogResult.OK){File.Copy(chosen.Svg,d.FileName,true);SetStatus("SVG 已导出");}}
            catch(Exception ex){Error(ex);}
        });
        copy.Margin=export.Margin=Padding.Empty;copy.TabIndex=5;export.TabIndex=6;outputs.Controls.Add(copy,0,0);outputs.Controls.Add(export,2,0);
        status.Dock=DockStyle.Fill;status.ForeColor=Theme.Muted;status.Font=new Font("Microsoft YaHei UI",8F);status.TextAlign=ContentAlignment.MiddleLeft;status.AutoEllipsis=true;status.Margin=Padding.Empty;
        layout.Controls.Add(header,0,0);layout.Controls.Add(options,0,1);layout.Controls.Add(editor,0,3);layout.Controls.Add(preview,0,5);layout.Controls.Add(outputs,0,7);layout.Controls.Add(status,0,8);Controls.Add(layout);
        tips.SetToolTip(source,"空格展开缩写，Tab 跳转；Ctrl+Z 撤销，Ctrl+Y 重做。\nAlt+Shift+M/A/C 插入矩阵/对齐/分段；Ctrl+Enter 精确渲染。");
        tips.SetToolTip(newFormula,"新建公式（Ctrl+N），保留当前字体和字号；旧内容进入最近草稿。");tips.SetToolTip(more,"快捷输入设置、最近草稿、批量统一公式样式。");
        tips.SetToolTip(edit,"在 Visio 中选中公式，再点击此处读回源码。");tips.SetToolTip(update,"把修改应用到读回的公式，保留中心位置和等比缩放。");
        tips.SetToolTip(grip,"精确结果就绪后，从预览区域直接拖入 Visio，源码随文档保存。");
        tips.SetToolTip(zoom,"仅改变预览大小，不改变导入字号。默认 150%；长公式自动缩小以完整显示。");
        UpdateFontHint();
        zoom.SelectedIndexChanged+=delegate{Script("vfZoom",PreviewZoom());RememberSettings();};
        source.TextChanged+=delegate{if(!applying){Schedule(false);RememberDraft();}};
        source.SelectionChanged+=delegate{if(!applying)RememberDraft();};
        font.SelectedIndexChanged+=delegate{UpdateFontHint();if(!applying){Schedule(false);RememberSettings();RememberDraft();}};
        size.ValueChanged+=delegate{if(!applying){Schedule(false);RememberSettings();RememberDraft();}};
        settingsTimer.Tick+=delegate{settingsTimer.Stop();SaveSettings();};
        draftTimer.Tick+=delegate{draftTimer.Stop();SaveDraft();};
        quickTimer.Tick+=delegate{quickTimer.Stop();PaintQuick();};
        realtime.Started+=delegate(int revision){if(!closing){bool mathjax=Current().Font=="mathjax";SetBadge(mathjax?"生成中":"精确渲染中",false,false);SetStatus(mathjax?"正在生成 MathJax 矢量结果…":"本机 LaTeX 正在生成精确结果…");Toggle();}};
        realtime.Ready+=delegate(int revision,Rendered result){
            if(closing)return;ready=result;quickTimer.Stop();PaintExact(result,revision);SetBadge(result.Formula.Font=="mathjax"?"原生结果":"精确结果",true,false);SetStatus("已就绪 · 源码将随 Visio 文档保存");Toggle();
        };
        realtime.Failed+=delegate(int revision,Exception ex){
            if(closing)return;ready=null;Log(ex);Script("vfExactError",revision);SetBadge("检查公式",false,true);SetStatus("渲染未通过："+ShortError(ex)+" · 修改后自动重试",true);Toggle();
        };
        KeyPreview=true;KeyDown+=delegate(object s,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.Enter&&!busy){e.SuppressKeyPress=true;Schedule(true);}if(e.Control&&e.KeyCode==Keys.N&&!busy){e.SuppressKeyPress=true;NewFormula();}};
        FormClosing+=delegate{SaveSettings();SaveDraft();closing=true;quickTimer.Stop();settingsTimer.Stop();draftTimer.Stop();realtime.Dispose();};
        FormClosed+=delegate{quickTimer.Dispose();settingsTimer.Dispose();draftTimer.Dispose();toolsMenu.Dispose();tips.Dispose();Application.RemoveMessageFilter(dragFilter);if(bridge!=null)bridge.Dispose();};
        Load+=delegate{SetMinimumWindowSize();};DpiChanged+=delegate{SetMinimumWindowSize();QueueViewportSync();};
        Shown+=delegate{picture.DocumentText=PreviewHtml.CreateShell();Schedule(false);source.Focus();};Toggle();ResumeLayout(true);
    }
    static TableLayoutPanel Row(int columns){var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=columns,RowCount=1,Margin=Padding.Empty};row.RowStyles.Add(new RowStyle(SizeType.Percent,100));return row;}
    int PreviewZoom(){return new[]{100,150,200,300,0}[Math.Max(0,zoom.SelectedIndex)];}
    void UpdateFontHint(){if(font.SelectedItem!=null)tips.SetToolTip(font,((MathFont)font.SelectedItem).Description+"\n其他字体的快速预览为近似字形，请以最终结果为准。");}
    void RememberSettings(){if(!closing){settingsTimer.Stop();settingsTimer.Start();}}
    void SaveSettings(){try{settings.Save(new Preferences{Font=Current().Font,Size=(double)size.Value,Zoom=PreviewZoom()});SaveDraft();}catch(Exception ex){Log(ex);if(!closing)SetStatus("设置未能保存："+ShortError(ex),true);}}
    void RememberDraft(){if(!closing){draftTimer.Stop();draftTimer.Start();}}
    void CaptureDraft(){workspace.Capture(Current(),source.SelectionStart,source.SelectionLength);}
    void SaveDraft(){try{CaptureDraft();workspace.Save();}catch(Exception ex){Log(ex);if(!closing)SetStatus("草稿未能保存："+ShortError(ex),true);}}
    void ArchiveDraft(){CaptureDraft();workspace.Archive();}
    void NewFormula(){if(busy)return;ArchiveDraft();target=null;loadedSize=0;source.LoadDocument("");SaveDraft();Schedule(false);source.Focus();}
    void RestoreDraft(){if(busy)return;using(var dialog=new DraftDialog(workspace.Data.History))if(dialog.ShowDialog(this)==DialogResult.OK){Draft d=dialog.Chosen;ArchiveDraft();target=null;loadedSize=0;LoadFormula(d.Formula,d.Start,d.Length);SetStatus("已恢复草稿；插入新图形或重新选择 Visio 公式后更新。");}}
    void LoadFormula(Formula f,int start=0,int length=0){applying=true;try{source.LoadDocument(f.Source,start,length);font.SelectedIndex=FontCatalog.IndexOf(f.Font);size.Value=(decimal)f.Size;}finally{applying=false;}SaveDraft();Schedule(false);RememberSettings();source.Focus();}
    void ConfigureInput(){if(busy)return;using(var dialog=new QuickSettingsDialog(workspace.Quick))if(dialog.ShowDialog(this)==DialogResult.OK)try{workspace.SaveQuick(dialog.Result);source.Configure(dialog.Result);SetStatus("快捷输入设置已保存");}catch(Exception ex){Error(ex);}}
    void BatchStyles(){if(busy)return;try{EnsureBridge();using(var dialog=new BatchDialog(Current(),whole=>bridge.Collect(whole).Count,RunBatch,()=>bridge.ContextName(),()=>bridge.ContextKey()))dialog.ShowDialog(this);}catch(Exception ex){Error(ex);}}
    async Task<int> RunBatch(BatchOptions options,Action<string> progress,CancellationToken token){
        if(options.ScopeKey!=null&&options.ScopeKey!=bridge.ContextKey())throw new Exception("当前 Visio 页面已改变，请重新打开批量样式窗口。");
        var targets=bridge.Collect(options.WholePage);if(targets.Count==0)throw new Exception("当前范围内没有本工具生成的公式。");
        busy=true;Toggle();int committed=0;
        try{
            // Stop the editor's outstanding compile before batch work uses the same renderer/browser.
            Task previous=realtime.LastTask;quickTimer.Stop();realtime.Request(new Formula{Source=""},0);if(previous!=null)await previous;ready=null;
            var results=new List<Rendered>();var cache=new Dictionary<string,Rendered>();
            for(int i=0;i<targets.Count;i++){
                token.ThrowIfCancellationRequested();Formula f=targets[i].Original.Clone();if(options.ChangeFont)f.Font=options.Font;if(options.ChangeSize)f.Size=options.Size;
                string key=f.Font+"|"+f.Size.ToString(System.Globalization.CultureInfo.InvariantCulture)+"|"+f.Source;Rendered rendered;
                progress("正在准备 "+(i+1)+" / "+targets.Count+"；尚未修改 Visio");
                if(!cache.TryGetValue(key,out rendered)){rendered=await RenderExactAsync(f,token);cache.Add(key,rendered);}results.Add(rendered);
            }
            token.ThrowIfCancellationRequested();if(options.ScopeKey!=null&&options.ScopeKey!=bridge.ContextKey())throw new Exception("准备期间切换了 Visio 页面，请重新打开批量样式窗口。");progress("正在统一写回 Visio…");bridge.ApplyBatch(targets,results,options.ChangeSize);committed=targets.Count;
            if(target!=null)foreach(var item in targets)if(object.Equals((object)target,item.Shape)){target=null;loadedSize=0;break;}
            return committed;
        }finally{busy=false;Toggle();Schedule(false);if(committed>0)SetStatus("已统一 "+committed+" 个公式；Visio 中可一次撤销，请保存文档。");}
    }
    async Task<Rendered> RenderExactAsync(Formula formula,CancellationToken token){
        if(formula.Font!="mathjax")return await Task.Run(()=>Renderer.Render(formula,token),token);
        Renderer.Validate(formula);var wait=System.Diagnostics.Stopwatch.StartNew();
        while(!browserReady){token.ThrowIfCancellationRequested();if(wait.ElapsedMilliseconds>15000)throw new Exception("MathJax 尚未启动，请重新打开工具。");await Task.Delay(25,token);}
        Dictionary<string,object> exported;
        while(true){
            token.ThrowIfCancellationRequested();
            exported=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>((string)picture.Document.InvokeScript("vfExport",new object[]{Renderer.Body(formula.Source)}));
            if(!exported.ContainsKey("pending"))break;
            if(wait.ElapsedMilliseconds>15000)throw new Exception("MathJax 启动超时，请重新打开工具。");await Task.Delay(25,token);
        }
        if(exported.ContainsKey("error"))throw new Exception("MathJax："+Convert.ToString(exported["error"]));
        string svg=Convert.ToString(exported["svg"]);return await Task.Run(()=>Renderer.RenderMathJax(formula,svg,token),token);
    }
    void SetMinimumWindowSize(){MinimumSize=SizeFromClientSize(new Size((int)Math.Ceiling(344*DeviceDpi/96.0),(int)Math.Ceiling(460*DeviceDpi/96.0)));}
    void QueueViewportSync(){if(browserReady&&!closing&&IsHandleCreated)BeginInvoke((Action)SyncViewport);}
    void SyncViewport(){if(browserReady)Script("vfViewport",picture.DeviceDpi,picture.ClientSize.Width);}
    static Label Caption(string text){return new Label{Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted,Margin=Padding.Empty};}
    void Setup(Button b,string text,EventHandler click){b.Text=text;b.Dock=DockStyle.Fill;b.Margin=new Padding(0,2,0,2);b.Click+=click;}
    void SetStatus(string text,bool error=false){status.Text=text;status.ForeColor=error?Theme.Error:Theme.Muted;tips.SetToolTip(status,text);}
    void SetBadge(string text,bool exact,bool error){badge.Text=text;badge.ForeColor=error?Theme.Error:exact?Theme.Accent:Theme.Muted;badge.BackColor=error?Color.FromArgb(252,237,232):exact?Theme.SoftAccent:Theme.Background;badge.Invalidate();}
    Formula Current(){return new Formula{Source=source.Text,Font=((MathFont)font.SelectedItem).Id,Size=(double)size.Value};}
    void Schedule(bool immediate){
        if(closing)return;ready=null;Formula f=Current();bool empty=string.IsNullOrWhiteSpace(f.Source);
        SetStatus(empty?"输入 LaTeX 公式，即可自动预览":"输入中 · 停顿后自动生成精确结果");SetBadge(empty?"自动预览":"快速预览",false,false);
        int revision=realtime.Request(f,immediate?0:RealtimeController.DelayFor(f),immediate);Script(empty?"vfClear":"vfSetRevision",revision);
        // Throttle rather than debounce: previews continue during sustained typing.
        if(empty)quickTimer.Stop();else if(!quickTimer.Enabled)quickTimer.Start();Toggle();
    }
    object Script(string name,params object[] args){if(!browserReady||closing)return null;try{return picture.Document.InvokeScript(name,args);}catch(Exception ex){Log(ex);return null;}}
    void PaintQuick(){
        if(closing||ready!=null||!browserReady)return;string text=source.Text;if(string.IsNullOrWhiteSpace(text)){Script("vfClear",realtime.Revision);return;}Script("vfQuick",Renderer.Body(text),realtime.Revision,(double)size.Value);
    }
    void PaintExact(Rendered result,int revision){if(browserReady){SyncViewport();Script("vfExact",PreviewHtml.DisplaySvg(File.ReadAllText(result.Svg)),revision,result.Width*96,result.Height*96);}}
    void EnsureBridge(){if(bridge==null)bridge=new Bridge(false);}
    void TryDrag(){
        if(ready==null||busy){if(!busy)SetStatus("精确结果尚未就绪，请稍候");return;}busy=true;Toggle();
        try{SetStatus("正在拖放到 Visio…");EnsureBridge();int effect=bridge.Drag(ready);SetStatus(effect==1?"已拖入 · 请在 Visio 中保存文档":"已取消拖放");}
        catch(Exception ex){Error(ex);}finally{busy=false;Toggle();}
    }
    void Toggle(){
        copy.Enabled=export.Enabled=ready!=null&&!busy;update.Enabled=target!=null&&ready!=null&&!busy;edit.Enabled=!busy;
        newFormula.Enabled=more.Enabled=font.Enabled=size.Enabled=!busy;source.ReadOnly=busy;
        grip.Text=ready!=null?"拖动公式到 Visio  ↗":"精确结果就绪后，即可拖入 Visio";grip.ForeColor=ready!=null?Theme.Accent:Theme.Muted;grip.Cursor=ready!=null?Cursors.Hand:Cursors.Default;
    }
    void Log(Exception ex){try{File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"app.log"),DateTime.Now+" "+ex+Environment.NewLine);}catch{}}
    static string ShortError(Exception ex){foreach(string line in ex.Message.Split('\n'))if(line.TrimStart().StartsWith("!"))return line.Trim().TrimStart('!').Trim();return ex.Message.Length>120?ex.Message.Substring(0,120):ex.Message;}
    void Error(Exception ex){SetStatus("操作未完成："+ShortError(ex),true);Log(ex);}
    void ReadSelected(){
        try{EnsureBridge();dynamic picked=bridge.Selected();Formula f=Bridge.Read(picked);FontCatalog.Get(f.Font);Renderer.Validate(f);ArchiveDraft();target=picked;loadedSize=f.Size;
            LoadFormula(f);
        }catch(Exception ex){Error(ex);}
    }
    void UpdateSelected(){
        if(target==null||busy||ready==null)return;busy=true;Toggle();
        try{EnsureBridge();bridge.Update(target,ready,Math.Abs(ready.Formula.Size-loadedSize)>0.0001);loadedSize=ready.Formula.Size;SetStatus("已原位更新 · 请在 Visio 中保存文档");}
        catch(Exception ex){Error(ex);}finally{busy=false;Toggle();}
    }
}
static class Program {
    [STAThread] static void Main(){
        try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainForm());}
        catch(Exception ex){File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"app.log"),DateTime.Now+" STARTUP "+ex+Environment.NewLine);MessageBox.Show(ex.Message,"Visio Formula");}
    }
}
}
