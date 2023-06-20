using MonoMod.ModInterop;
using System;

namespace ArcherLoaderMod 
{
    [ModImportName("com.fortrise.EightPlayerMod")]
    public class EightPlayerImport
    {
        public static Func<bool> IsEightPlayer;
        public static Func<bool> LaunchedEightPlayer;
    }
}