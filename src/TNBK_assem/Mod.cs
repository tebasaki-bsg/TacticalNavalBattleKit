using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.UI;
using Modding;
using Modding.Serialization;
using Modding.Modules;
using Modding.Blocks;
using Modding.Common;
using UnityEngine.SceneManagement;
using Localisation;

namespace TNBKSpace
{
	public class Mod : ModEntryPoint
	{
		public static GameObject TNBKMod;
		public static bool isJapanese = SingleInstance<LocalisationManager>.Instance.currLangName.Contains("日本語");

		public static GameObject MapObject;
		public static GameObject ShipIconPrefab;

		// メッセージ型(全マシンでOnLoad時に同一順序で生成される)
		public static MessageType MessageShipIdAssignType;
		public static MessageType MessageVisibilityType;

		// 大砲の弾にスクリプトを付けているか、OnSimulateStop()にてfalse
		public static bool CannonScriptAttached = false;

		public static List<Block> ShipBaseList = new List<Block>();

		public static DestructionBar destructionBar;	//ShipBaseが建築中SafeAwakeにて取得

		/// <summary>
		/// デバッグログ用の関数3種。主にLog()を使用
		/// <summary>
		public static void Log(string msg)
		{
			Debug.Log("MBS Log: " + msg);
		}
		public static void Warning(string msg)
		{
			Debug.LogWarning("MBS Warning: " + msg);
		}
		public static void Error(string msg)
		{
			Debug.LogError("MBS Error: " + msg);
		}

		/// <summary>
		/// Modロード時に一度だけ呼ばれる関数。
		/// 
		/// <summary>
		public override void OnLoad()
		{
			//TNBKModを作成、シーンチェンジしても壊さないように
			TNBKMod = new GameObject("TNBKMod");
			UnityEngine.Object.DontDestroyOnLoad(TNBKMod);

			//各ModuleとBehaviourをセットにし、XML上で使えるようにする。XMLからの読み込みとスクリプトの貼り付けはBesiege本体が行ってくれる。
			Modding.Modules.CustomModules.AddBlockModule<TNBKShipBaseModule, TNBKShipBaseModuleBehaviour>("TNBKShipBaseModule", true);
			Modding.Modules.CustomModules.AddBlockModule<TNBKFloatBlockModule, TNBKFloatBlockModuleBehaviour>("TNBKFloatBlockModule", true);
			Modding.Modules.CustomModules.AddBlockModule<TNBKTeamFlagModule, TNBKTeamFlagModuleBehaviour>("TNBKTeamFlagModule", true);

			//BlockSelectorを追加
			SingleInstance<BlockSelector>.Instance.transform.parent = TNBKMod.transform;

			//通信関係を初期化
			TNBKMapNetwork.Setup();

			//17番レイヤーの当たり判定を整備
			Physics.IgnoreLayerCollision(0, 17, false); //0: ブロック、マップオブジェクト、大砲の弾
			Physics.IgnoreLayerCollision(26, 17, false); //26: 一部ブロック
			Physics.IgnoreLayerCollision(29, 17, false);	//29: 床
		}

		public static class TNBKMapNetwork
		{
			public static MessageType ShipIdAssignType;
			public static MessageType VisibilityType;
			public static MessageType ProgressType;
			public static MessageType PinSetType;
			public static MessageType PinSnapshotType;

			/// <summary>既存Mod.csのOnLoad()から1回呼ぶ</summary>
			public static void Setup()
			{
				// ---- メッセージ型定義とCallback登録 ----
				ShipIdAssignType = ModNetworking.CreateMessageType(DataType.Block, DataType.Integer);
				ModNetworking.Callbacks[ShipIdAssignType] += new Action<Message>(OnAssignReceived);

				VisibilityType = ModNetworking.CreateMessageType(DataType.IntegerArray);
				ModNetworking.Callbacks[VisibilityType] += new Action<Message>(OnVisibilityReceived);

				ProgressType = ModNetworking.CreateMessageType(DataType.Integer, DataType.Integer);	//MPTeam(int), 達成度(int)
				ModNetworking.Callbacks[ProgressType] += new Action<Message>(OnProgressReceived);

				// ピン: プレイヤーが刺した座標(x, z)をホストへ
				PinSetType = ModNetworking.CreateMessageType(DataType.Single, DataType.Single);
				ModNetworking.Callbacks[PinSetType]	+= new Action<Message>(OnPinSetReceived);

				// ピン: ホストがチームへ配る全ピンスナップショット
				PinSnapshotType = ModNetworking.CreateMessageType(DataType.IntegerArray, DataType.SingleArray);
				ModNetworking.Callbacks[PinSnapshotType]	+= new Action<Message>(OnPinSnapshotReceived);


				// ---- 途中参加への対応表再送 ----
				Events.OnPlayerJoin += new Action<Player>(OnPlayerJoin);

				// ---- 常駐GameObject(シーン遷移で消えないように) ----
				GameObject go = new GameObject("TNBKMap");
				UnityEngine.Object.DontDestroyOnLoad(go);
				go.AddComponent<TNBKMapVisibilityHost>();
				go.AddComponent<TNBKMapRenderer>();
			}

			// ---- 受信ハンドラ ----

			public static void OnAssignReceived(Message message)
			{
				Block netBlock = (Block)message.GetData(0);
				ushort sessionId = (ushort)(int)message.GetData(1);   // boxされたintはint経由で

				if (netBlock == null) return;

				// クライアントのシミュレーション複製がまだ生成されておらず
				// InternalObjectが解決できない可能性があるため、失敗分は保留キューへ
				if (!TryRegisterFromNet(netBlock, sessionId))
					TNBKPendingAssigns.Enqueue(netBlock, sessionId);
			}

			/// <summary>Blockラッパーから登録を試みる。解決できなければfalse</summary>
			public static bool TryRegisterFromNet(Block netBlock, ushort sessionId)
			{
				BlockBehaviour bb = netBlock.InternalObject;
				if (bb == null) return false;

				// C6確認済み: 同一GameObjectへのGetComponentで取得。
				// 代替: (TNBKShipBaseModuleBehaviour)netBlock.BlockScript でも取得可能
				var module = bb.GetComponent<TNBKShipBaseModuleBehaviour>();
				if (module == null) return false;

				TNBKShipRegistry.Register(sessionId, bb, module, bb.Team);
				return true;
			}

			public static void OnVisibilityReceived(Message message)
			{
				int[] ids = (int[])message.GetData(0);
				TNBKMapVisibilityClient.ApplySnapshot(ids);
			}

			// プレイヤーが刺したピンをホストが受信 → 集約
			private static void OnPinSetReceived(Message message)
			{
				// ホストのみが受け取る想定(SendToHostで送られてくる)。
				// 送信者は message.Sender で取得できる(実APIのプロパティ名に合わせる)
				Player sender = message.Sender;
				if (sender == null) return;

				float x = (float)message.GetData(0);
				float z = (float)message.GetData(1);
				TNBKPinAuthority.SetPin(sender, x, z);
			}

			// ホストが配るピンスナップショットをクライアントが受信
			private static void OnPinSnapshotReceived(Message message)
			{
				int[] owners = (int[])message.GetData(0);
				float[] coords = (float[])message.GetData(1);
				TNBKPinClient.ApplySnapshot(owners, coords);
			}

			public static void OnPlayerJoin(Player player)
			{
				if (!StatMaster.isHosting) return;
				TNBKShipIdAuthority.ResendAllTo(player);
				// Assign受信側のRegisterはContainsKeyで弾くため、
				// 万一既存プレイヤーに重複して届いても壊れない(冪等)
			}

			//達成度を変更する関数
			public static void OnProgressReceived(Message message)
			{
				MPTeam team = (MPTeam)message.GetData(0);
				destructionBar.AddProgress(team, -1f * (int)message.GetData(1));
				
			}
		}

	}

	/// <summary>
	/// バニラブロック設置時に自作スクリプトを貼り付ける関数。
	/// この関数の変更はオーナーのみ可能。
	/// <summary>
	public class BlockSelector : SingleInstance<BlockSelector>
	{
		// ブロックのIDと、追加したいスクリプトを紐づけた辞書
		public Dictionary<string, Type> BlockDict = new Dictionary<string, Type>
		{
			//コアブロック
			{"StartingBlock", typeof(TNBKStartingBlockScript) },

			//カメラブロック
			{"CameraBlock", typeof(TNBKCameraBlockScript) },

			//大砲
			{"Cannon", typeof(TNBKCannonScript) },

			//ボム
			{"Bomb", typeof(TNBKBombScript) },

			//ボム
			{"NauticalScrew", typeof(TNBKNauticalScrewScript) }

		};

		// プロパティ
		// Guiと同様の呪文
		public override string Name
		{
			get
			{
				return "TNBKBlockSelector";
			}
		}

		public void Awake()
		{
			// ブロックを設置した場合に呼び出されるアクションに、AddScriptというメソッドを追加する
			Events.OnBlockInit += new Action<Block>(AddScript);
		}

		// ブロック設置時に、そのブロックに所定のスクリプトを貼り付ける関数
		public void AddScript(Block block)
		{
			BlockBehaviour internalObject = block.BuildingBlock.InternalObject;

			// そのブロックがスクリプトを貼り付けるべきブロックであるなら、貼り付ける
			if (BlockDict.ContainsKey(internalObject.name))
			{
				Type type = BlockDict[internalObject.name];
				try
				{
					// まだ所定のスクリプトが貼り付けられていない場合にのみ、貼り付ける
					if (internalObject.GetComponent(type) == null)
					{
						internalObject.gameObject.AddComponent(type);
					}
				}
				catch
				{
					Mod.Error("AddScript Error!");
				}
				return;
			}
		}


	}

	
}
