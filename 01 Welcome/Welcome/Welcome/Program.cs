
using Microsoft.EntityFrameworkCore;
using Welcome;

static void CreateDataInDatabase()
{
    using var context = new ProductContextFactory().CreateDbContext(null);
    var maker = new Maker { Name = "ЗАЗ" };
    var product = new Product { Name = "ЗАЗ - 966", Maker = maker };

    context.Makers.Add(maker);
    context.Products.Add(product);
    context.SaveChanges();
}
//CreateDataInDatabase();

static void ReadDataFromDatabase()
{
    using var context = new ProductContextFactory().CreateDbContext(null);
    var products = context.
        Products
        .Include(p => p.Maker)
        .Where(p => p.Maker.Name == "ЗАЗ")
        .OrderBy(static p => p.Name)
        .ToList();
    foreach (var product in products)
    {
        Console.WriteLine($"Product: {product.Name}, Maker: {product.Maker.Name}");
    }
}
ReadDataFromDatabase();
