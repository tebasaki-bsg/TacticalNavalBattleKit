using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TNBKSpace
{
    public static class TNBKMapSession
    {
        /// <summary>
        /// TNBKShipBaseModuleBehaviour.OnSimulateStop から呼ぶ(全艦分が多重発火するが
        /// 全処理が冪等なので無害)。加えてTNBKMapVisibilityHostのエッジ検出が
        /// フォールバックとして同じものを呼ぶ
        /// </summary>
        public static void OnSessionEnd()
        {
            TNBKShipRegistry.Clear();
            TNBKShipIdAuthority.Clear();        // ホスト以外で呼んでも無害
            TNBKMapVisibilityClient.Clear();
            TNBKPendingAssigns.Clear();
            TNBKCameraBlockScript.ActiveLocalCamera = null;
            TNBKPinAuthority.Clear();           // ホスト側のピン集約
            TNBKPinClient.Clear();              // 全マシンのピン表示
            if (TNBKMapVisibilityHost.Instance != null)
                TNBKMapVisibilityHost.Instance.ResetState();
        }
    }
}
