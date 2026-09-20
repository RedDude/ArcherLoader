using System.Reflection;
using ArcherEditorMod.Hair;
using FortRise;
using Monocle;
using MonoMod.Utils;
using TowerFall;
using HairInfo = ArcherEditorMod.Hair.HairInfo;

namespace ArcherEditorMod
{
    public class ArcherEditorCommands
    {
        public ArcherEditorCommands(IModuleContext context)
        {
            context.Registry.Commands.RegisterCommands("SetHat", new CommandConfiguration()
            {
                Callback = SetHat
            });
            
            context.Registry.Commands.RegisterCommands("SetTeam", new CommandConfiguration()
            {
                Callback = SetTeam
            });
            
            context.Registry.Commands.RegisterCommands("SetHairLinks", new CommandConfiguration()
            {
                Callback = SetHairLinks
            });
            
            context.Registry.Commands.RegisterCommands("SetHairPosition", new CommandConfiguration
            {
                Callback = SetHairPosition
            });
            
            context.Registry.Commands.RegisterCommands("LoseHat", new CommandConfiguration()
            {
                Callback = LoseHat
            });
            
            context.Registry.Commands.RegisterCommands("ReloadArcher", new CommandConfiguration()
            {
                Callback = ReloadArcher
            });

        }
        

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
        
        for (int i = 0; i < TFGame.PlayerInputs.Length; i++)
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
    
    public static void SetHairLinks(string[] args)
    {
        var index = int.Parse(args[0]);
        var state = int.Parse(args[1]);

        var hairInfo = GetHair(index);
        if(hairInfo == null) return;
        HairPatcher.LinksField.SetValue(hairInfo, state);
    }

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

        var exist = ArcherEditorMod.ArcherCustomDataDict.TryGetValue(player.ArcherData, out var archerCustomData);
        if (!exist) return null;
        HairPatcher.Hairs[player.PlayerIndex] = archerCustomData;
        return archerCustomData.HairInfo;
    }

    
    public static void ReloadArcher(string[] args)
    {
        var index = 0;
        if (args != null && args.Length > 0)
        {
            index = int.Parse(args[0]);
        }
        
        if(!TFGame.Players[index]) return;
        
        if (Engine.Instance.Scene is not Level level) return;
        var player = (Player) level.Players[index];

        var exist = ArcherEditorMod.ArcherCustomDataDict.TryGetValue(player.ArcherData, out var archerCustomData);
        if (!exist) return;

        ArcherEditorMod.LoadArcherContents();
        ArcherEditorMod.Start();

        
        // foreach (var customData in Mod.LoadContentAtPath(archerCustomData.FolderPath, ContentAccess.Content))
        // {
        //     
        // }
        //     
        // ArcherCustomManager.Initialize(archerCustomData.FolderPath, atlasArcher, atlasArcherMenu, spriteData, spriteDataMenu, archerName, FortEntrance.Settings.Validate);

        // var originalName = archerCustomData.originalName;
        // var original = Mod.AllArchersDataDict.Find(a => a.Name0 == originalName);
        //
        // archerCustomData.Parse(original, original.FolderPath);
        //
        // archerCustomData.Parse(archerCustomData.original, archerCustomData.ori);
        // return archerCustomData.HairInfo;
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