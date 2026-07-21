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

        public CanonBlock canonBlock;

        public void Awake()
        {
            
        }


    }



    //大砲の弾に追加するスクリプト
    public class TNBKCannonProjectileScript : MonoBehaviour
    {
        public LayerMask layerMask = 1 << 17;

        public Collider[] colliders;
        public BlockBehaviour blockBehaviour;
        public Rigidbody rigidbody;

        public UnityEngine.Vector3 prevPos;
        

        public void Awake()
        {
            //空気抵抗を0にする
            rigidbody = GetComponent<Rigidbody>();
            rigidbody.drag = 0f;
        }

        public void FixedUpdate()
        {
            //最後の場所を記録する（OnDisableの前に場所が移動してしまうため）
            prevPos = transform.position;
        }
        
        public void OnDisable()
        {
            //消えたとき（=炸裂する時）に周囲の船体を補足、HPを減らす

            colliders = Physics.OverlapSphere(prevPos, 5f * transform.localScale.x, layerMask);

            foreach(Collider col in colliders)
            {
                blockBehaviour = col.gameObject.transform.parent.transform.parent.GetComponent<CanonBlock>();
                blockBehaviour.BlockHealth.DamageBlock(1.6f * transform.localScale.x);
                blockBehaviour.BlockHealth.health --;

            }
        }

        
    }
}
