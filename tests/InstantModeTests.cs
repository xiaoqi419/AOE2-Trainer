using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class InstantModeTests
{
    static int failures;
    static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
    static object NewModes()
    {
        Type type=typeof(Engine).Assembly.GetType("ContinuousInstantModes");
        Assert(type!=null,"Continuous research/building mode is missing.");
        return Activator.CreateInstance(type,true);
    }
    static void Update(object modes,GameContext context,bool research,bool buildings)
    {
        try{modes.GetType().GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(modes,new object[]{context,research,buildings});}
        catch(TargetInvocationException e){throw e.InnerException;}
    }
    static bool Active(object modes){return (bool)modes.GetType().GetProperty("Active",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(modes,null);}
    static void ExpectFailure(Action action)
    {
        bool failed=false;try{action();}catch{failed=true;}Assert(failed,"Expected operation to reject invalid state.");
    }
    sealed class MemoryGame : IDisposable
    {
        internal readonly GameContext Context;
        internal readonly ulong AiResources;
        readonly System.Collections.Generic.List<IntPtr> blocks=new System.Collections.Generic.List<IntPtr>();
        internal MemoryGame()
        {
            Context=new GameContext{Game=Process.GetCurrentProcess(),Handle=Native.OpenProcess(Native.Q|Native.R|Native.W|Native.O,false,Process.GetCurrentProcess().Id),Player=Allocate(4096),Resources=Allocate(4096),ModuleBase=0x140000000UL,PlayerId=1};
            Assert(Context.Handle!=IntPtr.Zero,"Cannot open test process.");
            AiResources=Allocate(4096);
            Engine.SetResource(Context,86,0.75f);Engine.SetResource(Context,195,1.25f);
            Engine.Write(Context.Handle,AiResources+86*8,Raw(Context,86));
            Engine.Write(Context.Handle,AiResources+195*8,Raw(Context,195));
        }
        internal ulong Allocate(int bytes)
        {
            IntPtr p=Marshal.AllocHGlobal(bytes);blocks.Add(p);Marshal.Copy(new byte[bytes],0,p,bytes);return (ulong)p.ToInt64();
        }
        internal static byte[] Raw(GameContext c,int id){return Engine.Read(c.Handle,c.Resources+(ulong)id*8,8);}
        public void Dispose(){Context.Dispose();foreach(IntPtr p in blocks)Marshal.FreeHGlobal(p);}
    }
    static bool Equal(byte[] a,byte[] b){return BitConverter.ToUInt64(a,0)==BitConverter.ToUInt64(b,0);}
    static void DisabledDoesNotWrite()
    {
        using(var game=new MemoryGame())
        {
            object modes=NewModes();byte[] before=Engine.Read(game.Context.Handle,game.Context.Resources,2048);
            Update(modes,game.Context,false,false);
            Assert(!Active(modes),"Modes must default to off.");
            byte[] after=Engine.Read(game.Context.Handle,game.Context.Resources,2048);
            Assert(Convert.ToBase64String(before)==Convert.ToBase64String(after),"Disabled modes changed memory.");
        }
    }
    static void PersistentAndIndependentRestore()
    {
        using(var game=new MemoryGame())
        {
            object modes=NewModes();GameContext c=game.Context;
            byte[] research=MemoryGame.Raw(c,86),building=MemoryGame.Raw(c,195);
            for(int i=0;i<6;i++)
            {
                Update(modes,c,true,true);
                Assert(Math.Abs(Engine.Resource(c,86)-0.001f)<0.000001f,"Research did not remain enabled.");
                Assert(Engine.Resource(c,195)==1000f,"Construction did not remain enabled.");
                Engine.SetResource(c,86,0.5f);Engine.SetResource(c,195,2f);
            }
            Update(modes,c,false,true);
            Assert(Equal(MemoryGame.Raw(c,86),research),"Research did not restore its exact original bytes.");
            Assert(Engine.Resource(c,195)==1000f,"Disabling research also disabled construction.");
            Update(modes,c,false,false);
            Assert(Equal(MemoryGame.Raw(c,195),building),"Construction did not restore its original 1.25 multiplier.");
            Assert(!Active(modes),"Modes remained active after disable.");
            Assert(Equal(Engine.Read(c.Handle,game.AiResources+86*8,8),research),"AI research value changed.");
            Assert(Equal(Engine.Read(c.Handle,game.AiResources+195*8,8),building),"AI construction value changed.");
        }
    }
    static void CampaignZeroAndReenable()
    {
        using(var game=new MemoryGame())
        {
            object modes=NewModes();GameContext c=game.Context;Engine.SetResource(c,86,0f);
            Update(modes,c,true,false);Update(modes,c,false,false);
            Assert(Engine.Resource(c,86)==0f,"Campaign zero baseline was not restored.");
            Engine.SetResource(c,86,0.4f);Update(modes,c,true,false);Update(modes,c,false,false);
            Assert(Engine.Resource(c,86)==0.4f,"Re-enabling did not capture the new original value.");
        }
    }
    static void RejectInvalidBaseline()
    {
        using(var game=new MemoryGame())
        {
            object modes=NewModes();GameContext c=game.Context;Engine.SetResource(c,195,float.NaN);
            byte[] invalid=MemoryGame.Raw(c,195);
            ExpectFailure(delegate{Update(modes,c,false,true);});
            Assert(Equal(invalid,MemoryGame.Raw(c,195)),"Invalid construction baseline was overwritten.");
            Assert(!Active(modes),"Invalid enable left an active override.");
        }
    }
    static void SessionChangeNeverWritesOldAddresses()
    {
        using(var game=new MemoryGame())
        {
            object modes=NewModes();GameContext c=game.Context;Update(modes,c,true,true);
            byte[] oldResearch=MemoryGame.Raw(c,86),oldBuildings=MemoryGame.Raw(c,195);
            var next=new GameContext{Game=c.Game,Handle=c.Handle,ModuleBase=c.ModuleBase,Player=c.Player+16,Resources=game.AiResources,PlayerId=2};
            byte[] nextBefore=Engine.Read(c.Handle,next.Resources,2048);
            ExpectFailure(delegate{Update(modes,next,true,true);});
            Assert(!Active(modes),"Changing player/session did not turn modes off.");
            Assert(Equal(MemoryGame.Raw(c,86),oldResearch)&&Equal(MemoryGame.Raw(c,195),oldBuildings),"Stale player addresses were written.");
            Assert(Convert.ToBase64String(nextBefore)==Convert.ToBase64String(Engine.Read(c.Handle,next.Resources,2048)),"A new player was modified automatically.");
        }
    }
    static void FailedRestoreCanBeRetried()
    {
        using(var game=new MemoryGame())
        {
            object modes=NewModes();GameContext c=game.Context;byte[] before=MemoryGame.Raw(c,86);Update(modes,c,true,false);
            IntPtr handle=c.Handle;c.Handle=IntPtr.Zero;
            try{ExpectFailure(delegate{Update(modes,c,false,false);});Assert(Active(modes),"A failed restore discarded its saved original value.");}
            finally{c.Handle=handle;}
            Update(modes,c,false,false);Assert(Equal(before,MemoryGame.Raw(c,86)),"Restore retry lost the original value.");
        }
    }
    static void ResearchQueueChecksLiveOwner()
    {
        using(var game=new MemoryGame())
        {
            GameContext c=game.Context;ulong own=game.Allocate(4096),ai=game.Allocate(4096),queue=game.Allocate(64),node=game.Allocate(64),action=game.Allocate(256);
            Action<ulong,ulong> pointer=delegate(ulong address,ulong value){Marshal.WriteInt64(new IntPtr((long)address),(long)value);};
            pointer(own+0x18,c.Player);pointer(ai+0x18,c.Player+16);
            pointer(own+0x8A8,queue);pointer(ai+0x8A8,queue);pointer(queue+0x10,node);pointer(node,action);
            pointer(action,c.ModuleBase+Engine.ResearchActionVtableRva);Marshal.WriteInt32(new IntPtr((long)(action+8)),0x67);Marshal.WriteInt32(new IntPtr((long)(action+0x68)),1);
            int active;
            int rejected=Engine.FinishQueues(c,new System.Collections.Generic.List<ulong>{ai},0x67,0x70,1000f,out active);
            Assert(rejected==0&&active==0,"Research wrote a queue owned by another player.");
            int written=Engine.FinishQueues(c,new System.Collections.Generic.List<ulong>{own},0x67,0x70,1000f,out active);
            Assert(written==1&&active==1,"Research did not finish the current player's queue.");
        }
    }
    static FieldInfo Field(string name){return typeof(TrainerForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);}
    static void HotkeysToggleBothWays()
    {
        using(var form=new TrainerForm())
        {
            Field("closing").SetValue(form,true);
            MethodInfo route=typeof(TrainerForm).GetMethod("WndProc",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(int id in new[]{6,7})
            {
                string request=id==6?"researchRequested":"buildingsRequested";
                string control=id==6?"researchToggle":"buildingsToggle";
                for(int press=0;press<2;press++)
                {
                    Message message=Message.Create(IntPtr.Zero,0x0312,new IntPtr(id),IntPtr.Zero);
                    route.Invoke(form,new object[]{message});
                    bool expected=press==0;
                    Assert((bool)Field(request).GetValue(form)==expected,"Hotkey did not toggle the requested mode.");
                    Assert(((SpeedToggleSwitch)Field(control).GetValue(form)).IsOn==expected,"Hotkey and switch are out of sync.");
                }
            }
        }
    }
    static void ToggleUiAndLanguageRebuild()
    {
        Assert(Field("researchToggle")!=null&&Field("buildingsToggle")!=null,"Research and construction switch controls are missing.");
        using(var form=new TrainerForm())
        {
            var research=(SpeedToggleSwitch)Field("researchToggle").GetValue(form);
            var buildings=(SpeedToggleSwitch)Field("buildingsToggle").GetValue(form);
            Assert(!research.IsOn&&!buildings.IsOn,"Switches must start off.");
            Field("researchRequested").SetValue(form,true);Field("buildingsRequested").SetValue(form,true);
            Field("language").SetValue(form,0);
            typeof(TrainerForm).GetMethod("BuildUi",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
            Assert(!research.IsDisposed&&!buildings.IsDisposed&&research.IsOn&&buildings.IsOn,"Language rebuild lost the switch state.");
            // Render the real form off-screen without letting its timers attach to a game.
            Field("closing").SetValue(form,true);
            form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-2000,-2000);
            form.Show();
            ((System.Windows.Forms.Timer)Field("instantTimer").GetValue(form)).Stop();
            ((System.Windows.Forms.Timer)Field("detector").GetValue(form)).Stop();
            form.PerformLayout();
            using(var bitmap=new Bitmap(form.Width,form.Height))
            {
                form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
                bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"instant-mode-ui.png"));
            }
            form.Close();
        }
    }
    [STAThread] static int Main()
    {
        Application.EnableVisualStyles();
        Action[] tests={DisabledDoesNotWrite,PersistentAndIndependentRestore,CampaignZeroAndReenable,RejectInvalidBaseline,SessionChangeNeverWritesOldAddresses,FailedRestoreCanBeRetried,ResearchQueueChecksLiveOwner,HotkeysToggleBothWays,ToggleUiAndLanguageRebuild};
        foreach(Action test in tests)
        {
            try{test();Console.WriteLine("PASS "+test.Method.Name);}
            catch(Exception e){failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+e.Message);}
        }
        Console.WriteLine((tests.Length-failures)+"/"+tests.Length+" passed");return failures==0?0:1;
    }
}
