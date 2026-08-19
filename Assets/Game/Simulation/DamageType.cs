namespace Game.Simulation
{
    /// <summary>
    /// A kind of damage a weapon deals (TDD §15 combat seam, authored at 0.2.10). A weapon carries a
    /// LIST of typed amounts (see <see cref="WeaponDamage"/>), not one type — a sword is slash + pierce
    /// + a little bash, an axe slash + bash, an arrow pierce only. Extend freely; add resistances /
    /// creature weaknesses keyed on this when combat lands (deferred).
    /// </summary>
    public enum DamageType
    {
        Slash  = 0,
        Pierce = 1,
        Bash   = 2,
    }
}
