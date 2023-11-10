using Microsoft.Xna.Framework;
using Monocle;

namespace ArcherLoaderMod.Rainbow
{
    public class RainbowManager
    {
        public static Color CurrentColor;
        public static float Time;
        
        private static readonly Color[] PrismaticColors = new Color[6]
        {
            Color.Red,
            new(255, 120, 0),
            new(255, 217, 0),
            Color.Lime,
            Color.Cyan,
            Color.Violet
        };

        public static Color GetColor(int offset = 0, float speedMultiplier = 1f)
        {
            RainbowManager.Time += Engine.TimeMult;
            var time = RainbowManager.Time;
            if (time < 0) 
            {
                RainbowManager.Time = 0;
                return Color.White;
            }
            var interval = 1500f;
            var currentIndex = ((int) (time * speedMultiplier / interval) + offset) % PrismaticColors.Length;
            var nextIndex = (currentIndex + 1) % PrismaticColors.Length;
            var position = time * speedMultiplier / interval % 1f;
            var prismaticColor = default(Color);
            prismaticColor.R = (byte) (MathHelper.Lerp(PrismaticColors[currentIndex].R / 255f,
                PrismaticColors[nextIndex].R / 255f, position) * 255f);
            prismaticColor.G = (byte) (MathHelper.Lerp(PrismaticColors[currentIndex].G / 255f,
                PrismaticColors[nextIndex].G / 255f, position) * 255f);
            prismaticColor.B = (byte) (MathHelper.Lerp(PrismaticColors[currentIndex].B / 255f,
                PrismaticColors[nextIndex].B / 255f, position) * 255f);
            prismaticColor.A = (byte) (MathHelper.Lerp(PrismaticColors[currentIndex].A / 255f,
                PrismaticColors[nextIndex].A / 255f, position) * 255f);
            return prismaticColor;
        }
    }
}