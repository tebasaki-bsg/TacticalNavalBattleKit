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
    public static class TNBKShipIdAuthority
    {
        private static ushort nextId;

        private static readonly Dictionary<BlockBehaviour, ushort> assigned
            = new Dictionary<BlockBehaviour, ushort>();

        /// <summary>
        /// IDを発行し、艦船の登録をShipRegisterに行わせる関数。
        /// TNBKShipBaseModuleBehaviour.OnSimulateStart から呼ぶ。
        /// 呼び出し側でガードすること:
        ///   if (StatMaster.isHosting) TNBKShipIdAuthority.RegisterShip(this);
        /// </summary>
        public static void RegisterShip(TNBKShipBaseModuleBehaviour module)
        {
            BlockBehaviour bb = module.BlockBehaviour;
            if (bb == null) return;
            if (assigned.ContainsKey(bb)) return;   // 二重発番防止

            ushort id = nextId++;
            assigned.Add(bb, id);

            // ホスト自身のRegistryへ直接登録
            // (SendToAllは自分に届かないと確認済みのため、直接登録が唯一の経路)
            TNBKShipRegistry.Register(id, bb, module, bb.Team);

            // 全クライアントへ配布。Block.FromでBlockBehaviour→Blockラッパーに変換
            Message msg = Mod.TNBKMapNetwork.ShipIdAssignType
                .CreateMessage(Block.From(bb), (int)id);
            ModNetworking.SendToAll(msg);
        }

        /// <summary>途中参加者への対応表の再送(Event.OnPlayerJoinから)</summary>
        public static void ResendAllTo(Player newPlayer)
        {
            foreach (var pair in assigned)
            {
                Message msg = Mod.TNBKMapNetwork.ShipIdAssignType
                    .CreateMessage(Block.From(pair.Key), (int)pair.Value);
                ModNetworking.SendTo(newPlayer, msg);
            }
        }

        public static void Clear()
        {
            nextId = 0;
            assigned.Clear();
        }
    }
}
