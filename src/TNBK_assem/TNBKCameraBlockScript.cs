using System;
using System.Collections.Generic;
using Modding;
using Modding.Blocks;
using UnityEngine;
using Localisation;

namespace TNBKSpace
{
    public class TNBKCameraBlockScript : BlockScript
    {
        /// <summary>現在「使用されている」ローカルプレイヤーのカメラ(なければnull)。
        /// 同時使用は最大1つという仕様に対応するstatic参照</summary>
        public static TNBKCameraBlockScript ActiveLocalCamera;

        //カメラを管理しているやつ
        public FixedCameraController FixedCameraController;

        public Transform CompositeTracker3;

        private FixedCameraBlock fixedCam;
        private BlockBehaviour blockBehaviour;

        private TNBKShipBaseModuleBehaviour carrierShip;
        private bool carrierSearched;

        /// <summary>このカメラが搭載されている艦(未特定ならnull)</summary>
        public TNBKShipBaseModuleBehaviour CarrierShip { get { return carrierShip; } }

        public void Awake()
        {
            fixedCam = GetComponent<FixedCameraBlock>();
            blockBehaviour = GetComponent<BlockBehaviour>();
        }

        public void Start()
        {
            if(!blockBehaviour.isBuildBlock)
            {
                CompositeTracker3 = fixedCam.CompositeTracker3;

                //FixedCameraController（カメラブロックを管理してるやつ）を取得
                FixedCameraController = GameObject.Find("FixedCameraController").GetComponent<FixedCameraController>();

                SearchCarrierShip();
            }

            
        }

        public void Update()
        {
            
            // 観戦中はPlayerMachine.GetLocal()が動かず、また本人使用も発生しないため対象外
            if (StatMaster.PlayMode != BesiegePlayMode.GlobalSimulation) return;

            if(!carrierSearched)
            {
                return;
            }

            //fixedCam（カメラブロックのBehaviour）が無い or 使用中のカメラがfixedCamでない
            if (fixedCam == null || FixedCameraController.activeCamera == null || fixedCam.transform.position != FixedCameraController.activeCamera.transform.position)
            {
                if (ActiveLocalCamera == this) ActiveLocalCamera = null;
                return;
            }

            ActiveLocalCamera = this;
        }

        private void SearchCarrierShip()
        {
            carrierSearched = true;

            float bestSqr = float.MaxValue;

            foreach (Block block in Mod.ShipBaseList)
            {
                var ship = block.InternalObject.gameObject.GetComponent<TNBKShipBaseModuleBehaviour>();

                float sqr = (ship.transform.position - transform.position)
                    .sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    carrierShip = ship;
                }
            }
            // 見つからなかった場合はcarrierShip=nullのまま
            // (Renderer側がnullチェックして方向アイコンを描かない)
        }
    }
}
