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
    //XML上での名前
    [XmlRoot("TNBKShipBaseModule")]
    [Reloadable]

    public class TNBKShipBaseModule : BlockModule
    {
        [XmlElement("ShipClass")]
        [DefaultValue(60f)]
        public int ShipClass = 0;
    }

    public class TNBKShipBaseModuleBehaviour : BlockModuleBehaviour<TNBKShipBaseModule>
    {
        public BasicInfo basicinfo;

        public Transform CollidersTransform;

        public int initwait = 0;

        public Rigidbody rigidbody;

        public Joint joint;
        public float JointStrength = 2400000f;

        public ShipClass ShipClass = 0;

        public ushort PlayerID = 0;

        public override void SafeAwake()
        {
            base.SafeAwake();

            if(BlockBehaviour.isBuildBlock)
            {
                //艦種を取得
                ShipClass = (ShipClass)Module.ShipClass;

                //コライダーのレイヤーを変更し、ブロックを付けれるように
                CollidersTransform = transform.Find("Colliders");

                foreach (Transform child in CollidersTransform)
                {
                    child.gameObject.layer = 12;    //Adding Point
                }

                

            }
            else
            {
                //Playerから自分のチームを取得し、BlockBehaviour.Teamに代入
                PlayerID = BlockBehaviour.ParentMachine.PlayerID;
                Player Player = Player.From(PlayerID);

                BlockBehaviour.Team = Player.Team;

                //ホストならこの艦を登録

                if (StatMaster.isHosting)
                {
                    TNBKShipIdAuthority.RegisterShip(this);
                }

                //カメラ用にShipBaseのListを作っておく
                Mod.ShipBaseList.Add(Block.From(BlockBehaviour));
            }
        }

        public override void OnSimulateStart()
        {
            base.OnSimulateStart();

            basicinfo = GetComponent<BasicInfo>();
            basicinfo.IgnoredByWater = true;

            joint = GetComponent<Joint>();
            joint.breakForce = JointStrength;
            joint.breakTorque = JointStrength;

            rigidbody = GetComponent<Rigidbody>();
            rigidbody.centerOfMass += new UnityEngine.Vector3(0, -10f, 0);

            //ホストかつマルチ時、大砲の弾にスクリプトを付ける
            if (!Mod.CannonScriptAttached && StatMaster.isHosting && StatMaster.isMP)
            {
                Transform ProjectilePoolTrans = GameObject.Find("PROJECTILES").transform.Find("Projectile Pool");
                foreach (Transform child in ProjectilePoolTrans)
                {
                    child.gameObject.AddComponent<TNBKCannonProjectileScript>();
                }

                Mod.CannonScriptAttached = true;
            }

            //クライアントの場合はこのタイミングでコライダーのレイヤーを変更
            if (StatMaster.isMP && !StatMaster.isHosting)
            {
                CollidersTransform = transform.Find("Colliders");

                foreach (Transform child in CollidersTransform)
                {
                    child.gameObject.layer = 17;    //ShipBaseのみが使用
                }
            }
        }

        //ホストの場合は接続処理を待ってからレイヤーを17番に変更
        public override void SimulateFixedUpdateHost()
        {
            base.SimulateFixedUpdateHost();

            if (initwait < 10)
            {
                initwait++;

                return;
            }

            if (initwait > 10)
            {
                return;
            }

            CollidersTransform = transform.Find("Colliders");

            foreach (Transform child in CollidersTransform)
            {
                child.gameObject.layer = 17;    //ShipBaseのみが使用
            }

            initwait++;

        }

        public override void OnSimulateStop()
        {
            base.OnSimulateStop();

            TNBKMapSession.OnSessionEnd();
            Mod.CannonScriptAttached = false;
        }
    }
}
