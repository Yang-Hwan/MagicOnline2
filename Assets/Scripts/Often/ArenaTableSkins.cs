using UnityEngine;

namespace Assets.Scripts.Often
{
    public static class ArenaTableSkins
    {
        // HallIdx is shared by public matches and private friend challenges.
        static readonly string[] ByHall =
        {
            "ClassicWalnut", "TournamentBlue", "Porcelain", "StreetGraffiti",
            "MotherOfPearl", "Cordovan", "BlackGold", "Titanium"
        };

        public static void Apply(SpriteRenderer background, int hallId, string matchId)
        {
            if (!background || hallId < 1) return;
            int index = (hallId - 1) % ByHall.Length;
            if (hallId == 7 && !string.IsNullOrEmpty(matchId) && System.Uri.IsHexDigit(matchId[matchId.Length - 1]))
            {
                // Use a bit of the shared hexadecimal match ID on both clients.
                int lastDigit = System.Uri.FromHex(matchId[matchId.Length - 1]);
                index = 6 + (lastDigit & 1);
            }
            var sprite = Resources.Load<Sprite>("ArenaTables/" + ByHall[index]);
            if (sprite) background.sprite = sprite;
        }
    }
}
