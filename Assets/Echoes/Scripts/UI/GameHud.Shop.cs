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
        private void ShopButton(string text, float x, float y, float width, bool enabled, UnityEngine.Events.UnityAction action)
        {
            var button = Button(modal, text, new Vector2(x, -y), new Vector2(width, 44), action);
            button.GetComponent<UnityEngine.UI.Button>().interactable = enabled;
            foreach (var label in button.GetComponentsInChildren<TMPro.TMP_Text>()) { label.fontSize = 16; label.color = enabled ? Color.white : new Color(.35f,.4f,.46f); }
        }
        private void DrawShop()
        {
            ShopLabel($"补 给 商 店     /     金币 {run.Shop.Gold}", 0, 20, 1300, 45, 32, PixelArt.Gold);
            var weights = run.Shop.QualityWeights;
            float total = weights[0] + weights[1] + weights[2] + weights[3];
            ShopLabel($"已暂停 · Lv.{run.Level} · 下次刷新品质：白 {weights[0] / total:P0} / 蓝 {weights[1] / total:P0} / 紫 {weights[2] / total:P0} / 红 {weights[3] / total:P0} · 关闭保留库存", 0, 68, 1300, 30, 18, muted);
            for (int i = 0; i < run.Shop.Offers.Count; i++)
            {
                int index = i; var offer = run.Shop.Offers[i]; float x = (i - 1.5f) * 350;
                Panel("Offer", modal, new Vector2(x, -110), new Vector2(334, 250), panel, new Vector2(.5f, 1));
                ShopLabel($"{ShopController.TierName(offer.Tier)} · {run.Shop.Title(offer)}", x, 120, 310, 35, 22, ShopController.TierColor(offer.Tier));
                string description;
                if (offer.Item != null) description = offer.Item.description + $"\n持有 {run.Player.Passives.Count(offer.Item)}/{offer.Item.Limit}";
                else
                {
                    var weapon = run.Config.weapons[offer.Weapon];
                    description = $"伤害 {weapon.damage[offer.Tier] * run.Player.TotalDamageScale:0.#} × {weapon.pellets} · 间隔 {Mathf.Max(.1f, weapon.interval[offer.Tier] / run.Player.AttackSpeed):0.00}秒\n射程 {weapon.range} · 声音 {weapon.noise * run.Player.NoiseRadius:0.#}\n购买装备 / 与已有同款合成";
                }
                ShopLabel(description, x, 161, 310, 106, 17, muted);
                string blocked = run.Shop.Blocked(offer);
                ShopButton(blocked ?? $"购买 · {run.Shop.Price(offer)} 金币", x, 270, 308, blocked == null, () => run.Shop.Buy(index));
                if (offer.Item == null && offer.Tier < 3)
                    ShopButton($"购买并升至 {ShopController.TierName(offer.Tier + 1)} · {run.Shop.Price(offer)}", x, 315, 308,
                        run.Shop.Blocked(offer, true) == null, () => run.Shop.Buy(index, true));
            }
            ShopButton($"刷新 {run.Shop.RefreshPrice} 金币", -210, 373, 320, run.Shop.Gold >= run.Shop.RefreshPrice, () => run.Shop.RefreshStock());
            ShopButton("继续探索 [Esc]", 210, 373, 320, true, () => run.Shop.Close());
            ShopLabel($"已装备 {run.Player.Weapons.Slots.Count}/6 · 合成免费 · 出售返还基价50% · 保留最后一把", 0, 431, 1380, 32, 20, PixelArt.Cyan);
            for (int i = 0; i < run.Player.Weapons.Slots.Count; i++)
            {
                int index = i; var slot = run.Player.Weapons.Slots[i]; var definition = run.Player.Weapons.Definition(slot);
                float x = (i - 2.5f) * 230;
                ShopLabel($"{definition.title} {ShopController.TierName(slot.Tier)}\n伤害 {definition.damage[slot.Tier] * run.Player.TotalDamageScale:0.#} × {definition.pellets}\n间隔 {run.Player.Weapons.Interval(slot):0.00}秒", x, 471, 218, 85, 17, ShopController.TierColor(slot.Tier));
                ShopButton($"出售 +{definition.prices[slot.Tier] / 2}", x, 562, 215, run.Player.Weapons.Slots.Count > 1, () => run.Shop.Sell(index));
                ShopButton("两把合成 → 升一级", x, 610, 215, slot.Tier < 3 && run.Player.Weapons.Match(slot.Kind, slot.Tier, index) >= 0, () => run.Shop.MergeOwned(index));
            }
            var player = run.Player;
            ShopLabel($"公共伤害 +{(player.TotalDamageScale - 1) * 100:0}%   攻速 +{(player.AttackSpeed - 1) * 100:0}%   移速 {Mathf.Clamp(player.MoveScale + player.Passives.MoveSpeed,.6f,1.6f)*100:0}%   生命 {player.Health:0}/{player.MaxHealth:0}\n" +
                $"暴击 {player.CriticalChance*100:0}% × {player.CriticalMultiplier:0.0}   闪避 {Mathf.Clamp(player.Passives.Dodge,0,.5f)*100:0}%   枪声 {player.NoiseRadius:0.#}   每敌 +{player.PressureBonus*100:0}% / 上限 {player.PressureCap}", 0, 668, 1380, 62, 18, muted);
            var owned = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<PassiveDefinition, int>>(player.Passives.Owned);
            int pages = Mathf.Max(1, Mathf.CeilToInt(owned.Count / 4f)); itemPage = Mathf.Clamp(itemPage, 0, pages - 1);
            var text = new StringBuilder();
            for (int i = itemPage * 4; i < Mathf.Min(owned.Count, itemPage * 4 + 4); i++)
                text.AppendLine($"{owned[i].Key.title} ×{owned[i].Value}：{owned[i].Key.description}");
            ShopLabel(owned.Count == 0 ? "被动道具：尚未购买" : text.ToString(), -120, 744, 1140, 130, 16, muted);
            ShopButton($"道具 {itemPage + 1}/{pages} →", 595, 755, 195, pages > 1, () => { itemPage = (itemPage + 1) % pages; RefreshModal(); });
        }
    }
}
