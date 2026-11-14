using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echoes
{
    // Formal imported sprites. A missing asset is an integration error, never a silent placeholder.
    public static class GameArt
    {
        public static int Theme { get; private set; }
        private static readonly Dictionary<string, Sprite[]> sheets = new Dictionary<string, Sprite[]>();
        public static void SetTheme(int theme) => Theme = Mathf.Clamp(theme, 0, 2);
        public static Sprite Frame(string path, int index)
        {
            if (!sheets.TryGetValue(path, out var frames))
            {
                frames = Resources.LoadAll<Sprite>("Art/" + path);
                Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                if (frames.Length == 0) throw new InvalidOperationException("Missing game art: " + path);
                sheets[path] = frames;
            }
            return frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }
        public static string Floor => "Floor" + (Theme + 1) + "/";
        public static Sprite Get(string kind)
        {
            if (kind.StartsWith("room_")) return RoomArt.Sprite(kind);
            if (kind.StartsWith("weapon_") && int.TryParse(kind.Substring(7), out int weapon))
                return PresentationArt.Current.weapons[weapon];
            switch (kind)
            {
                case StartStall.Kind: return Frame(StartStall.ArtPath, 0);
                case "signpost":
                case "signArrow":
                case "spikePlate":
                case "raisedSpikes":
                case "floorBomb": return Frame("Common/" + kind, 0);
                case "player": return Frame("Common/hero", 0);
                case "enemy": return Frame(Floor + "walker", 0);
                case "walker":
                case "guard":
                case "elite":
                case "boss": return Frame(Floor + kind, 0);
                case "floor": return Frame(Floor + "tiles", 0);
                case "floorAlt": return Frame(Floor + "tiles", 1);
                case "floorDetail": return Frame(Floor + "tiles", 3);
                case "wall": return Frame(Floor + "tiles", 4);
                case "wallAlt": return Frame(Floor + "tiles", 5);
                case "gun": return Frame("Common/items", 0);
                case "gold": return PresentationArt.Current.goldPickup;
                case "bell": return Frame("Common/items", 1);
                case "bullet": return Frame("Common/items", 2);
                case "guardMuzzle": return Frame("Common/effects", 1);
                case "hostileBullet": return Frame("Common/items", 3);
                case "shadow": return Frame("Common/items", 4);
                case "sleepMarker": return Frame("Common/items", 5);
                case "alertMarker": return Frame("Common/items", 6);
                case "investigateMarker": return Frame("Common/items", 7);
                case "crate": return Frame(Floor + "props", 0);
                case "lamp": return Frame(Floor + "props", 1);
                case "doorpost": return Frame(Floor + "props", 2);
                case "station": return Frame(Floor + "props", 3);
                case "signature": return Frame(Floor + "props", 4);
                case "altar": return Frame(Floor + "props", 5);
                case "pillar": return Frame(Floor + "props", 6);
                case "rack": return Frame(Floor + "props", 7);
                case "pipe": return Frame(Floor + "props", 8);
                case "pipeBend": return Frame(Floor + "props", 9);
                case "bench": return Frame(Floor + "props", 10);
                case "fork": return Frame(Floor + "props", 11);
                case "crystal": return Frame(Floor + "props", 12);
                case "panel": return Frame(Floor + "props", 13);
                case "spares": return Frame(Floor + "props", 14);
                case "conduit": return Frame(Floor + "props", 15);
                default: return Frame("Common/solid", 0);
            }
        }
        public static int FootOrder(float y) => 20 - Mathf.RoundToInt(y * 2);
    }

    public sealed class ArtBinding : MonoBehaviour
    {
        public string Kind;
        public void Refresh() => GetComponent<SpriteRenderer>().sprite = GameArt.Get(Kind);
    }

    public sealed class CharacterArt : MonoBehaviour
    {
        public string Species { get; private set; }
        public int CurrentFrame { get; private set; }
        private SpriteRenderer renderer2D;
        private Rigidbody2D body;
        private PlayerController player;
        private EnemyController enemy;
        private float clock, pulse, hurt, deathClock;
        private bool wasDead;
        private SpriteRenderer footShadow;
        private GameConfig corpseConfig;
        public void Initialize(string species)
        {
            Species = species;
            renderer2D = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            player = GetComponent<PlayerController>();
            enemy = GetComponent<EnemyController>();
            footShadow = PixelArt.Create("FootShadow", "shadow", transform.position,
                -100, transform);
            footShadow.transform.localPosition = new Vector3(0, -.06f, 0);
            footShadow.transform.localScale = new Vector3(species == "boss" ? 2 : 1, .7f, 1);
            LateUpdate();
        }
        public void BeginCorpseFade(GameConfig config) => corpseConfig = config;
        public void Attack() => pulse = .2f;
        public void Hurt(float duration = .22f) => hurt = duration;
        private void LateUpdate()
        {
            if (!renderer2D) return;
            bool dead = player ? player.Health <= 0 : enemy && !enemy.Alive;
            // Import/CI frame spikes must not skip an entire short recoil or hurt animation.
            float dt = dead ? Time.unscaledDeltaTime : Mathf.Min(Time.deltaTime, .05f);
            clock += dt; pulse = Mathf.Max(0, pulse - dt); hurt = Mathf.Max(0, hurt - dt);
            bool moving = body && body.velocity.sqrMagnitude > .03f;
            if (dead)
            {
                if (!wasDead) { deathClock = 0; wasDead = true; }
                deathClock += dt;
                CurrentFrame = player ? 16 + Mathf.Min(3, (int)(deathClock * 10)) : deathClock < .09f ? 1 : 5;
            }
            else if (player)
            {
                int row = hurt > 0 ? 12 : pulse > 0 ? 8 : moving ? 4 : 0;
                CurrentFrame = row + (int)(clock * (moving || pulse > 0 ? 10 : 5)) % 4;
            }
            else
            {
                CurrentFrame = enemy.State == EnemyState.Sleeping ? 0 :
                    enemy.IsWindingUp || enemy.IsContactTelegraph || pulse > 0 ? 4 : moving ? 2 + (int)(clock * 8) % 2 : 1;
            }
            renderer2D.sprite = GameArt.Frame(player ? "Common/hero" : GameArt.Floor + Species, CurrentFrame);
            if (enemy) enemy.UpdateDamageShape(renderer2D.sprite, renderer2D.flipX);
            renderer2D.sortingOrder = GameArt.FootOrder(transform.position.y);
            var tint = hurt > 0 && (int)(hurt * 40) % 2 == 0 ? new Color(1, .6f, .6f) : Color.white;
            if (corpseConfig)
            {
                tint.a = 1f - Mathf.Clamp01((deathClock - corpseConfig.corpseHoldSeconds)
                    / Mathf.Max(.05f, corpseConfig.corpseFadeSeconds));
                footShadow.color = new Color(1, 1, 1, tint.a);
                if (tint.a <= 0) gameObject.SetActive(false);
            }
            renderer2D.color = tint;
        }
    }

    public sealed class ArtEffect : MonoBehaviour
    {
        private SpriteRenderer renderer2D;
        private float age;
        private int first;
        public static void Play(Vector2 position, bool impact, Color color)
        {
            var renderer = PixelArt.Create(impact ? "HitSpark" : "MuzzleSpark", "solid", position, 32);
            renderer.color = color;
            var fx = renderer.gameObject.AddComponent<ArtEffect>();
            fx.renderer2D = renderer; fx.first = impact ? 4 : 0;
            renderer.sprite = GameArt.Frame("Common/effects", fx.first);
        }
        private void Update()
        {
            age += Time.deltaTime;
            int frame = (int)(age * 24);
            if (frame >= 4) { Destroy(gameObject); return; }
            renderer2D.sprite = GameArt.Frame("Common/effects", first + frame);
        }
    }
}
