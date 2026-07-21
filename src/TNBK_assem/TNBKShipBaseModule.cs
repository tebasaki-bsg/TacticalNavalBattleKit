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
        [DefaultValue(0)]
        public int ShipClass = 0;

        [XmlElement("ProgressSlider")]
        [RequireToValidate]
        public MSliderReference ProgressSlider;

        [XmlElement("HPSlider")]
        [RequireToValidate]
        public MSliderReference HPSlider;

        [XmlElement("RaderRangeSlider")]
        [RequireToValidate]
        public MSliderReference RaderRangeSlider;
    }

    public class TNBKShipBaseModuleBehaviour : BlockModuleBehaviour<TNBKShipBaseModule>
    {
        public MSlider progressSlider;
        public int Progress;

        public MSlider HPSlider;
        public float HP;

        public MSlider RaderRangeSlider;
        public float RaderRange;

        //反転用
        public MToggle ReverseToggle;
        public bool Reversed;

        public BasicInfo basicinfo;
        public Transform CollidersTransform;

        public int initwait = 0;

        public Rigidbody rigidbody;

        public Joint joint;
        public float JointStrength = 2400000f;

        public ShipClass ShipClass = 0;

        public ushort PlayerID = 0;

        public bool isDestroyed = false;

        public BlockBehaviour blockBehaviour;
        public DestructionBar destructionBar;

        public override void SafeAwake()
        {
            base.SafeAwake();

            

            if (BlockBehaviour.isBuildBlock)
            {
                //艦種を取得
                ShipClass = (ShipClass)Module.ShipClass;

                //コライダーのレイヤーを変更し、ブロックを付けれるように
                CollidersTransform = transform.Find("Colliders");

                foreach (Transform child in CollidersTransform)
                {
                    child.gameObject.layer = 12;    //Adding Point
                }

                //達成度のバーを取得
                destructionBar = GameObject.Find("HUD").transform.Find("ProgressBar").gameObject.GetComponent<DestructionBar>();
                Mod.destructionBar = destructionBar;

                //反転モード用のトグルを追加
                ReverseToggle = BlockBehaviour.AddToggle(Mod.isJapanese ? "反転" : "Reverse Body", "reverse", false);
                ReverseToggle.DisplayInMapper = true;
                ReverseToggle.Toggled += ApplyReverse;
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

            progressSlider = GetSlider(Module.ProgressSlider);
            Progress = (int)progressSlider.Value;

            RaderRangeSlider = GetSlider(Module.RaderRangeSlider);
            RaderRange = RaderRangeSlider.Value;

            //HPを取得し変更
            HPSlider = GetSlider(Module.HPSlider);
            BlockBehaviour.BlockHealth.maxHealth = HPSlider.Value;
            BlockBehaviour.BlockHealth.health = HPSlider.Value;
            HP = HPSlider.Value;

            //ホストかつマルチ時、大砲の弾にスクリプトを付ける
            if (!Mod.CannonScriptAttached && StatMaster.isHosting && StatMaster.isMP)
            {
                Transform ProjectilePoolTrans = GameObject.Find("PROJECTILES").transform.Find("Projectile Pool");
                foreach (Transform child in ProjectilePoolTrans)
                {
                    if(child.gameObject.GetComponent<TNBKCannonProjectileScript>() == null)
                    {
                        child.gameObject.AddComponent<TNBKCannonProjectileScript>();
                    }
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

            //撃沈された場合の処理（達成度を一定数減らす）
            if(BlockBehaviour.BlockHealth.health <= 0f && !isDestroyed)
            {
                //達成度を減らす
                destructionBar.AddProgress(BlockBehaviour.Team, -1f * Progress);

                //クライアントに達成度を減らす命令を送る
                Message msg = Mod.TNBKMapNetwork.ProgressType.CreateMessage((int)BlockBehaviour.Team, Progress);
                ModNetworking.SendToAll(msg);

                isDestroyed = true;
            }

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
            Mod.ShipBaseList.Clear();

            TNBKMapRenderer.MapVisible = false;

        }

        //反転を適用させる関数
        public void ApplyReverse(bool value)
        {
            //オフの時
            if(!value)
            {
                //既に通常化していたらスルー
                if(!Reversed)
                {
                    Reversed = value;

                    return;
                }
                //されていなければ180°回転
                else
                {
                    Transform VisTrans = transform.Find("Vis");
                    Transform ColsTrans = transform.Find("Colliders");

                    //回転させる
                    VisTrans.rotation = VisTrans.rotation * new Quaternion(0, 1, 0, 0);
                    ColsTrans.rotation = ColsTrans.rotation * new Quaternion(0, 1, 0, 0);

                    Reversed = value;
                }
            }
            //オンの時
            else
            {
                //すでに反転されていたらスルー
                if (Reversed)
                {
                    Reversed = value;

                    return;
                }
                else
                {
                    Transform VisTrans = transform.Find("Vis");
                    Transform ColsTrans = transform.Find("Colliders");

                    //回転させる
                    VisTrans.rotation = VisTrans.rotation * new Quaternion(0, 1, 0, 0);
                    ColsTrans.rotation = ColsTrans.rotation * new Quaternion(0, 1, 0, 0);

                    Reversed = value;
                }


            }
        }
    }
}
