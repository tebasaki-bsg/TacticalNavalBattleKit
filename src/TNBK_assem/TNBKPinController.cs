using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Modding;
using Modding.Common;

namespace TNBKSpace
{
    // ============================================================================
    // 6.6 ピン集約(ホスト専用)
    //     プレイヤーが刺したピンを保持し、期限切れを掃除し、
    //     チーム別スナップショットを組み立てる。1プレイヤー1個(NetworkIdで上書き)。
    // ============================================================================
    public static class TNBKPinAuthority
    {
        private const float PinLifetime = 5f;   // 自動消滅までの秒数

        private struct PinData
        {
            public MPTeam Team;
            public float X, Z;
            public float SetTime;
        }

        // ownerNetworkId → ピン。1人1個なので新規Setで上書きされる
        private static readonly Dictionary<int, PinData> pins
            = new Dictionary<int, PinData>();

        // 使い回しバッファ(BuildSnapshotのアロケーション抑制)
        private static readonly List<int> ownerBuf = new List<int>();
        private static readonly List<float> coordBuf = new List<float>();

        /// <summary>ホストがPinSet受信時に呼ぶ(観戦者からの要求は無視)</summary>
        public static void SetPin(Player sender, float x, float z)
        {
            if (sender.IsSpectator) return;   // 観戦者はピンを刺せない

            pins[sender.NetworkId] = new PinData
            {
                Team = sender.Team,
                X = x,
                Z = z,
                SetTime = Time.time
            };
        }

        /// <summary>期限切れピンを除去</summary>
        public static void PurgeExpired(float now)
        {
            // .NET 3.5: 列挙中削除を避けるためキーを集めてから消す
            ownerBuf.Clear();
            foreach (var kv in pins)
                if (now - kv.Value.SetTime > PinLifetime)
                    ownerBuf.Add(kv.Key);
            for (int i = 0; i < ownerBuf.Count; i++)
                pins.Remove(ownerBuf[i]);
        }

        /// <summary>
        /// 指定チームの全ピンを owners(NetworkId列) と coords(x,z交互列) に展開。
        /// フルスナップショット(空でも送る=消えたことが伝わる)
        /// </summary>
        public static void BuildSnapshot(MPTeam team, out int[] owners, out float[] coords)
        {
            ownerBuf.Clear();
            coordBuf.Clear();
            foreach (var kv in pins)
            {
                if (kv.Value.Team != team) continue;
                ownerBuf.Add(kv.Key);
                coordBuf.Add(kv.Value.X);
                coordBuf.Add(kv.Value.Z);
            }
            owners = ownerBuf.ToArray();
            coords = coordBuf.ToArray();
        }

        public static void Clear()
        {
            pins.Clear();
        }
    }


    // ============================================================================
    // 6.7 ピン表示(全マシン)
    //     ホストから受け取ったチームのピンスナップショットを保持し、Rendererが描く。
    // ============================================================================
    public static class TNBKPinClient
    {
        public struct Pin
        {
            public int Owner;
            public Vector3 WorldPos;   // yは0(ミニマップはXZのみ使用)
        }

        private static readonly List<Pin> pins = new List<Pin>();

        /// <summary>owners と coords(x,z交互) から丸ごと置き換え(フルスナップショット)</summary>
        public static void ApplySnapshot(int[] owners, float[] coords)
        {
            pins.Clear();
            for (int i = 0; i < owners.Length; i++)
            {
                pins.Add(new Pin
                {
                    Owner = owners[i],
                    WorldPos = new Vector3(coords[i * 2], 0f, coords[i * 2 + 1])
                });
            }
        }

        public static List<Pin> Pins { get { return pins; } }

        public static void Clear()
        {
            pins.Clear();
        }
    }
}
