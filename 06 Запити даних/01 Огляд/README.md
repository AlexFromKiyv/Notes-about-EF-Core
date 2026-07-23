# Огляд

Entity Framework Core використовує Language-Integrated Query (LINQ) для запитів даних з бази даних. LINQ дозволяє використовувати C# (або вашу обрану мову .NET) для написання строго типізованих запитів. Він використовує ваш похідний контекст і класи сутностей для посилання на об'єкти бази даних. EF Core передає представлення запиту LINQ постачальнику бази даних. Постачальники баз даних, у свою чергу, перетворюють його на мову запитів, специфічну для бази даних (наприклад, SQL для реляційної бази даних). Запити завжди виконуються до бази даних, навіть якщо сутності, повернуті в результаті, вже існують у контексті.

Наведені нижче фрагменти коду показують кілька прикладів виконання поширених завдань за допомогою Entity Framework Core.

## Підготовка

```cs
public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
    public int? Rating { get; set; }
}
```

```cs
public class ApplicationDbContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>()
    .HasData(
        new Blog { BlogId = 1, Url = @"https://devblogs.microsoft.com/dotnet", Rating = 5 },
        new Blog { BlogId = 2, Url = @"https://mytravelblog.com/", Rating = 4 });

    }

}

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[]? args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        string connectionString = @"Server=(localdb)\mssqllocaldb;Database=MyDB;Trusted_Connection=True;ConnectRetryCount=0";
        optionsBuilder.UseSqlServer(connectionString);
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
```

## Завантаження всіх даних

```cs
static async Task DoAsync()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var blogs = await context.Blogs.ToListAsync();
    foreach (var blog in blogs)
    {
        Console.WriteLine($"{blog.BlogId}\t{blog.Url}\t{blog.Rating}");
    }
}
await DoAsync();
```
```
1       https://devblogs.microsoft.com/dotnet   5
2       https://mytravelblog.com/       4
```

## Завантаження окремого об'єкта

```cs
    var blog = await context.Blogs
        .SingleAsync(b => b.BlogId == 2);

    Console.WriteLine($"{blog.BlogId}\t{blog.Url}\t{blog.Rating}"); 
```
```
2       https://mytravelblog.com/       4
```

## Фільтрація

```cs
    var blogs = await context.Blogs
        .Where(b => b.Url.Contains("dotnet"))
        .ToListAsync();

    foreach (var blog in blogs)
    {
        Console.WriteLine($"{blog.BlogId}\t{blog.Url}\t{blog.Rating}");
    }
```
```
1       https://devblogs.microsoft.com/dotnet   5
```

## Додаткова інформація

* Дізнайтеся більше про вирази запитів LINQ " LINQ Standard query operators " 
* Для отримання детальнішої інформації про те, як обробляється запит в EF Core, див. статтю Як працюють запити.