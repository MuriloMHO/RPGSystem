namespace RPGSystem.Test
{
    internal class CriticalMissRandom : Random
    {
        public override int Next(int minValue, int maxValue)
        {
            if (minValue == 1 && maxValue == 21)
            {
                return 1;
            }

            return minValue;
        }
    }
}
