using UnityEngine;
using Pathfinding;

namespace MultiplayerARPG
{
	public class PlayerCharacterController2DAI : PlayerCharacterController
	{
        AstarCharacterMovement2D movement;
        GameObject groundSeekerGameObject;
        Seeker groundSeeker;
        GameObject entitySeekerGameObject;
        Seeker entitySeeker;
        Vector3 measuringPositionOffsets;
        Vector3 expectTargetPosition;
        float expectTargetDistance;

        protected override void Awake()
        {
            base.Awake();
            // Ground seeker
            groundSeekerGameObject = new GameObject("_ControllerGroundSeeker");
            groundSeeker = groundSeekerGameObject.AddComponent<Seeker>();
            groundSeeker.startEndModifier.exactStartPoint = StartEndModifier.Exactness.SnapToNode;
            groundSeeker.startEndModifier.exactEndPoint = StartEndModifier.Exactness.SnapToNode;
            groundSeeker.pathCallback += OnGroundPathComplete;
            // Entity seeker
            entitySeekerGameObject = new GameObject("_ControllerEntitySeeker");
            entitySeeker = entitySeekerGameObject.AddComponent<Seeker>();
            entitySeeker.startEndModifier.exactStartPoint = StartEndModifier.Exactness.SnapToNode;
            entitySeeker.startEndModifier.exactEndPoint = StartEndModifier.Exactness.SnapToNode;
            entitySeeker.pathCallback += OnEntityPathComplete;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            groundSeeker.pathCallback -= OnGroundPathComplete;
            Destroy(groundSeekerGameObject);
            entitySeeker.pathCallback -= OnEntityPathComplete;
            Destroy(entitySeekerGameObject);
        }

        public override void UpdatePointClickInput()
        {
            base.UpdatePointClickInput();
            if (_getMouseDown)
                _previousPointClickPosition = Vector3.positiveInfinity;
        }

        protected void OnGroundPathComplete(Path _p)
        {
            Vector3 nodePosition = (Vector3)_p.path[_p.path.Count - 1].position;
            _destination = nodePosition;
            PlayingCharacterEntity.PointClickMovement(nodePosition);
        }

        protected void OnEntityPathComplete(Path _p)
        {
            Vector3 nodePosition;
            for (int i = 0; i < _p.path.Count; ++i)
            {
                nodePosition = (Vector3)_p.path[i].position;
                if (Vector3.Distance(nodePosition + measuringPositionOffsets, expectTargetPosition) <= expectTargetDistance)
                {
                    _destination = null;
                    PlayingCharacterEntity.PointClickMovement(nodePosition);
                    break;
                }
            }
        }

        protected override void OnPointClickOnGround(Vector3 targetPosition)
        {
            if (Vector3.Distance(MovementTransform.position, targetPosition) > MIN_START_MOVE_DISTANCE)
                groundSeeker.StartPath(MovementTransform.position, targetPosition);
        }

        protected override void Setup(BasePlayerCharacterEntity characterEntity)
        {
            base.Setup(characterEntity);
            movement = characterEntity.GetComponent<AstarCharacterMovement2D>();
        }

        protected override void Desetup(BasePlayerCharacterEntity characterEntity)
        {
            base.Desetup(characterEntity);
            movement = null;
        }

        protected override void UpdateTargetEntityPosition(Vector3 measuringPosition, Vector3 targetPosition, float distance)
        {
            if (PlayingCharacterEntity.IsPlayingActionAnimation())
                return;

            if (Vector3.Distance(MovementTransform.position, targetPosition) > MIN_START_MOVE_DISTANCE &&
                Vector3.Distance(_previousPointClickPosition, targetPosition) > MIN_START_MOVE_DISTANCE)
            {
                measuringPositionOffsets = measuringPosition - MovementTransform.position;
                expectTargetPosition = targetPosition;
                expectTargetDistance = distance;
                entitySeeker.StartPath(MovementTransform.position, targetPosition);
                _previousPointClickPosition = targetPosition;
            }
        }

        protected override bool OverlappedEntity(ITargetableEntity entity, Vector3 sourcePosition, Vector3 targetPosition, float distance)
        {
            return base.OverlappedEntity(entity, sourcePosition, targetPosition, distance);
        }
    }
}