using System;
using Monocle;

namespace ArcherLoaderMod.Taunt
{
    public class TauntInfo
    {
        public int CharacterIndex;
        public SFX SFX;
        public SFXVaried SFXVaried;
        public SFXLooped SFXLooped;
        
        public Action OnCompleted;
        public string id;

        public Sprite<string> spriteData;
        public bool hasTaunt = false;
        public bool hasTauntCrown = false;
        public bool hasTauntNoHat = false;
        
        public string TauntTexture = null;
        public string NoHatTexture = null;
        public string CrownTexture = null;
        
        public string TauntTextureRed = null;
        public string NoHatTextureRed = null;
        public string CrownTextureRed = null;
      
        public string TauntTextureBlue = null;
        public string NoHatTextureBlue = null;
        public string CrownTextureBlue = null;
      
        public bool hasTauntNoHatBlue;
        public bool hasTauntBlue;
        public bool hasTauntCrownBlue;

        public bool hasTauntNoHatRed;
        public bool hasTauntCrownRed;
        public bool hasTauntRed;
        
        public bool SelfDestruction;
        public SFX Sound { get; set; }
    }
}