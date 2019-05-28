using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using UnityEngine.Profiling;

namespace MultiplayerARPG
{
    public class PlayerCharacterEntity2DAI : PlayerCharacterEntity2D
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

        protected override void EntityUpdate()
        {
            if (movementSecure == MovementSecure.ServerAuthoritative && !IsServer)
                (CacheAIPath as MonoBehaviour).enabled = false;
            else if (movementSecure == MovementSecure.NotSecure && !IsOwnerClient)
                (CacheAIPath as MonoBehaviour).enabled = false;
            else
                (CacheAIPath as MonoBehaviour).enabled = true;

            // Force set AILerp settings
            CacheAIPath.canMove = true;
            CacheAIPath.canSearch = true;
            CacheAIPath.maxSpeed = CacheMoveSpeed;
            base.EntityUpdate();
        }

        protected override void EntityFixedUpdate()
        {
            Profiler.BeginSample("PlayerCharacterEntity2DAI - FixedUpdate");

            if (movementSecure == MovementSecure.ServerAuthoritative && !IsServer)
                return;

            if (movementSecure == MovementSecure.NotSecure && !IsOwnerClient)
                return;

            if (currentDestination.HasValue && !IsDead())
            {
                // Set destination to AI Path
                CacheAIPath.isStopped = false;
                CacheAIPath.destination = currentDestination.Value;
                if (CacheAIPath.velocity.magnitude > 0)
                    UpdateCurrentDirection(CacheAIPath.velocity.normalized);
            }

            if (CacheAIPath.velocity.magnitude > 0)
            {
                // For 2d, just define that it is moving so can use any state
                SetMovementState(MovementState.Forward);
            }
            else
            {
                // No movement so state is none
                SetMovementState(MovementState.None);
            }

            Profiler.EndSample();
        }

        public override void StopMove()
        {
            CacheAIPath.isStopped = true;
            base.StopMove();
        }
    }
}
