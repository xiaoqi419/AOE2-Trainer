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

internal sealed class WindowCaptionButton : Button
{
    readonly bool closesWindow;
    bool hovered;
    internal WindowCaptionButton(bool close)
    {
        closesWindow=close;Size=new Size(44,32);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;
        BackColor=Color.FromArgb(28,30,31);ForeColor=Color.Gainsboro;Cursor=Cursors.Hand;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
    }
    protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(hovered?(closesWindow?Color.FromArgb(196,43,28):Color.FromArgb(55,58,61)):BackColor);
        float x=Width/2f,y=Height/2f;
        using(var pen=new Pen(ForeColor,1.5f))
        {
            if(closesWindow){e.Graphics.DrawLine(pen,x-5,y-5,x+5,y+5);e.Graphics.DrawLine(pen,x+5,y-5,x-5,y+5);}
            else e.Graphics.DrawLine(pen,x-6,y,x+6,y);
        }
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,Width-6,Height-6));
    }
}
