using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using UnityEngine.Profiling;

namespace MultiplayerARPG
{
    public class MonsterActivityComponent2DAI : MonsterActivityComponent2D
    {
        private IAstarAI cacheAIPath;
        public IAstarAI CacheAIPath
        {
            get
            {
                if (cacheAIPath == null)
                    cacheAIPath = GetComponent<IAstarAI>();
                return cacheAIPath;
            }
        }

        protected new void Update()
        {
            UpdateActivity(Time.unscaledTime);

            if (!CacheMonsterCharacterEntity.IsServer)
                return;

            if (CacheAIPath.velocity.magnitude > 0)
                CacheMonsterCharacterEntity.MovementState = MovementState.Forward | MovementState.IsGrounded;
            else
                CacheMonsterCharacterEntity.MovementState = MovementState.IsGrounded;
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
                CacheAIPath.isStopped = false;
                CacheAIPath.destination = currentDestination.Value;
                if (CacheAIPath.velocity.magnitude > 0)
                    CacheMonsterCharacterEntity.UpdateCurrentDirection(CacheAIPath.velocity.normalized);
            }
        }
    }
}
