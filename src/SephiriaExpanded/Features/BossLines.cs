using System.Collections.Generic;
using System.Linq;

namespace SephiriaExpanded
{
    internal static class BossLines
    {
        private static readonly string[,] Pairs =
        {
            { "모그디", "Mogdi" },
            { "어디 한번 붙어보자!", "Let's go at it!" },
            { "2라운드다!", "Round two!" },
            { "크윽... 내가 밀리다니.", "Ugh... pushed back by you?" },
            { "터져라!", "Blow up!" },
            { "날려주마!", "I'll blast you!" },
            { "한 방 더!", "Another one!" },
            { "피할 틈은 없다!", "No room to dodge!" },
            { "쾅이다!", "Kaboom!" },
            { "몸이 풀리는군!", "Just warming up!" },
            { "속도를 올린다!", "Picking up speed!" },
            { "아직 멀었다!", "Far from over!" },
            { "더 빠르게!", "Faster!" },
            { "끝까지 간다!", "All the way!" },

            { "오드너", "Odner" },
            { "놀아볼까, 친구?", "Wanna play, pal?" },
            { "고작 이 정도야?", "That's all you got?" },
            { "나 조금 ", "I might " },
            { "과열", "overheat" },
            { " 될지도!", " a bit!" },
            { "너, 강하네!", "You're strong!" },
            { "펑!", "Boom!" },
            { "한 대 더~!", "One more~!" },
            { "하늘에서 선물이야!", "A gift from the sky!" },
            { "위를 봐~!", "Look up~!" },
            { "폭죽 놀이다!", "Fireworks time!" },
            { "골라 먹어~!", "Pick one~!" },
            { "줄줄이 팡팡!", "Pop pop pop!" },
            { "어디로 숨을래?", "Where ya gonna hide?" },

            { "하즈", "Haz" },
            { "과업은 행동으로 증명하는 것이고.", "A task is proven through action," },
            { "책임은 침묵으로 견뎌내는 것이다.", "and duty is endured in silence." },

            { "라타카", "Rataka" },
            { "먼 길 오셨군요?", "Came a long way, did you?" },
            { "멈춰!", "Stop right there!" },
            { "공격해!", "Attack!" },
            { "아야...", "Ouch..." },
            { "피할 수 있겠어요?", "Can you dodge this?" },
            { "자, 춤춰 보세요!", "Now, dance for me!" },
            { "머리 조심하세요!", "Watch your head!" },
            { "위에서 떨어져요!", "Incoming from above!" },

            { "레리드", "Lerid" },
            { "용케 여기까지 왔군.", "So you made it this far." },
            { "흐음... 제법이군", "Hmm... not bad" },
            { "훌륭한 솜씨였다... 이 길, 지나가거라.", "Fine skill... You may pass." },
            { "따라올 수 있겠나!", "Can you keep up!" },
            { "일섬!", "Flash!" },
            { "빈틈이 보이는군!", "I see an opening!" },
            { "베어 가르마!", "I'll cut you down!" },
            { "눈 깜짝할 새다!", "In the blink of an eye!" },
            { "그림자는 하나가 아니다.", "There's more than one shadow." },
            { "피해 보아라!", "Try to dodge!" },
            { "맞혀 보거라.", "Try to hit me." },

            { "골렘", "Golem" },
            { "에르마", "Erma" },
            { "내.. 걸작이..!!", "My.. masterpiece..!!" },
            { "위협 등급 B. 제압 시퀀스 가동", "Threat level B. Suppression sequence engaged" },
            { "위협 등급 S. 전멸 시퀀스 가동", "Threat level S. Annihilation sequence engaged" },

            { "팬택시스", "Pentaxis" },
            { "적대자 감지. 파괴합니다.", "Hostile detected. Destroying." },
            { "작동 이상 감지.", "Malfunction detected." },
            { "리미터 해제. 최종 프로토콜 실행.", "Limiter released. Executing final protocol." },
            { "에러 코드 발동. 시스템 중지.", "Error code triggered. System halt." },
            { "돌진 강화.", "Charge enhanced." },
            { "가속.", "Accelerating." },
            { "추적 시스템 가동.", "Tracking system online." },
            { "회전.", "Spinning." },
            { "분쇄 시스템 가동.", "Crushing system online." },
            { "공격.", "Attacking." },
            { "파괴.", "Destroy." },

            { "아스카드", "Askard" },
            { "발자국을 겹쳐 새길수록, 네가 다다를 곳은 똑같은 폐허뿐이다.", "However many footsteps you carve, the same ruins are all you will reach." },
            { "인정하마... 네가 이겼다.", "I admit it... You win." },
            { "도망쳐라.", "Run." },
            { "무의미하다.", "Meaningless." },
            { "허망한 몸짓이다.", "A futile gesture." },
            { "눈빛이 가엽구나.", "What pitiful eyes." },
            { "증언해라.", "Bear witness." },
            { "어리석도다.", "How foolish." },
            { "겨우 이 정도인가?", "Is this all?" },
            { "절망을 직시해라!", "Face your despair!" },
            { "제법이군... 하지만..", "Not bad... but.." },

            { "코베스", "Kovess" },
            { "클리포트 님에게 더이상 접근하지 말거라.", "Come no closer to Lord Qliphoth." },
            { "너를 시험하마.", "I shall test you." },
            { "클리포트 님... 부디 용서를...", "Lord Qliphoth... please forgive me..." },
            { "한없이 나약한 자여.", "How endlessly weak you are." },
            { "분노가 느껴지는 군.", "I can feel your rage." },
            { "이것이 너의 대답인가?", "Is this your answer?" },

            { "크라즈", "Kraz" },
            { "이곳은 클리포트 님의 안식처. 지나가려면 저를 쓰러뜨리십시오.", "This is Lord Qliphoth's resting place. To pass, you must defeat me." },
            { "죄송합니다. 클리포트 님...", "Forgive me, Lord Qliphoth..." },
            { "이 힘. 알 것 같군요.", "This power... I think I understand." },
            { "최선입니까?", "Is that your best?" },
            { "멈추십시오.", "Cease this." },

            { "클리포트", "Qliphoth" },
            { "이대로 끝일 줄 알았습니까?", "Did you think it would end like this?" },
            { "어디 한번 보여주시지요.", "Go on, show me." },
            { "끝없는 고통을 안겨드리지요. 부디 꺾이지 않기를...", "I will give you endless pain. May you not break..." },
            { "크윽...", "Ugh..." },
            { "더이상...", "No more..." }
        };

        private static List<KeyValuePair<string, string>> toEnglish;
        private static List<KeyValuePair<string, string>> toKorean;

        internal static string Localize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            if (toEnglish == null)
            {
                var english = new List<KeyValuePair<string, string>>();
                var korean = new List<KeyValuePair<string, string>>();
                for (int i = 0; i < Pairs.GetLength(0); i++)
                {
                    english.Add(new KeyValuePair<string, string>(Pairs[i, 0], Pairs[i, 1]));
                    korean.Add(new KeyValuePair<string, string>(Pairs[i, 1], Pairs[i, 0]));
                }

                toEnglish = english.OrderByDescending(pair => pair.Key.Length).ToList();
                toKorean = korean.OrderByDescending(pair => pair.Key.Length).ToList();
            }

            foreach (KeyValuePair<string, string> pair in ModText.IsEnglish ? toEnglish : toKorean)
                if (text.Contains(pair.Key)) text = text.Replace(pair.Key, pair.Value);

            return text;
        }
    }
}
