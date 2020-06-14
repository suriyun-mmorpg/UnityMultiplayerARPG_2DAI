using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using LiteNetLibManager;

namespace MultiplayerARPG
{
    public class AstarCharacterMovement2D : RigidBodyEntityMovement2D
    {
        public IAstarAI CacheAIPath { get; private set; }
        public Seeker Seeker { get; private set; }
        protected bool remoteReachedEndOfPath = true;

        public bool reachedEndOfPath
        {
            get
            {
                if ((CacheEntity.MovementSecure == MovementSecure.ServerAuthoritative && IsServer) ||
                    (CacheEntity.MovementSecure == MovementSecure.NotSecure && IsOwnerClient))
                    return CacheAIPath.reachedEndOfPath;
                return remoteReachedEndOfPath;
            }
        }

        public override void EntityAwake()
        {
            base.EntityAwake();
            CacheAIPath = GetComponent<IAstarAI>();
            if (CacheAIPath == null)
            {
                CacheAIPath = gameObject.AddComponent<AILerp>();
                (CacheAIPath as AILerp).enableRotation = false;
            }
            Seeker = GetComponent<Seeker>();
        }

        public override void OnSetup()
        {
            base.OnSetup();
            RegisterNetFunction<bool>(NetFuncSetReachedEndOfPath);
            CacheNetTransform.onTeleport = (position, rotation) =>
            {
                CacheAIPath.Teleport(position);
            };
        }

        protected void NetFuncSetReachedEndOfPath(bool reachedEndOfPath)
        {
            remoteReachedEndOfPath = reachedEndOfPath;
        }

        public override void EntityUpdate()
        {
            if ((CacheEntity.MovementSecure == MovementSecure.ServerAuthoritative && !IsServer) ||
                    (CacheEntity.MovementSecure == MovementSecure.NotSecure && !IsOwnerClient))
            {
                (CacheAIPath as MonoBehaviour).enabled = false;
                return;
            }

            // Update reached end of path state
            CallNetFunction(NetFuncSetReachedEndOfPath, LiteNetLib.DeliveryMethod.Sequenced, FunctionReceivers.All, reachedEndOfPath);

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
                CacheAIPath.isStopped = false;
                CacheAIPath.destination = currentDestination.Value;
            }

            if (CacheAIPath.velocity.sqrMagnitude > 0.25f)
                CacheEntity.SetDirection2D(CacheAIPath.velocity.normalized);

            CacheEntity.SetMovement(CacheAIPath.velocity.sqrMagnitude > 0 ? MovementState.Forward : MovementState.None);
        }

        public override void SetLookRotation(Quaternion rotation)
        {
            if (CacheAIPath.velocity.sqrMagnitude == 0f)
                base.SetLookRotation(rotation);
        }
    }
}
