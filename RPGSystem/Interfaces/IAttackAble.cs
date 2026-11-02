namespace RPGSystem.Interfaces
{
    public interface IAttackAble
    {
        public int DamageToTarget(int succeded, int damage);
        public int HitTarget(Characters.Character defender);
    }
}