using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

namespace MultiplayerARPG
{
    public class AstarCharacterMovement2D : RigidBodyEntityMovement2D
    {
        public IAstarAI CacheAIPath { get; private set; }

        public override void EntityAwake()
        {
            base.EntityAwake();
            CacheAIPath = GetComponent<IAstarAI>();
            if (CacheAIPath == null)
            {
                CacheAIPath = gameObject.AddComponent<AILerp>();
                (CacheAIPath as AILerp).enableRotation = false;
            }
        }

        public override void EntityOnSetup()
        {
            base.EntityOnSetup();
            CacheNetTransform.onTeleport = (position, rotation) =>
            {
                CacheAIPath.Teleport(position);
            };
        }

        public override void EntityUpdate()
        {
            if (CacheEntity.MovementSecure == MovementSecure.ServerAuthoritative && !IsServer)
            {
                (CacheAIPath as MonoBehaviour).enabled = false;
                return;
            }

            if (CacheEntity.MovementSecure == MovementSecure.NotSecure && !IsOwnerClient)
            {
                (CacheAIPath as MonoBehaviour).enabled = false;
                return;
            }

            (CacheAIPath as MonoBehaviour).enabled = true;

            // Force set AILerp settings
            CacheAIPath.canMove = true;
            CacheAIPath.canSearch = true;
            CacheAIPath.maxSpeed = CacheEntity.GetMoveSpeed();
        }

        public override void KeyMovement(Vector3 moveDirection, MovementState movementState)
        {
            if (moveDirection.sqrMagnitude > 0.25f)
                PointClickMovement(CacheTransform.position + moveDirection);
        }

        public override void EntityFixedUpdate()
        {
            if (CacheEntity.MovementSecure == MovementSecure.ServerAuthoritative && !IsServer)
                return;

            if (CacheEntity.MovementSecure == MovementSecure.NotSecure && !IsOwnerClient)
                return;

            if (currentDestination.HasValue && CacheEntity.CanMove())
            {
                // Set destination to AI Path
                CacheAIPath.destination = currentDestination.Value;
            }

            if (CacheAIPath.velocity.sqrMagnitude > 0.25f)
                CacheEntity.SetDirection2D(CacheAIPath.velocity.normalized);

            CacheEntity.SetMovement(CacheAIPath.velocity.sqrMagnitude > 0 ? MovementState.Forward : MovementState.None);
        }
    }
}
