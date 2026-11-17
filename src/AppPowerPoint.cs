using System;
using System.Drawing;
using System.Windows.Forms;

namespace VisioFormula {
public sealed partial class MainForm {
    readonly ComboBox destination=new FontChoice();ToolStripItem batchMenu;PowerPointBridge powerpoint;
    bool PowerPointMode {get{return preferences.Destination=="powerpoint";}}
    string DestinationName {get{return PowerPointMode?"PowerPoint":"Visio";}}
    void InitializeDestination(TableLayoutPanel header){
        destination.AccessibleName="输出目标";destination.Anchor=AnchorStyles.Left|AnchorStyles.Right;destination.Margin=new Padding(0,0,6,0);destination.DropDownWidth=150;destination.Items.AddRange(new object[]{"Visio","PowerPoint"});destination.SelectedIndex=PowerPointMode?1:0;destination.TabIndex=8;header.Controls.Add(destination,1,0);
        destination.SelectedIndexChanged+=delegate{preferences.Destination=destination.SelectedIndex==1?"powerpoint":"visio";ClearTarget();SaveSettings();Toggle();SetStatus(PowerPointMode?"PowerPoint 原型 · 读回后可手动更新；拖放会使用剪贴板。":"已切换到 Visio；请重新读回目标公式。");};
    }
    IFormulaHost ActiveHost(){if(PowerPointMode){if(powerpoint==null)powerpoint=new PowerPointBridge();return powerpoint;}EnsureBridge();return new VisioHost(bridge);}
    string TargetLabel(){var ppt=(object)target as PowerPointTarget;return ppt!=null?ppt.Label:binding==null?DestinationName:binding.Label;}
    void ToggleDestination(){
        destination.Enabled=!busy;if(batchMenu!=null){batchMenu.Enabled=!busy&&!PowerPointMode;batchMenu.ToolTipText=PowerPointMode?"PowerPoint 原型暂不提供批量样式。":"";}
        copy.Text="复制到 "+DestinationName;
        tips.SetToolTip(edit,"在 "+DestinationName+" 中选中公式，再点击此处读回源码。");
        tips.SetToolTip(grip,PowerPointMode?"PowerPoint 原型拖放会把公式放入剪贴板。请拖入幻灯片画布；完成后可读回验证源码。":"精确结果就绪后，从预览区域直接拖入 Visio，源码随文档保存。");
        tips.SetToolTip(destination,"选择输出目标。切换会解除旧公式关联；输入和字体保持不变。");
    }
}
}