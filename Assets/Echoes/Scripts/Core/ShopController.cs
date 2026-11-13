using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public sealed class ShopController : MonoBehaviour
    {
        public sealed class Offer
        {
            public int Weapon = -1, Tier;
            public PassiveDefinition Item;
            public bool Sold;
        }
        private RunController run;
        private System.Random random;
        public IReadOnlyList<Offer> Offers => offers;
        private readonly List<Offer> offers = new List<Offer>();
        public int Gold { get; private set; }
        public int Refreshes { get; private set; }
        public Vector2 Position { get; private set; }
        public int RefreshPrice => run.Config.shopRefreshCost + Refreshes * run.Config.shopRefreshIncrease;
        public bool InRange => Vector2.Distance(run.Player.transform.position, Position) <= run.Config.shopInteractRadius &&
            run.Map.HasLineOfSight(run.Player.transform.position, Position);
        public void Initialize(RunController controller)
        {
            run = controller;
            random = new System.Random(run.Config.randomSeed + 137);
            BeginFloor(run.Map.Rooms[0].Center);
        }
        public void BeginFloor(Vector2 spawn)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            Position = run.Map.NearestFloor(spawn + Vector2.right * 2);
            var stand = PixelArt.Create("ShopPlaceholder", "solid", Position, 5, transform);
            stand.color = new Color(.25f, .14f, .07f);
            stand.transform.localScale = new Vector3(1.4f, 1, 1);
            var roof = PixelArt.Create("ShopAwning", "solid", Position + Vector2.up * .65f, 6, transform);
            roof.color = PixelArt.Gold; roof.transform.localScale = new Vector3(1.7f, .25f, 1);
            var sign = PixelArt.Create("ShopSign", "solid", Position + Vector2.up * .2f, 7, transform);
            sign.color = PixelArt.Gold; sign.transform.localScale = Vector3.one * .3f;
            Refreshes = 0;
            Roll();
        }
        public void Credit(int amount) { Gold += Mathf.Max(0, amount); }
        public bool Open()
        {
            if (run.State != RunState.Playing || !InRange) return false;
            run.SetState(RunState.Shop); return true;
        }
        public void Close() { if (run.State == RunState.Shop) run.SetState(RunState.Playing); }
        public int Price(Offer offer) => offer.Item != null ?
            (offer.Item.price > 0 ? offer.Item.price : run.Config.passivePrices[offer.Item.tier]) :
            run.Config.weapons[offer.Weapon].prices[offer.Tier];
        public string Title(Offer offer) => offer.Item != null ? offer.Item.title : run.Config.weapons[offer.Weapon].title;
        public string Blocked(Offer offer, bool merge = false)
        {
            if (offer.Sold) return "已售出";
            if (offer.Item != null && !run.Player.Passives.CanAdd(offer.Item)) return "已达持有上限或生命不足20";
            if (offer.Item == null)
            {
                if (merge && (offer.Tier >= 3 || run.Player.Weapons.Match(offer.Weapon, offer.Tier) < 0)) return "无可合成同款";
                if (!merge && run.Player.Weapons.Slots.Count >= WeaponInventory.Capacity) return "武器槽已满";
            }
            return Gold < Price(offer) ? $"还差 {Price(offer) - Gold} 金币" : null;
        }
        public bool Buy(int index, bool merge = false)
        {
            if (run.State != RunState.Shop || index < 0 || index >= offers.Count) return false;
            var offer = offers[index];
            if (Blocked(offer, merge) != null) return false;
            bool applied = offer.Item != null ? run.Player.Passives.Add(offer.Item) : merge ?
                run.Player.Weapons.Merge(run.Player.Weapons.Match(offer.Weapon, offer.Tier)) : run.Player.Weapons.Add(offer.Weapon, offer.Tier);
            if (!applied) return false;
            Gold -= Price(offer); offer.Sold = true;
            run.Hud.RefreshModal(); return true;
        }
        public bool RefreshStock()
        {
            if (run.State != RunState.Shop || Gold < RefreshPrice) return false;
            Gold -= RefreshPrice; Refreshes++; Roll(); run.Hud.RefreshModal(); return true;
        }
        public bool Sell(int slot)
        {
            if (run.State != RunState.Shop || slot < 0 || slot >= run.Player.Weapons.Slots.Count || run.Player.Weapons.Slots.Count == 1) return false;
            var weapon = run.Player.Weapons.Slots[slot];
            Credit(run.Player.Weapons.Definition(weapon).prices[weapon.Tier] / 2);
            run.Player.Weapons.Remove(slot); run.Hud.RefreshModal(); return true;
        }
        public bool MergeOwned(int slot)
        {
            if (run.State != RunState.Shop || slot < 0 || slot >= run.Player.Weapons.Slots.Count) return false;
            var weapon = run.Player.Weapons.Slots[slot];
            int other = run.Player.Weapons.Match(weapon.Kind, weapon.Tier, slot);
            if (other < 0 || !run.Player.Weapons.Merge(slot, other)) return false;
            run.Hud.RefreshModal(); return true;
        }
        private int RollTier()
        {
            var weights = run.Config.shopTierWeights;
            float total = 0; foreach (float weight in weights) total += Mathf.Max(0, weight);
            double value = random.NextDouble() * total;
            for (int i = 0; i < 4; i++) { value -= Mathf.Max(0, weights[i]); if (value < 0) return i; }
            return 0;
        }
        private void Roll()
        {
            offers.Clear();
            for (int i = 0; i < 2; i++) offers.Add(new Offer { Weapon = random.Next(run.Config.weapons.Length), Tier = RollTier() });
            var pool = new List<PassiveDefinition>();
            foreach (var item in run.Config.passives) if (run.Player.Passives.CanAdd(item)) pool.Add(item);
            for (int i = 0; i < 2 && pool.Count > 0; i++)
            {
                int tier = RollTier();
                var matching = pool.FindAll(p => p.tier == tier);
                if (matching.Count == 0) matching = pool;
                var item = matching[random.Next(matching.Count)];
                offers.Add(new Offer { Item = item, Tier = item.tier }); pool.Remove(item);
            }
        }
        public static string TierName(int tier) => new[] { "白Ⅰ", "蓝Ⅱ", "紫Ⅲ", "红Ⅳ" }[tier];
        public static Color TierColor(int tier) => new[] { Color.white, new Color(.4f,.7f,1), new Color(.8f,.5f,1), PixelArt.Red }[tier];
    }
}
