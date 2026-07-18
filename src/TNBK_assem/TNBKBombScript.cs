using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Modding;

namespace TNBKSpace
{
    public class TNBKBombScript : BlockScript
    {
        public LayerMask layerMask = 1 << 17;

        public Collider[] colliders;
        public BlockBehaviour blockBehaviour;

        public void OnDisable()
        {
            colliders = Physics.OverlapSphere(transform.position, 7f, layerMask);

            foreach (Collider col in colliders)
            {
                blockBehaviour = col.gameObject.transform.parent.transform.parent.GetComponent<BlockBehaviour>();
                blockBehaviour.BlockHealth.DamageBlock(4f);
            }
        }
    }
}
