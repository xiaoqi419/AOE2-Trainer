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

[assembly: AssemblyTitle("Age of Empires II Definitive Edition Plus 12 Trainer V3")]
[assembly: AssemblyDescription("Single-player trainer for AoE2DE 101.103.54800.0 with continuous research and construction toggles")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.42")]

internal static class Native
{
    internal const uint Q=0x400,R=0x10,W=0x20,O=0x08,T=0x02;
    internal const uint MEM_COMMIT=0x1000;
    internal const uint PAGE_GUARD=0x100,PAGE_NOACCESS=0x01;
    [StructLayout(LayoutKind.Sequential)] internal struct MBI { public IntPtr BaseAddress,AllocationBase; public uint AllocationProtect,Alignment1; public UIntPtr RegionSize; public uint State,Protect,Type,Alignment2; }
    [DllImport("kernel32.dll",SetLastError=true)] internal static extern IntPtr OpenProcess(uint a,bool i,int p);
    [DllImport("kernel32.dll",SetLastError=true)] internal static extern bool ReadProcessMemory(IntPtr h,IntPtr a,byte[]b,UIntPtr s,out UIntPtr n);
    [DllImport("kernel32.dll",SetLastError=true)] internal static extern bool WriteProcessMemory(IntPtr h,IntPtr a,byte[]b,UIntPtr s,out UIntPtr n);
    [DllImport("kernel32.dll")] internal static extern UIntPtr VirtualQueryEx(IntPtr h,IntPtr a,out MBI m,UIntPtr n);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(IntPtr h);
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
}

internal sealed class GameContext : IDisposable
{
    internal Process Game; internal IntPtr Handle; internal ulong ModuleBase,Player,Resources; internal int PlayerId;
    public void Dispose(){if(Handle!=IntPtr.Zero){Native.CloseHandle(Handle);Handle=IntPtr.Zero;}}
}

internal sealed class GameObjectInfo
{
    internal ulong Address,Owner,RawHealth; internal int PlayerId; internal bool Unit; internal float Health;
}

internal sealed class ObjectMemoryRegion
{
    internal ulong Base,Size;
}

internal static class Engine
{
    const ulong K1=0x6AE24D3352F04511UL,K2=0xC22D6B496D833977UL,K3=0x042AC42976FCCEF5UL,K4=0xA9834B738A7DD167UL;
    const ulong HX=0x0F2A55F7A61DD00EUL,HS=0x147B5E081EC087D9UL;
    internal const ulong UnitVtableRva=0x32B8270UL,BuildingVtableRva=0x32B5FC8UL,ResearchActionVtableRva=0x3228798UL;
    const ulong SpeedManagerOffset=0x8UL,SpeedSettingsOffset=0x228UL,SpeedValuesOffset=0xCUL;
    const ulong BuildingQueueOffset=0x8A8UL,QueueFirstNodeOffset=0x10UL,RecruitElapsedOffset=0x6CUL,ResearchIdOffset=0x68UL,ResearchElapsedOffset=0x70UL;
    const uint RecruitActionKind=0x66,ResearchActionKind=0x67;
    const int Chunk=4*1024*1024;
    static readonly object CacheLock=new object();
    static int cachedPid;
    static ulong cachedModule,cachedGlobal,cachedPlayer,cachedResources;
    static int cachedPlayerId;
    static List<GameObjectInfo> cachedObjects=new List<GameObjectInfo>();
    static List<ObjectMemoryRegion> cachedObjectRegions=new List<ObjectMemoryRegion>();
    static DateTime objectCacheTime=DateTime.MinValue;

    internal static GameContext Open(bool write)
    {
        Process[] ps=Process.GetProcessesByName("AoE2DE_s"); if(ps.Length!=1)throw new InvalidOperationException("请先启动一局游戏（需要且只能有一个 AoE2DE_s 进程）。");
        Process p=ps[0];if(p.MainModule.FileVersionInfo.FileVersion!="101.103.54800.0")throw new InvalidOperationException("仅支持游戏版本 101.103.54800.0。检测到："+p.MainModule.FileVersionInfo.FileVersion);
        uint access=Native.Q|Native.R|(write?Native.W|Native.O:0); IntPtr h=Native.OpenProcess(access,false,p.Id);
        if(h==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"无法打开游戏进程");
        try
        {
            lock(CacheLock)
            {
                if(cachedPid==p.Id&&cachedModule!=0&&cachedPlayer!=0&&cachedResources!=0)
                {
                    int id;
                    ulong cachedRoot,currentPlayer,currentResources;
                    if(TryU64(h,cachedGlobal,out cachedRoot)&&cachedRoot>=0x10000&&TryU64(h,cachedRoot+0x28,out currentPlayer)&&currentPlayer==cachedPlayer&&TryU64(h,currentPlayer+0x98,out currentResources)&&currentResources==cachedResources&&TryI32(h,cachedPlayer+0x648,out id)&&id==cachedPlayerId)
                        return new GameContext{Game=p,Handle=h,ModuleBase=cachedModule,Player=cachedPlayer,Resources=cachedResources,PlayerId=cachedPlayerId};
                }
            }
            ulong mb=(ulong)p.MainModule.BaseAddress.ToInt64(),global=0;
            lock(CacheLock)if(cachedPid==p.Id&&cachedModule==mb)global=cachedGlobal;
            if(global==0)
            {
                byte[] image=Read(h,mb,p.MainModule.ModuleMemorySize);int hit=FindRootAob(image);int mov=hit+2,disp=BitConverter.ToInt32(image,mov+3);global=(ulong)((long)mb+mov+7+disp);
                lock(CacheLock){cachedPid=p.Id;cachedModule=mb;cachedGlobal=global;cachedPlayer=0;cachedResources=0;cachedObjects.Clear();cachedObjectRegions.Clear();objectCacheTime=DateTime.MinValue;}
            }
            ulong root=U64(h,global);
            if(root<0x10000)throw new InvalidOperationException("当前不在有效的单人对局中。");
            ulong player=U64(h,root+0x28);
            if(player<0x10000)throw new InvalidOperationException("当前不在有效的单人对局中。");
            ulong resources=U64(h,player+0x98);int playerId=I32(h,player+0x648);
            if(resources<0x10000||playerId<1||playerId>8)throw new InvalidOperationException("当前不在有效的单人对局中。");
            lock(CacheLock){bool changed=cachedPlayer!=player;cachedPid=p.Id;cachedModule=mb;cachedGlobal=global;cachedPlayer=player;cachedResources=resources;cachedPlayerId=playerId;if(changed){cachedObjects.Clear();cachedObjectRegions.Clear();objectCacheTime=DateTime.MinValue;}}
            return new GameContext{Game=p,Handle=h,ModuleBase=mb,Player=player,Resources=resources,PlayerId=playerId};
        }
        catch{Native.CloseHandle(h);throw;}
    }

    static int FindRootAob(byte[] b)
    {
        int found=-1,count=0; for(int i=0;i<=b.Length-17;i++)if(b[i]==0xFF&&b[i+2]==0x48&&b[i+3]==0x89&&b[i+4]==0x35&&b[i+9]==0x48&&(b[i+10]&0xF0)==0x80&&b[i+14]==0&&b[i+15]==0&&b[i+16]==0xE8){found=i;count++;}
        if(count!=1)throw new InvalidOperationException("当前版本的动态根特征不唯一（命中 "+count+"）。"); return found;
    }

    internal static float Resource(GameContext c,int id){return DecodeResource(U64(c.Handle,c.Resources+(ulong)id*8));}
    internal static void SetResource(GameContext c,int id,float value){Write(c.Handle,c.Resources+(ulong)id*8,BitConverter.GetBytes(EncodeResource(value)));}
    internal static void SetResource(int id,float value){using(GameContext c=Open(true))SetResource(c,id,value);}
    internal static void SetFourResources(float value){using(GameContext c=Open(true)){SetResource(c,0,value);SetResource(c,1,value);SetResource(c,2,value);SetResource(c,3,value);}}

    internal static float GetGameSpeed(GameContext c)
    {
        ulong address=GameSpeedAddress(c);
        byte[] pair=Read(c.Handle,address,8);
        float selected=BitConverter.ToSingle(pair,0),current=BitConverter.ToSingle(pair,4);
        if(float.IsNaN(selected)||float.IsInfinity(selected)||selected<0.1f||selected>10f||Math.Abs(selected-current)>0.01f)
            throw new InvalidOperationException("游戏速度字段未通过校验。");
        return selected;
    }

    static ulong GameSpeedAddress(GameContext c)
    {
        ulong manager=U64(c.Handle,c.Player+SpeedManagerOffset);
        if(manager<0x10000)throw new InvalidOperationException("无法定位游戏速度管理器。");
        ulong settings=U64(c.Handle,manager+SpeedSettingsOffset);
        if(settings<0x10000)throw new InvalidOperationException("无法定位游戏速度字段。");
        return settings+SpeedValuesOffset;
    }

    internal static float SetGameSpeed(float value)
    {
        using(GameContext c=Open(true))return SetGameSpeed(c,value);
    }

    internal static float SetGameSpeed(GameContext c,float value)
    {
        if(float.IsNaN(value)||float.IsInfinity(value)||value<0.5f||value>10f)throw new ArgumentOutOfRangeException("value","游戏速度必须在 0.5–10 倍之间。");
            ulong address=GameSpeedAddress(c);
            byte[] before=Read(c.Handle,address,8);
            float selected=BitConverter.ToSingle(before,0),current=BitConverter.ToSingle(before,4);
            if(float.IsNaN(selected)||float.IsInfinity(selected)||selected<0.1f||selected>10f||Math.Abs(selected-current)>0.01f)
                throw new InvalidOperationException("游戏速度字段未通过校验，未写入。");
            byte[] pair=new byte[8];byte[] bits=BitConverter.GetBytes(value);
            Buffer.BlockCopy(bits,0,pair,0,4);Buffer.BlockCopy(bits,0,pair,4,4);
            Write(c.Handle,address,pair);
            byte[] after=Read(c.Handle,address,8);
            if(Math.Abs(BitConverter.ToSingle(after,0)-value)>0.001f||Math.Abs(BitConverter.ToSingle(after,4)-value)>0.001f)
            {
                Write(c.Handle,address,before);
                throw new InvalidOperationException("游戏速度写入后校验失败，已尝试恢复原值。");
            }
            return value;
    }

    internal static List<GameObjectInfo> ScanObjects(GameContext c)
    {
        ulong unit=c.ModuleBase+UnitVtableRva,building=c.ModuleBase+BuildingVtableRva; var result=new List<GameObjectInfo>();var objectRegions=new List<ObjectMemoryRegion>();ulong cursor=0x10000; int mbiSize=Marshal.SizeOf(typeof(Native.MBI));
        while(cursor<0x00007FFFFFFF0000UL)
        {
            Native.MBI m; if(Native.VirtualQueryEx(c.Handle,new IntPtr((long)cursor),out m,new UIntPtr((uint)mbiSize))==UIntPtr.Zero)break; ulong rb=(ulong)m.BaseAddress.ToInt64(),rs=m.RegionSize.ToUInt64(),next=rb+rs;if(next<=cursor)break;
            if(m.State==Native.MEM_COMMIT&&(m.Protect&(Native.PAGE_GUARD|Native.PAGE_NOACCESS))==0)
            {int before=result.Count;ScanRegion(c,rb,rs,unit,building,result);if(result.Count>before)objectRegions.Add(new ObjectMemoryRegion{Base=rb,Size=rs});}
            cursor=next;
        }
        lock(CacheLock)cachedObjectRegions=objectRegions;
        return result;
    }

    static void ScanRegion(GameContext c,ulong rb,ulong rs,ulong unit,ulong building,List<GameObjectInfo> result)
    {
        for(ulong done=0;done<rs;)
        {
            int want=(int)Math.Min((ulong)Chunk,rs-done);byte[] buf=new byte[want];UIntPtr got;
            if(Native.ReadProcessMemory(c.Handle,new IntPtr((long)(rb+done)),buf,new UIntPtr((uint)want),out got)&&got.ToUInt64()>=8)
            for(int i=0;i<=(int)got.ToUInt64()-8;i+=8)
            {
                ulong vt=BitConverter.ToUInt64(buf,i);if(vt!=unit&&vt!=building)continue;ulong obj=rb+done+(ulong)i,owner,raw;if(!TryU64(c.Handle,obj+0x18,out owner)||owner<0x10000||!TryU64(c.Handle,obj+0x130,out raw))continue;int pid;if(!TryI32(c.Handle,owner+0x648,out pid))continue;float hp=DecodeHealth(raw);if(float.IsNaN(hp)||float.IsInfinity(hp)||hp<0||hp>100000)continue;
                result.Add(new GameObjectInfo{Address=obj,Owner=owner,RawHealth=raw,PlayerId=pid,Unit=vt==unit,Health=hp});
            }
            done+=(ulong)want;
        }
    }

    static List<GameObjectInfo> ScanObjectsFast(GameContext c)
    {
        List<ObjectMemoryRegion> regions;lock(CacheLock)regions=new List<ObjectMemoryRegion>(cachedObjectRegions);if(regions.Count==0)return ScanObjects(c);
        ulong unit=c.ModuleBase+UnitVtableRva,building=c.ModuleBase+BuildingVtableRva;var result=new List<GameObjectInfo>();foreach(ObjectMemoryRegion r in regions)ScanRegion(c,r.Base,r.Size,unit,building,result);
        lock(CacheLock){cachedObjects=result;objectCacheTime=DateTime.UtcNow;}return result;
    }

    internal static int RefreshObjectCache()
    {
        using(GameContext c=Open(false))
        {
            List<GameObjectInfo> objects=ScanObjects(c);
            lock(CacheLock){if(c.Game.Id==cachedPid&&c.Player==cachedPlayer){cachedObjects=objects;objectCacheTime=DateTime.UtcNow;}}
            return objects.Count;
        }
    }

    static List<GameObjectInfo> Objects(GameContext c)
    {
        lock(CacheLock)if(c.Game.Id==cachedPid&&c.Player==cachedPlayer&&cachedObjects.Count>0&&DateTime.UtcNow-objectCacheTime<TimeSpan.FromSeconds(90))return new List<GameObjectInfo>(cachedObjects);
        List<GameObjectInfo> objects=ScanObjects(c);lock(CacheLock){cachedObjects=objects;objectCacheTime=DateTime.UtcNow;}return objects;
    }

    internal static string RestoreHealth()
    {
        using(GameContext c=Open(true)){int u=0,b=0;foreach(GameObjectInfo o in ScanObjectsFast(c)){if(o.Owner!=c.Player)continue;try{ulong raw=U64(c.Handle,o.Address+0x130);Write(c.Handle,o.Address+0x130,BitConverter.GetBytes(EncodeHealth(raw,9999f)));if(o.Unit)u++;else b++;}catch{}}return string.Format("已恢复：{0} 个单位、{1} 栋建筑",u,b);}
    }
    internal static string EmptyAiHealth()
    {
        using(GameContext c=Open(true)){int u=0,b=0;foreach(GameObjectInfo o in ScanObjectsFast(c)){if(o.PlayerId<1||o.PlayerId>8||o.Owner==c.Player||o.Health<=0)continue;try{ulong raw=U64(c.Handle,o.Address+0x130);Write(c.Handle,o.Address+0x130,BitConverter.GetBytes(EncodeHealth(raw,1f)));if(o.Unit)u++;else b++;}catch{}}return string.Format("AI 已设为 1 血：{0} 个单位、{1} 栋建筑",u,b);}
    }
    internal static int FinishQueues(GameContext c,List<ulong> buildings,uint expectedKind,ulong elapsedOffset,float completionElapsed,out int active)
    {
        int written=0;active=0;
        foreach(ulong building in buildings)
        {
            ulong queue,node,action,owner;
            if(!TryU64(c.Handle,building+0x18,out owner)||owner!=c.Player||
               !TryU64(c.Handle,building+BuildingQueueOffset,out queue)||queue<0x10000||
               !TryU64(c.Handle,queue+QueueFirstNodeOffset,out node)||node<0x10000||
               !TryU64(c.Handle,node,out action)||action<0x10000)continue;
            uint kind;
            if(!TryU32(c.Handle,action+8,out kind)||(kind&0xFFFF)!=expectedKind)continue;
            if(expectedKind==ResearchActionKind)
            {
                ulong vtable;uint techId;
                if(!TryU64(c.Handle,action,out vtable)||vtable!=c.ModuleBase+ResearchActionVtableRva||
                   !TryU32(c.Handle,action+ResearchIdOffset,out techId)||techId==0||techId>10000)continue;
            }
            active++;
            uint raw;ulong currentAction;
            if(!TryU64(c.Handle,building+0x18,out owner)||owner!=c.Player||
               !TryU32(c.Handle,action+elapsedOffset,out raw)||
               !TryU64(c.Handle,node,out currentAction)||currentAction!=action)continue;
            float elapsed=BitConverter.ToSingle(BitConverter.GetBytes(raw),0);
            if(float.IsNaN(elapsed)||float.IsInfinity(elapsed)||elapsed<0||elapsed>=1000f)continue;
            Write(c.Handle,action+elapsedOffset,BitConverter.GetBytes(completionElapsed));
            written++;
        }
        return written;
    }
    internal static List<ulong> PlayerBuildings(GameContext c){var x=new List<ulong>();foreach(GameObjectInfo o in ScanObjectsFast(c))if(o.Owner==c.Player&&!o.Unit)x.Add(o.Address);return x;}
    internal static string InstantRecruitOnce()
    {
        using(GameContext c=Open(true))
        {
            List<ulong> buildings=PlayerBuildings(c);int total=0,idle=0;DateTime end=DateTime.UtcNow.AddMinutes(2);
            while(DateTime.UtcNow<end&&idle<8){int active;total+=FinishQueues(c,buildings,RecruitActionKind,RecruitElapsedOffset,100000f,out active);if(active==0)idle++;else idle=0;Thread.Sleep(25);}
            return total==0?"当前没有生产队列":"当前生产队列已全部完成（处理 "+total+" 项）";
        }
    }
    internal static void FinishCurrentResearch(GameContext c)
    {
        int active;
        FinishQueues(c,PlayerBuildings(c),ResearchActionKind,ResearchElapsedOffset,1000f,out active);
    }

    internal static float DecodeResource(ulong raw){ulong v=((raw^K1)^K2)-K3;v^=K4;return BitConverter.ToSingle(BitConverter.GetBytes((uint)v),0);}
    static ulong EncodeResource(float v){ulong bits=BitConverter.ToUInt32(BitConverter.GetBytes(v),0);return (((bits^K4)+K3)^K2)^K1;}
    static float DecodeHealth(ulong raw){ulong p=unchecked((raw^HX)-HS);return BitConverter.ToSingle(BitConverter.GetBytes((uint)p),0);}
    static ulong EncodeHealth(ulong raw,float v){ulong p=unchecked((raw^HX)-HS),bits=BitConverter.ToUInt32(BitConverter.GetBytes(v),0);p=(p&0xFFFFFFFF00000000UL)|bits;return unchecked((p+HS)^HX);}
    internal static byte[] Read(IntPtr h,ulong a,int n){byte[]b=new byte[n];UIntPtr g;if(!Native.ReadProcessMemory(h,new IntPtr((long)a),b,new UIntPtr((uint)n),out g)||g.ToUInt64()!=(ulong)n)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"读取游戏内存失败");return b;}
    internal static void Write(IntPtr h,ulong a,byte[]b){UIntPtr n;if(!Native.WriteProcessMemory(h,new IntPtr((long)a),b,new UIntPtr((uint)b.Length),out n)||n.ToUInt64()!=(ulong)b.Length)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"写入游戏内存失败");}
    static ulong U64(IntPtr h,ulong a){return BitConverter.ToUInt64(Read(h,a,8),0);} static int I32(IntPtr h,ulong a){return BitConverter.ToInt32(Read(h,a,4),0);}
    static bool TryU64(IntPtr h,ulong a,out ulong v){v=0;byte[]b=new byte[8];UIntPtr n;if(!Native.ReadProcessMemory(h,new IntPtr((long)a),b,new UIntPtr(8),out n)||n.ToUInt64()!=8)return false;v=BitConverter.ToUInt64(b,0);return true;}
    static bool TryU32(IntPtr h,ulong a,out uint v){v=0;byte[]b=new byte[4];UIntPtr n;if(!Native.ReadProcessMemory(h,new IntPtr((long)a),b,new UIntPtr(4),out n)||n.ToUInt64()!=4)return false;v=BitConverter.ToUInt32(b,0);return true;}
    static bool TryI32(IntPtr h,ulong a,out int v){uint x;if(!TryU32(h,a,out x)){v=0;return false;}v=(int)x;return true;}
}

internal static class Entry
{
    [STAThread] static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new TrainerForm());}
}
