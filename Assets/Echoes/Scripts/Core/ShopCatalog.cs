using System;
using UnityEngine;

namespace Echoes
{
    [Serializable]
    public sealed class WeaponDefinition
    {
        public string title;
        public float range, speed, noise;
        public int pellets;
        public float spread;
        public int[] prices;
        public float[] damage, interval;
        public static WeaponDefinition[] Defaults() => new[]
        {
            new WeaponDefinition { title = "拾音手枪", range = 8, speed = 22, noise = 1, pellets = 1,
                prices = new[] {24,48,96,192}, damage = new[] {10f,18,32,58}, interval = new[] {.45f,.43f,.40f,.37f} },
            new WeaponDefinition { title = "轰鸣霰弹枪", range = 4.5f, speed = 18, noise = 1.5f, pellets = 5, spread = 8,
                prices = new[] {30,60,120,240}, damage = new[] {6f,10,18,32}, interval = new[] {.95f,.90f,.85f,.80f} },
            new WeaponDefinition { title = "静弦弩", range = 10, speed = 26, noise = .5f, pellets = 1,
                prices = new[] {30,60,120,240}, damage = new[] {24f,43,77,138}, interval = new[] {1.1f,1.05f,1f,.95f} }
        };
    }

    public enum ItemSpecial { None, SpeedDamage, DodgeSpeed, HealthDamage, LoneFighter, Repair }

    [Serializable]
    public sealed class PassiveDefinition
    {
        public string title, description;
        public int tier, price;
        public float damage, attackSpeed, moveSpeed, health, dodge, critical, criticalDamage, noise, pressure;
        public int pressureCap;
        public float decoyCooldown, decoyRadius, decoyDuration;
        public ItemSpecial special;
        public int Limit => tier < 2 ? 2 : 1;
        public static PassiveDefinition[] Defaults() => new[]
        {
            new PassiveDefinition { title="硬质弹头", description="伤害 +8% · 攻速 -4%", damage=.08f, attackSpeed=-.04f },
            new PassiveDefinition { title="轻型枪机", description="攻速 +12% · 伤害 -5%", attackSpeed=.12f, damage=-.05f },
            new PassiveDefinition { title="厚衬外套", description="生命上限 +16 · 移速 -4%", health=16, moveSpeed=-.04f },
            new PassiveDefinition { title="软底长靴", description="移速 +6% · 生命上限 -6", moveSpeed=.06f, health=-6 },
            new PassiveDefinition { title="消声套管", tier=1, description="枪声 -1 · 伤害 +10% · 攻速 -5%", noise=-1, damage=.1f, attackSpeed=-.05f },
            new PassiveDefinition { title="扩音枪口", tier=1, description="枪声 +1.5 · 伤害 +18% · 移速 -4%", noise=1.5f, damage=.18f, moveSpeed=-.04f },
            new PassiveDefinition { title="承压线圈", tier=1, description="每敌 +4点 · 上限 +2只 · 攻速 -6%", pressure=.04f, pressureCap=2, attackSpeed=-.06f },
            new PassiveDefinition { title="回声铜铃", tier=1, description="诱饵范围 +2 · 持续 +1秒 · 枪声 +0.5", decoyRadius=2, decoyDuration=1, noise=.5f },
            new PassiveDefinition { title="疾行发电机", tier=2, description="移速 +8% · 每1%永久移速转0.6%伤害（最多24%）· 生命 -12", moveSpeed=.08f, health=-12, special=ItemSpecial.SpeedDamage },
            new PassiveDefinition { title="应急修复器", tier=2, description="获得时回复12；此后每击杀6只普通敌人回复8", special=ItemSpecial.Repair },
            new PassiveDefinition { title="遗迹扩声阵列", tier=3, description="枪声 +2 · 伤害 +30% · 每敌 +4点 · 移速 -8%", noise=2, damage=.3f, pressure=.04f, moveSpeed=-.08f },
            new PassiveDefinition { title="寂静终焉", tier=3, description="枪声 -2 · 暴击 +20点 · 倍率 +100点 · 生命 -20", noise=-2, critical=.2f, criticalDamage=1, health=-20 }
        };
    }
}
