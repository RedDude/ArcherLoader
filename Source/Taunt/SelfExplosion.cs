using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;

namespace ArcherLoaderMod.Taunt
{
  public class SelfExplosion : Explosion
  {
    // Removed all of constants in here because they're unnecessary and is not used anyway.
    // Revert it back if it's causing an issue.
    private List<Counter> counters;
    private List<Sprite<int>> sprites;
    private Alarm alarm;
    private MoonGlassBlock inMoonglass;
    private bool super;
    public bool killOthers = false;
    public bool SelfProtection = false;

    /* NEW */
    public new int PlayerIndex;
    public new uint Kills;

    public static new bool Spawn(
      Level level,
      Vector2 at,
      int playerIndex,
      bool plusOneKill,
      bool selfProtection,
      bool killOthers = false)
    {
      if (level == null)
        return false;
      // Entity entity = level.CollideFirst(at, GameTags.Solid);
      // if ((bool) entity)
      // {
      //   if (!(entity is MoonGlassBlock))
      //     return false;
      //   (entity as MoonGlassBlock).InsideExplode();
      // }
      //
      // if ((bool) entity)
      //   level.Add<Explosion>(Cache.Create<Explosion>().InitInMoonglass(playerIndex, at, entity as MoonGlassBlock));
      // else
        level.Add<Explosion>(Cache.Create<SelfExplosion>()
          .Init(playerIndex, at, false, plusOneKill, selfProtection, killOthers));
      return true;
    }

    public static new Explosion SpawnGet(
      Level level,
      Vector2 at,
      int playerIndex,
      bool plusOneKill,
      bool selfProtection,
      bool dontKillOthers)
    {
      if (level == null)
        return null;
      var entity = level.CollideFirst(at, GameTags.Solid);
      if ((bool) entity)
      {
        if (entity is MoonGlassBlock)
          (entity as MoonGlassBlock).InsideExplode();
        return null;
      }

      Explosion explosion;
      // if ((bool) entity)
      //   level.Add<Explosion>(explosion =
      //     Cache.Create<Explosion>().InitInMoonglass(playerIndex, at, entity as MoonGlassBlock));
      // else
        level.Add<Explosion>(explosion =
          Cache.Create<SelfExplosion>().Init(playerIndex, at, false, plusOneKill, selfProtection, dontKillOthers));
      return explosion;
    }

    public static new bool SpawnSuper(Level level, Vector2 at, int playerIndex, bool plusOneKill)
    {
      if (level == null)
        return false;
      var entity = level.CollideFirst(at, GameTags.Solid);
      if ((bool) entity)
      {
        if (!(entity is MoonGlassBlock))
          return false;
        (entity as MoonGlassBlock).InsideExplode();
      }

      // if ((bool) entity)
        // level.Add<Explosion>(Cache.Create<Explosion>().InitInMoonglass(playerIndex, at, entity as MoonGlassBlock));
      // else
        level.Add<Explosion>(Cache.Create<SelfExplosion>().Init(playerIndex, at, true, plusOneKill, false, false));
      return true;
    }

    public SelfExplosion()
      : base()
    {
      Collider = null;
      alarm = Alarm.Create(Alarm.AlarmMode.Persist, new Action(this.RemoveSelf), 120);
      Add(alarm);
      counters = new List<Counter>();
      sprites = new List<Sprite<int>>();
    }

    private SelfExplosion Init(
      int playerIndex,
      Vector2 position,
      bool super,
      bool plusOneKill,
      bool selfProtection,
      bool dontKillOthers)
    {
      SelfProtection = selfProtection;
      killOthers = dontKillOthers; 
      Position = position;
      inMoonglass = null;
      this.super = super;
      if (super)
        Collider = new WrapHitbox(150f, 150f, -75f, -75f);
      else
        Collider = new WrapHitbox(90f, 90f, -45f, -45f);
      Depth = -40;
      ScreenWrap = true;
      PlayerIndex = playerIndex;
      Kills = plusOneKill ? 1U : 0U;
      alarm.Start();
      counters.Clear();
      sprites.Clear();
      return this;
    }

    private SelfExplosion InitInMoonglass(
      int playerIndex,
      Vector2 position,
      MoonGlassBlock inBlock)
    {
      Position = position;
      inMoonglass = inBlock;
      super = false;
      Depth = -40;
      ScreenWrap = true;
      PlayerIndex = playerIndex;
      Kills = 0U;
      alarm.Start();
      counters.Clear();
      sprites.Clear();
      return this;
    }

    public override void Added()
    {
      base.Added();
      var type = BombParticle.Type.Bomb;
      if (Lava.Suffix == "Green")
        type = BombParticle.Type.GreenBomb;
      if ((bool) inMoonglass)
      {
        var num1 = (int) (inMoonglass.Width / 10.0) - 1;
        var num2 = (int) (inMoonglass.Height / 10.0) - 1;
        for (var index1 = 0; index1 < num2; ++index1)
        {
          for (var index2 = 0; index2 < num1; ++index2)
          {
            var vector2 = new Vector2(index2 * 10, index1 * 10);
            var setTo = Math.Max(1, Math.Abs(index2 - 4) + Math.Abs(index1 - 4));
            var num3 = WrapMath.WrapDistanceSquared(Position, inMoonglass.Position + vector2);
            var sprite = (double) num3 > 100.0
              ? ((double) num3 > 2450.0
                ? GenSprite("ExplosionPush" + Lava.Suffix)
                : GenSprite("ExplosionKill" + Lava.Suffix))
              : GenSprite("ExplosionCore" + Lava.Suffix);
            sprite.Position = vector2 + Calc.Random.Range(Vector2.One * -1f, Vector2.One * 2f);
            sprite.Scale = Vector2.One * Calc.Random.Range(0.44f, 0.46f);
            sprite.Rotation = Calc.Random.NextFloat(1.570796f);
            sprites.Add(sprite);
            counters.Add(new Counter(setTo));
          }
        }

        Position = inMoonglass.Position + Vector2.One * 10f;
      }
      else if (super)
      {
        for (var index1 = 0; index1 < 15; ++index1)
        {
          for (var index2 = 0; index2 < 3; ++index2)
          {
            var counter = new Counter();
            var num1 = (index1 - 7) * 9.333f;
            var num2 = num1 * num1;
            var to = 1 + Math.Abs(index1 - 7);
            if (num2 <= 5625.0)
            {
              var vector2_1 = new Vector2(num1, (float) (9.33300018310547 * index2 - 9.33300018310547));
              if (!WrapMath.WrapLineHit(Level, GameTags.Solid, Position, Position + vector2_1))
              {
                var sprite = (double) num2 > 100.0
                  ? ((double) num2 > 3025.0
                    ? GenSprite("ExplosionPush" + Lava.Suffix)
                    : GenSprite("ExplosionKill" + Lava.Suffix))
                  : GenSprite("ExplosionCore" + Lava.Suffix);
                sprite.Position = vector2_1 + Calc.Random.Range(Vector2.One * -1f, Vector2.One * 2f);
                sprite.Scale = Vector2.One * Calc.Random.Range(0.44f, 0.46f);
                sprite.Rotation = Calc.Random.NextFloat(1.570796f);
                counter.Set(to);
                sprites.Add(sprite);
                counters.Add(counter);
              }

              var vector2_2 = new Vector2((float) (9.33300018310547 * index2 - 4.66650009155273), num1);
              if (!WrapMath.WrapLineHit(Level, GameTags.Solid, Position, Position + vector2_2))
              {
                var sprite = (double) num2 > 100.0
                  ? ((double) num2 > 3025.0
                    ? GenSprite("ExplosionPush" + Lava.Suffix)
                    : GenSprite("ExplosionKill" + Lava.Suffix))
                  : GenSprite("ExplosionCore" + Lava.Suffix);
                sprite.Position = vector2_2 + Calc.Random.Range(Vector2.One * -1f, Vector2.One * 2f);
                sprite.Scale = Vector2.One * Calc.Random.Range(0.44f, 0.46f);
                sprite.Rotation = Calc.Random.NextFloat(1.570796f);
                counter.Set(to);
                sprites.Add(sprite);
                counters.Add(counter);
              }
            }
          }
        }

        Level.ScreenShake(12);
        var num = Calc.Random.Next(360);
        for (var index = num; index < num + 360; index += 60)
          Level.Add<BombParticle>(new BombParticle(Position, index, type));
        CollideDo(GameTags.ExplosionCollider, new Action<Entity>(DoCollideSuper));
      }
      else
      {
        for (var index1 = 0; index1 < 9; ++index1)
        {
          for (var index2 = 0; index2 < 9; ++index2)
          {
            var counter = new Counter();
            var vector2 = new Vector2((index2 - 4) * 9.333f, (index1 - 4) * 9.333f);
            var to = Math.Max(1, Math.Abs(index2 - 4) + Math.Abs(index1 - 4));
            var num = WrapMath.WrapDistanceSquared(Position, Position + vector2);
            if (num <= 2025.0 &&
                !WrapMath.WrapLineHit(Level, GameTags.Solid, Position, Position + vector2))
            {
              var sprite = (double) num > 100.0
                ? ((double) num > 1225.0
                  ? GenSprite("ExplosionPush" + Lava.Suffix)
                  : GenSprite("ExplosionKill" + Lava.Suffix))
                : GenSprite("ExplosionCore" + Lava.Suffix);
              sprite.Position = vector2 + Calc.Random.Range(Vector2.One * -1f, Vector2.One * 2f);
              sprite.Scale = Vector2.One * Calc.Random.Range(0.44f, 0.46f);
              sprite.Rotation = Calc.Random.NextFloat(1.570796f);
              counter.Set(to);
              sprites.Add(sprite);
              counters.Add(counter);
            }
          }
        }

        Level.ScreenShake(12);
        var num1 = Calc.Random.Next(360);
        for (var index = num1; index < num1 + 360; index += 60)
          Level.Add<BombParticle>(new BombParticle(Position, index, type));
        CollideDo(GameTags.ExplosionCollider, new Action<Entity>(DoCollide));
      }

      Level.Add<LightFade>(Cache.Create<LightFade>().Init(this, null, 135));
      if (PlayerIndex == -1)
        return;
      Level.Session.MatchStats[PlayerIndex].BombMultiKill = Math.Max(Kills,
        Level.Session.MatchStats[PlayerIndex].BombMultiKill);
    }

    public override void Removed()
    {
      base.Removed();
      Cache.Store<SelfExplosion>(this);
    }

    private void DoCollide(Entity entity)
    {
      var entity1 = entity as LevelEntity;
      var zero = Vector2.Zero;
      if (!CanHurt(entity1, ref zero))
        return;
      var vec = WrapMath.Shortest(Position, zero);
      var normal = vec.SafeNormalize();
      entity1.OnExplodePush(this, normal);
      if (vec.LengthSquared() > 1225.0)
        return;

      if (entity is TowerFall.Player player)
      {
        if (player.PlayerIndex == PlayerIndex && !SelfProtection)
        {
          player.OnExplode(this, normal);
          return;
        }
      }
      if(killOthers)
        entity1.OnExplode(this, normal);
    }

    private void DoCollideSuper(Entity entity)
    {
      var entity1 = entity as LevelEntity;
      var zero = Vector2.Zero;
      if (!CanHurt(entity1, ref zero))
        return;
      var vec = WrapMath.Shortest(Position, zero);
      var normal = vec.SafeNormalize();
      entity1.OnExplodePush(this, normal);
      if (vec.LengthSquared() > 3025.0)
        return;
      if (entity is TowerFall.Player player)
      {
        if (player.PlayerIndex == PlayerIndex && !SelfProtection)
        {
          player.OnExplode(this, normal);
          return;
        }
      }
      if(killOthers)
        entity1.OnExplode(this, normal);
    }

    private bool CanHurt(Vector2 pos)
    {
      if (WrapMath.WrapLineHit(Level, GameTags.Solid, Position, pos))
        return false;
      if (!super)
        return WrapMath.WrapDistanceSquared(Position, pos) <= 2025.0;
      return (WrapMath.AbsDiffX(Position.X, pos.X) <= 15.0 ||
              WrapMath.AbsDiffY(Position.Y, pos.Y) <= 15.0) &&
             WrapMath.WrapDistanceSquared(Position, pos) <= 5625.0;
    }

    private bool CanHurt(LevelEntity entity, ref Vector2 at)
    {
      var flag = false;
      entity.Collidable = false;
      if (entity.ExplosionCheckOffsets != null)
      {
        foreach (var explosionCheckOffset in entity.ExplosionCheckOffsets)
        {
          if (CanHurt(entity.Position + explosionCheckOffset))
          {
            flag = true;
            at = entity.Position + explosionCheckOffset;
            break;
          }
        }
      }
      else if (CanHurt(entity.Position))
      {
        flag = true;
        at = entity.Position;
      }

      entity.Collidable = true;
      return flag;
    }

    public override void Update()
    {
      base.Update();
      for (var index = 0; index < sprites.Count; ++index)
      {
        if (sprites[index] != null && (bool) counters[index])
        {
          counters[index].Update();
          if (!(bool) counters[index])
            Add(sprites[index]);
        }
      }
    }

    private Sprite<int> GenSprite(string name)
    {
      var spriteInt = TFGame.SpriteData.GetSpriteInt(name);
      spriteInt.Play(0);
      spriteInt.OnAnimationComplete = new Action<Sprite<int>>(FinishAnim);
      return spriteInt;
    }

    private void FinishAnim(Sprite<int> sprite) => Remove(sprite);

    public override void DebugRender()
    {
      if (super)
      {
        Draw.HollowRect(X - 75f, Y - 15f, 150f, 30f, Color.Red);
        Draw.HollowRect(X - 15f, Y - 75f, 30f, 150f, Color.Red);
      }
      else
        base.DebugRender();
    }
  }
}
