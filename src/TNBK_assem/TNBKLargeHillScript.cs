using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.ComponentModel;
using Modding;
using Modding.Blocks;
using Modding.Common;
using Modding.Modules;
using Modding.Levels;
using Modding.Serialization;
using UnityEngine;
using Localisation;

namespace TNBKSpace
{
    public class TNBKLargeHillScript : MonoBehaviour
    {
        public static List<Transform> LargeHillsList = new List<Transform>(); 

        //シミュ開始時にTNBKMapVisibilityHostから呼ぶ
        public void RegisterToList()
        {
            if (LargeHillsList.Contains(transform))
            {
                return;
            }

            LargeHillsList.Add(transform);
        }
    }
}
