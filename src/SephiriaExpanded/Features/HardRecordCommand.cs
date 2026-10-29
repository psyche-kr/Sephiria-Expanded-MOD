using System;
using System.Collections.Generic;

namespace SephiriaExpanded
{
    internal static class HardRecordCommand
    {
        internal const string Command = "//hard";
        private const int MaxPoints = 999;
        private const int AllDefaultPoints = 60;
        private const string AllWord = "all";
        private const string ResetWord = "reset";
        private const string Usage = "사용법: //hard 숫자(현재 무기) | //hard all [숫자](모든 무기, 기본 60) | //hard reset(현재 무기 초기화) | //hard reset all(모든 무기 초기화)";

        internal static bool Matches(string text)
        {
            return text.Equals(Command, StringComparison.OrdinalIgnoreCase) || text.StartsWith(Command + " ", StringComparison.OrdinalIgnoreCase);
        }

        internal static void Run(string text)
        {
            string[] args = text.Substring(Command.Length).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0)
            {
                ShowCurrent();
                DeveloperMode.ShowMessage(Usage);
                return;
            }

            bool reset = args[0].Equals(ResetWord, StringComparison.OrdinalIgnoreCase);
            bool all = args[args.Length - 1].Equals(AllWord, StringComparison.OrdinalIgnoreCase) || args[0].Equals(AllWord, StringComparison.OrdinalIgnoreCase);
            int points;
            if (reset) points = 0;
            else if (all) points = args.Length > 1 && int.TryParse(args[1], out points) ? points : AllDefaultPoints;
            else if (!int.TryParse(args[0], out points))
            {
                DeveloperMode.ShowMessage(Usage);
                return;
            }

            points = Math.Max(0, Math.Min(MaxPoints, points));
            if (all)
            {
                List<int> ids = new List<int>();
                foreach (WeaponEntity weapon in WeaponDatabase.GetAll())
                {
                    if (weapon != null) ids.Add(weapon.id);
                }

                if (JournalWeaponOutline.SetClearedPoints(ids, points)) DeveloperMode.ShowMessage("모든 무기(" + ids.Count + "개)의 하드모드 클리어 기록을 " + points + "(으)로 바꿨습니다.");
                return;
            }

            int current = CurrentWeaponId();
            if (current < 0)
            {
                DeveloperMode.ShowMessage("들고 있는 무기를 찾지 못했습니다.");
                return;
            }

            if (JournalWeaponOutline.SetClearedPoints(new[] { current }, points)) DeveloperMode.ShowMessage(WeaponName(current) + "의 하드모드 클리어 기록을 " + points + "(으)로 바꿨습니다.");
        }

        private static void ShowCurrent()
        {
            int current = CurrentWeaponId();
            if (current < 0) DeveloperMode.ShowMessage("들고 있는 무기를 찾지 못했습니다.");
            else DeveloperMode.ShowMessage(WeaponName(current) + "의 하드모드 클리어 기록: " + JournalWeaponOutline.ClearedPoints(current));
        }

        private static int CurrentWeaponId()
        {
            PlayerAvatar player = CombatManager.Instance != null ? CombatManager.Instance.CurrentPlayer : null;
            WeaponControllerSimple controller = player != null ? player.GetComponent<WeaponControllerSimple>() : null;
            return controller != null && controller.currentWeapon != null ? controller.currentWeapon.entityId : -1;
        }

        private static string WeaponName(int id)
        {
            WeaponEntity entity = WeaponDatabase.FindWeaponById(id);
            string name = entity != null ? entity.Name : null;
            return string.IsNullOrEmpty(name) ? "무기 " + id : name;
        }
    }
}
