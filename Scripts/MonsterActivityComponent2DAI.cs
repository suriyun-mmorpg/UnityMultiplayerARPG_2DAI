﻿using UnityEngine;

namespace MultiplayerARPG
{
    public class MonsterActivityComponent2DAI : MonsterActivityComponent
    {
        protected override bool OverlappedEntity<T>(T entity, Vector3 measuringPosition, Vector3 targetPosition, float distance)
        {
            // Must reached end of path before doing an actions
            return base.OverlappedEntity(entity, measuringPosition, targetPosition, distance) && (Entity.Movement as AstarCharacterMovement2D).ReachedEndOfPath;
        }
    }
}
