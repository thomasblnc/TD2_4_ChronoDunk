namespace ChronoDunk
{
    public class CharacterStats
    {
        public string Name { get; set; }
        public string ImagePath { get; set; }
        public double Speed { get; set; }
        public double JumpForce { get; set; }
        public double Power { get; set; }

        public CharacterStats(string name, string img, double speed, double jump, double power)
        {
            Name = name;
            ImagePath = img;
            Speed = speed;
            JumpForce = jump;
            Power = power;
        }
    }
}