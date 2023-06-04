using LiteNetLib.Utils;
using LiteNetLibManager;
using Pathfinding;
using UnityEngine;

namespace MultiplayerARPG
{
    public class AstarCharacterMovement3D : BaseNetworkedGameEntityComponent<BaseGameEntity>, IEntityMovementComponent
    {
        protected static readonly RaycastHit[] s_findGroundRaycastHits = new RaycastHit[4];

        public IAstarAI CacheAIPath { get; private set; }
        public Seeker Seeker { get; private set; }
        [Header("Movement Settings")]
        [Range(0.01f, 1f)]
        public float stoppingDistance = 0.1f;
        public MovementSecure movementSecure = MovementSecure.NotSecure;

        [Header("Networking Settings")]
        public float moveThreshold = 0.01f;
        public float snapThreshold = 5.0f;

        public float StoppingDistance
        {
            get { return stoppingDistance; }
        }
        public MovementState MovementState { get; protected set; }
        public ExtraMovementState ExtraMovementState { get; protected set; }
        public DirectionVector2 Direction2D { get { return Vector2.down; } set { } }
        public float CurrentMoveSpeed { get { return CacheAIPath.isStopped ? 0f : CacheAIPath.maxSpeed; } }

        protected long _acceptedPositionTimestamp;
        protected float? _targetYRotation;
        protected float _yRotateLerpTime;
        protected float _yRotateLerpDuration;
        protected EntityMovementInput _oldInput;
        protected EntityMovementInput _currentInput;
        protected ExtraMovementState _tempExtraMovementState;
        protected bool _isTeleporting;

        public override void EntityAwake()
        {
            base.EntityAwake();
            CacheAIPath = GetComponent<IAstarAI>();
            if (CacheAIPath == null)
            {
                AIPath newPathComp = gameObject.AddComponent<AIPath>();
                newPathComp.orientation = OrientationMode.ZAxisForward;
                newPathComp.enableRotation = true;
                newPathComp.rotationSpeed = 800;
                CacheAIPath = newPathComp;
            }
            Seeker = GetComponent<Seeker>();
        }

        public void SetLookRotation(Quaternion rotation)
        {
            if (CacheAIPath.velocity.sqrMagnitude > 0f)
                return;
            if (!Entity.CanMove())
                return;
            if (CanPredictMovement())
            {
                // Always apply movement to owner client (it's client prediction for server auth movement)
                CacheTransform.rotation = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
                _currentInput = Entity.SetInputRotation(_currentInput, CacheTransform.rotation);
            }
        }

        public Quaternion GetLookRotation()
        {
            return Quaternion.Euler(0f, CacheTransform.eulerAngles.y, 0f);
        }

        public void SetSmoothTurnSpeed(float speed)
        {
            // TODO: MAY implement it later
        }

        public float GetSmoothTurnSpeed()
        {
            // TODO: MAY implement it later
            return 0f;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (!IsServer)
            {
                Logging.LogWarning(nameof(AstarCharacterMovement3D), $"Teleport function shouldn't be called at client [{name}]");
                return;
            }
            _isTeleporting = true;
            OnTeleport(position, rotation.eulerAngles.y);
        }

        public bool FindGroundedPosition(Vector3 fromPosition, float findDistance, out Vector3 result)
        {
            return PhysicUtils.FindGroundedPosition(fromPosition, s_findGroundRaycastHits, findDistance, GameInstance.Singleton.GetGameEntityGroundDetectionLayerMask(), out result, CacheTransform);
        }

        public void KeyMovement(Vector3 moveDirection, MovementState movementState)
        {
            if (!Entity.CanMove())
                return;
            if (moveDirection.sqrMagnitude <= 0)
                return;
            if (CanPredictMovement())
            {
                // Always apply movement to owner client (it's client prediction for server auth movement)
                SetMovePaths(CacheTransform.position + moveDirection);
                _currentInput = Entity.SetInputPosition(_currentInput, CacheTransform.position + moveDirection);
                _currentInput = Entity.SetInputIsKeyMovement(_currentInput, true);
            }
        }

        public void PointClickMovement(Vector3 position)
        {
            if (!Entity.CanMove())
                return;
            if (CanPredictMovement())
            {
                // Always apply movement to owner client (it's client prediction for server auth movement)
                SetMovePaths(position);
                _currentInput = Entity.SetInputPosition(_currentInput, position);
                _currentInput = Entity.SetInputIsKeyMovement(_currentInput, false);
            }
        }

        public void SetExtraMovementState(ExtraMovementState extraMovementState)
        {
            if (!Entity.CanMove())
                return;
            if (CanPredictMovement())
            {
                // Always apply movement to owner client (it's client prediction for server auth movement)
                _tempExtraMovementState = extraMovementState;
            }
        }

        public void StopMove()
        {
            StopMoveFunction();
        }

        private void StopMoveFunction()
        {
            CacheAIPath.isStopped = true;
        }

        public override void EntityUpdate()
        {
            // Force set AILerp settings
            CacheAIPath.canMove = true;
            CacheAIPath.canSearch = true;
            CacheAIPath.maxSpeed = Entity.GetMoveSpeed();
        }

        private void SetMovePaths(Vector3 position)
        {
            CacheAIPath.isStopped = false;
            CacheAIPath.destination = position;
        }

        public bool WriteClientState(NetDataWriter writer, out bool shouldSendReliably)
        {
            shouldSendReliably = false;
            if (IsOwnerClient || (IsServer && movementSecure == MovementSecure.ServerAuthoritative))
            {
                // Update movement state
                MovementState = (CacheAIPath.velocity.sqrMagnitude > 0 ? MovementState.Forward : MovementState.None) | MovementState.IsGrounded;
                // Update extra movement state
                ExtraMovementState = this.ValidateExtraMovementState(MovementState, _tempExtraMovementState);
            }
            if (movementSecure == MovementSecure.NotSecure && IsOwnerClient && !IsServer)
            {
                // Sync transform from owner client to server (except it's both owner client and server)
                this.ClientWriteSyncTransform3D(writer);
                return true;
            }
            if (movementSecure == MovementSecure.ServerAuthoritative && IsOwnerClient && !IsServer)
            {
                _currentInput = Entity.SetInputMovementState(_currentInput, MovementState);
                _currentInput = Entity.SetInputExtraMovementState(_currentInput, _tempExtraMovementState);
                if (Entity.DifferInputEnoughToSend(_oldInput, _currentInput, out EntityMovementInputState inputState))
                {
                    this.ClientWriteMovementInput3D(writer, inputState, _currentInput.MovementState, _currentInput.ExtraMovementState, _currentInput.Position, _currentInput.Rotation);
                    _oldInput = _currentInput;
                    _currentInput = null;
                    return true;
                }
            }
            return false;
        }

        public bool WriteServerState(NetDataWriter writer, out bool shouldSendReliably)
        {
            shouldSendReliably = false;
            if (_isTeleporting)
            {
                shouldSendReliably = true;
                MovementState |= MovementState.IsTeleport;
            }
            else
            {
                MovementState &= ~MovementState.IsTeleport;
            }
            // Sync transform from server to all clients (include owner client)
            this.ServerWriteSyncTransform3D(writer);
            _isTeleporting = false;
            return true;
        }

        public void ReadClientStateAtServer(NetDataReader reader)
        {
            switch (movementSecure)
            {
                case MovementSecure.NotSecure:
                    ReadSyncTransformAtServer(reader);
                    break;
                case MovementSecure.ServerAuthoritative:
                    ReadMovementInputAtServer(reader);
                    break;
            }
        }

        public void ReadServerStateAtClient(NetDataReader reader)
        {
            if (IsServer)
            {
                // Don't read and apply transform, because it was done at server
                return;
            }
            reader.ReadSyncTransformMessage3D(out MovementState movementState, out ExtraMovementState extraMovementState, out Vector3 position, out float yAngle, out long timestamp);
            if (movementState.Has(MovementState.IsTeleport))
            {
                // Server requested to teleport
                OnTeleport(position, yAngle);
            }
            else if (_acceptedPositionTimestamp <= timestamp)
            {
                if (Vector3.Distance(position, CacheTransform.position) >= snapThreshold)
                {
                    // Snap character to the position if character is too far from the position
                    if (movementSecure == MovementSecure.ServerAuthoritative || !IsOwnerClient)
                    {
                        CacheTransform.eulerAngles = new Vector3(0, yAngle, 0);
                        CacheTransform.position = position;
                        CurrentGameManager.ShouldPhysicSyncTransforms2D = true;
                    }
                    MovementState = movementState;
                    ExtraMovementState = extraMovementState;
                }
                else if (!IsOwnerClient)
                {
                    _targetYRotation = yAngle;
                    _yRotateLerpTime = 0;
                    _yRotateLerpDuration = Time.fixedDeltaTime;
                    if (Vector3.Distance(position.GetXZ(), CacheTransform.position.GetXZ()) > moveThreshold)
                    {
                        SetMovePaths(position);
                    }
                    MovementState = movementState;
                    ExtraMovementState = extraMovementState;
                }
                _acceptedPositionTimestamp = timestamp;
            }
        }

        public void ReadMovementInputAtServer(NetDataReader reader)
        {
            if (IsOwnerClient)
            {
                // Don't read and apply inputs, because it was done (this is both owner client and server)
                return;
            }
            if (movementSecure == MovementSecure.NotSecure)
            {
                // Movement handling at client, so don't read movement inputs from client (but have to read transform)
                return;
            }
            if (!Entity.CanMove())
                return;
            reader.ReadMovementInputMessage3D(out EntityMovementInputState inputState, out MovementState movementState, out ExtraMovementState extraMovementState, out Vector3 position, out float yAngle, out long timestamp);
            if (_acceptedPositionTimestamp <= timestamp)
            {
                if (!inputState.Has(EntityMovementInputState.IsStopped))
                {
                    _tempExtraMovementState = extraMovementState;
                    if (inputState.Has(EntityMovementInputState.PositionChanged))
                    {
                        SetMovePaths(position);
                    }
                    if (inputState.Has(EntityMovementInputState.RotationChanged))
                    {
                        if (IsClient)
                        {
                            _targetYRotation = yAngle;
                            _yRotateLerpTime = 0;
                            _yRotateLerpDuration = Time.fixedDeltaTime;
                        }
                        else
                        {
                            CacheTransform.eulerAngles = new Vector3(0, yAngle, 0);
                        }
                    }
                }
                else
                {
                    StopMoveFunction();
                }
                _acceptedPositionTimestamp = timestamp;
            }
        }

        public void ReadSyncTransformAtServer(NetDataReader reader)
        {
            if (IsOwnerClient)
            {
                // Don't read and apply transform, because it was done (this is both owner client and server)
                return;
            }
            if (movementSecure == MovementSecure.ServerAuthoritative)
            {
                // Movement handling at server, so don't read sync transform from client
                return;
            }
            reader.ReadSyncTransformMessage3D(out MovementState movementState, out ExtraMovementState extraMovementState, out Vector3 position, out float yAngle, out long timestamp);
            if (_acceptedPositionTimestamp <= timestamp)
            {
                CacheTransform.eulerAngles = new Vector3(0, yAngle, 0);
                if (Vector3.Distance(position.GetXZ(), CacheTransform.position.GetXZ()) > moveThreshold)
                {
                    if (!IsClient)
                    {
                        // If it's server only (not a host), set position follows the client immediately
                        CacheTransform.position = position;
                        CurrentGameManager.ShouldPhysicSyncTransforms2D = true;
                    }
                    else
                    {
                        // It's both server and client, translate position
                        SetMovePaths(position);
                    }
                }
                MovementState = movementState;
                ExtraMovementState = extraMovementState;
                _acceptedPositionTimestamp = timestamp;
            }
        }

        protected virtual void OnTeleport(Vector3 position, float yAngle)
        {
            CacheAIPath.isStopped = true;
            CacheAIPath.Teleport(position);
            CacheTransform.rotation = Quaternion.Euler(0, yAngle, 0);
        }

        public bool CanPredictMovement()
        {
            return Entity.IsOwnerClient || (Entity.IsOwnerClientOrOwnedByServer && movementSecure == MovementSecure.NotSecure) || (Entity.IsServer && movementSecure == MovementSecure.ServerAuthoritative);
        }
    }
}
