using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.ComponentModel;
using Modding;
using Modding.Blocks;
using Modding.Common;
using Modding.Modules;
using Modding.Serialization;
using UnityEngine;
using Localisation;

namespace TNBKSpace
{
    public class TNBKNauticalScrewScript : BlockScript
    {
        public BlockBehaviour blockBehaviour;
        public MSlider HPSlider;

        public void Awake()
        {
            blockBehaviour = GetComponent<BlockBehaviour>();

            //スライダーを追加
            HPSlider = blockBehaviour.AddSlider(Mod.isJapanese ? "体力" : "Block HP", "health", 6.0f, 0.1f, 100f);   //名前, キー, デフォルト, 最小, 最大
            HPSlider.DisplayInMapper = true;

            //建築中は終わり
            if(blockBehaviour.isBuildBlock)
            {
                return;
            }

            //シミュ開始時ならHPを弄る
            if (!StatMaster.isMP || StatMaster.isHosting || StatMaster.isLocalSim)
            {
                blockBehaviour.BlockHealth.maxHealth = HPSlider.Value;
                blockBehaviour.BlockHealth.health = HPSlider.Value;

            }
        }
    }
}
