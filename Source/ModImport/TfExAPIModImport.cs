using System;
using FortRise;
using MonoMod.ModInterop;

namespace ArcherLoaderMod.Source.ModImport
{
    [ModImportName("TF.EX.API")]
    public class TfExAPIModImport
    {
        public static Action<FortModule> MarkModuleAsSafe;

        static TfExAPIModImport()
        {
            typeof(TfExAPIModImport).ModInterop();
        }
    }
}
