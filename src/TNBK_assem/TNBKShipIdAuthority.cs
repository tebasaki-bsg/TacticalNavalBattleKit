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

        //二重登録防止のためのDictionary
        private static readonly Dictionary<BlockBehaviour, ushort> assigned
            = new Dictionary<BlockBehaviour, ushort>();

        /// <summary>
        /// IDを発行し、艦船の登録をShipRegisterに行わせる関数。
        /// TNBKShipBaseModuleBehaviour.OnSimulateStart から呼ぶ。
        /// 戻り値で自身のsessionIDを取得させる
        /// </summary>
        public static ushort RegisterShip(TNBKShipBaseModuleBehaviour module)
        {
            BlockBehaviour bb = module.BlockBehaviour;
            if (bb == null) return 0;   //nullの時はどうでもいいので0を返す
            if (assigned.ContainsKey(bb)) return assigned[bb];   // 二重発番防止、番号を書き換えないように元の番号を返す

            //sessionIDを生成し、このクラスの持つ二重登録防止用Dictionaryに登録
            ushort id = nextId++;
            assigned.Add(bb, id);

            // ホスト自身のRegistryへ直接登録
            // (SendToAllは自分に届かないと確認済みのため、直接登録が唯一の経路)
            TNBKShipRegistry.Register(id, bb, module, bb.Team);

            // 全クライアントへ配布。Block.FromでBlockBehaviour→Blockラッパーに変換
            Message msg = Mod.TNBKMapNetwork.ShipIdAssignType
                .CreateMessage(Block.From(bb), (int)id);
            ModNetworking.SendToAll(msg);

            return id;
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
