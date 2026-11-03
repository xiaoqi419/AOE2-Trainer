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

internal sealed class SpeedToggleSwitch : Control
{
    bool isOn;
    internal bool IsOn { get { return isOn; } set { if(isOn!=value){isOn=value;Invalidate();} } }

    internal SpeedToggleSwitch()
    {
        Size=new Size(52,24);BackColor=Color.FromArgb(30,32,34);Cursor=Cursors.Hand;TabStop=false;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        Color track=isOn?Color.FromArgb(255,140,0):Color.FromArgb(17,18,19);
        int trackHeight=Height-2,thumbSize=Height-6;
        using(var brush=new SolidBrush(track))
        {
            e.Graphics.FillEllipse(brush,0,1,trackHeight,trackHeight);
            e.Graphics.FillRectangle(brush,trackHeight/2,1,Width-trackHeight,trackHeight);
            e.Graphics.FillEllipse(brush,Width-trackHeight,1,trackHeight,trackHeight);
        }
        using(var brush=new SolidBrush(Color.Gainsboro))
            e.Graphics.FillEllipse(brush,isOn?Width-Height+2:2,3,thumbSize,thumbSize);
    }
}
