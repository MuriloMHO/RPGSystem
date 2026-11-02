namespace RPGSystem.Test
{
    public class CriticalHitRandom : Random
    {
        public override int Next(int minValue, int maxValue)
        {
            if (minValue == 1 && maxValue == 21)
            {
                return 20;
            }

            return minValue;
        }
    }
}
