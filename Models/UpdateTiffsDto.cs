namespace WebApplication1.Models
{
    public class UpdateTiffsDto
    {
        public DateTime Date { get; set; }

        public IFormFile? Zip {  get; set; }
    }
}
