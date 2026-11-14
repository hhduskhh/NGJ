using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public sealed partial class GameHud
    {
        private static readonly Color ResultShade = new Color32(5, 9, 13, 78);
        private static readonly Color TrayColor = new Color32(19, 24, 30, 250);
        private static readonly Color SlotColor = new Color32(38, 43, 49, 255);

        private readonly List<GameObject> hiddenHud = new List<GameObject>();

        private void RestoreCombatHud()
        {
            foreach (var element in hiddenHud)
                if (element) element.SetActive(true);
            hiddenHud.Clear();
        }

        private void DrawResult()
        {
            foreach (Transform child in canvas)
            {
                if (child == modalBackdrop || !child.gameObject.activeSelf) continue;
                hiddenHud.Add(child.gameObject);
                child.gameObject.SetActive(false);
            }
            bool descend = run.State == RunState.FloorCleared;
            bool won = run.State == RunState.Won;
            bool cleared = descend || won;
            var accent = cleared ? Brass : (Color)new Color32(231, 132, 113, 255);
            var size = descend ? new Vector2(890, 112) : new Vector2(1200, 240);
            modal = Panel("RunResult", modalBackdrop, new Vector2(0, 44), size, TrayColor, new Vector2(.5f, 0));
            modalBackdrop.GetComponent<UnityEngine.UI.Image>().color = ResultShade;
            Panel("Rim", modal, Vector2.zero, new Vector2(size.x, 3), accent);
            if (descend)
            {
                ArtPanel("FloorGuardian", modal, new Vector2(10, -10), new Vector2(96, 92), GameArt.Get("boss"), icon: true);
                Label(modal, FloorName + "  已净化", new Vector2(122, -13), new Vector2(470, 44), 30, Paper);
                Label(modal, $"下潜恢复 {run.Config.floorEntryHealFraction:P0} 生命，随身装备保留", new Vector2(122, -65), new Vector2(490, 32), 19, muted);
            }
            else
            {
                ArtPanel("Explorer", modal, new Vector2(12, -14), new Vector2(116, 116), GameArt.Get("player"), icon: true);
                Label(modal, won ? "遗迹已净化" : "探索失败", new Vector2(132, -14), new Vector2(290, 60), 42, accent);
                Label(modal, run.Map.Rooms[run.Map.CurrentRoom].Name, new Vector2(134, -76), new Vector2(250, 34), 21, Paper);
                DrawDepthTrail(cleared);
                Label(modal, $"等级 {run.Level}     击败 {run.Kills}     {(int)run.Elapsed / 60:00}:{(int)run.Elapsed % 60:00}", new Vector2(32, -190), new Vector2(354, 34), 21, Paper);
                DrawResultEquipment();
            }
            var next = Button(modal, descend ? "下潜  [E]" : "再来一局", descend ? new Vector2(302, -25) : new Vector2(468, -155),
                new Vector2(208, 64), ConfirmResult);
            SkinButton(next);
            if (cleared) BeginResultReveal();
        }

        private void DrawDepthTrail(bool cleared)
        {
            for (int i = 1; i <= FloorTheme.Count; i++)
            {
                bool reached = i <= run.Floor;
                var color = reached ? Brass : muted;
                if (!cleared && i == run.Floor) color = PixelArt.Red;
                var mark = Panel("Floor" + i, modal, new Vector2(34 + (i - 1) * 108, -127), new Vector2(98, 43), SlotColor);
                Panel("Progress", mark, Vector2.zero, new Vector2(98, 3), reached ? color : SlotColor);
                var name = Label(mark, FloorTheme.Title(i).Replace(" ", ""), Vector2.zero, mark.sizeDelta, 16, color);
                name.alignment = TMPro.TextAlignmentOptions.Center;
            }
        }

        private void DrawResultEquipment()
        {
            var art = PresentationArt.Current;
            var slots = run.Player.Weapons.Slots;
            Label(modal, $"武器  {slots.Count}/{WeaponInventory.Capacity}", new Vector2(400, -16), new Vector2(440, 30), 19, muted);
            for (int i = 0; i < WeaponInventory.Capacity; i++)
            {
                var tile = Panel("Weapon" + i, modal, new Vector2(400 + i * 89, -54), new Vector2(82, 76), SlotColor);
                if (i >= slots.Count) continue;
                var weapon = slots[i];
                ArtPanel("Icon", tile, new Vector2(9, -2), new Vector2(64, 64), art.Weapon(weapon.Kind, weapon.Tier), icon: true);
                var color = ShopController.TierColor(weapon.Tier);
                Panel("Tier", tile, new Vector2(6, -67), new Vector2(12 * (weapon.Tier + 1), 4), color);
                var name = Label(modal, run.Player.Weapons.Definition(weapon).title, new Vector2(397 + i * 89, -134), new Vector2(89, 24), 14, Paper);
                name.alignment = TMPro.TextAlignmentOptions.Top;
            }

            var owned = run.Player.Passives.Owned;
            Label(modal, owned.Count == 0 ? "被动道具  0" : "被动道具", new Vector2(400, -185), new Vector2(110, 30), 17, muted);
            int n = 0;
            foreach (var pair in owned)
            {
                int columns = Mathf.Min(12, Mathf.Max(1, owned.Count));
                float step = Mathf.Min(45, 425f / columns);
                float side = Mathf.Min(36, step - 2);
                var tile = Panel("Passive" + n, modal, new Vector2(500 + (n % columns) * step, -175 - (n / columns) * side),
                    new Vector2(side, side), SlotColor);
                ArtPanel("Icon", tile, Vector2.zero, tile.sizeDelta, art.Passive(pair.Key.title), icon: true);
                if (pair.Value > 1)
                {
                    var count = Label(tile, pair.Value.ToString(), Vector2.zero, new Vector2(23, 18), 14, Color.white, Vector2.right);
                    count.alignment = TMPro.TextAlignmentOptions.BottomRight;
                }
                n++;
            }
            ArtPanel("Coin", modal, new Vector2(1012, -46), new Vector2(36, 36), art.shopGoldCoin, icon: true);
            Label(modal, run.Shop.GoldText, new Vector2(1062, -44), new Vector2(126, 40), 27, Brass);
        }
    }
}
