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
            CharacterMovement = GetComponent<BaseCharacterMovement>();
            if (CharacterMovement == null)
                CharacterMovement = gameObject.AddComponent<AstarCharacterMovement2D>();
        }
    }
}
