using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

internal sealed partial class TrainerForm
{
    internal const string ProjectUrl = "https://github.com/xiaoqi419/AOE2-Trainer";

    void BuildSidebar()
    {
        var sidebar = new Panel
        {
            Name = "sidebar", Location = new Point(6, 74),
            Size = new Size(220, 598), BackColor = PanelColor
        };
        Controls.Add(sidebar);
        sidebar.Controls.Add(new Label
        {
            Text = "AOE II", ForeColor = Orange, Font = new Font(Font.FontFamily, 26, FontStyle.Bold),
            Location = new Point(14, 18), Size = new Size(192, 48)
        });
        sidebar.Controls.Add(new Label
        {
            Text = T("决定版 · 单人修改器", "決定版 · 單人修改器", "Definitive Edition · Trainer"),
            ForeColor = Color.Gainsboro, Location = new Point(16, 70), Size = new Size(188, 38)
        });
        sidebar.Controls.Add(new Label
        {
            Text = "BUILD 185872", ForeColor = Orange, Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
            Location = new Point(16, 116), Size = new Size(188, 25)
        });
        string version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
        sidebar.Controls.Add(new Label
        {
            Text = "101.103.54800.0  /  v" + version, ForeColor = Color.Silver,
            Font = new Font(Font.FontFamily, 8.5f), Location = new Point(16, 146), Size = new Size(196, 24)
        });
        sidebar.Controls.Add(new Label
        {
            BackColor = Color.FromArgb(62, 65, 68), Location = new Point(16, 190), Size = new Size(188, 1)
        });
        processInfo.Location = new Point(16, 212);
        processInfo.Size = new Size(188, 129);
        processInfo.ForeColor = Color.Gainsboro;
        processInfo.RightToLeft = language == 6 ? RightToLeft.Yes : RightToLeft.No;
        sidebar.Controls.Add(processInfo);
        soundLabel.Text = T("提示音", "提示音", "Sound effects");
        soundLabel.AutoSize = true;
        soundLabel.ForeColor = Color.Silver;
        soundLabel.RightToLeft = language == 6 ? RightToLeft.Yes : RightToLeft.No;
        sidebar.Controls.Add(soundLabel);
        soundToggle.AccessibleName = T("提示音开关", "提示音開關", "Sound effects toggle");
        sidebar.Controls.Add(soundToggle);
        sidebar.Controls.Add(new Label
        {
            Text = T("研究 / 建造仅作用于自己\r\n关闭开关后恢复原值\r\n仅限单人战局使用",
                "研究 / 建造僅作用於自己\r\n關閉開關後恢復原值\r\n僅限單人戰局使用",
                "Research / build: player only\r\nSwitch off to restore values\r\nSingle-player matches only"),
            ForeColor = Color.Silver, Location = new Point(16, 416), Size = new Size(188, 86)
        });
        sidebar.Controls.Add(new Label
        {
            Text = "xiaoqi419 / AOE2-Trainer", ForeColor = Color.Gray, Location = new Point(16, 522),
            Size = new Size(192, 24), Font = new Font(Font.FontFamily, 8.5f)
        });
        var link = new LinkLabel
        {
            Name = "projectLink", Text = T("项目主页：GitHub", "專案首頁：GitHub", "Project page: GitHub"),
            Location = new Point(16, 554), AutoSize = true, LinkColor = Orange, ActiveLinkColor = Color.White
        };
        link.Links.Add(0, link.Text.Length, ProjectUrl);
        link.LinkClicked += OpenProjectPage;
        sidebar.Controls.Add(link);
    }

    void OpenProjectPage(object sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo((string)e.Link.LinkData) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            string detail = error.Message;
            SetStatus(() => T("无法打开项目主页：", "無法開啟專案首頁：", "Cannot open project page: ") + detail, true);
        }
    }

    void AddSection(Panel panel, ref int y, string name, string title)
    {
        panel.Controls.Add(new Label
        {
            Name = name, Text = title, ForeColor = Orange, Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Location = new Point(5, y + 3), Size = new Size(530, 24)
        });
        y += 32;
    }

    void BuildFeaturePanel()
    {
        var right = new Panel
        {
            Name = "featurePanel", Location = new Point(232, 74), Size = new Size(552, 598), BackColor = Dark
        };
        Controls.Add(right);
        int y = 0;
        AddSection(right, ref y, "resourcesSection", T("资源调整", "資源調整", "Resources"));
        AddRow(right, ref y, "Num 1", T("修改木材", "修改木材", "Edit Wood"), wood, delegate { SetResource(1, () => T("木材", "木材", "Wood"), wood); });
        AddRow(right, ref y, "Num 2", T("修改食物", "修改食物", "Edit Food"), food, delegate { SetResource(0, () => T("食物", "食物", "Food"), food); });
        AddRow(right, ref y, "Num 3", T("修改黄金", "修改黃金", "Edit Gold"), gold, delegate { SetResource(3, () => T("黄金", "黃金", "Gold"), gold); });
        AddRow(right, ref y, "Num 4", T("修改石料", "修改石料", "Edit Stone"), stone, delegate { SetResource(2, () => T("石料", "石料", "Stone"), stone); });

        AddSection(right, ref y, "continuousSection", T("持续开关", "持續開關", "Continuous modes"));
        AddInstantRow(right, ref y, "Num 7", T("瞬间研发（仅自己）", "瞬間研發（僅自己）", "Instant Research - player only"), researchToggle);
        AddInstantRow(right, ref y, "Num 8", T("瞬间建造（仅自己）", "瞬間建造（僅自己）", "Instant Buildings - player only"), buildingsToggle);
        AddSpeedRow(right, ref y);

        AddSection(right, ref y, "actionsSection", T("即时操作", "即時操作", "One-shot actions"));
        AddRow(right, ref y, "Num 6", T("恢复单位与建筑生命值", "恢復單位與建築生命值", "Restore Units & Buildings Health"), null, RestoreHealthAction);
        AddRow(right, ref y, "Num 9", T("瞬间生产单位（一次）", "瞬間生產單位（一次）", "Instant Recruit Units (Once)"), null, InstantRecruitAction);
        AddRow(right, ref y, "Num 0", T("AI 单位与建筑设为 1 血", "AI 單位與建築設為 1 血", "Set AI Units & Buildings to 1 HP"), null, EmptyAiHealthAction);
        AddRow(right, ref y, "Num +", T("最大人口上限 200", "最大人口上限 200", "Max Population Cap: 200"), null, MaxPopulationAction);
        AddRow(right, ref y, "Num -", T("当前人口设为 0（无限人口）", "當前人口設為 0（無限人口）", "Set Population To 0 (Infinite Population)"), null, ZeroPopulationAction);

        hotkeyHint.Location = new Point(0, 510);hotkeyHint.Size = new Size(542, 22);
        hotkeyHint.TextAlign = ContentAlignment.MiddleCenter;hotkeyHint.ForeColor = Color.Silver;
        hotkeyHint.RightToLeft = language == 6 ? RightToLeft.Yes : RightToLeft.No;right.Controls.Add(hotkeyHint);
        status.Location = new Point(0, 540);status.Size = new Size(542, 52);
        status.BorderStyle = BorderStyle.FixedSingle;status.Padding = new Padding(8);status.ForeColor = Color.Silver;
        status.RightToLeft = language == 6 ? RightToLeft.Yes : RightToLeft.No;right.Controls.Add(status);
    }
}
