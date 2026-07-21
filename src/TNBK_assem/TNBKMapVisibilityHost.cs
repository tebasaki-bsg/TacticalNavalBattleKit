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
    public class TNBKMapVisibilityHost : MonoBehaviour
    {
        public static TNBKMapVisibilityHost Instance;

        private const int SendInterval = 10;       // 物理フレーム(100Hzで0.1秒)
        private const float GraceSeconds = 0.5f;   // 最終可視からの表示維持時間
        private const int IslandLayerMask = 1 << 29;

        private int frameCounter;
        private bool wasSimulating = false;

        // グレース期間: チームごとに「敵艦ID → 最後に可視だった時刻」
        private readonly Dictionary<MPTeam, Dictionary<ushort, float>> lastSeen
            = new Dictionary<MPTeam, Dictionary<ushort, float>>();

        // 使い回しバッファ(毎周期のGCアロケーション回避)
        private readonly List<ushort> sendBuffer = new List<ushort>();

        public void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// GlobalSimulationが実際に進行中か(確認済み仕様):
        /// - 参加者のマシンでは PlayMode == GlobalSimulation で判定できる
        /// - 観戦マシンでは PlayMode が Spectator を返し、シミュの有無を直接
        ///   教えてくれないため、「Registryに艦が登録されているか」を代理指標にする。
        ///   Registryはシミュ開始の登録で埋まり、OnSimulateStopのクリーンアップで
        ///   空になるので、非空 ≒ シミュ進行中が成立する。
        ///   (艦が1隻も存在しないシミュではマップが出ないが、その場合
        ///    表示すべきものが無いため実害なし)
        /// </summary>
        public static bool IsSimActive()
        {
            if (StatMaster.PlayMode == BesiegePlayMode.GlobalSimulation)
                return true;
            if (StatMaster.PlayMode == BesiegePlayMode.Spectator)
                return TNBKShipRegistry.HasAny;
            return false;
        }

        public void ResetState()
        {
            lastSeen.Clear();
            frameCounter = 0;
        }

        public void FixedUpdate()
        {
            bool sim = IsSimActive();

            // --- フォールバック: シミュ終了エッジ検出(全マシン) ---
            // 主経路はOnSimulateStop。冪等なので二重に走っても壊れない
            if (wasSimulating && !sim)
            {
                TNBKMapSession.OnSessionEnd();
            }

            //シミュがスタートした瞬間
            /*
            if(!wasSimulating && sim)
            {
                
            }
            */

            wasSimulating = sim;

            // --- Assign解決の保留キュー再試行(全マシン) ---
            // 必ずIsSimActiveゲートより前に置くこと。観戦マシンでは
            // 「Registryが空 → IsSimActiveがfalse」のため、ゲートの後ろに置くと
            // Retryが一度も走らずRegistryが永久に埋まらないデッドロックになる
            TNBKPendingAssigns.Retry();

            if (!sim) return;

            // --- ここからホスト専用処理 ---
            if (!StatMaster.isHosting) return;

            if (++frameCounter < SendInterval) return;
            frameCounter = 0;

            RunVisibilityCheckAndSend();
            RunPinBroadcast();
        }

        // ピンの期限切れ処理とチーム別スナップショット配信。
        // 可視判定と同じ送信周期(10物理フレーム)に相乗りさせる
        private void RunPinBroadcast()
        {
            // 期限切れピンを除去(自動消滅はホストで一元管理し、
            // フルスナップショットで「消えた状態」を全員に届ける)
            TNBKPinAuthority.PurgeExpired(Time.time);

            List<Player> allPlayers = Player.GetAllPlayers();
            Player local = Player.GetLocalPlayer();
            bool localIsSpectator =
                StatMaster.PlayMode == BesiegePlayMode.Spectator;

            foreach (MPTeam team in TNBKTeamUtil.AllTeams)
            {
                int[] owners;
                float[] coords;
                TNBKPinAuthority.BuildSnapshot(team, out owners, out coords);

                // ホスト自身がこのチームの非観戦プレイヤーなら直書き
                if (!localIsSpectator && local != null && local.Team == team)
                    TNBKPinClient.ApplySnapshot(owners, coords);

                // 同チームの非観戦プレイヤーへ配信
                for (int i = 0; i < allPlayers.Count; i++)
                {
                    Player p = allPlayers[i];
                    if (p == local) continue;
                    if (p.IsSpectator) continue;   // 観戦者はピンを見られない
                    if (p.Team != team) continue;

                    Message msg = Mod.TNBKMapNetwork.PinSnapshotType
                        .CreateMessage((object)owners, (object)coords);
                    ModNetworking.SendTo(p, msg);
                }
            }
        }

        //可視艦リストを更新する関数
        private void RunVisibilityCheckAndSend()
        {
            float now = Time.time;

            // プレイヤーリストは周期に1回だけ取得し、5チームで使い回す
            List<Player> allPlayers = Player.GetAllPlayers();
            Player local = Player.GetLocalPlayer();
            bool localIsSpectator =
                StatMaster.PlayMode == BesiegePlayMode.Spectator;

            //各チームごとに可視艦を確認、そのチーム全員に送信させる
            foreach (MPTeam team in TNBKTeamUtil.AllTeams)
            {
                List<TNBKShipEntry> friendly = TNBKShipRegistry.GetTeam(team);
                Dictionary<ushort, float> seen = GetSeenMap(team);

                // --- 1. 生の可視判定(距離+遮蔽)で lastSeen を更新 ---
                foreach (TNBKShipEntry enemy in TNBKShipRegistry.All)
                {
                    if (enemy.Team == team) continue;   // 敵艦のみ対象
                    if (!enemy.Alive) continue;

                    if (IsDetected(friendly, enemy))
                        seen[enemy.SessionId] = now;
                }

                // --- 2. グレース期間を適用して送信リストを構築 ---
                sendBuffer.Clear();
                foreach (var pair in seen)
                {
                    if (now - pair.Value > GraceSeconds) continue;  // 期限切れ
                    TNBKShipEntry e;
                    if (TNBKShipRegistry.TryGet(pair.Key, out e) && e.Alive)
                        sendBuffer.Add(pair.Key);
                }

                // --- 3. 配信(空リストも送る=「全て消えた」も明示的に届ける) ---
                DeliverToTeam(team, sendBuffer, allPlayers, local, localIsSpectator);
            }
        }

        // 敵艦enemyが、friendlyのいずれかの生存艦から探知されているか
        private bool IsDetected(List<TNBKShipEntry> friendly, TNBKShipEntry enemy)
        {
            UnityEngine.Vector3 ep = enemy.Position;

            for (int i = 0; i < friendly.Count; i++)
            {
                TNBKShipEntry s = friendly[i];
                if (!s.Alive) continue;

                float r = s.DetectionRadius;   // DD=700 / CC=500
                UnityEngine.Vector3 sp = s.Position;

                // 足切り: 平方距離のみ(平方根なし)。ここで大半の組を弾く
                if ((ep - sp).sqrMagnitude > r * r) continue;

                // 遮蔽: レイヤー29(島)に遮られていなければ可視。
                // Linecastは2点を渡すだけで距離の再計算が不要
                if (!Physics.Linecast(sp, ep, IslandLayerMask))
                    return true;
            }
            return false;
        }

        //チームに可視艦の情報を送る関数
        private void DeliverToTeam(MPTeam team, List<ushort> ids,
                                   List<Player> allPlayers, Player local,
                                   bool localIsSpectator)
        {
            // ホスト自身がこのチームのプレイヤーなら直書き。
            // 観戦中のホストは全艦表示(Renderer側の観戦分岐)なので書かない
            if (!localIsSpectator && local != null && local.Team == team)
                TNBKMapVisibilityClient.ApplyLocal(ids);

            // payloadは遅延生成: このチームに送信相手がいるときだけ配列を作る
            int[] payload = null;

            for (int i = 0; i < allPlayers.Count; i++)
            {
                Player p = allPlayers[i];

                if (p == local) continue;        // 自分は直書き済み(参照比較で有効と確認済み)
                if (p.IsSpectator) continue;     // 観戦者には送らない(Teamが不定でも安全)
                if (p.Team != team) continue;    // チームでフィルタ

                if (payload == null)
                {
                    payload = new int[ids.Count];
                    for (int j = 0; j < ids.Count; j++) payload[j] = ids[j];
                }

                // CreateMessageはparams object[]のため、int[]が引数展開されないよう
                // (object)キャストで「1つの引数」として渡す
                Message msg = Mod.TNBKMapNetwork.VisibilityType
                    .CreateMessage((object)payload);
                ModNetworking.SendTo(p, msg);
            }
        }

        private Dictionary<ushort, float> GetSeenMap(MPTeam team)
        {
            Dictionary<ushort, float> map;
            if (!lastSeen.TryGetValue(team, out map))
            {
                map = new Dictionary<ushort, float>();
                lastSeen.Add(team, map);
            }
            return map;
        }
    }
}
