using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed partial class TrainerForm : Form
{
    readonly Color Dark=Color.FromArgb(30,32,34),PanelColor=Color.FromArgb(35,37,39),Orange=Color.FromArgb(255,140,0);
    readonly Label processInfo=new Label(),status=new Label(),hotkeyHint=new Label(),soundLabel=new Label();
    readonly SpeedToggleSwitch soundToggle=new SpeedToggleSwitch{IsOn=true,Size=new Size(36,18)};
    readonly SpeedToggleSwitch speedToggle=new SpeedToggleSwitch();
    readonly SpeedToggleSwitch researchToggle=new SpeedToggleSwitch(),buildingsToggle=new SpeedToggleSwitch();
    readonly SpeedToggleSwitch healthToggle=new SpeedToggleSwitch(),recruitToggle=new SpeedToggleSwitch(),populationToggle=new SpeedToggleSwitch();
    volatile bool healthRequested,recruitRequested;
    bool populationRequested,speedRequested;
    readonly SessionSpeedOverride sessionSpeed=new SessionSpeedOverride();
    Func<string,bool> confirmIrreversible;
    readonly ContinuousInstantModes instantModes=new ContinuousInstantModes();
    readonly System.Windows.Forms.Timer instantTimer=new System.Windows.Forms.Timer();
    Task instantWorker;
    bool researchRequested,buildingsRequested,instantBusy;
    volatile bool closing;
    DateTime nextResearchTick=DateTime.MinValue,nextInstantAttempt=DateTime.MinValue;
    int instantRevision;
    readonly TextBox speedEditor=new TextBox();
    readonly NumericUpDown wood=ResourceBox(),food=ResourceBox(),gold=ResourceBox(),stone=ResourceBox(),speed=SpeedBox();
    readonly System.Windows.Forms.Timer detector=new System.Windows.Forms.Timer();
    int hookedPid,detectedPid,language;bool hookBusy,hotkeysEnabled=true;
    Func<string> statusMessage;DateTime statusTime;bool statusError;
    DateTime nextCacheRefresh=DateTime.MinValue,nextAttachAttempt=DateTime.MinValue;
    const int WM_HOTKEY=0x0312;

    internal TrainerForm()
    {
        Text="Age of Empires II: Definitive Edition +12 Trainer - Continuous";ClientSize=new Size(790,680);FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;BackColor=Dark;ForeColor=Color.Gainsboro;Font=new Font("Microsoft YaHei UI",9.5f);
        language=LanguageForCulture(CultureInfo.CurrentUICulture);
        using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AoE.Icon"))if(stream!=null)Icon=new Icon(stream);
        speedToggle.Click+=delegate{ToggleSpeed();};speed.ValueChanged+=delegate{speedEditor.Text=FormatSpeed();UpdateActiveSpeed();};
        soundToggle.Click+=delegate{soundToggle.IsOn=!soundToggle.IsOn;};
        researchToggle.Click+=delegate{InstantResearchAction();};
        buildingsToggle.Click+=delegate{InstantBuildingsAction();};
        healthToggle.Click+=delegate{RestoreHealthAction();};
        recruitToggle.Click+=delegate{InstantRecruitAction();};
        populationToggle.Click+=delegate{MaxPopulationAction();};
        confirmIrreversible=message=>MessageBox.Show(this,message,T("确认单次操作","確認單次操作","Confirm one-shot action"),MessageBoxButtons.OKCancel,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.OK;
        instantTimer.Interval=250;instantTimer.Tick+=delegate{ApplyInstantModes();};
        speedEditor.Leave+=delegate{CommitSpeedEditor();};
        speedEditor.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter){CommitSpeedEditor();e.Handled=true;e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Escape){speedEditor.Text=FormatSpeed();e.Handled=true;e.SuppressKeyPress=true;}};
        BuildUi();detector.Interval=1000;detector.Tick+=delegate{DetectGame();};Shown+=delegate{RegisterKeys();detector.Start();instantTimer.Start();DetectGame();};
    }

    string FormatSpeed(){return speed.Value.ToString("0.##",CultureInfo.InvariantCulture);}
    void ChangeSpeed(decimal delta)
    {
        decimal requested=Math.Min(speed.Maximum,Math.Max(speed.Minimum,speed.Value+delta));
        if(speed.Value!=requested)speed.Value=requested;
        else speedEditor.Text=FormatSpeed();
    }
    bool CommitSpeedEditor()
    {
        decimal requested;
        if(!decimal.TryParse(speedEditor.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out requested)&&
           !decimal.TryParse(speedEditor.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out requested))
        {
            speedEditor.Text=FormatSpeed();
            SetStatus(()=>T("请输入有效的游戏速度","請輸入有效的遊戲速度","Enter a valid game speed"),true);
            return false;
        }
        if(requested<speed.Minimum||requested>speed.Maximum)
        {
            speedEditor.Text=FormatSpeed();
            SetStatus(()=>T("游戏速度范围：0.5–10 倍","遊戲速度範圍：0.5–10 倍","Game speed range: 0.5–10x"),true);
            return false;
        }
        speed.Value=decimal.Round(requested,2);
        speedEditor.Text=FormatSpeed();
        return true;
    }
    void UpdateSpeedToggle()
    {
        speedToggle.IsOn=speedRequested;
        speedToggle.AccessibleName=T("游戏速度开关（跨对局沿用）","遊戲速度開關（跨對局沿用）","Game speed preference (across matches)");
    }
    void ToggleSpeed()
    {
        if(!speedRequested&&!CommitSpeedEditor())return;
        speedRequested=!speedRequested;UpdateSpeedToggle();InstantRequestChanged();
    }
    void UpdateActiveSpeed()
    {
        if(speedRequested)InstantRequestChanged();
    }
    static bool IsSessionWaiting(string error)
    {
        return error.StartsWith("当前不在有效的单人对局")||error.StartsWith("请先启动一局游戏")||error.StartsWith("对局已改变");
    }
    void HandleContinuousError(string error)
    {
        appliedSpeed=appliedResearch=appliedBuildings=appliedHealth=appliedRecruit=appliedPopulation=false;
        hookedPid=0;nextCacheRefresh=DateTime.MinValue;
        if(!IsSessionWaiting(error))
        {
            researchRequested=buildingsRequested=healthRequested=recruitRequested=populationRequested=speedRequested=false;
            instantRevision++;
        }
        nextInstantAttempt=DateTime.UtcNow.AddSeconds(1);
        UpdateInstantToggles();UpdateSpeedToggle();UpdateProcessInfo();
        if(IsSessionWaiting(error))SetStatus(()=>T("设置已保留，等待新对局后自动应用。","設定已保留，等待新對局後自動套用。","Preferences retained; waiting for the next match."));
        else SetStatus(()=>T("应用失败，已停止自动启用：","套用失敗，已停止自動啟用：","Apply failed; automatic enable stopped: ")+Localization.Error(error,language),true);
    }

    void SetResource(int id,Func<string> name,NumericUpDown box)
    {
        float value=(float)box.Value;
        Run(()=>T("设置","設定","Set ")+name(),delegate{Engine.SetResource(id,value);},
            ()=>name()+T("已设为 ","已設為 "," set to ")+value.ToString("0.##",CultureInfo.InvariantCulture));
    }
    void RestoreHealthAction(){healthRequested=!healthRequested;InstantRequestChanged();}
    void InstantResearchAction(){researchRequested=!researchRequested;InstantRequestChanged();}
    void InstantBuildingsAction(){buildingsRequested=!buildingsRequested;InstantRequestChanged();}
    void InstantRequestChanged()
    {
        instantRevision++;nextInstantAttempt=DateTime.MinValue;
        UpdateInstantToggles();
        SetStatus(()=>T("正在应用开关；关闭仅停止后续效果。","正在套用開關；關閉僅停止後續效果。","Applying switches; off only stops future effects."));
        ApplyInstantModes();
    }
    void UpdateInstantToggles()
    {
        healthToggle.IsOn=healthRequested;recruitToggle.IsOn=recruitRequested;populationToggle.IsOn=populationRequested;
        healthToggle.AccessibleName=T("持续回血","持續回血","Continuous health");recruitToggle.AccessibleName=T("持续生产","持續生產","Continuous recruitment");populationToggle.AccessibleName=T("人口上限开关","人口上限開關","Population cap toggle");
        researchToggle.IsOn=researchRequested;buildingsToggle.IsOn=buildingsRequested;
        researchToggle.AccessibleName=T("瞬间研发开关","瞬間研發開關","Instant Research toggle");
        buildingsToggle.AccessibleName=T("瞬间建造开关","瞬間建造開關","Instant Buildings toggle");
    }
    void ApplyInstantModes()
    {
        if(closing||instantBusy||DateTime.UtcNow<nextInstantAttempt||(!researchRequested&&!buildingsRequested&&!healthRequested&&!recruitRequested&&!populationRequested&&!speedRequested&&!sessionSpeed.Active&&!instantModes.Active))return;
        bool research=researchRequested,buildings=buildingsRequested,health=healthRequested,recruit=recruitRequested,population=populationRequested;int revision=instantRevision;
        bool speedEnabled=speedRequested;float speedValue=(float)speed.Value;
        bool changed=speedEnabled!=appliedSpeed||speedValue!=appliedSpeedValue||research!=appliedResearch||buildings!=appliedBuildings||health!=appliedHealth||recruit!=appliedRecruit||population!=appliedPopulation;
        instantBusy=true;
        instantWorker=Task.Run(delegate
        {
            string error=null;
            try
            {
                using(GameContext c=Engine.Open(true))
                {
                    try
                    {
                        instantModes.UpdateModes(c,research,buildings,population,health||recruit);
                        sessionSpeed.Update(c,speedEnabled,speedValue);
                        if((research||health||recruit)&&DateTime.UtcNow>=nextResearchTick)
                        {
                            nextResearchTick=DateTime.UtcNow.AddMilliseconds(500);
                            if(!closing&&research&&researchRequested)Engine.FinishCurrentResearch(c);
                            if(!closing&&health&&healthRequested)Engine.MaintainHealth(c);
                            if(!closing&&recruit&&recruitRequested)Engine.FinishCurrentRecruitment(c);
                        }
                    }
                    catch
                    {
                        try{instantModes.Update(c,false,false);}finally{sessionSpeed.Update(c,false,speedValue);}
                        throw;
                    }
                }
            }
            catch(Exception e)
            {
                error=e.Message;
                if(IsSessionWaiting(error)){instantModes.Forget();sessionSpeed.Forget();}
            }
            if(closing)return;
            try
            {
                BeginInvoke((Action)delegate
                {
                    if(closing||IsDisposed)return;
                    instantBusy=false;
                    if(error!=null)
                    {
                        HandleContinuousError(error);
                    }
                    else
                    {
                        appliedSpeed=speedEnabled;appliedSpeedValue=speedValue;appliedResearch=research;appliedBuildings=buildings;appliedHealth=health;appliedRecruit=recruit;appliedPopulation=population;
                        if(changed&&revision==instantRevision)
                        {
                            SetStatus(()=>T("开关已更新；关闭只停止后续效果，不撤销已完成操作。","開關已更新；不撤銷已完成操作。","Switches updated; stopping does not undo completed actions."));
                            if(research||buildings||health||recruit||population)PlaySuccessFeedback();else PlayDisableFeedback();
                        }
                    }
                });
            }
            catch(InvalidOperationException){}
        });
    }
    bool appliedResearch,appliedBuildings,appliedHealth,appliedRecruit,appliedPopulation,appliedSpeed;
    float appliedSpeedValue;
    void InstantRecruitAction(){recruitRequested=!recruitRequested;InstantRequestChanged();}
    void EmptyAiHealthAction(){if(!confirmIrreversible(T("将其他玩家的单位和建筑设为 1 血，无法撤销。是否执行？","將其他玩家的單位和建築設為 1 血，無法撤銷。是否執行？","Set other players to 1 HP? This cannot be undone.")))return;Run(()=>T("清空 AI 生命值","清空 AI 生命值","Empty AI Health"),delegate{Engine.EmptyAiHealth();},()=>T("AI 单位与建筑已设为 1 血","AI 單位與建築已設為 1 血","AI units and buildings set to 1 HP"));}
    void MaxPopulationAction(){populationRequested=!populationRequested;InstantRequestChanged();}
    void ZeroPopulationAction(){if(!confirmIrreversible(T("人口归零是一次性修改，无法可靠还原；后续生产会继续改变人口。是否执行？","人口歸零無法可靠還原。是否執行？","Zero population cannot be reliably undone. Continue?")))return;Run(()=>T("人口归零","人口歸零","Zero Population"),delegate{Engine.SetResource(11,0);},()=>T("当前人口已设为 0","當前人口已設為 0","Current population set to 0"));}
    void Run(Func<string> name,Action work,Func<string> success)
    {
        SetStatus(()=>name()+T("处理中……","處理中……"," processing..."));
        Task.Run(delegate
        {
            try{work();BeginInvoke((Action)delegate{SetStatus(success);PlaySuccessFeedback();});}
            catch(Exception e){string detail=e.Message;BeginInvoke((Action)delegate{SetStatus(()=>T("错误：","錯誤：","Error: ")+Localization.Error(detail,language),true);});}
        });
    }
    void SetStatus(Func<string> message,bool error=false)
    {
        statusMessage=message;statusTime=DateTime.Now;statusError=error;RenderStatus();
    }
    void RenderStatus()
    {
        if(statusMessage==null)return;
        status.Text=statusTime.ToString("HH:mm:ss")+"  "+statusMessage();
        status.ForeColor=statusError?Color.Salmon:Color.LightGreen;
    }
    void PlaySuccessFeedback(){if(soundToggle.IsOn)AudioFeedback.Success();}
    void PlayDisableFeedback(){if(soundToggle.IsOn)AudioFeedback.Disable();}
    void DetectGame()
    {
        Process[] ps=Process.GetProcessesByName("AoE2DE_s");
        if(ps.Length!=1)
        {
            bool wasDetected=detectedPid!=0;
            hookedPid=detectedPid=0;UpdateSpeedToggle();UpdateProcessInfo();
            if(wasDetected)SetStatus(()=>T("等待游戏进程……","等待遊戲進程……","Waiting for game process..."));
            return;
        }
        int pid=ps[0].Id;detectedPid=pid;
        if(DateTime.UtcNow<nextAttachAttempt||hookedPid==pid&&DateTime.UtcNow<nextCacheRefresh){UpdateProcessInfo();return;}
        if(hookBusy)return;hookBusy=true;UpdateProcessInfo();
        Task.Run(delegate
        {
            try
            {
                int count=Engine.RefreshObjectCache();ulong player;
                using(GameContext c=Engine.Open(false))player=c.Player;
                BeginInvoke((Action)delegate
                {
                    UpdateSpeedToggle();
                    hookedPid=pid;nextCacheRefresh=DateTime.UtcNow.AddSeconds(60);UpdateProcessInfo();
                    SetStatus(()=>T("已连接游戏，对象缓存 ","已連接遊戲，物件快取 ","Game hooked, object cache: ")+count);
                });
            }
            catch(Exception e)
            {
                string detail=e.Message;
                BeginInvoke((Action)delegate
                {
                    nextAttachAttempt=DateTime.UtcNow.AddSeconds(5);UpdateProcessInfo();
                    if(detail.StartsWith("仅支持游戏版本"))SetStatus(()=>T("仅支持游戏版本 101.103.54800.0。检测到：","僅支援遊戲版本 101.103.54800.0。偵測到：","Only game version 101.103.54800.0 is supported. Detected: ")+detail.Substring(detail.LastIndexOf('：')+1),true);
                    else if(detail.StartsWith("当前不在有效的单人对局"))
                    {
                        UpdateSpeedToggle();
                        SetStatus(()=>T("已检测游戏，等待进入单人战局……","已偵測遊戲，等待進入單人戰局……","Game detected; waiting for a single-player match..."));
                    }
                });
            }
            finally{hookBusy=false;}
        });
    }

    static readonly int[] FeatureKeys={0x61,0x62,0x63,0x64,0x66,0x67,0x68,0x69,0x60,0x6B,0x6D,0x6E};
    bool RegisterFeatureKeys(){bool ok=true;for(int i=0;i<FeatureKeys.Length;i++)ok&=Native.RegisterHotKey(Handle,i+1,0x4000,(uint)FeatureKeys[i]);return ok;}
    void UnregisterFeatureKeys(){for(int i=1;i<=FeatureKeys.Length;i++)Native.UnregisterHotKey(Handle,i);}
    void RegisterKeys()
    {
        bool toggleOk=Native.RegisterHotKey(Handle,13,0x4000|0x2|0x4,0x24);
        bool featureOk=RegisterFeatureKeys();
        if(!toggleOk||!featureOk)SetStatus(()=>T("部分热键注册失败，请关闭其他修改器。","部分熱鍵註冊失敗，請關閉其他修改器。","Some hotkeys failed; close other trainers."),true);
    }
    void UpdateHotkeyHint()
    {
        hotkeyHint.Text=T("快捷键：Ctrl+Shift+Home 切换（当前：","快捷鍵：Ctrl+Shift+Home 切換（目前：","Hotkeys: Ctrl+Shift+Home to toggle (currently: ")+
            (hotkeysEnabled?T("启用","啟用","on"):T("关闭","關閉","off"))+T("）","）",")");
    }
    void ToggleHotkeys()
    {
        if(hotkeysEnabled)
        {
            UnregisterFeatureKeys();hotkeysEnabled=false;
            SetStatus(()=>T("功能快捷键已关闭","功能快捷鍵已關閉","Feature hotkeys disabled"));
            PlayDisableFeedback();
        }
        else
        {
            bool ok=RegisterFeatureKeys();hotkeysEnabled=true;
            SetStatus(ok?new Func<string>(()=>T("功能快捷键已启用","功能快捷鍵已啟用","Feature hotkeys enabled")):
                new Func<string>(()=>T("部分热键注册失败，请关闭其他修改器。","部分熱鍵註冊失敗，請關閉其他修改器。","Some hotkeys failed; close other trainers.")),!ok);
            if(ok)PlaySuccessFeedback();
        }
        UpdateHotkeyHint();
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        detector.Stop();instantTimer.Stop();instantTimer.Dispose();
        UnregisterFeatureKeys();Native.UnregisterHotKey(Handle,13);base.OnFormClosed(e);
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        closing=true;instantTimer.Stop();
        if(instantWorker!=null)instantWorker.Wait();
        if(instantModes.Active||sessionSpeed.Active)
        {
            try{using(GameContext c=Engine.Open(true)){try{instantModes.Update(c,false,false);}finally{sessionSpeed.Update(c,false,1f);}}}
            catch(Exception error)
            {
                if(IsSessionWaiting(error.Message)){instantModes.Forget();sessionSpeed.Forget();}
                // A terminated game or replaced session has no live override to restore.
                if((instantModes.Active||sessionSpeed.Active)&&Process.GetProcessesByName("AoE2DE_s").Length!=0)
                {
                    e.Cancel=true;closing=false;instantBusy=false;
                    researchRequested=buildingsRequested=healthRequested=recruitRequested=populationRequested=speedRequested=false;UpdateInstantToggles();instantTimer.Start();
                    string detail=error.Message;
                    SetStatus(()=>T("恢复原值失败，请保持修改器开启后重试：","恢復原值失敗，請保持修改器開啟後重試：","Restore failed; keep the trainer open and retry: ")+detail,true);
                    return;
                }
            }
        }
        base.OnFormClosing(e);
        if(e.Cancel){closing=false;instantBusy=false;instantTimer.Start();}
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg!=WM_HOTKEY){base.WndProc(ref m);return;}
        if(m.WParam.ToInt32()==13){ToggleHotkeys();return;}
        if(!hotkeysEnabled)return;
        switch(m.WParam.ToInt32())
        {
            case 1:SetResource(1,()=>T("木材","木材","Wood"),wood);break;
            case 2:SetResource(0,()=>T("食物","食物","Food"),food);break;
            case 3:SetResource(3,()=>T("黄金","黃金","Gold"),gold);break;
            case 4:SetResource(2,()=>T("石料","石料","Stone"),stone);break;
            case 5:RestoreHealthAction();break;
            case 6:InstantResearchAction();break;
            case 7:InstantBuildingsAction();break;
            case 8:InstantRecruitAction();break;
            case 9:EmptyAiHealthAction();break;
            case 10:MaxPopulationAction();break;
            case 11:ZeroPopulationAction();break;
            case 12:ToggleSpeed();break;
        }
    }
}
