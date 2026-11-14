using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Echoes
{
    public sealed partial class GameHud
    {
        private bool showAttributes;
        private int selectedSlot, passivePage;
        private const int PassivesPerPage = 12;

        private RectTransform DrawItemIcon(string name, Transform parent, Vector2 position, float side,
            Sprite sprite, int tier, int quantity = 1)
        {
            var tile = Panel(name, parent, position, Vector2.one * side, ShopController.TierColor(tier));
            Panel("Inset", tile, new Vector2(2, -2), tile.sizeDelta - Vector2.one * 4, SlotColor);
            if (sprite)
                ArtPanel("Icon", tile, Vector2.zero, Vector2.one * (side - 8), sprite, new Vector2(.5f, .5f), true);
            else
                Label(tile, "?", Vector2.zero, tile.sizeDelta, 24, Paper).alignment = TextAlignmentOptions.Center;
            if (quantity > 1)
            {
                var plate = Panel("Stack", tile, new Vector2(-2, 2), new Vector2(27, 18), Iron, Vector2.right);
                Label(plate, "×" + quantity, Vector2.zero, plate.sizeDelta, 12, Paper).alignment = TextAlignmentOptions.Center;
            }
            return tile;
        }

        private void DrawBackpack()
        {
            float width = showAttributes ? 808 : 1112;
            modal = Panel("Backpack", modalBackdrop, Vector2.zero, new Vector2(width, 774), TrayColor, new Vector2(.5f, .5f));
            Panel("Spine", modal, Vector2.zero, new Vector2(4, 774), Brass);
            Label(modal, "背包", new Vector2(40, -26), new Vector2(180, 54), 36, Paper);
            var close = Button(modal, "返回 [Esc]", new Vector2(width / 2 - 130, -24), new Vector2(184, 50), run.CloseBackpack);
            close.name = "CloseBackpack";
            SkinButton(close);
            DrawBackpackTab("装备", false, 40);
            DrawBackpackTab("属性", true, 204);
            Panel("Rule", modal, new Vector2(40, -136), new Vector2(width - 80, 2), muted);
            if (showAttributes) DrawAttributes();
            else DrawBagContents();
        }

        private void DrawBackpackTab(string title, bool attributes, float x)
        {
            bool active = showAttributes == attributes;
            var tab = Button(modal, title, new Vector2(x + 76 - modal.sizeDelta.x / 2, -84), new Vector2(152, 48), () =>
            {
                showAttributes = attributes;
                RefreshModal();
            });
            tab.name = attributes ? "AttributesTab" : "EquipmentTab";
            if (active) Panel("Selected", tab, new Vector2(0, -48), new Vector2(152, 4), Brass);
        }

        private void DrawBagContents()
        {
            var weapons = run.Player.Weapons;
            var items = new List<KeyValuePair<PassiveDefinition, int>>(run.Player.Passives.Owned);
            int pages = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)PassivesPerPage));
            passivePage = Mathf.Clamp(passivePage, 0, pages - 1);
            if (selectedSlot >= WeaponInventory.Capacity + items.Count ||
                (selectedSlot < WeaponInventory.Capacity && selectedSlot >= weapons.Slots.Count)) selectedSlot = 0;
            var art = PresentationArt.Current;
            Label(modal, $"武器  {weapons.Slots.Count}/{WeaponInventory.Capacity}", new Vector2(40, -168), new Vector2(580, 42), 24, Brass);
            for (int i = 0; i < WeaponInventory.Capacity; i++)
            {
                float x = 40 + i * 102;
                if (i >= weapons.Slots.Count)
                {
                    Panel("EmptyWeapon", modal, new Vector2(x + 9, -228), Vector2.one * 72, SlotColor);
                    continue;
                }
                var slot = weapons.Slots[i];
                DrawBagSlot(i, x, 218, weapons.Definition(slot).title, art.Weapon(slot.Kind, slot.Tier), slot.Tier, 1);
            }

            Label(modal, $"随身道具  {items.Count}", new Vector2(40, -382), new Vector2(440, 42), 24, Brass);
            if (items.Count == 0)
                Label(modal, "尚未获得道具", new Vector2(40, -454), new Vector2(540, 40), 24, muted);
            int start = passivePage * PassivesPerPage;
            for (int i = start; i < Mathf.Min(items.Count, start + PassivesPerPage); i++)
            {
                var item = items[i];
                int cell = i - start;
                DrawBagSlot(WeaponInventory.Capacity + i, 40 + cell % 6 * 102, 438 + cell / 6 * 132,
                    item.Key.title, art.Passive(item.Key.title), item.Key.tier, item.Value);
            }
            if (pages > 1)
                Button(modal, $"{passivePage + 1}/{pages}  →", new Vector2(26, -377), new Vector2(140, 44), () =>
                {
                    passivePage = (passivePage + 1) % pages;
                    selectedSlot = WeaponInventory.Capacity + passivePage * PassivesPerPage;
                    RefreshModal();
                });
            var detail = Panel("ItemDetails", modal, new Vector2(688, -168), new Vector2(384, 558), Iron);
            if (selectedSlot < weapons.Slots.Count)
            {
                var slot = weapons.Slots[selectedSlot];
                var def = weapons.Definition(slot);
                ItemDetails(detail, art.Weapon(slot.Kind, slot.Tier), def.title, slot.Tier, "已装备",
                    $"伤害    {def.damage[slot.Tier] * run.Player.TotalDamageScale:0.#} × {def.pellets}\n间隔    {weapons.Interval(slot):0.00} 秒\n弹速    {def.speed:0.#}\n枪声    {def.noise * run.Player.NoiseRadius:0.#}");
            }
            else if (selectedSlot >= WeaponInventory.Capacity)
            {
                var item = items[selectedSlot - WeaponInventory.Capacity];
                ItemDetails(detail, art.Passive(item.Key.title), item.Key.title, item.Key.tier,
                    $"持有  {item.Value}/{item.Key.Limit}", item.Key.description.Replace(" · ", "\n").Replace("；", "\n"));
            }
        }

        private void DrawBagSlot(int index, float x, float y, string title, Sprite icon, int tier, int quantity)
        {
            var button = Button(modal, "", new Vector2(x + 45 - modal.sizeDelta.x / 2, -y), new Vector2(90, 118), () =>
            {
                selectedSlot = index;
                RefreshModal();
            });
            button.name = "BagSlot" + index;
            var face = button.GetComponent<HudButton>().Face;
            face.GetComponent<UnityEngine.UI.Image>().color = SlotColor;
            DrawItemIcon("Item", face, new Vector2(9, -10), 72, icon, tier, quantity);
            Label(face, title, new Vector2(0, -88), new Vector2(90, 24), 12, Paper).alignment = TextAlignmentOptions.Center;
            if (index == selectedSlot) Panel("Selected", face, new Vector2(0, -115), new Vector2(90, 3), Brass);
        }

        private void ItemDetails(RectTransform parent, Sprite sprite, string title, int tier, string owned, string description)
        {
            DrawItemIcon("DetailIcon", parent, new Vector2(24, -26), 94, sprite, tier);
            Label(parent, title, new Vector2(24, -146), new Vector2(336, 54), 36, Paper);
            Label(parent, ShopController.TierName(tier) + "    " + owned, new Vector2(24, -210), new Vector2(336, 40), 24, ShopController.TierColor(tier));
            Panel("Rule", parent, new Vector2(24, -266), new Vector2(336, 2), SlotColor);
            Label(parent, description, new Vector2(24, -296), new Vector2(336, 234), 24, Paper);
        }

        private void DrawAttributes()
        {
            var p = run.Player;
            int row = 0;
            void Stat(string name, string value)
            {
                var line = Panel("Stat" + row, modal, new Vector2(40, -158 - row * 42), new Vector2(728, 40), row % 2 == 0 ? SlotColor : Iron);
                Label(line, name, new Vector2(18, 0), new Vector2(400, 40), 24, Paper).alignment = TextAlignmentOptions.MidlineLeft;
                Label(line, value, new Vector2(-18, 0), new Vector2(290, 40), 24, Brass, Vector2.one).alignment = TextAlignmentOptions.MidlineRight;
                row++;
            }
            Stat("生命", $"{Mathf.CeilToInt(p.Health)} / {p.MaxHealth:0}");
            Stat("公共伤害", (p.TotalDamageScale - 1).ToString("+0%;-0%;0%"));
            Stat("攻速加成", (p.AttackSpeed - 1).ToString("+0%;-0%;0%"));
            Stat("移动速度", Mathf.Clamp(p.MoveScale + p.Passives.MoveSpeed, .6f, 1.6f).ToString("0%"));
            Stat("暴击率", p.CriticalChance.ToString("0%"));
            Stat("暴击倍率", $"× {p.CriticalMultiplier:0.00}");
            Stat("闪避率", Mathf.Clamp(p.Passives.Dodge, 0, .5f).ToString("0%"));
            Stat("幸运 · 双倍掉落", p.DoubleLootChance.ToString("0%"));
            Stat("枪声半径", p.NoiseRadius.ToString("0.#"));
            Stat("围攻 · 每敌伤害", p.PressureBonus.ToString("+0%;-0%;0%"));
            Stat("围攻 · 敌人数上限", p.PressureCap.ToString());
            Stat("诱饵冷却", $"{p.DecoyCooldown:0.#} 秒");
            Stat("诱饵半径", (run.Config.decoyRadius + p.Passives.DecoyRadius).ToString("0.#"));
            Stat("诱饵持续", $"{run.Config.decoyDuration + p.Passives.DecoyDuration:0.#} 秒");
        }
    }
}
