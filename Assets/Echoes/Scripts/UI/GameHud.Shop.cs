using System.Text;
using UnityEngine;

namespace Echoes
{
    public sealed partial class GameHud
    {
        private int itemPage;
        private void ShopLabel(string text, float x, float y, float width, float height, int size, Color color)
        {
            Label(modal, text, new Vector2(x, -y), new Vector2(width, height), size, color, new Vector2(.5f, 1));
        }
        private void ShopButton(string text, float x, float y, float width, bool enabled, UnityEngine.Events.UnityAction action,
            string name = "ShopButton")
        {
            var rect = Button(modal, text, new Vector2(x, -y), new Vector2(width, 44), action);
            rect.name = name;
            SkinButton(rect);
            rect.GetComponent<UnityEngine.UI.Button>().interactable = enabled;
            foreach (var label in rect.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                label.fontSize = 16; label.enableAutoSizing = true;
                label.fontSizeMin = 12; label.fontSizeMax = 16;
                label.color = enabled ? Color.white : new Color(.58f, .62f, .67f);
            }
        }
        private void DrawShop()
        {
            var art = PresentationArt.Current;
            var background = ArtPanel("ShopBackground", modal, Vector2.zero, Vector2.zero, art.shopBackground);
            background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one;
            background.offsetMin = background.offsetMax = Vector2.zero;
            // Cover different aspect ratios uniformly instead of stretching the background artwork.
            var aspect = background.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = 1600f / 900;
            ArtPanel("ShopMainPanel", modal, new Vector2(0, -18), new Vector2(1440, 864), art.shopMainPanel, new Vector2(.5f, 1));
            var title = Label(modal, "商店", new Vector2(0, -40), new Vector2(400, 48), 34, PixelArt.Gold, new Vector2(.5f, 1));
            title.alignment = TMPro.TextAlignmentOptions.Center;
            ShopButton("退出 [Esc]", -570, 42, 220, true, () => run.Shop.Close(), "ShopExit");
            ArtPanel("ShopGoldIcon", modal, new Vector2(515, -50), new Vector2(32, 32), art.shopGoldCoin, new Vector2(.5f, 1), true);
            ShopLabel(run.Shop.Gold.ToString(), 600, 50, 120, 36, 25, PixelArt.Gold);
            var weights = run.Shop.QualityWeights;
            float total = weights[0] + weights[1] + weights[2] + weights[3];
            ShopLabel($"Lv.{run.Level} · 刷新品质：白 {weights[0] / total:P0} / 蓝 {weights[1] / total:P0} / 紫 {weights[2] / total:P0} / 红 {weights[3] / total:P0}",
                0, 91, 1380, 28, 18, muted);
            for (int i = 0; i < run.Shop.Offers.Count; i++)
            {
                int index = i; var offer = run.Shop.Offers[i]; float x = (i - 1.5f) * 350;
                var card = ArtPanel("Offer" + i, modal, new Vector2(x, -128), new Vector2(334, 262),
                    art.offerPanels[offer.Tier], new Vector2(.5f, 1));
                bool weaponOffer = offer.Item == null;
                ShopLabel($"{ShopController.TierName(offer.Tier)} · {run.Shop.Title(offer)}",
                    weaponOffer ? x - 36 : x, 140, weaponOffer ? 238 : 310, 40, 21, ShopController.TierColor(offer.Tier));
                if (weaponOffer)
                    ArtPanel("WeaponIcon", card, new Vector2(-16, -12), new Vector2(48, 48),
                        art.Weapon(offer.Weapon, offer.Tier), Vector2.one, true);
                string description;
                if (offer.Item != null) description = offer.Item.description + $"\n持有 {run.Player.Passives.Count(offer.Item)}/{offer.Item.Limit}";
                else
                {
                    var weapon = run.Config.weapons[offer.Weapon];
                    description = $"伤害 {weapon.damage[offer.Tier] * run.Player.TotalDamageScale:0.#} × {weapon.pellets} · 间隔 {Mathf.Max(.1f, weapon.interval[offer.Tier] / run.Player.AttackSpeed):0.00}秒\n射程 {weapon.range} · 声音 {weapon.noise * run.Player.NoiseRadius:0.#}";
                }
                ShopLabel(description, x, 185, 310, 86, 17, muted);
                string blocked = run.Shop.Blocked(offer);
                ShopButton(blocked ?? $"购买 · {run.Shop.Price(offer)} 金币", x, 282, 308, blocked == null,
                    () => run.Shop.Buy(index), "BuyOffer" + i);
                if (weaponOffer && offer.Tier < 3)
                    ShopButton($"购买并升至 {ShopController.TierName(offer.Tier + 1)} · {run.Shop.Price(offer)}", x, 330, 308,
                        run.Shop.Blocked(offer, true) == null, () => run.Shop.Buy(index, true), "MergeOffer" + i);
            }
            ShopButton($"刷新 {run.Shop.RefreshPrice} 金币", 525, 410, 308, run.Shop.Gold >= run.Shop.RefreshPrice,
                () => run.Shop.RefreshStock(), "ShopRefresh");
            ShopLabel($"已装备 {run.Player.Weapons.Slots.Count}/6", 0, 458, 1380, 32, 20, PixelArt.Cyan);
            for (int i = 0; i < run.Player.Weapons.Slots.Count; i++)
            {
                int index = i; var slot = run.Player.Weapons.Slots[i]; var definition = run.Player.Weapons.Definition(slot);
                float x = (i - 2.5f) * 230;
                var frame = ArtPanel("WeaponSlot" + i, modal, new Vector2(x, -496), new Vector2(218, 100),
                    art.shopWeaponSlot, new Vector2(.5f, 1));
                ArtPanel("EquippedWeaponIcon", frame, new Vector2(22, -24), new Vector2(48, 48),
                    art.Weapon(slot.Kind, slot.Tier), new Vector2(0, 1), true);
                ShopLabel($"{definition.title} {ShopController.TierName(slot.Tier)}\n伤害 {definition.damage[slot.Tier] * run.Player.TotalDamageScale:0.#} × {definition.pellets}\n间隔 {run.Player.Weapons.Interval(slot):0.00}秒",
                    x + 32, 512, 126, 76, 14, ShopController.TierColor(slot.Tier));
                ShopButton($"出售 +{definition.prices[slot.Tier] / 2}", x, 600, 215, run.Player.Weapons.Slots.Count > 1,
                    () => run.Shop.Sell(index), "SellWeapon" + i);
                bool canMerge = slot.Tier < 3 && run.Player.Weapons.Match(slot.Kind, slot.Tier, index) >= 0;
                if (canMerge)
                    ShopButton("两把合成 → 升一级", x, 648, 215, true, () => run.Shop.MergeOwned(index), "MergeWeapon" + i);
            }
            var player = run.Player;
            ShopLabel($"公共伤害 +{(player.TotalDamageScale - 1) * 100:0}%   攻速 +{(player.AttackSpeed - 1) * 100:0}%   移速 {Mathf.Clamp(player.MoveScale + player.Passives.MoveSpeed,.6f,1.6f)*100:0}%   生命 {player.Health:0}/{player.MaxHealth:0}\n" +
                $"暴击 {player.CriticalChance*100:0}% × {player.CriticalMultiplier:0.0}   闪避 {Mathf.Clamp(player.Passives.Dodge,0,.5f)*100:0}%   枪声 {player.NoiseRadius:0.#}   每敌 +{player.PressureBonus*100:0}% / 上限 {player.PressureCap}",
                0, 698, 1380, 48, 18, muted);
            var owned = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<PassiveDefinition, int>>(player.Passives.Owned);
            int pages = Mathf.Max(1, Mathf.CeilToInt(owned.Count / 4f)); itemPage = Mathf.Clamp(itemPage, 0, pages - 1);
            var text = new StringBuilder();
            for (int i = itemPage * 4; i < Mathf.Min(owned.Count, itemPage * 4 + 4); i++)
                text.AppendLine($"{owned[i].Key.title} ×{owned[i].Value}：{owned[i].Key.description}");
            ShopLabel(owned.Count == 0 ? "被动道具：尚未购买" : text.ToString(), -120, 750, 1140, 78, 15, muted);
            ShopButton($"道具 {itemPage + 1}/{pages} →", 595, 765, 195, pages > 1,
                () => { itemPage = (itemPage + 1) % pages; RefreshModal(); }, "ItemPage");
        }
    }
}
