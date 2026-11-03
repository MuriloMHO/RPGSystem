using RPGSystem.Areas;
using RPGSystem.Areas.AreaTypes;
using RPGSystem.Characters;
using RPGSystem.Characters.Enemies;
using RPGSystem.Characters.Enemies.EnemyTypes;
using RPGSystem.Characters.Players;
using RPGSystem.Characters.Players.PlayerClasses;

namespace RPGSystem.Web
{
    public class CreatePath
    {
        public static string CreateAreaPath(Area area)
        {
            string imagePath = "Assets/Backgrounds";

            if (area is GreenFields)
            {
                imagePath += "GreenFields.png";
            }
            else if (area is DarkForest)
            {
                imagePath += "DarkForest.png";
            }
            else if (area is OrcCamp)
            {
                imagePath += "OrcCamp.png";
            }
            else if (area is RuinedHighlands)
            {
                imagePath += "RuinedHighlands.png";
            }
            else if (area is CursedCrypt)
            {
                imagePath += "/CursedCrypt.png";
            }
            else if (area is DarkCitadel)
            {
                imagePath += "/DarkCitadel.png";
            }

            return imagePath;
        }
        public static string CreateCharacterPath(Character character)
        {
            string imagePath = "Assets/Characters";

            if (character is Player)
            {
                imagePath += "/Players";

                if (character is Knight)
                {
                    imagePath += "/Knight.png";
                }
                else if (character is Archer)
                {
                    imagePath += "/Archer.png";
                }
                else if (character is Mage)
                {
                    imagePath += "/Mage.png";
                }
            }

            if (character is Enemy)
            {
                imagePath += "/Enemies";

                if (character is Goblin)
                {
                    imagePath += "/Goblin.png";
                }
                else if (character is Wolf)
                {
                    imagePath += "/Wolf.png";
                }
                else if (character is Orc)
                {
                    imagePath += "/Orc.png";
                }
                else if (character is Troll)
                {
                    imagePath += "/Troll.png";
                }
                else if (character is Necromancer)
                {
                    imagePath += "/Necromancer.png";
                }
                else if (character is Boss)
                {
                    imagePath += "/DarkLord.png";
                }
            }
            return imagePath;
        }
    }
}
