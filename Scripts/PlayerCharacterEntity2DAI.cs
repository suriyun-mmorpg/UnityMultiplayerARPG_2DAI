using UnityEngine;

namespace MultiplayerARPG
{
    [System.Obsolete("This is deprecated, but still keep it for backward compatibilities. Use `PlayerCharacterEntity` instead")]
    /// <summary>
    /// This is deprecated, but still keep it for backward compatibilities.
    /// Use `PlayerCharacterEntity` instead
    /// </summary>
    public partial class PlayerCharacterEntity2DAI : BasePlayerCharacterEntity
    {
        public override void InitialRequiredComponents()
        {
            base.InitialRequiredComponents();
            if (Movement == null)
                Debug.LogError("[" + ToString() + "] Did not setup entity movement component to this entity.");
        }
    }
}
