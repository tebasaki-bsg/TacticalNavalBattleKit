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
using UnityEngine.UI;
using Localisation;

namespace TNBKSpace
{
    //XML上での名前
    [XmlRoot("TNBKHUDProjectorModule")]
    [Reloadable]

    public class TNBKHUDProjectorModule : BlockModule
    {
        [XmlElement("ScaleSlider")]
        [RequireToValidate]
        public MSliderReference ScaleSlider;

        [XmlElement("AlphaSlider")]
        [RequireToValidate]
        public MSliderReference AlphaSlider;

        [XmlElement("EnableKey")]
        [RequireToValidate]
        public MKeyReference EnableKey;
    }

    public class TNBKHUDProjectorModuleBehaviour : BlockModuleBehaviour<TNBKHUDProjectorModule>
    {
        public static GameObject CullentHUDObject;
        public static Texture2D CullentHUDTex;
        public static Sprite CullentHUDSprite;
        public static Image CullentHUDImage;
        public static RectTransform CullentHUDRectTransform;

        public static BlockBehaviour CullentHUDBlock;

        public MSlider ScaleSlider;
        public MSlider AlphaSlider;
        public MColourSlider ColorSlider;
        public MKey EnableKey;

        public Color color = new Color(1f, 1f, 1f, 1f);

        public MToggle OpenFolderToggle;
        public MToggle ApplyTextureToggle;

        public MText PngName;

        public Texture2D HUDTex;

        public bool isOwnerSame = false;

        public bool BuildInit = false;

        public Joint joint;
        public float JointStrength = 80000f;

        public override void SafeAwake()
        {
            base.SafeAwake();

            //フォルダを開くトグルボタンを作る
            OpenFolderToggle = BlockBehaviour.AddToggle(Mod.isJapanese ? "画像フォルダを開く" : "Open Folder", "OP-Folder", false);
            OpenFolderToggle.DisplayInMapper = true;
            OpenFolderToggle.Toggled += OpenFolder;

            //設定欄に画像の名前を追加
            PngName = BlockBehaviour.AddText(Mod.isJapanese ? "画像名" : "PNG name", "png-name", "");
            PngName.DisplayInMapper = true;

            //色用（多分乗算）
            ColorSlider = BlockBehaviour.AddColourSlider("Color", "png-color", new Color(1f, 1f, 1f, 1f), false);

            //建築中⇒HUDオブジェクトが無ければ作る
            if(BlockBehaviour.isBuildBlock)
            {
                UpdateOwnerFlag();

                if(!isOwnerSame)
                {
                    return;
                }

                if (CullentHUDObject == null)
                {
                    //画像用のオブジェクトを生成
                    CullentHUDObject = new GameObject("TNBK_HUD");
                    CullentHUDObject.transform.SetParent(Mod.TNBKMod.transform);   //Modオブジェクトの子に
                    CullentHUDObject.layer = 12;   //HUD
                    CullentHUDObject.SetActive(false);
                    
                    //Spriteを作成し、CullentHUDTexにそのSpriteのテクスチャを代入
                    //デフォ画像を拾ってくる
                    Texture2D tex = ModTexture.GetTexture("DefaultHUD");
                    CullentHUDSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    CullentHUDTex = CullentHUDSprite.texture;

                    //Imageコンポーネントを追加し、Spriteに作ったSpriteを割り当て
                    CullentHUDImage = CullentHUDObject.AddComponent<Image>();
                    CullentHUDImage.sprite = CullentHUDSprite;

                    //RectTransformを追加（画像用のtransform）
                    CullentHUDRectTransform = CullentHUDObject.GetComponent<RectTransform>();
                    CullentHUDRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    CullentHUDRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    CullentHUDRectTransform.sizeDelta = new Vector2(CullentHUDTex.width, CullentHUDTex.height);
                    CullentHUDRectTransform.anchoredPosition = Vector2.zero;
                }


            }
        }

        public void Start()
        {
            if(BlockBehaviour.isBuildBlock)
            {
                BuildInit = true;
            }
        }

        public override void OnSimulateStart()
        {
            base.OnSimulateStart();

            if (!isOwnerSame)
            {
                return;
            }

            //画像読み込み
            ReadTexture();

            ScaleSlider = GetSlider(Module.ScaleSlider);
            AlphaSlider = GetSlider(Module.AlphaSlider);
            EnableKey = GetKey(Module.EnableKey);

            color = new Color(ColorSlider.Value.r, ColorSlider.Value.r, ColorSlider.Value.r, AlphaSlider.Value);

            if(!StatMaster.isMP || StatMaster.isHosting || StatMaster.isLocalSim)
            {
                joint = GetComponent<Joint>();
                joint.breakForce = JointStrength;
                joint.breakTorque = JointStrength;
            }
        }

        public override void SimulateUpdateAlways()
        {
            base.SimulateUpdateAlways();

            if (!isOwnerSame)
            {
                return;
            }

            if (EnableKey.IsPressed || EnableKey.EmulationPressed())
            {
                ChangeTexture(HUDTex);
            }
        }

        public override void OnSimulateStop()
        {
            base.OnSimulateStop();

            CullentHUDObject.SetActive(false);
        }

        //プレイヤーのIDとブロックの親のIDを比べる関数
        private void UpdateOwnerFlag()
        {
            ushort OwnerID;
            ushort BlockPlayerID;

            if (StatMaster.isMP)
            {
                BlockPlayerID = BlockBehaviour.ParentMachine.PlayerID;

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

        //画像フォルダを開く関数
        public void OpenFolder(bool value)
        {
            //建築中かつトグルのデフォルト設定が終わっていたらフォルダを開く
            if (BlockBehaviour.isBuildBlock && BuildInit)
            {
                Modding.ModIO.OpenFolderInFileBrowser(Mod.FolderName, true);
                OpenFolderToggle.IsActive = false;
            }
        }

        //画像を読み込む関数
        public void ReadTexture()
        {
            try
            {
                byte[] data = Modding.ModIO.ReadAllBytes(Mod.path + "/" + PngName.Value + ".png", true);
                HUDTex = new Texture2D(256, 256, TextureFormat.RGBA32, false, true);
                HUDTex.LoadImage(data);
                HUDTex.wrapMode = TextureWrapMode.Clamp;
            }
            catch
            {
                Mod.Warning("Failed to get texture. Please check PNG file name");
                HUDTex = ModTexture.GetTexture("DefaultHUD");
            }

        }

        //表示中の画像を変える関数
        public void ChangeTexture(Texture2D tex)
        {
            //HUDを使用中のブロックが自身でない場合（画像で判断すると同じ画像で違うブロックの場合にオフにしてしまう）
            if(CullentHUDBlock == null || CullentHUDBlock != BlockBehaviour)
            {
                CullentHUDBlock = BlockBehaviour;

                //新しくスプライトを作る
                CullentHUDSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                CullentHUDImage.sprite = CullentHUDSprite;

                //色を変える
                CullentHUDImage.color = color;

                //画像を更新
                CullentHUDTex = tex;

                //大きさを新しい画像に合わせる
                CullentHUDRectTransform.sizeDelta = new Vector2(CullentHUDTex.width * ScaleSlider.Value, CullentHUDTex.height * ScaleSlider.Value);

                CullentHUDObject.SetActive(true);
            }
            //HUDを使用中のブロックが自身の場合
            else
            {
                //デフォ画像を使用する場合も大きさ・色は変わっていないため先に変える

                //大きさの適用
                CullentHUDRectTransform.sizeDelta = new Vector2(CullentHUDTex.width * ScaleSlider.Value, CullentHUDTex.height * ScaleSlider.Value);

                //色を変える
                CullentHUDImage.color = color;

                CullentHUDObject.SetActive(!CullentHUDObject.activeInHierarchy);
            }
        }

    }
}
