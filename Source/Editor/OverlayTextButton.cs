using System;
using Microsoft.Xna.Framework;
using Monocle;
using TowerFall;
using TowerFall.Editor;

namespace ArcherLoader.Editor;

public class OverlayTextButton : EditorUI
{
    public string text;

    private Action<OverlayTextButton> onClick;

    private Func<bool> activeCheck;

    private Wiggler rotateWiggler;

    private Wiggler scaleWiggler;
    private int width;
    private int height;
    
    public Color SelectedColor;
    public Color Color;

    public OverlayTextButton(Vector2 position, string text, Action<OverlayTextButton> onClick, Func<bool> activeCheck, int width = 120, int height = 30)
        : base(position, 120, 30, -60, -15)
    {
        this.text = text;
        this.onClick = onClick;
        this.activeCheck = activeCheck;
        rotateWiggler = Wiggler.Create(height / 2, 4f);
        Add(rotateWiggler);
        scaleWiggler = Wiggler.Create(height / 2, 4f);
        Add(scaleWiggler);
        Collidable = activeCheck();
        this.width = width;
        this.height = height;
        LayerIndex = 3;
        SelectedColor = EditorScene.SelectedColor;
        Color = Color.White;
    }

    public override void Update()
    {
        base.Update();
        Collidable = activeCheck();
    }

    public override void Render()
    {
        base.Render();
        Color color = (Hovered ? SelectedColor : Color);
        if (!Collidable)
        {
            color = Color.DarkGray * 0.1f;
        }
        else
        {
            Draw.HollowRect(base.X - 60f, base.Y - 15f, width, height, Color.White);
        }

        Draw.TextCentered(TFGame.Font, text, Position, color, Vector2.One * (3f + 0.5f * scaleWiggler.Value), MathF.PI / 30f * rotateWiggler.Value);
    }

    public override void OnMouseEnter()
    {
        base.OnMouseEnter();
        Sounds.ed_buttonMouse.Play();
        rotateWiggler.Start();
    }

    public override void OnMouseClick(Vector2 localPosition)
    {
        base.OnMouseClick(localPosition);
        Sounds.ed_buttonClick.Play();
        onClick(this);
        scaleWiggler.Start();
    }
}