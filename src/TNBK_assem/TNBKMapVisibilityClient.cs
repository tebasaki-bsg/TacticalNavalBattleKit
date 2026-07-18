using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TNBKSpace
{
    public static class TNBKMapVisibilityClient
    {
        private static readonly HashSet<ushort> visibleEnemies
            = new HashSet<ushort>();

        /// <summary>ネットワーク受信から。フルスナップショットで丸ごと置き換え</summary>
        public static void ApplySnapshot(int[] ids)
        {
            visibleEnemies.Clear();
            for (int i = 0; i < ids.Length; i++)
                visibleEnemies.Add((ushort)ids[i]);
        }

        /// <summary>ホストがネットワークを介さず自チーム分を直接書く口</summary>
        public static void ApplyLocal(List<ushort> ids)
        {
            visibleEnemies.Clear();
            for (int i = 0; i < ids.Count; i++)
                visibleEnemies.Add(ids[i]);
        }

        public static bool IsVisible(ushort sessionId)
        {
            return visibleEnemies.Contains(sessionId);
        }

        public static void Clear()
        {
            visibleEnemies.Clear();
        }
    }
}
