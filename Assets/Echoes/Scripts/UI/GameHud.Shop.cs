using UnityEngine;

namespace Echoes
{
    public sealed partial class GameHud
    {
        private int itemPage;
        private TMPro.TMP_Text ShopLabel(string text, float x, float y, float width, float height, int size, Color color)
        {
            return Label(modal, text, new Vector2(x, -y), new Vector2(width, height), size, color, new Vector2(.5f, 1));
        }
        private void ShopButton(string text, float x, float y, float width, bool enabled, UnityEngine.Events.UnityAction action,
            string name = "ShopButton", bool shortage = false)
        {
            var rect = Button(modal, text, new Vector2(x, -y), new Vector2(width, 44), action);
            rect.name = name;
            SkinButton(rect);
            rect.GetComponent<UnityEngine.UI.Button>().interactable = enabled;
            foreach (var label in rect.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                // Keep short actions on the native 2x grid. Long reasons use the 1x face.
                label.fontSize = 24;
                if (label.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity).x > width - 24)
                    label.fontSize = 12;
                label.color = enabled ? Color.white : new Color(.58f, .62f, .67f);
                if (shortage) rect.GetComponent<HudButton>().SetShortageLabel(label);
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
            var title = Label(modal, "商店", new Vector2(0, -40), new Vector2(400, 48), 36, PixelArt.Gold, new Vector2(.5f, 1));
            title.alignment = TMPro.TextAlignmentOptions.Center;
            ShopButton("退出 [Esc]", -570, 42, 220, true, () => run.Shop.Close(), "ShopExit");
            ArtPanel("ShopGoldIcon", modal, new Vector2(515, -50), new Vector2(32, 32), art.shopGoldCoin, new Vector2(.5f, 1), true);
            shopBalance = ShopLabel(run.Shop.GoldText, 600, 50, 120, 36, 25, PixelArt.Gold);
            DrawOffers();
            DrawInventory();
        }

        private void DrawOffers()
        {
            var art = PresentationArt.Current;
            for (int i = 0; i < run.Shop.Offers.Count; i++)
            {
                int index = i; var offer = run.Shop.Offers[i]; float x = (i - 1.5f) * 350;
                var card = ArtPanel("Offer" + i, modal, new Vector2(x, -105), new Vector2(334, 260),
                    art.offerPanels[offer.Tier], new Vector2(.5f, 1));
                bool weaponOffer = offer.Item == null;
                Label(card, run.Shop.Title(offer), new Vector2(86, -19), new Vector2(226, 40), 24, Paper);
                Label(card, ShopController.TierName(offer.Tier), new Vector2(86, -57), new Vector2(208, 28), 16, ShopController.TierColor(offer.Tier));
                var icon = weaponOffer ? art.Weapon(offer.Weapon, offer.Tier) : art.Passive(offer.Item.title);
                if (icon)
                    ArtPanel(weaponOffer ? "WeaponIcon" : "PassiveIcon", card, new Vector2(18, -20),
                        new Vector2(56, 56), icon, icon: true);
                string description;
                if (offer.Item != null) description = offer.Item.description + $"\n持有 {run.Player.Passives.Count(offer.Item)}/{offer.Item.Limit}";
                else
                {
                    var weapon = run.Config.weapons[offer.Weapon];
                    description = $"伤害 {weapon.damage[offer.Tier] * run.Player.TotalDamageScale:0.#} × {weapon.pellets} · 间隔 {Mathf.Max(.1f, weapon.interval[offer.Tier] / run.Player.AttackSpeed):0.00}秒\n射程 {weapon.range} · 声音 {weapon.noise * run.Player.NoiseRadius:0.#}";
                }
                Label(card, description, new Vector2(18, -90), new Vector2(298, 66), 17, Paper);
                string blocked = run.Shop.Blocked(offer);
                ShopButton(blocked ?? $"购买 · {run.Shop.Price(offer)} 金币", x, 264, 298, blocked == null,
                    () => BuyOffer(index, false), "BuyOffer" + i, blocked != null && blocked.StartsWith("还差"));
                if (weaponOffer && offer.Tier < 3)
                {
                    string mergeBlocked = run.Shop.Blocked(offer, true);
                    bool shortOnGold = mergeBlocked != null && mergeBlocked.StartsWith("还差");
                    ShopButton(mergeBlocked ?? $"买入合成 {ShopController.TierName(offer.Tier + 1)} · {run.Shop.Price(offer)}", x, 312, 298,
                        mergeBlocked == null, () => BuyOffer(index, true), "MergeOffer" + i, shortOnGold);
                }
            }
            var weights = run.Shop.QualityWeights;
            float total = weights[0] + weights[1] + weights[2] + weights[3];
            ShopLabel($"刷新概率   白 {weights[0] / total:P0} / 蓝 {weights[1] / total:P0} / 紫 {weights[2] / total:P0} / 红 {weights[3] / total:P0}",
                -172, 395, 970, 30, 16, muted);
            bool canRefresh = run.Shop.CanAfford(run.Shop.RefreshPrice);
            string refreshText = canRefresh ? $"刷新 {run.Shop.RefreshPrice} 金币" : $"刷新 · 还差 {run.Shop.RefreshPrice - run.Shop.Gold} 金币";
            ShopButton(refreshText, 525, 380, 298, canRefresh, RefreshShelf, "ShopRefresh", !canRefresh);
        }

        private void RefreshShelf()
        {
            if (!run.Shop.RefreshStock()) return;
            var button = modal.Find("ShopRefresh").GetComponent<HudButton>();
            button.Rebound();
        }

        private void BuyOffer(int index, bool merge)
        {
            if (!run.Shop.Buy(index, merge)) return;
            run.Noise.PlayPurchase();
            balanceFlashUntil = Time.unscaledTime + .22f;
            shopBalance.color = Color.white;
        }

        private void DrawInventory()
        {
            var art = PresentationArt.Current;
            ShopLabel($"已装备 {run.Player.Weapons.Slots.Count}/6", 0, 440, 1380, 32, 20, Brass);
            for (int i = 0; i < run.Player.Weapons.Slots.Count; i++)
            {
                int index = i; var slot = run.Player.Weapons.Slots[i]; var definition = run.Player.Weapons.Definition(slot);
                float x = (i - 2.5f) * 230;
                var frame = ArtPanel("WeaponSlot" + i, modal, new Vector2(x, -478), new Vector2(218, 100),
                    art.shopWeaponSlot, new Vector2(.5f, 1));
                ArtPanel("EquippedWeaponIcon", frame, new Vector2(22, -24), new Vector2(48, 48),
                    art.Weapon(slot.Kind, slot.Tier), new Vector2(0, 1), true);
                ShopLabel($"{definition.title} {ShopController.TierName(slot.Tier)}\n伤害 {definition.damage[slot.Tier] * run.Player.TotalDamageScale:0.#} × {definition.pellets}\n间隔 {run.Player.Weapons.Interval(slot):0.00}秒",
                    x + 32, 491, 126, 76, 14, ShopController.TierColor(slot.Tier));
                ShopButton($"出售 +{definition.prices[slot.Tier] / 2}", x, 582, 215, run.Player.Weapons.Slots.Count > 1,
                    () => run.Shop.Sell(index), "SellWeapon" + i);
                bool canMerge = slot.Tier < 3 && run.Player.Weapons.Match(slot.Kind, slot.Tier, index) >= 0;
                if (canMerge)
                    ShopButton($"两把合成 → {ShopController.TierName(slot.Tier + 1)}", x, 630, 215, true, () => run.Shop.MergeOwned(index), "MergeWeapon" + i);
            }
            var player = run.Player;
            ShopLabel($"公共伤害 +{(player.TotalDamageScale - 1) * 100:0}%   攻速 +{(player.AttackSpeed - 1) * 100:0}%   移速 {Mathf.Clamp(player.MoveScale + player.Passives.MoveSpeed,.6f,1.6f)*100:0}%   生命 {player.Health:0}/{player.MaxHealth:0}\n" +
                $"暴击 {player.CriticalChance*100:0}% × {player.CriticalMultiplier:0.0}   闪避 {Mathf.Clamp(player.Passives.Dodge,0,.5f)*100:0}%   枪声 {player.NoiseRadius:0.#}   每敌 +{player.PressureBonus*100:0}% / 上限 {player.PressureCap}",
                0, 689, 1380, 62, 17, Paper);
            var owned = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<PassiveDefinition, int>>(player.Passives.Owned);
            int pages = Mathf.Max(1, Mathf.CeilToInt(owned.Count / 4f)); itemPage = Mathf.Clamp(itemPage, 0, pages - 1);
            if (owned.Count == 0) ShopLabel("被动道具：尚未购买", -120, 750, 1140, 78, 15, muted);
            for (int i = itemPage * 4; i < Mathf.Min(owned.Count, itemPage * 4 + 4); i++)
            {
                var item = owned[i].Key;
                float x = -658 + (i % 2) * 582;
                float y = 748 + (i % 4 / 2) * 43;
                var icon = art.Passive(item.title);
                if (icon) ArtPanel("OwnedPassiveIcon", modal, new Vector2(x, -y), new Vector2(32, 32),
                    icon, new Vector2(.5f, 1), true);
                ShopLabel($"{item.title} ×{owned[i].Value}：{item.description}",
                    x + 288, y, 528, 44, 15, muted);
            }
            if (pages > 1) ShopButton($"道具 {itemPage + 1}/{pages} →", 595, 765, 195, true,
                () => { itemPage = (itemPage + 1) % pages; RefreshModal(); }, "ItemPage");
        }
    }
}
