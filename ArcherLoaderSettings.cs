using System.Reflection;
using ArcherLoaderMod.Hair;
using FortRise;
using Monocle;
using MonoMod.Utils;
using TowerFall;

namespace ArcherLoaderMod
{
    
    public class ArcherLoaderSettings : ModuleSettings 
    {
        [SettingsName("Taunt Always On")]
        public bool TauntAlwaysOn;

        [SettingsName("Quick Start")]
        public bool QuickStart;
        
        [SettingsName("P1 Quick index"), SettingsNumber(-1)]
        public int Player1CharacterIndex;
        
        [SettingsName("P2 Quick index"), SettingsNumber(-1)]
        public int Player2CharacterIndex = -1;
        
        [SettingsName("P3 Quick index"), SettingsNumber(-1)]
        public int Player3CharacterIndex;

        [SettingsName("Over-Taunt Combustion")]
        public bool TauntTooExplode;

        [SettingsName("Idle R Stick + Right Drops Hat (Or L key)")]
        public bool DropHat = true;
        
        [SettingsName("Aim + R Stick + Left selfkills (Or K key)")]
        public bool SelfKill = false;

        [SettingsName("Taunt Hide Arrows")]
        public bool HideArrowsWhileTaunt = true;
        
        [SettingsName("Disable Particles")]
        public bool DisableParticles = false;
        
        [SettingsName("Disable Hairs")]
        public bool DisableHairs = false;

        [SettingsName("Disable Layers")]
        public bool DisableLayers = false;
        
        [SettingsName("Disable Team Colors")]
        public bool DisableTeamColors = true;
        
        public bool DisableCustomGhosts = false;
        
        public bool DisableCustomWings = false;
        
        [SettingsName("Validate")]
        public bool Validate = true;

        // [SettingsNumber(0, 20, 2)]
        // public int OnStepping;

        // public Action FlightTest;
    }
//
public static class CommandList 
{
    [Command("SetHat")]
    public static void SetHat(string[] args)
    {
        var index = int.Parse(args[0]);
        var state = int.Parse(args[1]);

        if (Engine.Instance.Scene is not Level level) return;
        var player = ((Player) level.Players[index]);
        DynamicData.For(player).Set("HatState",  (Player.HatStates)state);
        UpdateHead(player);
        // DynamicData.For(player).Methods["InitHead"].Invoke(new object[0], new object[0]);
        // ((Player) level.Players[index]).HatState = (Player.HatStates)state;
    }

    private static void UpdateHead(Player player)
    {
        var initHead = typeof(Player).GetMethod("InitHead", BindingFlags.Instance | BindingFlags.NonPublic);
        initHead?.Invoke(player, new object[0] { });
    }

    [Command("SetTeam")]
    public static void SetTeam(string[] args)
    {
        var teamString = args[0];
        var team = Allegiance.Neutral;
        if (teamString.ToLower() == "blue" || teamString == "1")
        {
            team = Allegiance.Blue;
        }
        if (teamString.ToLower() == "red" || teamString == "2")
        {
            team = Allegiance.Red;
        }
        
        if (Engine.Instance.Scene is not Level level) return;
        
        for (int i = 0; i < 4; i++)
        {
            if (TFGame.Players[i] && TFGame.PlayerInputs[i] != null)
            {
                MainMenu.VersusMatchSettings.Teams[i] = team;
            }
        }
        
        var matchSettings = new MatchSettings(GameData.VersusTowers[0].GetLevelSystem(), Modes.LevelTest,
            MatchSettings.MatchLengths.Standard);
        (matchSettings.LevelSystem as VersusLevelSystem).StartOnLevel(1);
        var session = new Session(matchSettings)
        {
            TestHatState = Player.HatStates.NoHat,
            TestTeam = (Allegiance) team,
        };
        session.StartGame();
        
        // Session.TestTeam = Allegiance.Blue;
        // var InitHead = typeof(Player).GetMethod("InitHead", BindingFlags.Instance | BindingFlags.NonPublic);
        // InitHead.Invoke(player, new object[0]{});
        // DynamicData.For(player).Methods["InitHead"].Invoke(new object[0], new object[0]);
        // ((Player) level.Players[index]).HatState = (Player.HatStates)state;
    }
    
    [Command("SetHairLinks")]
    public static void SetHairLinks(string[] args)
    {
        var index = int.Parse(args[0]);
        var state = int.Parse(args[1]);

        var hairInfo = GetHair(index);
        if(hairInfo == null) return;
        HairPatcher.LinksField.SetValue(hairInfo, state);
    }

    [Command("SetHairPosition")]
    public static void SetHairPosition(string[] args)
    {
        var index = int.Parse(args[0]);
        var x = int.Parse(args[1]);
        var y = int.Parse(args[2]);

        var hairInfo = GetHair(index);
        if(hairInfo == null) return;
        hairInfo.Position.X = x;
        hairInfo.Position.Y = y;
    }
    
    [Command("LoseHat")]
    public static void LoseHat(string[] args)
    {
        var index = int.Parse(args[0]);
        LoseHat(index);
        if (Engine.Instance.Scene is not Level level) return;
        var player = ((Player) level.Players[index]);
        UpdateHead(player);
    }
    
    public static void LoseHat(int index)
    {
        if (Engine.Instance.Scene is not Level level) return;
        var player = ((Player) level.Players[index]);
        var loseHat = typeof(Player).GetMethod("LoseHat", BindingFlags.Instance | BindingFlags.NonPublic);
        loseHat.Invoke(player, new object[2]{null, null});
    }
    //
    // [Command("SetHairPosition")]
    // public static void SetHairPosition(string[] args)
    // {
    //     var index = int.Parse(args[0]);
    //     var x = int.Parse(args[1]);
    //     var y = int.Parse(args[2]);
    //
    //     var hairInfo = GetHair(index);
    //     if(hairInfo == null) return;
    //     hairInfo.position.X = x;
    //     hairInfo.position.Y = y;
    // }

    
    
    private static HairInfo GetHair(int index)
    {
        if (Engine.Instance.Scene is not Level level) return null;
        var player = (Player) level.Players[index];

        var exist = Mod.ArcherCustomDataDict.TryGetValue(player.ArcherData, out var archerCustomData);
        if (!exist) return null;
        HairPatcher.Hairs[player.PlayerIndex] = archerCustomData;
        return archerCustomData.HairInfo;
    }

    
    // TFGame.Players[index]
    // Engine.Instance.Commands.Log("Hello");
    
    // [Command("arrows")]
    // public static void AddArrow(string[] args) 
    // {
    //     if (Engine.Instance.Scene is Level)
    //     {
    //         int num = Commands.ParseInt(args, 0, 0);
    //         if (num < 0 || num >= Arrow.ARROW_TYPES + RiseCore.ArrowsID.Count)
    //         {
    //             Engine.Instance.Commands.Log("Invalid arrow type!");
    //             return;
    //         }
    //         ArrowTypes arrowTypes = (ArrowTypes)num;
    //         using (List<Entity>.Enumerator enumerator = (Engine.Instance.Scene as Level).Players.GetEnumerator())
    //         {
    //             while (enumerator.MoveNext())
    //             {
    //                 Entity entity = enumerator.Current;
    //                 ((Player)entity).Arrows.AddArrows(new ArrowTypes[]
    //                 {
    //                     arrowTypes,
    //                     arrowTypes
    //                 });
    //             }
    //             return;
    //         }
    //     }
    //     Engine.Instance.Commands.Log("Command can only be used during gameplay!");
    // }
}

}
