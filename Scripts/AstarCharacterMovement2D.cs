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
                AILerp newPathComp = gameObject.AddComponent<AILerp>();
                newPathComp.orientation = OrientationMode.YAxisForward;
                newPathComp.enableRotation = false;
                CacheAIPath = newPathComp;
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
            // Update reached end of path state
            CallNetFunction(NetFuncSetReachedEndOfPath, 0, LiteNetLib.DeliveryMethod.Sequenced, FunctionReceivers.All, reachedEndOfPath);

            // Force set AILerp settings
            CacheAIPath.canMove = true;
            CacheAIPath.canSearch = true;
            CacheAIPath.maxSpeed = Entity.GetMoveSpeed();
        }

        public override void KeyMovement(Vector3 moveDirection, MovementState movementState)
        {
            if (moveDirection.sqrMagnitude <= 0.25f)
                return;
            PointClickMovement(CacheTransform.position + moveDirection);
        }

        public override void EntityFixedUpdate()
        {
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
            if (IsOwnerClient || (IsServer && Entity.MovementSecure == MovementSecure.ServerAuthoritative))
            {
                // Update movement state
                MovementState = (CacheAIPath.velocity.sqrMagnitude > 0 ? MovementState.Forward : MovementState.None) | MovementState.IsGrounded;
                // Update extra movement state
                ExtraMovementState = this.ValidateExtraMovementState(MovementState, tempExtraMovementState);
            }
            SyncTransform();
        }

        public override void SetLookRotation(Quaternion rotation)
        {
            if (CacheAIPath.velocity.sqrMagnitude > 0f)
                return;
            base.SetLookRotation(rotation);
        }

        protected override void OnTeleport(Vector2 position)
        {
            base.OnTeleport(position);
            CacheAIPath.Teleport(position);
        }
    }
}
