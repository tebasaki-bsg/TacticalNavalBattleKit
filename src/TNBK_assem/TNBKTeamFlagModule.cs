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
    [XmlRoot("TNBKTeamFlagModule")]
    [Reloadable]

    public class TNBKTeamFlagModule : BlockModule
    {

    }

    public class TNBKTeamFlagModuleBehaviour : BlockModuleBehaviour<TNBKTeamFlagModule>
    {
        public MMenu TeamMenu;
        public Material MyMaterial;

        public ModTexture NoneTex;
        public ModTexture RedTex;
        public ModTexture GreenTex;
        public ModTexture OrangeTex;
        public ModTexture BlueTex;

        public CapsuleCollider MyCollider;

        public ushort PlayerID = 0;
        public MPTeam team;

        public override void SafeAwake()
        {
            base.SafeAwake();

            MyMaterial = transform.Find("Vis").gameObject.GetComponent<MeshRenderer>().material;
            
            //各チームの画像を読み込み
            NoneTex = ModTexture.GetTexture("TeamNone");
            RedTex = ModTexture.GetTexture("TeamRed");
            GreenTex = ModTexture.GetTexture("TeamGreen");
            OrangeTex = ModTexture.GetTexture("TeamOrange");
            BlueTex = ModTexture.GetTexture("TeamBlue");

        }

        public override void OnSimulateStart()
        {
            base.OnSimulateStart();

            PlayerID = BlockBehaviour.ParentMachine.PlayerID;
            Player Player = Player.From(PlayerID);

            team = Player.Team;

            ChangeFlagColor(team);
            
            if(!StatMaster.isMP || StatMaster.isHosting || StatMaster.isLocalSim)
            {
                //コライダーの判定をオフに
                MyCollider = transform.Find("Colliders/Capsule Collider").GetComponent<CapsuleCollider>();
                MyCollider.enabled = false;
            }
            
        }

        //ブロックのテクスチャを変える関数
        public void ChangeFlagColor(MPTeam team)
        {
            switch (team)
            {
                case MPTeam.None:       MyMaterial.SetTexture("_MainTex", NoneTex);

                    break;

                case MPTeam.Red:        MyMaterial.SetTexture("_MainTex", RedTex);

                    break;

                case MPTeam.Green:      MyMaterial.SetTexture("_MainTex", GreenTex);

                    break;

                case MPTeam.Orange:     MyMaterial.SetTexture("_MainTex", OrangeTex);

                    break;

                case MPTeam.Blue:       MyMaterial.SetTexture("_MainTex", BlueTex);

                    break;
            }
        }
    }
}
