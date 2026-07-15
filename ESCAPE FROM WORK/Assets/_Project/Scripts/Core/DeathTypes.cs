namespace EscapeFromWork.Core
{
    /// <summary>
    /// Context data describing the circumstances of a player character's death.
    /// </summary>
    public class DeathContext
    {
        public int floorNumber;
        public bool isSafeFloor;
        public string characterName;
        public int lootValueReturned;
        public string causeOfDeath;
    }

    /// <summary>
    /// Immutable memorial record created when a character dies. Displayed in UI
    /// (graveyard, memorial wall, death recap) and used for run statistics.
    /// </summary>
    public class CharacterMemorial
    {
        public string name;
        public int deathFloor;
        public string causeOfDeath;
        public int lootValue;

        public CharacterMemorial(DeathContext ctx)
        {
            name = ctx.characterName;
            deathFloor = ctx.floorNumber;
            causeOfDeath = ctx.causeOfDeath;
            lootValue = ctx.lootValueReturned;
        }
    }
}
