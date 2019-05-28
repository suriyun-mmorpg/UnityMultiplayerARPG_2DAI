using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using UnityEngine.Profiling;

namespace MultiplayerARPG
{
    [RequireComponent(typeof(AIPath))]
    public class PlayerCharacterEntity2DAI : PlayerCharacterEntity2D
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

        protected override void EntityUpdate()
        {
            // Force set AILerp settings
            CacheAIPath.isStopped = IsDead();
            CacheAIPath.canMove = true;
            CacheAIPath.canSearch = true;
            CacheAIPath.enableRotation = false;
            CacheAIPath.maxSpeed = CacheMoveSpeed;
            CacheAIPath.orientation = OrientationMode.YAxisForward;
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
                CacheAIPath.destination = currentDestination.Value;
                UpdateCurrentDirection(CacheAIPath.steeringTarget);
            }

            if (CacheAIPath.reachedDestination)
            {
                // No movement so state is none
                SetMovementState(MovementState.None);
            }
            else
            {
                // For 2d, just define that it is moving so can use any state
                SetMovementState(MovementState.Forward);
            }

            Profiler.EndSample();
        }
    }
}
