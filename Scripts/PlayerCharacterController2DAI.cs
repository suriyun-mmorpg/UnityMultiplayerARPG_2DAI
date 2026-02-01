using UnityEngine;
using Pathfinding;

namespace MultiplayerARPG
{
    public class PlayerCharacterController2DAI : PlayerCharacterController
    {
        AstarCharacterMovement2D _movement;
        GameObject _groundSeekerGameObject;
        Seeker _groundSeeker;
        GameObject _entitySeekerGameObject;
        Seeker _entitySeeker;
        Vector3 _measuringPositionOffsets;
        Vector3 _expectTargetPosition;
        float _expectTargetDistance;
        int? _prevNodeIdx;

        protected override void Awake()
        {
            base.Awake();
            // Ground seeker
            _groundSeekerGameObject = new GameObject("_ControllerGroundSeeker");
            _groundSeeker = _groundSeekerGameObject.AddComponent<Seeker>();
            _groundSeeker.startEndModifier.exactStartPoint = StartEndModifier.Exactness.SnapToNode;
            _groundSeeker.startEndModifier.exactEndPoint = StartEndModifier.Exactness.SnapToNode;
            _groundSeeker.pathCallback += OnGroundPathComplete;
            // Entity seeker
            _entitySeekerGameObject = new GameObject("_ControllerEntitySeeker");
            _entitySeeker = _entitySeekerGameObject.AddComponent<Seeker>();
            _entitySeeker.startEndModifier.exactStartPoint = StartEndModifier.Exactness.SnapToNode;
            _entitySeeker.startEndModifier.exactEndPoint = StartEndModifier.Exactness.SnapToNode;
            _entitySeeker.pathCallback += OnEntityPathComplete;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _groundSeeker.pathCallback -= OnGroundPathComplete;
            Destroy(_groundSeekerGameObject);
            _entitySeeker.pathCallback -= OnEntityPathComplete;
            Destroy(_entitySeekerGameObject);
        }

        public override void UpdatePointClickInput()
        {
            base.UpdatePointClickInput();
            if (_getMouseDown)
                _previousPointClickPosition = Vector3.positiveInfinity;
        }

        protected void OnGroundPathComplete(Path _p)
        {
            GraphNode node = _p.path[_p.path.Count - 1];
            Vector3 nodePosition = (Vector3)node.position;
            _destination = nodePosition;
            if (!_prevNodeIdx.HasValue || _prevNodeIdx.Value != node.NodeIndex)
            {
                _prevNodeIdx = node.NodeIndex;
                PlayingCharacterEntity.PointClickMovement(nodePosition);
            }
        }

        protected void OnEntityPathComplete(Path _p)
        {
            Vector3 nodePosition;
            for (int i = 0; i < _p.path.Count; ++i)
            {
                GraphNode node = _p.path[i];
                nodePosition = (Vector3)node.position;
                if (Vector3.Distance(nodePosition + _measuringPositionOffsets, _expectTargetPosition) <= _expectTargetDistance)
                {
                    _destination = null;
                    if (!_prevNodeIdx.HasValue || _prevNodeIdx.Value != node.NodeIndex)
                    {
                        _prevNodeIdx = node.NodeIndex;
                        PlayingCharacterEntity.PointClickMovement(nodePosition);
                    }
                    break;
                }
            }
        }

        protected override void OnPointClickOnGround(Vector3 targetPosition)
        {
            if (Vector3.Distance(MovementTransform.position, targetPosition) > MIN_START_MOVE_DISTANCE)
                _groundSeeker.StartPath(MovementTransform.position, targetPosition);
        }

        protected override void Setup(BasePlayerCharacterEntity characterEntity)
        {
            base.Setup(characterEntity);
            _movement = characterEntity.GetComponent<AstarCharacterMovement2D>();
        }

        protected override void Desetup(BasePlayerCharacterEntity characterEntity)
        {
            base.Desetup(characterEntity);
            _movement = null;
        }

        protected override void UpdateTargetEntityPosition(Vector3 measuringPosition, Vector3 targetPosition, float distance)
        {
            if (PlayingCharacterEntity.IsPlayingActionAnimation())
                return;

            if (Vector3.Distance(MovementTransform.position, targetPosition) > MIN_START_MOVE_DISTANCE &&
                Vector3.Distance(_previousPointClickPosition, targetPosition) > MIN_START_MOVE_DISTANCE)
            {
                _measuringPositionOffsets = measuringPosition - MovementTransform.position;
                _expectTargetPosition = targetPosition;
                _expectTargetDistance = distance;
                _entitySeeker.StartPath(MovementTransform.position, targetPosition);
                _previousPointClickPosition = targetPosition;
            }
        }
    }
}