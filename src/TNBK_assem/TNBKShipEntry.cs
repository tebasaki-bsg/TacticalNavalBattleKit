using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.UI;
using Modding;
using Modding.Serialization;
using Modding.Modules;
using Modding.Blocks;
using Modding.Common;

namespace TNBKSpace
{
    public class TNBKShipEntry
    {
        public ushort SessionId;
        public BlockBehaviour Block;
        public TNBKShipBaseModuleBehaviour Module;
        public MPTeam Team;           // 登録時点のBlockBehaviour.Teamを保持

        // HealthはBesiege本体が同期済みのため、pushせず常に直接読む
        public float Health { get { return Block.BlockHealth.health; } }
        public bool Alive { get { return Block != null && Health > 0f; } }

        public UnityEngine.Vector3 Position { get { return Block.transform.position; } }

        public float DetectionRadius
        {
            get
            {
                //探索距離をModule側に付ける
                return Module.RaderRange;
            }
        }
    }

    public static class TNBKShipRegistry
    {
        private static readonly Dictionary<ushort, TNBKShipEntry> byId
            = new Dictionary<ushort, TNBKShipEntry>();

        private static readonly Dictionary<MPTeam, List<TNBKShipEntry>> byTeam
            = new Dictionary<MPTeam, List<TNBKShipEntry>>();

        private static readonly List<TNBKShipEntry> emptyList
            = new List<TNBKShipEntry>();

        public static void Register(ushort sessionId, BlockBehaviour block,
                                    TNBKShipBaseModuleBehaviour module, MPTeam team)
        {
            // 再送(ResendAllTo等)による二重登録を正常系として吸収
            if (byId.ContainsKey(sessionId)) return;

            var e = new TNBKShipEntry
            {
                SessionId = sessionId,
                Block = block,
                Module = module,
                Team = team
            };
            byId.Add(sessionId, e);

            List<TNBKShipEntry> list;
            if (!byTeam.TryGetValue(team, out list))
            {
                list = new List<TNBKShipEntry>();
                byTeam.Add(team, list);
            }
            list.Add(e);
        }

        public static bool TryGet(ushort sessionId, out TNBKShipEntry entry)
        {
            return byId.TryGetValue(sessionId, out entry);
        }

        /// <summary>読み取り専用として扱うこと</summary>
        public static List<TNBKShipEntry> GetTeam(MPTeam team)
        {
            List<TNBKShipEntry> list;
            return byTeam.TryGetValue(team, out list) ? list : emptyList;
        }

        public static IEnumerable<TNBKShipEntry> All { get { return byId.Values; } }

        /// <summary>1隻でも登録されているか。観戦マシンでの
        /// 「シミュレーション進行中」の代理指標として使う</summary>
        public static bool HasAny { get { return byId.Count > 0; } }

        public static void Clear()
        {
            byId.Clear();
            byTeam.Clear();
        }
    }

    /// <summary>
    /// Assign解決の保留キュー
    /// 「Assignメッセージ到着 → まだ自分のシミュ複製が無い」の競合を吸収する。
    /// TNBKMapVisibilityHost(全マシンで動く)のFixedUpdateから毎フレーム再試行
    /// </summary>
    public static class TNBKPendingAssigns
    {
        private struct Pending
        {
            public Block NetBlock;
            public ushort SessionId;
        }

        private static readonly List<Pending> pending = new List<Pending>();

        public static void Enqueue(Block netBlock, ushort sessionId)
        {
            pending.Add(new Pending { NetBlock = netBlock, SessionId = sessionId });
        }

        /// <summary>毎物理フレーム呼ばれ、解決できたものから登録してキューを縮める</summary>
        public static void Retry()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Mod.TNBKMapNetwork.TryRegisterFromNet(
                        pending[i].NetBlock, pending[i].SessionId))
                    pending.RemoveAt(i);
            }
        }

        public static void Clear()
        {
            pending.Clear();
        }
    }

    //AllTeams[]を作っておくためのやつ
    public static class TNBKTeamUtil
    {
        // MPTeamの5メンバー全てが実チーム(None="White"相当)。観戦はここに含まれない
        public static readonly MPTeam[] AllTeams = new MPTeam[]
        {
        MPTeam.None, MPTeam.Red, MPTeam.Green, MPTeam.Orange, MPTeam.Blue
        };
    }

    //艦種
    public enum ShipClass
    {
        DD, //0
        CC, //1
        BB, //2
        CV,	//3
        EX  //4
    }
}
