using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.ComponentModel;
using Modding;
using Modding.Blocks;
using Modding.Modules;
using Modding.Serialization;
using UnityEngine;
using Localisation;

namespace TNBKSpace
{
    //XML上での名前
    [XmlRoot("TNBKFloatBlockModule")]
    [Reloadable]

    public class TNBKFloatBlockModule : BlockModule
    {
        [XmlElement("WaterDepthSlider")]
        [RequireToValidate]
        public MSliderReference WaterDepthSlider;

        [XmlElement("Buoyancy")]
        [DefaultValue(60f)]
        public float Buoyancy = 60f;
    }

    public class TNBKFloatBlockModuleBehaviour : BlockModuleBehaviour<TNBKFloatBlockModule>
    {
        public BlockBehaviour blockbehaviour;

        public MSlider WaterDepthSlider;
        public MToggle EraceBuoyancyToggle;
        public float WaterDepth = 0f;
        public float Buoyancy = 60f;

        public float Distance = 0f;

        public Rigidbody rigidbody;

        public Joint joint;
        public float JointStrength = 80000f;

        public int initwait = 0;
        public TNBKShipBaseModuleBehaviour ShipBaseModuleBehaviour;
        public BlockBehaviour ShipBaseBehaviour;
        public float ShipHealth = 10f;

        public override void SafeAwake()
        {
            base.SafeAwake();

            blockbehaviour = GetComponent<BlockBehaviour>();
            EraceBuoyancyToggle = blockbehaviour.AddToggle(Mod.isJapanese ? "撃沈時に浮力を消す" : "Erace Buoyancy", "erace-buoyancy", true);
        }

        public override void OnSimulateStart()
        {
            base.OnSimulateStart();

            //目標の水深を取得（水深0を想定、波の分を足しておく）
            WaterDepthSlider = GetSlider(Module.WaterDepthSlider);
            WaterDepth = WaterDepthSlider.Value;
            WaterDepth += 3f;

            Buoyancy = Module.Buoyancy;

            rigidbody = GetComponent<Rigidbody>();
            rigidbody.drag = 0.3f;

            //接続強度を強化
            joint = GetComponent<Joint>();
            joint.breakForce = JointStrength;
            joint.breakTorque = JointStrength;

        }

        public override void SimulateFixedUpdateHost()
        {
            base.SimulateFixedUpdateHost();

            if(Buoyancy == 0f)
            {
                return;
            }

            Distance = WaterDepth - transform.position.y;   //水面 - Y座標, 水面より下だと正

            if (Distance > 0f)
            {
                rigidbody.AddForce(UnityEngine.Vector3.up * Distance * Buoyancy);
            }

            //撃破時に浮力を消す
            if(ShipHealth < 1f)
            {
                Mod.Log("Ship Destroyed");

                if(EraceBuoyancyToggle.IsActive)
                {
                    Buoyancy = 0f;
                }
            }

            //初期化用

            if(initwait < 12)
            {
                initwait++;
                return;
            }

            if(initwait > 12)
            {
                ShipHealth = ShipBaseBehaviour.BlockHealth.health;

                return;
            }

            if(initwait == 12)
            {
                initwait++;

                //接続先を取得し、ShipBaseなら体力を取得
                ShipBaseModuleBehaviour = joint.connectedBody.gameObject.GetComponent<TNBKShipBaseModuleBehaviour>();
                ShipBaseBehaviour = joint.connectedBody.gameObject.GetComponent<BlockBehaviour>();

                if (!(ShipBaseModuleBehaviour == null))
                {
                    ShipHealth = ShipBaseBehaviour.BlockHealth.health;
                }
                else
                {
                    Buoyancy = 0f;
                }
            }
        }
    }
}
