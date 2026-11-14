using UnityEngine;

namespace Echoes
{
    public sealed partial class GameHud
    {
        public void RefreshModal()
        {
            RestoreCombatHud();
            ResetScreenFeedback();
            if (run.State != RunState.Paused) settingsOpen = false;
            if (modalBackdrop)
            {
                modalBackdrop.gameObject.SetActive(false);
                Destroy(modalBackdrop.gameObject);
            }
            if (run.State == RunState.Playing) return;

            modalBackdrop = Panel("Modal", canvas, Vector2.zero, Vector2.zero, new Color32(7, 10, 12, 225));
            modalBackdrop.anchorMin = Vector2.zero;
            modalBackdrop.anchorMax = Vector2.one;
            modalBackdrop.offsetMin = modalBackdrop.offsetMax = Vector2.zero;
            modalBackdrop.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            if (run.State == RunState.Shop)
            {
                modal = modalBackdrop;
                DrawShop();
                return;
            }

            if (run.State == RunState.FloorCleared || run.State == RunState.Won || run.State == RunState.Lost)
            {
                DrawResult();
                return;
            }

            modal = Panel("FieldBook", modalBackdrop, Vector2.zero, new Vector2(860, 480), Iron, new Vector2(.5f, .5f));
            Panel("Spine", modal, Vector2.zero, new Vector2(4, 480), Brass);
            switch (run.State)
            {
                case RunState.Paused:
                    if (settingsOpen) DrawSettings();
                    else DrawPause();
                    break;
                case RunState.Upgrade:
                    DrawUpgrade();
                    break;
            }
        }

        private void Heading(string title, string context)
        {
            Label(modal, title, new Vector2(40, -28), new Vector2(490, 58), 36, Paper);
            var location = Label(modal, context, new Vector2(-40, -45), new Vector2(310, 35), 19, Brass, Vector2.one);
            location.alignment = TMPro.TextAlignmentOptions.TopRight;
            Panel("Rule", modal, new Vector2(40, -99), new Vector2(780, 2), new Color32(80, 78, 63, 255));
        }

        private string FloorName => $"第{run.Floor}层  {run.FloorTitle.Replace(" ", "")}";

        private void DrawPause()
        {
            Heading("暂停探索", FloorName);
            Label(modal, "操作", new Vector2(40, -124), new Vector2(290, 34), 23, Brass);
            Label(modal, "WASD     移动\n左键         射击并自动瞄准\nQ              向鼠标方向投诱饵\nE              商店、祭坛或下潜\nM             开关声音", new Vector2(40, -172), new Vector2(365, 204), 21, Paper);
            Label(modal, run.Map.Rooms[run.Map.CurrentRoom].Name, new Vector2(446, -124), new Vector2(365, 34), 23, Brass);
            Label(modal, $"枪声半径 {run.Player.NoiseRadius:0.#}    武器 {run.Player.Weapons.Slots.Count}/6\n青色细环内的敌人提供围攻加成\n伤害加成上限 +{run.Player.PressureCap * run.Player.PressureBonus * 100:0}%\n\n金环为枪声，青色声波为诱饵\n灰线沉睡，金点警戒，红线追击", new Vector2(446, -172), new Vector2(380, 200), 19, muted);
            Button(modal, "继续探索  [Esc]", new Vector2(-284, -385), new Vector2(212, 58), () => run.SetState(RunState.Playing));
            Button(modal, "设置", new Vector2(-94, -385), new Vector2(136, 58), () =>
            {
                settingsOpen = true;
                RefreshModal();
            }).name = "OpenSettings";
        }

        private void DrawSettings()
        {
            Heading("设置", FloorName);
            Label(modal, "手感调试模式", new Vector2(40, -150), new Vector2(490, 52), 24, Paper);
            Button(modal, run.FeelDebugMode ? "已开启" : "已关闭", new Vector2(300, -144), new Vector2(180, 54), () =>
            {
                run.SetFeelDebugMode(!run.FeelDebugMode);
                RefreshModal();
            }).name = "FeelDebugModeToggle";
            Label(modal, "本次探索可免费购买商品和刷新货架。\n关闭后恢复正常消费，已有金币和拾取收益保留。\n重新开始探索时自动关闭。", new Vector2(40, -224), new Vector2(780, 136), 22, muted);
            Button(modal, "返回暂停页", new Vector2(-284, -385), new Vector2(212, 58), () =>
            {
                settingsOpen = false;
                RefreshModal();
            }).name = "CloseSettings";
        }

        private void DrawUpgrade()
        {
            Heading("选择升级", $"等级 {run.Level} → {run.Level + 1}");
            for (int i = 0; i < run.Choices.Count; i++)
            {
                int index = i;
                var choice = run.Choices[i];
                var row = Button(modal, "", new Vector2(0, -124 - i * 110), new Vector2(780, 94), () => run.ChooseUpgrade(index));
                row.name = "UpgradeChoice" + i;
                var button = row.GetComponent<HudButton>();
                upgradeButtons[i] = button;
                var face = button.Face;
                Label(face, $"[{i + 1}]", new Vector2(16, -30), new Vector2(48, 32), 19, Brass);
                ArtPanel("UpgradeIcon", face, new Vector2(68, -15), new Vector2(64, 64), PresentationArt.Current.Upgrade(choice.Title), icon: true);
                Label(face, choice.Title, new Vector2(154, -29), new Vector2(208, 45), 24, choice.IncreasesNoise ? PixelArt.Gold : Paper);
                var detail = Label(face, choice.Description, new Vector2(374, -6), new Vector2(388, 82), 19, Paper);
                detail.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            }
        }
    }
}
