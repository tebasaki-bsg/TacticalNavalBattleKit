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

namespace TNBKSpace
{
    public class TNBKCannonScript : BlockScript
    {
        public GameObject ParticleObject;
        public GameObject ParticlePrefab;

        public void Awake()
        {
            
        }
    }

    public class TNBKCannonProjectileScript : MonoBehaviour
    {
        public LayerMask layerMask = 1 << 17;

        public Collider[] colliders;
        public BlockBehaviour blockBehaviour;

        public UnityEngine.Vector3 prevpos;

        public void FixedUpdate()
        {
            prevpos = transform.position;
        }

        //消えたとき（=炸裂する時）に周囲の船体を補足、HPを減らす
        public void OnDisable()
        {
            colliders = Physics.OverlapSphere(prevpos, 5f * transform.localScale.x, layerMask);

            foreach(Collider col in colliders)
            {
                blockBehaviour = col.gameObject.transform.parent.transform.parent.GetComponent<BlockBehaviour>();
                blockBehaviour.BlockHealth.DamageBlock(1f);
                blockBehaviour.BlockHealth.health --;

            }
        }
    }
}
