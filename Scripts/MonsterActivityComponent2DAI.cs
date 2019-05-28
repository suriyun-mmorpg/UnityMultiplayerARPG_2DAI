using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using UnityEngine.Profiling;

namespace MultiplayerARPG
{
    [RequireComponent(typeof(AIPath))]
    public class MonsterActivityComponent2DAI : MonsterActivityComponent2D
    {
        private AIPath cacheAIPath;
        public AIPath CacheAIPath
        {
            get
            {
                if (cacheAIPath == null)
                    cacheAIPath = GetComponent<AIPath>();
                return cacheAIPath;
            }
        }

        protected new void FixedUpdate()
        {
            if (!CacheMonsterCharacterEntity.IsServer)
                return;

            if (isStopped)
            {
                CacheAIPath.isStopped = true;
                CacheRigidbody2D.velocity = Vector2.zero;
                return;
            }

            if (currentDestination.HasValue)
            {
                // Set destination to AI Path
                CacheAIPath.destination = currentDestination.Value;
                CacheMonsterCharacterEntity.UpdateCurrentDirection(CacheAIPath.steeringTarget);
            }
        }
    }
}
