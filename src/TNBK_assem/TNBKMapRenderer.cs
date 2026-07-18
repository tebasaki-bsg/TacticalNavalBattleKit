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
        // ---- TNBKStartingBlockScriptから設定する公開パラメータ ----

        /// <summary>表示ON/OFF。キー入力での切り替えはStartingBlock側で
        /// TNBKMapRenderer.MapVisible = !TNBKMapRenderer.MapVisible; のように行う</summary>
        public static bool MapVisible = false;

        /// <summary>表示倍率(1 = 960x960px)</summary>
        public static float MapScale = 1f;

        /// <summary>画面中心からのオフセット(px)。(0,0)で画面中央</summary>
        public static Vector2 MapPosition = Vector2.zero;

        // ---- マップ定義 ----
        private const float WorldSizeMeters = 3840f;   // ワールドの一辺
        private const float MapTexSize = 960f;         // 背景テクスチャの一辺(px)

        /// <summary>★2 マップ背景の中心に対応するワールドXZ座標。
        /// ワールド原点がマップ中央でない場合はここを設定する</summary>
        public static Vector2 WorldCenter = Vector2.zero;

        /// <summary>測距用: 1mあたりのpx(スケール込み)。
        /// 方向アイコンの目盛りはスケール1のとき0.25px/mで焼き込まれている前提</summary>
        public static float PixelsPerMeter
        {
            get { return (MapTexSize / WorldSizeMeters) * MapScale; }
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
        private Texture2D texBackground, texDD, texCC, texDirection;
        private bool texturesLoaded;

        private void EnsureTextures()
        {
            if (texturesLoaded) return;
            texBackground = ModTexture.GetTexture("Map-Background");
            texDD = ModTexture.GetTexture("Map-DD");
            texCC = ModTexture.GetTexture("Map-CC");
            texDirection = ModTexture.GetTexture("Map-Direction");
            texturesLoaded = true;
        }

        public void OnGUI()
        {
            if (!MapVisible) return;
            if (!TNBKMapVisibilityHost.IsSimActive()) return;

            EnsureTextures();

            // ---- マップ矩形(デフォルト: 画面中央、等倍) ----
            float size = MapTexSize * MapScale;
            Rect mapRect = new Rect(
                (Screen.width - size) * 0.5f + MapPosition.x,
                (Screen.height - size) * 0.5f + MapPosition.y,
                size, size);

            GUI.DrawTexture(mapRect, texBackground);

            // ---- 表示判定の材料 ----
            bool spectator = StatMaster.PlayMode == BesiegePlayMode.Spectator;
            MPTeam myTeam = MPTeam.None;
            if (!spectator)
            {
                Player local = Player.GetLocalPlayer();
                if (local == null) return;   // 取得不能時は今フレームの艦描画を諦める
                myTeam = local.Team;
            }

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

        private void DrawShipIcon(Rect mapRect, TNBKShipEntry e)
        {
            Vector2 pos = WorldToMap(mapRect, e.Position);

            // Unityのヨー角は上空から見て時計回り、マップは北(+Z)が上なので
            // RotateAroundPivot(時計回り正)にそのまま渡せる
            float yaw = e.Block.transform.eulerAngles.y;

            // ★1 と同じ仮定(ShipClassプロパティ)
            Texture2D icon = (e.Module.ShipClass == ShipClass.DD) ? texDD : texCC;

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

        // ---- 座標変換(固定表示の核) ----
        private static Vector2 WorldToMap(Rect mapRect, UnityEngine.Vector3 worldPos)
        {
            float half = WorldSizeMeters * 0.5f;
            float nx = (worldPos.x - (WorldCenter.x - half)) / WorldSizeMeters;
            float nz = (worldPos.z - (WorldCenter.y - half)) / WorldSizeMeters;
            return new Vector2(
                mapRect.x + nx * mapRect.width,
                mapRect.y + (1f - nz) * mapRect.height);   // GUIはY下向きのため反転
        }
    }
}
