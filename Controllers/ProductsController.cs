using API.Data;
using API.Modeles;
using API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly StoreContext _context;
        private readonly API.Services.IPhotoService _photoService;

        public ProductsController(StoreContext context, API.Services.IPhotoService photoService)
        {
            _context = context;
            _photoService = photoService;
        }

        // GET: api/Products?categoryId=1
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts([FromQuery] int? categoryId)
        {
            if (categoryId.HasValue)
            {
                return await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Images)
                    .Where(p => p.CategoryId == categoryId.Value)
                    .ToListAsync();
            }
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return product;
        }

        // POST: api/Products
        [HttpPost]
        public async Task<ActionResult<Product>> PostProduct([FromForm] API.DTOs.CreateProductDto productDto)
        {
            string mainImageUrl = "";
            var imageUrls = new List<string>();

            // Upload all images to Cloudinary
            if (productDto.ImageFiles != null && productDto.ImageFiles.Count > 0)
            {
                foreach (var imageFile in productDto.ImageFiles)
                {
                    if (imageFile.Length > 0)
                    {
                        var result = await _photoService.AddPhotoAsync(imageFile);
                        if (result.Error != null) return BadRequest(result.Error.Message);
                        imageUrls.Add(result.SecureUrl.AbsoluteUri);
                    }
                }

                // Set the first image as the main image (for backward compatibility)
                if (imageUrls.Count > 0)
                {
                    mainImageUrl = imageUrls[0];
                }
            }

            var product = new Product
            {
                Name = productDto.Name,
                Description = productDto.Description,
                Price = productDto.Price,
                CategoryId = productDto.CategoryId,
                AvailableColors = productDto.AvailableColors ?? "",
                ImageUrl = mainImageUrl
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Add all images to ProductImages table
            for (int i = 0; i < imageUrls.Count; i++)
            {
                var productImage = new ProductImage
                {
                    ProductId = product.Id,
                    ImageUrl = imageUrls[i],
                    DisplayOrder = i
                };
                _context.ProductImages.Add(productImage);
            }

            await _context.SaveChangesAsync();

            return CreatedAtAction("GetProduct", new { id = product.Id }, product);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutProduct(int id, [FromForm] API.DTOs.CreateProductDto productDto)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            product.Name = productDto.Name;
            product.Description = productDto.Description;
            product.Price = productDto.Price;
            product.CategoryId = productDto.CategoryId;
            product.AvailableColors = productDto.AvailableColors ?? "";

            // Handle new images if provided
            if (productDto.ImageFiles != null && productDto.ImageFiles.Count > 0)
            {
                var imageUrls = new List<string>();

                // Upload all new images
                foreach (var imageFile in productDto.ImageFiles)
                {
                    if (imageFile.Length > 0)
                    {
                        var result = await _photoService.AddPhotoAsync(imageFile);
                        if (result.Error != null) return BadRequest(result.Error.Message);
                        imageUrls.Add(result.SecureUrl.AbsoluteUri);
                    }
                }

                // Update main image
                if (imageUrls.Count > 0)
                {
                    product.ImageUrl = imageUrls[0];
                }

                // Remove old images from database
                var oldImages = await _context.ProductImages.Where(pi => pi.ProductId == id).ToListAsync();
                _context.ProductImages.RemoveRange(oldImages);

                // Add new images
                for (int i = 0; i < imageUrls.Count; i++)
                {
                    var productImage = new ProductImage
                    {
                        ProductId = product.Id,
                        ImageUrl = imageUrls[i],
                        DisplayOrder = i
                    };
                    _context.ProductImages.Add(productImage);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}
