﻿using UnityEngine;
using Pathfinding;
using LiteNetLibManager;

namespace MultiplayerARPG
{
    public class AstarCharacterMovement2D : RigidBodyEntityMovement2D
    {
        public Seeker Seeker { get; private set; }

        protected SyncFieldBool syncReachedEndOfPath = new SyncFieldBool()
        {
            syncMode = LiteNetLibSyncFieldMode.ClientMulticast,
        };
        protected float _nodeSize;
        protected Vector3? _endOfPathPosition;

        public bool ReachedEndOfPath => syncReachedEndOfPath.Value;

        public override void EntityAwake()
        {
            base.EntityAwake();
            Seeker = GetComponent<Seeker>();
            Seeker.startEndModifier.exactStartPoint = StartEndModifier.Exactness.SnapToNode;
            Seeker.startEndModifier.exactEndPoint = StartEndModifier.Exactness.SnapToNode;
            Seeker.pathCallback += OnPathComplete;
        }

        public override void EntityOnDestroy()
        {
            base.EntityOnDestroy();
            Seeker.pathCallback -= OnPathComplete;
        }

        protected void OnPathComplete(Path _p)
        {
            NavPaths = null;
            NavPaths = new System.Collections.Generic.Queue<Vector2>();
            GraphNode node;
            Vector3 nodePosition;
            for (int i = 0; i < _p.path.Count; ++i)
            {
                node = _p.path[i];
                nodePosition = (Vector3)node.position;
                NavPaths.Enqueue(nodePosition);
                if (i == 0 && node.Graph is GridGraph gridGraph)
                {
                    _nodeSize = gridGraph.nodeSize;
                    if (Vector3.Distance(nodePosition, Entity.MovementTransform.position) < _nodeSize)
                        NavPaths.Dequeue();
                }
                _endOfPathPosition = nodePosition;
            }
        }

        public override void KeyMovement(Vector3 moveDirection, MovementState movementState)
        {
            if (moveDirection.sqrMagnitude <= 0.25f)
                return;
            PointClickMovement(EntityTransform.position + moveDirection);
        }

        public override void PointClickMovement(Vector3 position)
        {
            base.PointClickMovement(position);
            Seeker.StartPath(Entity.MovementTransform.position, position);
        }

        public override void EntityUpdate()
        {
            base.EntityUpdate();
            // Update reached end of path state
            if (IsOwnedByServer || IsOwnerClient)
            {
                if (_endOfPathPosition.HasValue && HasNavPaths && Vector3.Distance(_endOfPathPosition.Value, Entity.MovementTransform.position) >= _nodeSize)
                    syncReachedEndOfPath.Value = false;
                else
                    syncReachedEndOfPath.Value = true;
            }
        }

        public override void SetLookRotation(Quaternion rotation, bool immediately)
        {
            if (!Entity.CanMove() || !Entity.CanTurn())
                return;
            if (CanPredictMovement())
            {
                // Always apply movement to owner client (it's client prediction for server auth movement)
                Direction2D = (Vector2)(rotation * Vector3.forward);
            }
        }
    }
}
