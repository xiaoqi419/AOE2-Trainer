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

internal sealed class RoundedApplyButton : Control
{
    bool hovered,pressed;

    internal RoundedApplyButton()
    {
        Size=new Size(52,24);BackColor=Color.FromArgb(30,32,34);ForeColor=Color.FromArgb(40,40,40);
        Font=new Font("Microsoft YaHei UI",7.5f,FontStyle.Bold);Cursor=Cursors.Hand;TabStop=false;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        var rect=new Rectangle(0,0,Width-1,Height-1);
        using(var path=new GraphicsPath())
        {
            const int radius=5,diameter=radius*2;
            path.AddArc(rect.Left,rect.Top,diameter,diameter,180,90);
            path.AddArc(rect.Right-diameter,rect.Top,diameter,diameter,270,90);
            path.AddArc(rect.Right-diameter,rect.Bottom-diameter,diameter,diameter,0,90);
            path.AddArc(rect.Left,rect.Bottom-diameter,diameter,diameter,90,90);
            path.CloseFigure();
            Color fill=pressed?Color.FromArgb(173,173,173):(hovered?Color.White:Color.Gainsboro);
            using(var brush=new SolidBrush(fill))e.Graphics.FillPath(brush,path);
        }
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(0,0,Width,Height),ForeColor,
            TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
    }

    protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
    protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hovered=pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){pressed=true;Invalidate();}base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
}
