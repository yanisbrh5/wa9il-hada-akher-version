using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Modeles
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        // Keep for backward compatibility (will be the first/main image)
        public string ImageUrl { get; set; } = string.Empty;

        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        // Comma-separated list of colors (e.g., "Red,Blue,Green")
        public string AvailableColors { get; set; } = string.Empty;

        // Collection of product images
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    }
}
