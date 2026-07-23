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
        public BlockBehaviour blockBehaviour;

        public MSlider ScaleSlider;
        public MSlider OffsetSliderX;
        public MSlider OffsetSliderY;

        public bool isOwnerSame = false;

        public void Awake()
        {
            blockBehaviour = GetComponent<BlockBehaviour>();

            //ScaleとOffsetのスライダーを追加
            ScaleSlider = blockBehaviour.AddSlider("Map Scale", "map-scale", 0.4f, 0.01f, 5f);
            ScaleSlider.DisplayInMapper = true;

            OffsetSliderX = blockBehaviour.AddSlider("Offset X", "map-offset-x", -700f, -1000f, 1000f);
            OffsetSliderX.DisplayInMapper = true;

            OffsetSliderY = blockBehaviour.AddSlider("Offset Y", "map-offset-y", 300f, -1000f, 1000f);
            OffsetSliderY.DisplayInMapper = true;
        }

        public void Start()
        {
            //建築中でない場合のみ作動
            if (!blockBehaviour.isBuildBlock)
            {
                //オーナーチェック
                UpdateOwnerFlag();

                //オーナーであればミニマップを調整
                if(isOwnerSame)
                {
                    TNBKMapRenderer.MapScale = ScaleSlider.Value;   //デフォで960x960
                    TNBKMapRenderer.MapPosition = new UnityEngine.Vector2(OffsetSliderX.Value, OffsetSliderY.Value);
                }
            }
        }

        public void OnDisable()
        {
            //消された＆観戦中⇒リスポと判断
            if (StatMaster.levelSimulating)
            {
                Mod.SomeoneRespawn = true;

                return;
            }
        }

        private void UpdateOwnerFlag()  //プレイヤーのIDとブロックの親のIDを比べる関数
        {
            ushort OwnerID;
            ushort BlockPlayerID;

            if (StatMaster.isMP)
            {
                BlockPlayerID = blockBehaviour.ParentMachine.PlayerID;

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
