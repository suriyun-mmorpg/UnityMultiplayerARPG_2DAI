using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using System;

namespace MultiplayerARPG
{
	public class PlayerCharacterController2DAI : PlayerCharacterController
	{
        AstarCharacterMovement2D movement;
        Seeker seeker;

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Setup(BasePlayerCharacterEntity characterEntity)
        {
            base.Setup(characterEntity);
            movement = characterEntity.GetComponent<AstarCharacterMovement2D>();
            seeker = characterEntity.GetComponent<Seeker>();
        }

        protected override void Desetup(BasePlayerCharacterEntity characterEntity)
        {
            base.Desetup(characterEntity);
            movement = null;
            seeker = null;
        }

        protected override void UpdateTargetEntityPosition(Vector3 measuringPosition, Vector3 targetPosition, float distance)
        {
            if (PlayerCharacterEntity.IsPlayingActionAnimation())
                return;

            Path path = seeker.StartPath(measuringPosition, targetPosition);
            if (path.path.Count > 0)
            {
                targetPosition = (Vector3)path.path[path.path.Count - 1].position;
                Vector3 direction = (targetPosition - measuringPosition).normalized;
                Vector3 position = targetPosition - (direction * (distance - StoppingDistance));
                if (Vector3.Distance(previousPointClickPosition, position) > 0.01f)
                {
                    PlayerCharacterEntity.PointClickMovement(position);
                    previousPointClickPosition = position;
                }
            }
        }

		protected override void AttackOrMoveToEntity(IDamageableEntity entity, float distance, int layerMask)
        {
            Transform damageTransform = PlayerCharacterEntity.GetWeaponDamageInfo(ref isLeftHandAttacking).GetDamageTransform(PlayerCharacterEntity, isLeftHandAttacking);
            Vector3 measuringPosition = damageTransform.position;
            Vector3 targetPosition = entity.OpponentAimTransform.position;
            Debug.LogError("Dist " + Vector3.Distance(measuringPosition, targetPosition) + " ag dist " + distance + " ? " + movement.reachedEndOfPath);
            if (Vector3.Distance(measuringPosition, targetPosition) <= distance && movement.reachedEndOfPath)
            {
                // Stop movement to attack
                PlayerCharacterEntity.StopMove();
                // Turn character to attacking target
                TurnCharacterToEntity(entity.Entity);
                // Do action
                RequestAttack();
                // This function may be used by extending classes
                OnAttackOnEntity();
            }
            else
            {
                // Move to target entity
                UpdateTargetEntityPosition(measuringPosition, targetPosition, distance);
            }
        }

		protected override void UseSkillOrMoveToEntity(IDamageableEntity entity, float distance)
        {
            if (queueUsingSkill.skill != null)
            {
                Transform applyTransform = queueUsingSkill.skill.GetApplyTransform(PlayerCharacterEntity, false);
                Vector3 measuringPosition = applyTransform.position;
                Vector3 targetPosition = entity.OpponentAimTransform.position;
                if ((entity.GetObjectId() == PlayerCharacterEntity.GetObjectId() /* Applying skill to user? */ ||
                    Vector3.Distance(measuringPosition, targetPosition) <= distance) && movement.reachedEndOfPath)
                {
                    // Set next frame target action type
                    targetActionType = queueUsingSkill.skill.IsAttack() ? TargetActionType.Attack : TargetActionType.Activate;
                    // Stop movement to use skill
                    PlayerCharacterEntity.StopMove();
                    // Turn character to attacking target
                    TurnCharacterToEntity(entity.Entity);
                    // Use the skill
                    RequestUsePendingSkill();
                    // This function may be used by extending classes
                    OnUseSkillOnEntity();
                }
                else
                {
                    // Move to target entity
                    UpdateTargetEntityPosition(measuringPosition, targetPosition, distance);
                }
            }
            else
            {
                // Can't use skill
                targetActionType = TargetActionType.Activate;
                ClearQueueUsingSkill();
                return;
            }
        }

		protected override void DoActionOrMoveToEntity(BaseGameEntity entity, float distance, Action action)
        {
            Vector3 measuringPosition = MovementTransform.position;
            Vector3 targetPosition = entity.CacheTransform.position;
            if (Vector3.Distance(measuringPosition, targetPosition) <= distance && movement.reachedEndOfPath)
            {
                // Stop movement to do action
                PlayerCharacterEntity.StopMove();
                // Do action
                action.Invoke();
                // This function may be used by extending classes
                OnDoActionOnEntity();
            }
            else
            {
                // Move to target entity
                UpdateTargetEntityPosition(measuringPosition, targetPosition, distance);
            }
        }
	}
}