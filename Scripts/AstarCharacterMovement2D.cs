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
                if ((Entity.MovementSecure == MovementSecure.ServerAuthoritative && IsServer) ||
                    (Entity.MovementSecure == MovementSecure.NotSecure && IsOwnerClient))
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
        }

        protected void NetFuncSetReachedEndOfPath(bool reachedEndOfPath)
        {
            remoteReachedEndOfPath = reachedEndOfPath;
        }

        public override void EntityUpdate()
        {
            if ((Entity.MovementSecure == MovementSecure.ServerAuthoritative && !IsServer) ||
                    (Entity.MovementSecure == MovementSecure.NotSecure && !IsOwnerClient))
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
            CacheAIPath.maxSpeed = Entity.GetMoveSpeed();
        }

        public override void KeyMovement(Vector3 moveDirection, MovementState movementState)
        {
            if (moveDirection.sqrMagnitude > 0.25f)
                PointClickMovement(CacheTransform.position + moveDirection);
        }

        public override void EntityFixedUpdate()
        {
            if (Entity.MovementSecure == MovementSecure.ServerAuthoritative && !IsServer)
                return;

            if (Entity.MovementSecure == MovementSecure.NotSecure && !IsOwnerClient)
                return;

            if (currentDestination.HasValue && Entity.CanMove())
            {
                // Set destination to AI Path
                CacheAIPath.isStopped = false;
                CacheAIPath.destination = currentDestination.Value;
            }
            else
            {
                // Character dead?
                CacheAIPath.isStopped = true;
            }

            if (CacheAIPath.velocity.sqrMagnitude > 0.25f)
                Entity.SetDirection2D(CacheAIPath.velocity.normalized);

            Entity.SetMovement(CacheAIPath.velocity.sqrMagnitude > 0 ? MovementState.Forward : MovementState.None);
        }

        public override void SetLookRotation(Quaternion rotation)
        {
            if (CacheAIPath.velocity.sqrMagnitude == 0f)
                base.SetLookRotation(rotation);
        }

        protected override void OnTeleport(Vector2 position)
        {
            base.OnTeleport(position);
            CacheAIPath.Teleport(position);
        }
    }
}
