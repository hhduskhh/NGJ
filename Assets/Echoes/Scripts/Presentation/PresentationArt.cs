using System;
using UnityEngine;

namespace Echoes
{
    // Serialized references keep art outside Resources while including it in the game.
    public sealed class PresentationArt : ScriptableObject
    {
        public Sprite shopBackground, shopMainPanel, shopWeaponSlot, shopGoldCoin, goldPickup;
        public Sprite[] offerPanels = new Sprite[4];
        public Sprite[] buttonStates = new Sprite[4]; // Normal, hover, pressed, disabled.
        public Sprite[] weapons = new Sprite[12]; // Kind * 4 + tier.
        [SerializeField] private Sprite[] passiveIcons;
        public Sprite quickBolt, lightStep, emergencyRepair, resonanceAmplifier, decoyTuning;
        public Sprite criticalDamage, luckyClover;
        private static PresentationArt current;
        public static PresentationArt Current
        {
            get
            {
                if (!current) current = Resources.Load<PresentationArt>("PresentationArt");
                if (!current) throw new InvalidOperationException("Missing PresentationArt resource.");
                return current;
            }
        }
        public Sprite Weapon(int kind, int tier) => weapons[kind * 4 + tier];
        public Sprite Passive(string title) => Array.Find(passiveIcons, icon => icon && icon.name == title);
        public Sprite Upgrade(string title)
        {
            switch (title)
            {
                case "精密弹头": return GameArt.Frame("Common/items", 2);
                case "快装枪机": return quickBolt;
                case "轻装行进": return lightStep;
                case "应急修复": return emergencyRepair;
                case "共振扩音器": return resonanceAmplifier;
                case "诱饵调校": return decoyTuning;
                case "致命打击": return criticalDamage;
                case "幸运四叶草": return luckyClover;
                default: return null;
            }
        }
    }
}
