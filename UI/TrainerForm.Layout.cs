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

internal sealed partial class TrainerForm
{
    internal static int LanguageForCulture(CultureInfo culture)
    {
        string name=culture.Name.ToLowerInvariant();
        if(name=="zh"||name.StartsWith("zh-hans")||name.StartsWith("zh-chs")||name.StartsWith("zh-cn")||name.StartsWith("zh-sg")||name.StartsWith("zh-my"))return 0;
        if(name.StartsWith("zh-hant")||name.StartsWith("zh-cht")||name.StartsWith("zh-tw")||name.StartsWith("zh-hk")||name.StartsWith("zh-mo"))return 1;
        switch(culture.TwoLetterISOLanguageName)
        {
            case "ru":return 3;
            case "es":return 4;
            case "ja":return 5;
            case "ar":return 6;
            case "fr":return 7;
            case "de":return 8;
            case "ko":return 9;
        }
        return 2;
    }

    string T(string simplified,string traditional,string english){return language==0?simplified:(language==1?traditional:Localization.Get(english,language));}

    void BuildUi()
    {
        SuspendLayout();
        foreach(Control reused in new Control[]{processInfo,status,hotkeyHint,soundLabel,soundToggle,speedToggle,researchToggle,buildingsToggle,speedEditor,wood,food,gold,stone,speed})
            if(reused.Parent!=null)reused.Parent.Controls.Remove(reused);
        while(Controls.Count>0){Control old=Controls[0];Controls.RemoveAt(0);old.Dispose();}
        var header=new Panel{Dock=DockStyle.Top,Height=70,BackColor=Color.FromArgb(28,30,31)};Controls.Add(header);
        var title=new Label{Text="Age of Empires II: Definitive Edition",ForeColor=Orange,Font=new Font(Font.FontFamily,15,FontStyle.Bold),AutoSize=true,Location=new Point(20,10)};header.Controls.Add(title);
        var subtitle=new Label{Text="Build 185872 V3 - Continuous Research / Buildings",ForeColor=Color.Gainsboro,Font=new Font(Font.FontFamily,10,FontStyle.Bold),AutoSize=true,Location=new Point(20,40)};header.Controls.Add(subtitle);
        EnableWindowDrag(header);EnableWindowDrag(title);EnableWindowDrag(subtitle);
        var minimize=new WindowCaptionButton(false){Name="windowMinimize",AccessibleName=T("最小化","最小化","Minimize"),Location=new Point(694,0)};
        var close=new WindowCaptionButton(true){Name="windowClose",AccessibleName=T("关闭","關閉","Close"),Location=new Point(738,0)};
        minimize.Click+=delegate{WindowState=FormWindowState.Minimized;};close.Click+=delegate{Close();};
        header.Controls.Add(minimize);header.Controls.Add(close);
        AddLanguageSelector(header);

        BuildSidebar();
        BuildFeaturePanel();
        UpdateProcessInfo();UpdateSpeedToggle();UpdateInstantToggles();UpdateHotkeyHint();
        if(statusMessage==null)SetStatus(()=>T("等待游戏进程……","等待遊戲進程……","Waiting for game process..."));else RenderStatus();
        ResumeLayout(true);
    }

    void EnableWindowDrag(Control surface)
    {
        Point startMouse=Point.Empty,startWindow=Point.Empty;bool dragging=false;
        surface.MouseDown+=delegate(object sender,MouseEventArgs e)
        {
            if(e.Button!=MouseButtons.Left)return;
            startMouse=Cursor.Position;startWindow=Location;dragging=true;surface.Capture=true;
        };
        surface.MouseMove+=delegate
        {
            if(dragging){Point current=Cursor.Position;Location=new Point(startWindow.X+current.X-startMouse.X,startWindow.Y+current.Y-startMouse.Y);}
        };
        surface.MouseUp+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){dragging=false;surface.Capture=false;}};
        surface.MouseCaptureChanged+=delegate{if(!surface.Capture)dragging=false;};
    }

    void AddLanguageSelector(Panel header)
    {
        // English language names A–Z; values remain stable language IDs for translations.
        int[] displayOrder={6,0,1,2,7,8,5,9,3,4};
        header.Controls.Add(new Label{Text=T("语言选择：","語言選擇：","Language:"),ForeColor=Color.Gainsboro,
            Location=new Point(552,37),Size=new Size(88,27),TextAlign=ContentAlignment.MiddleRight,Font=new Font(Font.FontFamily,9,FontStyle.Bold)});
        var selector=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,
            DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=22,MaxDropDownItems=10,Location=new Point(646,37),Size=new Size(132,27),
            BackColor=PanelColor,ForeColor=Color.Gainsboro,Font=new Font(Font.FontFamily,9)};
        selector.Items.AddRange(new object[]{"العربية","简体中文","繁體中文","English","Français","Deutsch","日本語","한국어","Русский","Español"});
        selector.DrawItem+=delegate(object sender,DrawItemEventArgs e)
        {
            if(e.Index<0)return;
            bool selected=selector.DroppedDown&&(e.State&DrawItemState.Selected)!=0;
            using(var background=new SolidBrush(selected?Color.FromArgb(55,58,61):PanelColor))e.Graphics.FillRectangle(background,e.Bounds);
            TextRenderer.DrawText(e.Graphics,selector.GetItemText(selector.Items[e.Index]),selector.Font,
                new Rectangle(e.Bounds.X+7,e.Bounds.Y,e.Bounds.Width-7,e.Bounds.Height),Color.Gainsboro,
                TextFormatFlags.VerticalCenter|(e.Index==0?TextFormatFlags.Right:TextFormatFlags.Left)|TextFormatFlags.SingleLine|
                (e.Index==0?TextFormatFlags.RightToLeft:TextFormatFlags.Default));
        };
        selector.SelectedIndex=Array.IndexOf(displayOrder,language);
        selector.SelectedIndexChanged+=delegate{if(selector.SelectedIndex<0||displayOrder[selector.SelectedIndex]==language)return;language=displayOrder[selector.SelectedIndex];BeginInvoke((Action)BuildUi);};
        header.Controls.Add(selector);
    }

    static NumericUpDown ResourceBox(){return new NumericUpDown{Minimum=0,Maximum=99999999,Value=999999,Increment=1000,Size=new Size(92,24),ForeColor=Color.Orange,BackColor=Color.FromArgb(30,32,34),BorderStyle=BorderStyle.FixedSingle,TextAlign=HorizontalAlignment.Center};}

    static NumericUpDown SpeedBox(){return new NumericUpDown{Minimum=0.5m,Maximum=10m,Value=1.7m,Increment=0.1m,DecimalPlaces=2};}

    void AddSpeedRow(Panel p,ref int y)
    {
        p.Controls.Add(new Label{Text="Num .",Font=new Font(Font.FontFamily,9.5f,FontStyle.Bold),Location=new Point(5,y+4),Size=new Size(105,24)});
        speedToggle.Location=new Point(118,y);p.Controls.Add(speedToggle);
        p.Controls.Add(new Label{Text=T("游戏速度（0.5–10 倍）","遊戲速度（0.5–10 倍）","Game Speed (0.5–10x)"),Location=new Point(178,y+3),Size=new Size(235,24),Font=new Font(Font.FontFamily,9.5f,FontStyle.Bold),RightToLeft=language==6?RightToLeft.Yes:RightToLeft.No,TextAlign=language==6?ContentAlignment.MiddleRight:ContentAlignment.MiddleLeft});
        var less=new Label{Text="<",Location=new Point(420,y-2),Size=new Size(20,27),ForeColor=Orange,Font=new Font(Font.FontFamily,15,FontStyle.Regular),TextAlign=ContentAlignment.MiddleCenter,Cursor=Cursors.Hand};
        less.Click+=delegate{ChangeSpeed(-0.1m);};p.Controls.Add(less);
        speedEditor.Location=new Point(442,y+3);speedEditor.Size=new Size(48,22);speedEditor.BorderStyle=BorderStyle.None;speedEditor.TextAlign=HorizontalAlignment.Center;speedEditor.BackColor=Dark;speedEditor.ForeColor=Orange;speedEditor.Font=new Font(Font.FontFamily,10,FontStyle.Regular);speedEditor.Text=FormatSpeed();p.Controls.Add(speedEditor);
        p.Controls.Add(new Label{Location=new Point(442,y+23),Size=new Size(48,1),BackColor=Orange});
        var more=new Label{Text=">",Location=new Point(492,y-2),Size=new Size(20,27),ForeColor=Orange,Font=new Font(Font.FontFamily,15,FontStyle.Regular),TextAlign=ContentAlignment.MiddleCenter,Cursor=Cursors.Hand};
        more.Click+=delegate{ChangeSpeed(0.1m);};p.Controls.Add(more);y+=34;
    }

    void AddRow(Panel p,ref int y,string hotkey,string option,NumericUpDown value,Action action)
    {
        p.Controls.Add(new Label{Text=hotkey,Font=new Font(Font.FontFamily,9.5f,FontStyle.Bold),Location=new Point(5,y+4),Size=new Size(105,24)});
        var apply=new RoundedApplyButton{Text=T("应用","套用","APPLY"),Location=new Point(118,y)};apply.Click+=delegate{action();};p.Controls.Add(apply);
        p.Controls.Add(new Label{Text=option,Location=new Point(178,y+3),Size=new Size(value==null?365:235,24),Font=new Font(Font.FontFamily,9.5f,FontStyle.Bold),RightToLeft=language==6?RightToLeft.Yes:RightToLeft.No,TextAlign=language==6?ContentAlignment.MiddleRight:ContentAlignment.MiddleLeft});
        if(value!=null){value.Location=new Point(420,y);p.Controls.Add(value);}y+=34;
    }

    void AddInstantRow(Panel p,ref int y,string hotkey,string option,SpeedToggleSwitch toggle)
    {
        p.Controls.Add(new Label{Text=hotkey,Font=new Font(Font.FontFamily,9.5f,FontStyle.Bold),Location=new Point(5,y+4),Size=new Size(105,24)});
        toggle.Location=new Point(118,y);p.Controls.Add(toggle);
        p.Controls.Add(new Label{Text=option,Location=new Point(178,y+3),Size=new Size(365,24),Font=new Font(Font.FontFamily,9.5f,FontStyle.Bold),RightToLeft=language==6?RightToLeft.Yes:RightToLeft.No,TextAlign=language==6?ContentAlignment.MiddleRight:ContentAlignment.MiddleLeft});
        y+=34;
    }

    void UpdateProcessInfo()
    {
        bool waitingForMatch=detectedPid!=0&&hookedPid!=detectedPid;
        string state=detectedPid==0?T("等待游戏","等待遊戲","Waiting for game"):(waitingForMatch?T("已检测 / 等待进入战局","已偵測 / 等待進入戰局","Detected / waiting for match"):T("已连接 / 就绪","已連接 / 就緒","Hooked / Ready"));
        processInfo.Text=T("游戏进程名称：","遊戲程序名稱：","Game Process Name:")+"\r\nAoE2DE_s.exe\r\n\r\n"+T("进程 ID：","程序 ID：","Process ID: ")+(detectedPid==0?"--":detectedPid.ToString())+"\r\n"+T("状态：","狀態：","Status: ")+state;
        processInfo.Height=waitingForMatch?129:108;
        int soundY=processInfo.Bottom+20;
        soundLabel.Location=new Point(16,soundY);
        soundToggle.Location=new Point(soundLabel.Right+10,soundY-1);
    }
}
