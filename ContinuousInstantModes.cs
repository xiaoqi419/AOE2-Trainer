using System;

// Only holds overrides for one player in one running game session.
internal sealed class ContinuousInstantModes
{
    int pid,playerId;
    long processStart;
    ulong player,resources,module;
    byte[] savedResearch,savedBuildings;

    internal bool Active { get { return savedResearch!=null||savedBuildings!=null; } }

    internal void Update(GameContext c,bool research,bool buildings)
    {
        if(!Active&&!research&&!buildings)return;
        long start=c.Game.StartTime.ToUniversalTime().Ticks;
        if(Active&&(pid!=c.Game.Id||processStart!=start||player!=c.Player||playerId!=c.PlayerId||resources!=c.Resources||module!=c.ModuleBase))
        {
            // Old allocations may already be freed or reused; never restore through stale addresses.
            Forget();
            throw new InvalidOperationException("对局已改变，持续研究和建造已关闭。");
        }
        if(!Active){pid=c.Game.Id;processStart=start;player=c.Player;playerId=c.PlayerId;resources=c.Resources;module=c.ModuleBase;}
        UpdateResource(c,86,research,0.001f,100f,ref savedResearch,"研发");
        UpdateResource(c,195,buildings,1000f,1000f,ref savedBuildings,"建造");
    }

    static void UpdateResource(GameContext c,int id,bool enabled,float value,float maximum,ref byte[] saved,string name)
    {
        ulong address=c.Resources+(ulong)id*8;
        if(enabled)
        {
            if(saved==null)
            {
                byte[] original=Engine.Read(c.Handle,address,8);
                float baseline=Engine.DecodeResource(BitConverter.ToUInt64(original,0));
                if(float.IsNaN(baseline)||float.IsInfinity(baseline)||baseline<0||baseline>maximum)
                    throw new InvalidOperationException(name+"速度原值未通过校验，未修改。");
                // Keep the exact bytes, including metadata, until restoration actually succeeds.
                saved=original;
            }
            Engine.SetResource(c,id,value);
        }
        else if(saved!=null)
        {
            Engine.Write(c.Handle,address,saved);
            saved=null;
        }
    }

    internal void Forget(){savedResearch=null;savedBuildings=null;}
}
