using System;

// Worker-thread owned: a preference is separate from the current match's saved speed.
internal sealed class SessionSpeedOverride
{
    int pid;long start;ulong player,resources,module;float original,applied;
    internal bool Active { get; private set; }
    internal void Update(GameContext c,bool enabled,float value)
    {
        long currentStart=c.Game.StartTime.ToUniversalTime().Ticks;
        if(Active&&(pid!=c.Game.Id||start!=currentStart||player!=c.Player||resources!=c.Resources||module!=c.ModuleBase))Forget();
        if(!enabled)
        {
            if(Active){Engine.SetGameSpeed(c,original);Active=false;}
            return;
        }
        if(!Active)
        {
            original=Engine.GetGameSpeed(c);pid=c.Game.Id;start=currentStart;player=c.Player;resources=c.Resources;module=c.ModuleBase;
            Active=true;applied=float.NaN;
        }
        if(applied!=value){Engine.SetGameSpeed(c,value);applied=value;}
    }
    internal void Forget(){Active=false;}
}
