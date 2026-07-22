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
    public class TNBKMapRenderer : MonoBehaviour
    {
        //マップを表示するか
        public static bool MapVisible = false;

        /// <summary>表示倍率(1 = 960x960px)</summary>
        public static float MapScale = 0.4f;

        /// <summary>画面中心からのオフセット(px)。TNBKStartingBlockScriptで調整。(0,0)で画面中央</summary>
        public static Vector2 MapPosition = new Vector2(-700f, 300f);

        // ---- マップ定義 ----
        public static float WorldSizeMeters = 3840f;   // ワールドの一辺
        public static float MapTexSize = 960f;         // 背景テクスチャの一辺(px)

        // 毎フレームの除算を避けるための逆数(定数なのでコンパイル時に確定)
        // WorldToMap / MapToWorld の正規化で「/ WorldSizeMeters」の代わりに使う
        public static float InvWorldSize = 1f / WorldSizeMeters;

        /// <summary>
        /// マップ背景の中心に対応するワールドXZ座標
        /// </summary>
        public static Vector2 WorldCenter = Vector2.zero;

        /// <summary>測距用: 1mあたりのpx(スケール込み)。
        /// 方向アイコンの目盛りはスケール1のとき0.25px/mで焼き込まれている前提
        /// アイコンを書き込むので固定値に
        /// </summary>
        public static float PixelsPerMeter
        {
            get { return 0.25f * MapScale; }
        }

        // ---- チーム色(★3 仮の5色。アイコンは白ベース+乗算着色を想定) ----
        public static readonly Dictionary<MPTeam, Color> TeamColors
            = new Dictionary<MPTeam, Color>
        {
        { MPTeam.None,   Color.white },
        { MPTeam.Red,    Color.red },
        { MPTeam.Green,  new Color(0.2f, 0.8f, 0.2f) },
        { MPTeam.Orange, new Color(1.0f, 0.6f, 0.1f) },
        { MPTeam.Blue,   new Color(0.2f, 0.6f, 1.0f) },
        };

        // ---- テクスチャ(初回描画時に遅延ロード) ----
        private Texture2D texBackground, texDD, texCC, texBB, texCV, texEX, texDirection, texPin;
        private bool texturesLoaded;

        // 現在のマップ矩形(OnGUIで毎回更新)。ピン逆変換にも使うためstatic
        private static Rect currentMapRect;
        private static bool mapRectValid;

        //シミュ中にF5キーを押したらマップを表示
        public void Update()
        {
            if(StatMaster.PlayMode == BesiegePlayMode.BuildMode)
            {
                return;
            }

            if(Input.GetKeyDown(KeyCode.F5))
            {
                MapVisible = !MapVisible;
            }

            // ピン検出。押されたら試行するだけで、
            // 「マップ表示中・シミュ中・非観戦・カーソルがマップ内」の全判定は
            // TryPlacePinAtCursor内で行う(観戦者はここで自然に弾かれる)
            if (Input.GetKeyDown(KeyCode.P))
            {
                TryPlacePinAtCursor();
            }
            
        }

        //テクスチャをロードする関数（毎フレーム呼ばれる）
        private void EnsureTextures()
        {
            //2回目以降は無視
            if (texturesLoaded) return;

            texBackground = ModTexture.GetTexture("Map-Background");
            texDD = ModTexture.GetTexture("Map-DD");
            texCC = ModTexture.GetTexture("Map-CC");
            texBB = ModTexture.GetTexture("Map-BB");
            texCV = ModTexture.GetTexture("Map-CV");
            texEX = ModTexture.GetTexture("Map-EX");    //航空機等
            texDirection = ModTexture.GetTexture("Map-Direction");
            texPin = ModTexture.GetTexture("Map-Pin");

            texturesLoaded = true;
        }

        /// <summary>
        /// カーソル位置にピンを刺す。このクラスのUpdate()がPキー押下時に呼ぶ。
        /// マップ表示中・シミュ中・非観戦・カーソルがマップ内の全条件を満たさなければ何もしない。
        /// </summary>
        private static void TryPlacePinAtCursor()
        {
            if (!MapVisible) return;
            if (!TNBKMapVisibilityHost.IsSimActive()) return;
            if (StatMaster.PlayMode == BesiegePlayMode.Spectator) return;  // 観戦者不可
            if (!mapRectValid) return;

            // Input.mousePositionは左下原点(Y上向き)、GUIは左上原点(Y下向き)。
            // currentMapRectはGUI座標系なので、マウスYを反転して合わせる
            Vector2 mouseGui = new Vector2(
                Input.mousePosition.x,
                Screen.height - Input.mousePosition.y);

            if (!currentMapRect.Contains(mouseGui)) return;   // マップ外は無効

            UnityEngine.Vector3 world = MapToWorld(currentMapRect, mouseGui);

            // ホスト自身が刺した場合はSendToHostが自分に届かない可能性があるため
            // 直接集約に入れる。クライアントはホストへ送信
            if (StatMaster.isHosting)
            {
                Player local = Player.GetLocalPlayer();
                if (local != null)
                    TNBKPinAuthority.SetPin(local, world.x, world.z);
            }
            else
            {
                Message msg = Mod.TNBKMapNetwork.PinSetType
                    .CreateMessage(world.x, world.z);
                ModNetworking.SendToHost(msg);
            }
        }

        //GUIの更新（毎フレーム数回）
        public void OnGUI()
        {
            if (!MapVisible) return;
            if (!TNBKMapVisibilityHost.IsSimActive()) return;

            EnsureTextures();

            //全体で使用するRectを作成
            float size = MapTexSize * MapScale;
            Rect mapRect = new Rect(
                (Screen.width - size) * 0.5f + MapPosition.x,  //左上原点X：(スクリーン - 画像サイズ×スケール）×0.5 + オフセットX
                (Screen.height - size) * 0.5f + MapPosition.y, //左上原点Y：(スクリーン - 画像サイズ×スケール）×0.5 + オフセットY
                size,      //マップサイズX：画像サイズ×スケール
                size);     //マップサイズY：画像サイズ×スケール

            //背景用のRectを作成
            float sizeX = texBackground.width * MapScale;
            float sizeY = texBackground.height * MapScale;
            Rect mapBackgroundRect = new Rect(
                (Screen.width - sizeX) * 0.5f + MapPosition.x,  //左上原点X：(スクリーン - 画像サイズ×スケール）×0.5 + オフセットX
                (Screen.height - sizeY) * 0.5f + MapPosition.y, //左上原点Y：(スクリーン - 画像サイズ×スケール）×0.5 + オフセットY
                sizeX,      //マップサイズX：画像サイズ×スケール
                sizeY);     //マップサイズY：画像サイズ×スケール

            //背景を描画
            GUI.DrawTexture(mapBackgroundRect, texBackground);

            // ---- 表示判定の材料 ----
            bool spectator = StatMaster.PlayMode == BesiegePlayMode.Spectator;  //観戦中か
            MPTeam myTeam = MPTeam.None;    //自チーム

            if (!spectator)
            {
                Player local = Player.GetLocalPlayer();
                if (local == null) return;   // 取得不能時は今フレームの艦描画を諦める
                myTeam = local.Team;
            }

            // Updateでのピン逆変換に使うため保存(OnGUIはUpdateより後に走るので
            // 1フレーム遅れになるが、マップ矩形は毎フレームほぼ不変のため実害なし)
            currentMapRect = mapRect;
            mapRectValid = true;

            // ---- ピン(味方共有。艦アイコンより下のレイヤーに描く) ----	
            // 観戦者はピンスナップショットを受信しないためPinsは空。分岐不要	
            DrawPins(mapRect);

            // ---- 艦アイコン ----
            foreach (TNBKShipEntry e in TNBKShipRegistry.All)
            {
                if (!e.Alive) continue;

                // 観戦: 全艦 / 自チーム: 常時 / 敵: ホストのスナップショットに従う
                bool show = spectator
                    || e.Team == myTeam
                    || TNBKMapVisibilityClient.IsVisible(e.SessionId);
                if (!show) continue;

                DrawShipIcon(mapRect, e);
            }

            // ---- カメラ方向アイコン(本人のみ。観戦中は使用が発生しないため除外) ----
            if (!spectator)
                DrawCameraIndicator(mapRect);
        }

        //艦種のアイコンを表示する関数
        private void DrawShipIcon(Rect mapRect, TNBKShipEntry e)
        {
            Vector2 pos = WorldToMap(mapRect, e.Position);

            // Unityのヨー角は上空から見て時計回り、マップは北(+Z)が上なので
            // RotateAroundPivot(時計回り正)にそのまま渡せる
            float yaw = e.Block.transform.eulerAngles.y;

            // 艦種によってアイコンを変更
            Texture2D icon = (e.Module.ShipClass == ShipClass.DD) ? texDD : texCC;

            switch(e.Module.ShipClass)
            {
                case ShipClass.DD: icon = texDD; break;
                case ShipClass.CC: icon = texCC; break;
                case ShipClass.BB: icon = texBB; break;
                case ShipClass.CV: icon = texCV; break;
                case ShipClass.EX: icon = texEX; break;
            }

            // アイコンはマップと同じ縮尺基準(960px基準)で作られている前提で、
            // 自然サイズ x MapScale で描画。中心をpivotに回転
            float w = icon.width * MapScale;
            float h = icon.height * MapScale;
            Rect r = new Rect(pos.x - w * 0.5f, pos.y - h * 0.5f, w, h);

            Matrix4x4 savedMatrix = GUI.matrix;
            Color savedColor = GUI.color;

            GUIUtility.RotateAroundPivot(yaw, pos);
            Color teamColor;

            GUI.color = TeamColors.TryGetValue(e.Team, out teamColor) ? teamColor : Color.white;
            GUI.DrawTexture(r, icon);

            GUI.color = savedColor;
            GUI.matrix = savedMatrix;
        }

        private void DrawCameraIndicator(Rect mapRect)
        {
            TNBKCameraBlockScript cam = TNBKCameraBlockScript.ActiveLocalCamera;
            if (cam == null) return;                       // 破棄済み参照もここでfalseになる

            TNBKShipBaseModuleBehaviour ship = cam.CarrierShip;
            if (ship == null) return;                      // 搭載艦未特定(探索前/未検出)

            BlockBehaviour shipBB = ship.BlockBehaviour;
            if (shipBB == null || shipBB.BlockHealth.health <= 0f) return;

            Vector2 pos = WorldToMap(mapRect, shipBB.transform.position);
            float camYaw = cam.CompositeTracker3.eulerAngles.y;

            // 方向アイコンは中心位置から生えてくるのでx,y（(0,0)が左上）から0.5Xずつずらす
            // 測距目盛りはスケール1で0.25px/m基準
            float w = texDirection.width * MapScale;
            float h = texDirection.height * MapScale;
            Rect r = new Rect(pos.x - w * 0.5f, pos.y - h * 0.5f, w, h);

            Matrix4x4 savedMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(camYaw, pos);
            GUI.DrawTexture(r, texDirection);
            GUI.matrix = savedMatrix;
        }

        // 味方共有ピン(角度追従なし、中心=座標)
        private void DrawPins(Rect mapRect)
        {
            List<TNBKPinClient.Pin> pins = TNBKPinClient.Pins;
            float w = texPin.width * MapScale;
            float h = texPin.height * MapScale;

            for (int i = 0; i < pins.Count; i++)
            {
                Vector2 pos = WorldToMap(mapRect, pins[i].WorldPos);
                // ピンは「下端の先が座標を指す」意匠が一般的だが、指定により
                // 角度追従なし=中心を座標に合わせる素直な配置とする
                Rect r = new Rect(pos.x - w * 0.5f, pos.y - h * 0.5f, w, h);
                GUI.DrawTexture(r, texPin);
            }
        }

        // ---- 座標変換(固定表示の核) ----
        private static Vector2 WorldToMap(Rect mapRect, UnityEngine.Vector3 worldPos)
        {
            float half = WorldSizeMeters * 0.5f;
            float nx = (worldPos.x - (WorldCenter.x - half)) * InvWorldSize;
            float nz = (worldPos.z - (WorldCenter.y - half)) * InvWorldSize;
            return new Vector2(
                mapRect.x + nx * mapRect.width,
                mapRect.y + (1f - nz) * mapRect.height);   // GUIはY下向きのため反転
        }

        // WorldToMapの逆。GUI座標(mapRect内の点)→ワールドXZ。yは0で返す
        private static UnityEngine.Vector3 MapToWorld(Rect mapRect, Vector2 guiPos)
        {
            float half = WorldSizeMeters * 0.5f;
            float nx = (guiPos.x - mapRect.x) / mapRect.width;
            float nz = 1f - (guiPos.y - mapRect.y) / mapRect.height;  // 反転を戻す
            return new UnityEngine.Vector3(
                (WorldCenter.x - half) + nx * WorldSizeMeters,
                0f,
                (WorldCenter.y - half) + nz * WorldSizeMeters);
        }
    }
}
