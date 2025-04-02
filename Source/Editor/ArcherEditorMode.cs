using FortRise;
using TowerFall;

public class ArcherEditorMode : CustomGameMode
{
    public override void Initialize()
    {
        // Load your mod's metadata here. Changing Name property is optional, however if you don't trust the loader, you can freely set it on your own.
        // Name = "AwesomeNess";
        // The Icon must point to your icon path. (MyMod is just an example, use your mod name instead).
        //   Icon = TFGame.Atlas["MyMod/gameModes/awesomeness"];
        //   NameColor = Color.Yellow; // NameColor is the text color that shown when the game started.
        TeamMode = false; // If it's team mode
    }

    public override void InitializeSounds()
    {
        // Due to how FortRise loads vanilla sound, this is separated unless if you have your own custom sfx in your module.
        // EarnedCoinSound = ???;
        // LoseCoinSound = ???;
    }

    public override RoundLogic CreateRoundLogic(Session session)
    {
        return new ArcherEditorModeRoundLogic(session);
    }

    public class ArcherEditorModeRoundLogic : RoundLogic // You can freely inherit the vanilla round logic as well, as long as it's valid versus mode.
    {
        public ArcherEditorModeRoundLogic(Session session) : base(session, false) // false if you don't want a time limit on your game mode
        {

        }
    }
}