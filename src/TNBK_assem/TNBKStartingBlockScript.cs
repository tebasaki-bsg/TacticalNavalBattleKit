using System;
using System.Collections.Generic;
using Modding;
using Modding.Blocks;
using Modding.Serialization;
using Modding.Modules;
using UnityEngine;
using Localisation;

namespace TNBKSpace
{
    public class TNBKStartingBlockScript : BlockScript
    {
        public BlockBehaviour blockbehaviour;
        public MKey MapActivateKey;

        public MSlider ScaleSlider;
        public MSlider OffsetSliderX;
        public MSlider OffsetSliderY;

        public bool isOwnerSame = false;

        public void Awake()
        {
            blockbehaviour = GetComponent<BlockBehaviour>();

            MapActivateKey = blockbehaviour.AddKey(Mod.isJapanese ? "マップ表示切り替え" : "Change Map Status", "change-map-status", KeyCode.P);
            MapActivateKey.DisplayInMapper = true;

            //ScaleとOffsetのスライダーを追加
            ScaleSlider = blockbehaviour.AddSlider("Scale X", "map-scale", 0.4f, 0.01f, 5f);
            ScaleSlider.DisplayInMapper = true;

            OffsetSliderX = blockbehaviour.AddSlider("Offset X", "map-offset-x", -700f, -1000f, 1000f);
            OffsetSliderX.DisplayInMapper = true;

            OffsetSliderY = blockbehaviour.AddSlider("Offset Y", "map-offset-y", 300f, -1000f, 1000f);
            OffsetSliderY.DisplayInMapper = true;
        }

        public void Start()
        {
            TNBKMapRenderer.MapVisible = false;

            TNBKMapRenderer.MapScale = ScaleSlider.Value;   //デフォで960x960
            TNBKMapRenderer.MapPosition = new UnityEngine.Vector2(OffsetSliderX.Value, OffsetSliderY.Value);

            if (!blockbehaviour.isBuildBlock)
            {
                UpdateOwnerFlag();
            }
        }

        public void Update()
        {
            //建築中は無視
            if(StatMaster.PlayMode == BesiegePlayMode.BuildMode)
            {
                return;
            }

            //その画面のプレイヤーじゃなければ無視
            if(!isOwnerSame)
            {
                return;
            }

            if(MapActivateKey.IsPressed || MapActivateKey.EmulationPressed())
            {
                TNBKMapRenderer.MapVisible = !TNBKMapRenderer.MapVisible;
            }
        }

        private void UpdateOwnerFlag()  //プレイヤーのIDとブロックの親のIDを比べる関数
        {
            ushort OwnerID;
            ushort BlockPlayerID;

            if (StatMaster.isMP)
            {
                BlockPlayerID = blockbehaviour.ParentMachine.PlayerID;

                if (StatMaster.PlayMode == BesiegePlayMode.Spectator)   //観戦モード
                {
                    isOwnerSame = false;

                    return;
                }
                else
                {
                    OwnerID = PlayerMachine.GetLocal().Player.NetworkId;
                }

                isOwnerSame = BlockPlayerID == OwnerID;
            }
            else
            {
                isOwnerSame = true;
            }
        }
    }
}
