using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

namespace MultiplayerARPG
{
    public class AstarCharacterMovement2D : RigidBodyCharacterMovement2D
    {
        private IAstarAI cacheAIPath;
        public IAstarAI CacheAIPath
        {
            get
            {
                if (cacheAIPath == null)
                    cacheAIPath = GetComponent<IAstarAI>();
                if (cacheAIPath == null)
                {
                    cacheAIPath = gameObject.AddComponent<AILerp>();
                    (cacheAIPath as AILerp).enableRotation = false;
                }
                return cacheAIPath;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (movementSecure == MovementSecure.ServerAuthoritative && !IsServer)
                (CacheAIPath as MonoBehaviour).enabled = false;
            else if (movementSecure == MovementSecure.NotSecure && !IsOwnerClient)
                (CacheAIPath as MonoBehaviour).enabled = false;
            else
                (CacheAIPath as MonoBehaviour).enabled = true;

            // Force set AILerp settings
            CacheAIPath.canMove = true;
            CacheAIPath.canSearch = true;
            CacheAIPath.maxSpeed = gameplayRule.GetMoveSpeed(CacheCharacterEntity); ;
        }

        protected override void FixedUpdate()
        {
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

            SetMovementState(CacheAIPath.velocity.magnitude > 0 ? MovementState.Forward : MovementState.None);
        }

        public override void StopMove()
        {
            CacheAIPath.isStopped = true;
            base.StopMove();
        }
    }
}
