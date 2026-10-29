using System.Collections.Generic;

namespace SephiriaExpanded
{
    internal static class OptionTooltips
    {
        private static readonly Dictionary<string, (string Title, string Description)> Entries =
            new Dictionary<string, (string, string)>
        {
            ["ChatConvenienceEnabled"] = ("채팅 편의성 기능", "시간, 채팅창 스크롤, /clear (채팅창 지우기), 아이템 등급 표시 등 편의 기능을 추가합니다."),
            ["ChatTimeMode"] = ("시간 표시 방식", "채팅 메시지 앞에 붙는 시간을 실제 시각 또는 플레이 시간 중에서 선택합니다."),
            ["ShopFavoriteEnabled"] = ("상점 즐겨찾기 표시", "즐겨찾기한 아이템 표시가 상점에서도 보이게 합니다."),
            ["EnchantMarkEnabled"] = ("인챈트 횟수 표시", "아이템의 인챈트 수치를 표시합니다."),
            ["MorePresetEnabled"] = ("프리셋 슬롯 확장", "프리셋 슬롯을 최대 100칸으로 변경합니다."),
            ["AlwaysRunEnabled"] = ("자동 달리기", "달릴 수 있는 환경이라면 항상 달리기 상태를 유지합니다."),
            ["HiddenRoomFinderEnabled"] = ("숨겨진 방 추적", "숨겨진 방의 위치를 화면에 표시해 줍니다."),
            ["LevelUpItemCount"] = ("남은 보상 횟수 표시", "선택지를 고를 때 남은 보상 획득 기회가 얼마나 있는지 표시합니다."),
            ["MinimapOpenEnabled"] = ("미니맵 밝히기", "미니맵을 밝혀줍니다."),
            ["MinimapOpenTeleportEnabled"] = ("미니맵 이동 허용", "미니맵을 클릭하여 먼 곳도 이동할 수 있습니다."),
            ["FountainArtifactUnlockEnabled"] = ("소원 분수에 모든 아이템 해금", "소원 분수에서 결속 아티팩트, 석판, 수상한 상인이 판매하는 아티팩트를 챙길 수 있습니다."),
            ["AnytimeOpenInventoryEnabled"] = ("전투 중 인벤토리 열기", "전투 중에도 인벤토리와 스탯창을 열 수 있게 합니다."),
            ["BossHpTextEnabled"] = ("보스 체력 수치 표시", "보스 체력바 위에 정확한 체력 수치를 텍스트로 표시합니다."),
            ["BossAggroEnabled"] = ("보스 어그로 표시", "보스의 공격 대상이 된 플레이어를 하얀색 테두리로 표시합니다."),
            ["RightClickHoldEnabled"] = ("특수공격, 마법서, 발동형 자동 사용", "키를 누르고 있어도 계속 유지됩니다."),
            ["JournalWeaponOutlineMode"] = ("뿌리거둠 무기 테두리 효과", "하드모드 15/30/50/60을 클리어 한 무기를 일지에 표시합니다."),
            ["WeaponShineEnabled"] = ("뿌리거둠 무기 반짝임 효과", "하드모드 60을 클리어 한 무기를 들고 있으면 무기에 반짝이는 빛 효과를 표시합니다. 모드를 사용하는 팀원의 무기에도 표시됩니다."),
            ["CostumeFrameWatcherEnabled"] = ("뿌리거둠 코스튬 애니메이션 효과", "하드모드 60을 클리어 한 코스튬의 배너를 꾸며줍니다."),

            ["MoreInventoryEnabled"] = ("레벨당 인벤토리 지급", "설정한 만큼 레벨이 상승할 때마다 인벤토리 슬롯을 지급합니다."),
            ["PlayerLevelCapEnabled"] = ("플레이어 레벨 최대치 확장", "기본 레벨 상한이 30레벨에서 60레벨로 확장됩니다.\n(멀티에서 사용하려면 게스트도 모드가 필요합니다.)"),
            ["InstantLeafExp"] = ("잎, 경험치 최적화", "잎과 경험치를 획득할 때 생기는 오브젝트를 없애고 즉시 획득합니다."),
            ["SellStartItem"] = ("시작 아이템 판매 가능", "게임 시작 시 지급되는 코스튬의 기본 아이템도 상점에서 판매할 수 있게 합니다."),

            ["TradeMoneyShareEnabled"] = ("행운의 메달 공유", "행운의 메달 효과를 파티원과 공유합니다."),
            ["PotionShareEnabled"] = ("포션 획득 공유", "포션을 획득하면 파티원 전원에게 지급합니다."),
            ["FriendlyFireEnabled"] = ("팀킬 허용", "아군에게 피해를 입히고, 아군에게 피해를 입을 수 있습니다."),
            ["FriendlyFireMultiplier"] = ("팀킬 데미지 배율", "팀킬로 입히는 피해의 배율을 0.1배 ~ 1배 사이로 조절합니다."),
            ["KillLogEnabled"] = ("킬 로그 표시", "플레이어가 사망하면 처치한 대상을 채팅창에 표시합니다.\n(모드가 없는 게스트는 채팅창에 킬로그가 보이지 않습니다.)"),
            ["FountainArtifactShare"] = ("소원 분수 아티팩트 타인이 줍기", "소원 분수로 챙겨온 아티팩트를 버렸을 때 다른 플레이어도 주울 수 있게 합니다."),
            ["Multiplayer16"] = ("최대 인원 확장","멀티플레이 최대 인원 제한을 16명까지 늘립니다."),
            ["MysteryPotGuaranteed"] = ("신비 항아리 확정 등장", "신비 항아리가 나올 수 있는 환경일 경우 랜덤이 아닌 100% 확률로 등장합니다."),
            ["InventorySell"] = ("상인 인벤토리 확정 판매", "소 상인이 인벤토리를 100% 확률로 판매합니다."),
            ["FirstArtifactLegendary"] = ("첫번째 아티팩트 전설 보장", "게임 중 처음으로 아이템을 획득하기 전까지 아티팩트 선택지가 항상 전설 등급 이상으로 나오도록 보장합니다. 리롤해도 유지되며, 아이템을 하나 획득하면 그 이후부터는 정상으로 돌아갑니다."),
                
            ["EnchantTableEnabled"] = ("인챈트 최소 3강 보장", "모든 인챈트 제단이 3회 인챈트 가능한 제단으로 고정됩니다."),
            ["FirstAnvilEnabled"] = ("첫 방 모루 보장", "1, 2층에서 첫번째 방에 모루를 반드시 보장하며, 강화 가능한 모든 무기의 리스트를 보여줍니다."),
            ["MoreMapEnabled"] = ("맵 확장", "확장이 가능한 방이라면 4~6칸 사이로 랜덤하게 확장합니다. 만약 6칸이 확장되면 보상 방은 추가로 1개 제공됩니다.\n(멀티에서 사용하려면 게스트도 모드가 필요합니다.)"),
            ["RandomEventMultiplier"] = ("랜덤 이벤트 등장 가중치", "헌혈, 비석, 수상한 상인, 아공간 상점 등 랜덤 이벤트가 발생할 확률의 배율을 조정합니다.\n(최대로 올려도 반드시 등장하지는 않습니다.)"),
            ["FountainCapacityEnabled"] = ("소원 분수 확장 사용", "소원 분수의 최대 용량을 늘립니다."),
            ["DifficultyMode"] = ("패턴 강화","모든 몬스터의 전체적인 배율이 향상되고 보스 몬스터의 패턴이 강화, 추가되어 기존보다 한층 어려워집니다.\n(모드가 없는 게스트도 이용 가능합니다.)\n(모드가 없는 게스트는 채팅창에 보스 대사가 나타나지 않습니다.)"),
            ["CursorOutlineEnabled"] = ("마우스 커서 가시성 향상", "마우스 커서에 두꺼운 테두리를 씌워 전투 이펙트 위에서도 커서가 잘 보이게 합니다. 테두리 색은 하위 옵션 '테두리 색'에서 고를 수 있습니다."),
            ["DifficultyBossBreak"] = ("보스 무력화 게이지 사용","패턴 강화가 적용된 보스에게 무력화 게이지가 생깁니다. 보스에게 피해를 입혀 게이지를 모두 깎으면 보스의 패턴이 취소되고 잠시 기절합니다.\n(모드가 없는 게스트에게도 적용되지만 게이지는 모드가 있어야 보입니다.)"),
            ["DifficultyRecommended"] = ("추천 배율 적용", "패턴 강화 난이도에 맞춰 몬스터 배율을 함께 올립니다. (노말 / 하드)\n일반 몬스터·미니보스 체력 x2 / x4, 일반 몬스터 생성 수 x2 / x2, 일반 몬스터·미니보스 공격 속도 x1.2 / x1.2\n보스 체력 x2 / x4, 보스 공격력 +15% / +25%\n배율 탭에서 직접 조절한 값과 곱하지 않고 둘 중 높은 값을 적용합니다."),
            ["DifficultyGlobalLevel"] = ("난이도", "모든 층과 보스에 일괄 적용되는 난이도 단계입니다. 난이도를 키면 노말은 +300%, 하드는 +500% 만큼 추가 사파이어를 획득합니다."),
            ["MonsterScalerMiniBossSpawnEnabled"] = ("미니보스 생성 배율 적용", "생성 배율만큼 미니보스도 늘릴 것인지 선택합니다."),
            ["MonsterScalerSpawnExpBalanceEnabled"] = ("경험치, 잎 획득량을 배율만큼 감소", "옵션을 키게 되면 생성 배율에 따라 증가한 몬스터가 잎과 경험치를 추가로 주지 않도록 보정합니다."),
            ["BloodFestivalEnabled"] = ("피의 축제 완화", "옵션을 키게 되면 체력 배율에 따라 증가한 몬스터의 체력이 피의 축제 효과로 인해 회복량도 같이 늘어나지 않도록 보정합니다."),

            ["CutsceneSkipEnabled"] = ("클리포트 연출 스킵", "5층 클리포트와 6층 클리포트 1페이즈의 연출을 건너뜁니다.\n(멀티에서 사용하려면 게스트도 모드가 필요합니다.)"),
            ["WhichIsThisModFromEnabled"] = ("모드 아이템 툴팁 추가", "아이템 설명 UI에 해당 아이템이 어느 모드에서 추가된 것인지 표시합니다."),
            ["FPSMeterEnabled"] = ("FPS 표시", "화면 좌측 상단에 현재 프레임레이트를 표시합니다."),
            ["UpdateHistory"] = ("업데이트 내역", "체크하면 모드의 업데이트 내역(릴리스) 페이지를 웹 브라우저로 엽니다."),
            ["DamageMeterEnabled"] = ("미터기 활성화", "개인과 파티의 피해량과 DPS를 측정해 화면에 표시합니다.\n(알트를 누른 채 창을 이동할 수 있습니다.)"),
            ["NameColorStart"] = ("닉네임 색상 변경", "내 닉네임의 색상을 변경합니다. (모드가 없는 게스트에게도 색이 보입니다.)\n그라데이션 색 1, 2를 설정하면 닉네임 색상 → 그라데이션 색 1 → 그라데이션 색 2 순서로 이어지는 그라데이션이 적용됩니다.\n(없음으로 둔 그라데이션 색은 건너뛰고, 설정한 색만 순서대로 이어집니다. 예: 그라데이션 색 1이 없음이고 그라데이션 색 2만 있으면 닉네임 색상 → 그라데이션 색 2)"),
            ["MoreMultiplayerInfoEnabled"] = ("팀원 재화 표시", "멀티플레이에서 팀원의 체력바 아래에 팀원이 소지한 주사위, 사파이어, 잎의 양을 작게 표시합니다."),
            ["WalletShareEnabled"] = ("팀원과 재화 공유", "모든 팀원의 잎 또는 주사위를 합쳐 하나의 공용 지갑으로 사용하고 관리합니다."),
            ["GuestReturnToTownEnabled"] = ("세션 퇴장 시 시작마을로 이동", "게스트로 멀티플레이 세션에서 나가면 타이틀 화면 대신 내 시작마을로 바로 이동합니다."),
            ["PingSoundMode"] = ("핑 소리 조절", "핑 소리를 끄거나 반복해서 들리지 않도록 합니다.\n없음: 바닐라 그대로\nN초에 한번 : 첫 핑 소리가 난 뒤 해당 시간 동안은 핑 소리가 겹치지 않습니다.\n소리 끄기: 핑 소리를 듣지 않습니다."),
            ["PingIgnoreEnabled"] = ("핑 난사 방지", "다음 맵으로 이동하는 포탈에 상호작용하거나 이동하려 할 때 생기는 핑과 알림을 모든 플레이어가 스테이지마다 1회만 가능하도록 제한합니다."),
            ["MoreDummyHpEnabled"] = ("허수아비 체력 증가", "데미지를 측정하는 허수아비가 파괴되지 않고 체력이 증가합니다."),
            ["ResetAllSettings"] = ("모든 설정 초기화", "모드의 모든 설정을 기본값으로 되돌립니다."),

        };

        internal static bool TryGet(string name, out string title, out string description)
        {
            (string, string) entry;
            if (!string.IsNullOrEmpty(name) && Entries.TryGetValue(name, out entry))
            {
                title = entry.Item1;
                description = entry.Item2;
                return true;
            }

            title = null;
            description = null;
            return false;
        }
    }
}
