using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    public sealed class PassiveInventory
    {
        private readonly PlayerController player;
        private readonly Dictionary<PassiveDefinition, int> owned = new Dictionary<PassiveDefinition, int>();
        public IReadOnlyDictionary<PassiveDefinition, int> Owned => owned;
        public float Damage, AttackSpeed, MoveSpeed, Dodge, Critical, CriticalDamage, Noise, Pressure;
        public float DecoyCooldown, DecoyRadius, DecoyDuration;
        public int PressureCap;
        private int repairKills;
        public PassiveInventory(PlayerController player) { this.player = player; }
        public int Count(PassiveDefinition item) => owned.TryGetValue(item, out int count) ? count : 0;
        public bool CanAdd(PassiveDefinition item) => Count(item) < item.Limit && player.MaxHealth + item.health >= 20;
        public bool Has(ItemSpecial special)
        {
            foreach (var item in owned.Keys) if (item.special == special) return true;
            return false;
        }
        public bool Add(PassiveDefinition item)
        {
            if (!CanAdd(item)) return false;
            owned[item] = Count(item) + 1;
            player.MaxHealth += item.health;
            player.Heal(Mathf.Max(0, item.health));
            Damage += item.damage; AttackSpeed += item.attackSpeed; MoveSpeed += item.moveSpeed;
            Dodge += item.dodge; Critical += item.critical; CriticalDamage += item.criticalDamage;
            Noise += item.noise; Pressure += item.pressure; PressureCap += item.pressureCap;
            DecoyCooldown += item.decoyCooldown; DecoyRadius += item.decoyRadius; DecoyDuration += item.decoyDuration;
            if (item.special == ItemSpecial.Repair) { repairKills = 0; player.Heal(12); }
            return true;
        }
        public float ConvertedDamage =>
            (Has(ItemSpecial.SpeedDamage) ? Mathf.Clamp((player.MoveScale - 1 + MoveSpeed) * .6f, 0, .24f) : 0) +
            (Has(ItemSpecial.HealthDamage) ? Mathf.Min(.30f, Mathf.Floor(player.MaxHealth / 20) * .03f) : 0);
        public float ConvertedAttackSpeed => Has(ItemSpecial.DodgeSpeed) ? Mathf.Clamp(Dodge * .8f, 0, .32f) : 0;
        public void OnKill()
        {
            if (Has(ItemSpecial.Repair) && ++repairKills % 6 == 0) player.Heal(8);
        }
    }
}
