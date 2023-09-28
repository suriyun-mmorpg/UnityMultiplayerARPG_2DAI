using UnityEngine;
using Pathfinding;
using LiteNetLibManager;

namespace MultiplayerARPG
{
    public class AstarCharacterMovement2D : RigidBodyEntityMovement2D
    {
        public IAstarAI CacheAIPath { get; private set; }
        public Seeker Seeker { get; private set; }
        protected bool _remoteReachedEndOfPath = true;

        public bool reachedEndOfPath
        {
            get
            {
                if ((movementSecure == MovementSecure.ServerAuthoritative && IsServer) ||
                    (movementSecure == MovementSecure.NotSecure && IsOwnerClient))
                    return CacheAIPath.reachedEndOfPath;
                return _remoteReachedEndOfPath;
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
            _remoteReachedEndOfPath = reachedEndOfPath;
        }

        public override void EntityUpdate()
        {
            UpdateMovement(Time.deltaTime);

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

        protected override void UpdateMovement(float deltaTime)
        {
            if (HasNavPaths && Entity.CanMove())
            {
                // Set destination to AI Path
                CacheAIPath.isStopped = false;
                CacheAIPath.destination = NavPaths.Peek();
            }
            else
            {
                // Character dead?
                CacheAIPath.isStopped = true;
            }

            // Change direction by move direction
            if (CacheAIPath.velocity.sqrMagnitude > 0.25f)
                Direction2D = CacheAIPath.velocity.normalized;

            if (IsOwnerClient || (IsServer && movementSecure == MovementSecure.ServerAuthoritative))
            {
                // Update movement state
                MovementState = (CacheAIPath.velocity.sqrMagnitude > 0 ? MovementState.Forward : MovementState.None) | MovementState.IsGrounded;
                // Update extra movement state
                ExtraMovementState = this.ValidateExtraMovementState(MovementState, _tempExtraMovementState);
            }

            // Set inputs
            if (CacheAIPath.velocity.sqrMagnitude > 0f)
            {
                _currentInput = Entity.SetInputMovementState2D(_currentInput, _tempMovementState);
                _currentInput = Entity.SetInputPosition(_currentInput, CacheAIPath.destination);
                _currentInput = Entity.SetInputIsKeyMovement(_currentInput, false);
            }
            _currentInput = Entity.SetInputDirection2D(_currentInput, Direction2D);
        }

        public override void SetLookRotation(Quaternion rotation)
        {
            if (CacheAIPath.velocity.sqrMagnitude > 0f)
                return;
            base.SetLookRotation(rotation);
        }

        protected override void OnTeleport(Vector2 position, bool stillMoveAfterTeleport)
        {
            base.OnTeleport(position, stillMoveAfterTeleport);
            CacheAIPath.Teleport(position);
        }
    }
}
