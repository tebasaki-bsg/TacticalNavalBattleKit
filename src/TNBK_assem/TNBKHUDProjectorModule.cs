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

                    //Spriteを作成し、CullentHUDTexにそのSpriteのテクスチャを代入
                    //デフォ画像を拾ってくる
                    Texture2D tex = ModTexture.GetTexture("DefaultHUD");
                    CullentHUDSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    CullentHUDTex = CullentHUDSprite.texture;

                    //Imageコンポーネントを追加し、Spriteに作ったSpriteを割り当て
                    CullentHUDImage = CullentHUDObject.AddComponent<Image>();
                    CullentHUDImage.sprite = CullentHUDSprite;
                }
            }
            //シミュ中⇒画像を読み込む
            else
            {
                if(!isOwnerSame)
                {
                    return;
                }

                //画像読み込み
                ReadTexture();
            }
        }

        public override void OnSimulateStart()
        {
            base.OnSimulateStart();

            if (!isOwnerSame)
            {
                return;
            }

            ScaleSlider = GetSlider(Module.ScaleSlider);
            AlphaSlider = GetSlider(Module.AlphaSlider);
            EnableKey = GetKey(Module.EnableKey);

            color = new Color(ColorSlider.Value.r, ColorSlider.Value.r, ColorSlider.Value.r, AlphaSlider.Value);
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
            if (BlockBehaviour.isBuildBlock)
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
            //HUDの画像が同じ場合：オンオフを変える
            if(CullentHUDTex == tex)
            {
                CullentHUDObject.SetActive(!CullentHUDObject.activeInHierarchy);
            }
            //違う場合：画像を変え、オンにする
            else
            {
                //新しくスプライトを作る
                CullentHUDSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                //CullentHUDImage.sprite = CullentHUDSprite;

                //色を変える
                CullentHUDImage.color = color;

                CullentHUDObject.SetActive(true);

                //画像を更新
                CullentHUDTex = tex;
            }
        }

    }
}
