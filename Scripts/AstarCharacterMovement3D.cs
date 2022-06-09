using LiteNetLib.Utils;
using LiteNetLibManager;
using Pathfinding;
using UnityEngine;

namespace MultiplayerARPG
{
    public class AstarCharacterMovement3D : BaseNetworkedGameEntityComponent<BaseGameEntity>, IEntityMovementComponent
    {
        protected static readonly RaycastHit[] findGroundRaycastHits = new RaycastHit[25];

        public IAstarAI CacheAIPath { get; private set; }
        public Seeker Seeker { get; private set; }
        [Header("Movement Settings")]
        [Range(0.01f, 1f)]
        public float stoppingDistance = 0.1f;
        public float StoppingDistance
        {
            get { return stoppingDistance; }
        }
        public MovementState MovementState { get; protected set; }
        public ExtraMovementState ExtraMovementState { get; protected set; }
        public DirectionVector2 Direction2D { get { return Vector2.down; } set { } }
        public float CurrentMoveSpeed { get { return CacheAIPath.isStopped ? 0f : CacheAIPath.maxSpeed; } }

        [Header("Networking Settings")]
        public float moveThreshold = 0.01f;
        public float snapThreshold = 5.0f;

        protected long acceptedPositionTimestamp;
        protected float? targetYRotation;
        protected float yRotateLerpTime;
        protected float yRotateLerpDuration;
        protected EntityMovementInput oldInput;
        protected EntityMovementInput currentInput;
        protected ExtraMovementState tempExtraMovementState;
        protected bool isTeleporting;

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
            if (this.CanPredictMovement())
            {
                // Always apply movement to owner client (it's client prediction for server auth movement)
                CacheTransform.rotation = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
                currentInput = this.SetInputRotation(currentInput, CacheTransform.rotation);
            }
        }

        public Quaternion GetLookRotation()
        {
            return Quaternion.Euler(0f, CacheTransform.eulerAngles.y, 0f);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (!IsServer)
            {
                Logging.LogWarning("CharacterControllerEntityMovement", "Teleport function shouldn't be called at client [" + name + "]");
                return;
            }
            isTeleporting = true;
            OnTeleport(position, rotation.eulerAngles.y);
        }

        public bool FindGroundedPosition(Vector3 fromPosition, float findDistance, out Vector3 result)
        {
            return PhysicUtils.FindGroundedPosition(fromPosition, findGroundRaycastHits, findDistance, GameInstance.Singleton.GetGameEntityGroundDetectionLayerMask(), out result, CacheTransform);
        }

        public void KeyMovement(Vector3 moveDirection, MovementState movementState)
        {
            if (!Entity.CanMove())
                return;
            if (moveDirection.sqrMagnitude <= 0)
                return;
            if (this.CanPredictMovement())
            {
                // Always apply movement to owner client (it's client prediction for server auth movement)
                SetMovePaths(CacheTransform.position + moveDirection);
                currentInput = this.SetInputPosition(currentInput, CacheTransform.position + moveDirection);
                currentInput = this.SetInputIsKeyMovement(currentInput, true);
            }
        }

        public void PointClickMovement(Vector3 position)
        {
            if (!Entity.CanMove())
                return;
            if (this.CanPredictMovement())
            {
                // Always apply movement to owner client (it's client prediction for server auth movement)
                SetMovePaths(position);
                currentInput = this.SetInputPosition(currentInput, position);
                currentInput = this.SetInputIsKeyMovement(currentInput, false);
            }
        }

        public void SetExtraMovementState(ExtraMovementState extraMovementState)
        {
            if (!Entity.CanMove())
                return;
            if (this.CanPredictMovement())
            {
                // Always apply movement to owner client (it's client prediction for server auth movement)
                tempExtraMovementState = extraMovementState;
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
            if (IsOwnerClient || (IsServer && Entity.MovementSecure == MovementSecure.ServerAuthoritative))
            {
                // Update movement state
                MovementState = (CacheAIPath.velocity.sqrMagnitude > 0 ? MovementState.Forward : MovementState.None) | MovementState.IsGrounded;
                // Update extra movement state
                ExtraMovementState = this.ValidateExtraMovementState(MovementState, tempExtraMovementState);
            }
            if (Entity.MovementSecure == MovementSecure.NotSecure && IsOwnerClient && !IsServer)
            {
                // Sync transform from owner client to server (except it's both owner client and server)
                this.ClientWriteSyncTransform3D(writer);
                return true;
            }
            if (Entity.MovementSecure == MovementSecure.ServerAuthoritative && IsOwnerClient && !IsServer)
            {
                EntityMovementInputState inputState;
                if (this.DifferInputEnoughToSend(oldInput, currentInput, out inputState))
                {
                    currentInput = this.SetInputMovementState(currentInput, MovementState);
                    currentInput = this.SetInputExtraMovementState(currentInput, tempExtraMovementState);
                    this.ClientWriteMovementInput3D(writer, inputState, currentInput.MovementState, currentInput.ExtraMovementState, currentInput.Position, currentInput.Rotation);
                    oldInput = currentInput;
                    currentInput = null;
                    return true;
                }
            }
            return false;
        }

        public bool WriteServerState(NetDataWriter writer, out bool shouldSendReliably)
        {
            shouldSendReliably = false;
            if (isTeleporting)
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
            isTeleporting = false;
            return true;
        }

        public void ReadClientStateAtServer(NetDataReader reader)
        {
            switch (Entity.MovementSecure)
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
            MovementState movementState;
            ExtraMovementState extraMovementState;
            Vector3 position;
            float yAngle;
            long timestamp;
            reader.ReadSyncTransformMessage3D(out movementState, out extraMovementState, out position, out yAngle, out timestamp);
            if (acceptedPositionTimestamp < timestamp)
            {
                acceptedPositionTimestamp = timestamp;
                // Snap character to the position if character is too far from the position
                if (movementState.Has(MovementState.IsTeleport))
                {
                    OnTeleport(position, yAngle);
                }
                else if (Vector3.Distance(position, CacheTransform.position) >= snapThreshold)
                {
                    if (Entity.MovementSecure == MovementSecure.ServerAuthoritative || !IsOwnerClient)
                    {
                        CacheTransform.eulerAngles = new Vector3(0, yAngle, 0);
                        CacheTransform.position = position;
                    }
                    MovementState = movementState;
                    ExtraMovementState = extraMovementState;
                }
                else if (!IsOwnerClient)
                {
                    targetYRotation = yAngle;
                    yRotateLerpTime = 0;
                    yRotateLerpDuration = Time.fixedDeltaTime;
                    if (Vector3.Distance(position.GetXZ(), CacheTransform.position.GetXZ()) > moveThreshold)
                    {
                        SetMovePaths(position);
                    }
                    MovementState = movementState;
                    ExtraMovementState = extraMovementState;
                }
            }
        }

        public void ReadMovementInputAtServer(NetDataReader reader)
        {
            if (IsOwnerClient)
            {
                // Don't read and apply inputs, because it was done (this is both owner client and server)
                return;
            }
            if (Entity.MovementSecure == MovementSecure.NotSecure)
            {
                // Movement handling at client, so don't read movement inputs from client (but have to read transform)
                return;
            }
            if (!Entity.CanMove())
                return;
            EntityMovementInputState inputState;
            MovementState movementState;
            ExtraMovementState extraMovementState;
            Vector3 position;
            float yAngle;
            long timestamp;
            reader.ReadMovementInputMessage3D(out inputState, out movementState, out extraMovementState, out position, out yAngle, out timestamp);
            if (acceptedPositionTimestamp < timestamp)
            {
                if (!inputState.Has(EntityMovementInputState.IsStopped))
                {
                    tempExtraMovementState = extraMovementState;
                    if (inputState.Has(EntityMovementInputState.PositionChanged))
                    {
                        SetMovePaths(position);
                    }
                    if (inputState.Has(EntityMovementInputState.RotationChanged))
                    {
                        if (IsClient)
                        {
                            targetYRotation = yAngle;
                            yRotateLerpTime = 0;
                            yRotateLerpDuration = Time.fixedDeltaTime;
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
                acceptedPositionTimestamp = timestamp;
            }
        }

        public void ReadSyncTransformAtServer(NetDataReader reader)
        {
            if (IsOwnerClient)
            {
                // Don't read and apply transform, because it was done (this is both owner client and server)
                return;
            }
            if (Entity.MovementSecure == MovementSecure.ServerAuthoritative)
            {
                // Movement handling at server, so don't read sync transform from client
                return;
            }
            MovementState movementState;
            ExtraMovementState extraMovementState;
            Vector3 position;
            float yAngle;
            long timestamp;
            reader.ReadSyncTransformMessage3D(out movementState, out extraMovementState, out position, out yAngle, out timestamp);
            if (acceptedPositionTimestamp < timestamp)
            {
                CacheTransform.eulerAngles = new Vector3(0, yAngle, 0);
                if (Vector3.Distance(position.GetXZ(), CacheTransform.position.GetXZ()) > moveThreshold)
                {
                    if (!IsClient)
                    {
                        // If it's server only (not a host), set position follows the client immediately
                        CacheTransform.position = position;
                    }
                    else
                    {
                        // It's both server and client, translate position
                        SetMovePaths(position);
                    }
                }
                MovementState = movementState;
                ExtraMovementState = extraMovementState;
                acceptedPositionTimestamp = timestamp;
            }
        }

        protected virtual void OnTeleport(Vector3 position, float yAngle)
        {
            CacheAIPath.isStopped = true;
            CacheAIPath.Teleport(position);
            CacheTransform.rotation = Quaternion.Euler(0, yAngle, 0);
        }
    }
}
