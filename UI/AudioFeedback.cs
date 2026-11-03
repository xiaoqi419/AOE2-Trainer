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

internal static class AudioFeedback
{
    static readonly object Gate=new object();
    static System.Media.SoundPlayer successPlayer,disablePlayer;

    internal static void Success(){Play(true);}
    internal static void Disable(){Play(false);}

    static void Play(bool success)
    {
        try
        {
            lock(Gate)
            {
                if(success)
                {
                    if(successPlayer==null)successPlayer=new System.Media.SoundPlayer(new MemoryStream(CreateWave(true),false));
                    successPlayer.Play();
                }
                else
                {
                    if(disablePlayer==null)disablePlayer=new System.Media.SoundPlayer(new MemoryStream(CreateWave(false),false));
                    disablePlayer.Play();
                }
            }
        }
        catch { /* Audio must never prevent the trainer action from succeeding. */ }
    }

    internal static byte[] CreateWave(bool success)
    {
        const int rate=24000;
        double duration=success?0.34:0.23;
        int samples=(int)(rate*duration);
        using(var stream=new MemoryStream(44+samples*2))
        using(var writer=new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples*2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);
            writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);
            writer.Write((short)2);writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples*2);
            for(int i=0;i<samples;i++)
            {
                double t=(double)i/rate;
                double attack=Math.Min(1,t/0.006),release=Math.Min(1,(duration-t)/0.035);
                double phase=2*Math.PI*(460*t-180*t*t);
                double tone=success?
                    0.70*Math.Sin(2*Math.PI*1047*t)+0.22*Math.Sin(2*Math.PI*2094*t)+0.08*Math.Sin(2*Math.PI*3141*t):
                    0.85*Math.Sin(phase)+0.15*Math.Sin(2*phase);
                double envelope=attack*Math.Max(0,release)*Math.Exp(-t/(success?0.14:0.35));
                writer.Write((short)(tone*envelope*(success?0.14:0.12)*short.MaxValue));
            }
            return stream.ToArray();
        }
    }
}
