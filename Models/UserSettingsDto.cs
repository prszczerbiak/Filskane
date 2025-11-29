namespace WebApplication1.Models
{
    public class UserShortInfoDto
    {
        public string? Name { get; set; }
        public int DarkMode { get; set; } // 0 = light, 1 = dark
        public int Surface { get; set; }  // 0 = ha, 1 = a, 2 = akr

        public double? FarmX { get; set; }
        public double? FarmY { get; set; }
    }
}
