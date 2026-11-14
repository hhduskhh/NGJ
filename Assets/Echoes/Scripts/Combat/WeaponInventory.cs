using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public sealed class WeaponInventory
    {
        public sealed class Slot
        {
            public int Kind, Tier;
            public float Cooldown;
            public Transform Visual;
        }
        private readonly RunController run;
        private readonly List<Slot> slots = new List<Slot>();
        public IReadOnlyList<Slot> Slots => slots;
        public const int Capacity = 6;
        public WeaponInventory(RunController run) { this.run = run; Add(0, 0); slots[0].Cooldown = 0; }
        public WeaponDefinition Definition(Slot slot) => run.Config.weapons[slot.Kind];
        public float Interval(Slot slot) => Mathf.Max(.1f, Definition(slot).interval[slot.Tier] / run.Player.AttackSpeed);
        public int Match(int kind, int tier, int except = -1) => slots.FindIndex(s => s.Kind == kind && s.Tier == tier && slots.IndexOf(s) != except);
        public bool Add(int kind, int tier)
        {
            if (slots.Count >= Capacity) return false;
            var visual = PixelArt.Create("Weapon", "weapon_" + (kind * 4 + tier),
                run.Player.transform.position, 25, run.Player.transform).transform;
            slots.Add(new Slot { Kind = kind, Tier = tier, Visual = visual, Cooldown = run.Config.weapons[kind].interval[tier] });
            UpdateVisuals(run.Player.AimPoint);
            return true;
        }
        public bool MergeOwned(int index)
        {
            if (index < 0 || index >= slots.Count) return false;
            var slot = slots[index];
            int other = Match(slot.Kind, slot.Tier, index);
            if (slot.Tier >= 3 || other < 0) return false;
            float cooldown = Mathf.Max(slot.Cooldown, slots[other].Cooldown);
            Upgrade(slot, cooldown);
            Remove(other);
            return true;
        }
        public bool MergePurchase(int kind, int tier)
        {
            int index = Match(kind, tier);
            if (tier >= 3 || index < 0) return false;
            var slot = slots[index];
            Upgrade(slot, slot.Cooldown);
            return true;
        }
        private void Upgrade(Slot slot, float cooldown)
        {
            float fraction = cooldown / Interval(slot);
            slot.Tier++;
            string art = "weapon_" + (slot.Kind * 4 + slot.Tier);
            slot.Visual.GetComponent<ArtBinding>().Kind = art;
            slot.Visual.GetComponent<SpriteRenderer>().sprite = PixelArt.Get(art);
            slot.Cooldown = Mathf.Max(0, fraction) * Interval(slot);
        }
        public void Remove(int index)
        {
            Object.Destroy(slots[index].Visual.gameObject);
            slots.RemoveAt(index);
        }
        public void Tick(float dt, Vector2 manualPoint)
        {
            foreach (var slot in slots) slot.Cooldown = Mathf.Max(0, slot.Cooldown - dt);
            UpdateVisuals(manualPoint);
        }
        private Vector2 Position(int index)
        {
            Vector2 center = (Vector2)run.Player.transform.position + Vector2.up * .35f;
            if (slots.Count == 1) return center + run.Player.AimDirection * .55f;
            float angle = Mathf.PI * 2 * index / slots.Count;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.1f;
        }
        private Vector2 Direction(Slot slot, Vector2 position, Vector2 manualPoint, bool manual)
        {
            EnemyController target = manual ? null : run.Player.FindVisibleTarget(Definition(slot).range, position);
            Vector2 delta = (target ? (Vector2)target.transform.position : manualPoint) - position;
            return delta.sqrMagnitude > .001f ? delta.normalized : Vector2.right;
        }
        private void UpdateVisuals(Vector2 manualPoint)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i]; var position = Position(i);
                var direction = Direction(slot, position, manualPoint, false);
                var renderer = slot.Visual.GetComponent<SpriteRenderer>();
                float tip = (renderer.sprite.rect.width * .5f - 1) / renderer.sprite.pixelsPerUnit;
                // Align the visible barrel with the existing logical muzzle; ballistics do not move.
                slot.Visual.position = position + direction * (.35f - tip);
                slot.Visual.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                slot.Visual.localScale = Vector3.one;
                renderer.color = Color.white;
                renderer.flipY = direction.x < 0;
                bool behind = slots.Count > 1 && position.y > run.Player.transform.position.y + .55f;
                renderer.sortingOrder = GameArt.FootOrder(run.Player.transform.position.y) + (behind ? -1 : 1);
            }
        }
        public bool Fire(Vector2 manualPoint, bool manual)
        {
            int weaponMask = 0;
            run.RefreshPressure();
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.Cooldown > 0) continue;
                var definition = Definition(slot);
                Vector2 position = Position(i), direction = Direction(slot, position, manualPoint, manual);
                Vector2 muzzle = position + direction * .35f;
                if (!run.Map.HasClearInteractionPath(run.Player.transform.position, muzzle)) continue;
                slot.Cooldown = Interval(slot);
                float damage = definition.damage[slot.Tier] * run.Player.TotalDamageScale * run.DamageMultiplier;
                if (run.Random.NextDouble() < run.Player.CriticalChance) damage *= run.Player.CriticalMultiplier;
                var impactTargets = definition.pellets > 1 ? new HashSet<EnemyController>() : null;
                for (int pellet = 0; pellet < definition.pellets; pellet++)
                {
                    Vector2 shot = Quaternion.Euler(0, 0, (pellet - (definition.pellets - 1) / 2f) * definition.spread) * direction;
                    Projectile.Create(run, muzzle, shot, definition.speed, damage, false,
                        slot.Kind == 2 ? Mathf.Max(0, slot.Tier - 1) : 0, impactTargets);
                }
                run.Noise.Emit(run.Player.transform.position, run.Player.NoiseRadius * definition.noise, NoiseKind.Gunshot);
                ArtEffect.Play(muzzle, false, PixelArt.Gold);
                weaponMask |= 1 << slot.Kind;
            }
            if (weaponMask != 0) run.Noise.PlayShot(weaponMask);
            return weaponMask != 0;
        }
    }
}
