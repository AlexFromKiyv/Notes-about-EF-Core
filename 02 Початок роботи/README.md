# Початок роботи

Давайте створимо консольний додаток .NET, який здійснює доступ до даних бази даних SQLite за допомогою Entity Framework Core.

# Створення проекту

Створіть папку EFGetStarted в ній

```
dotnet new console
dotnet add package Microsoft.EntityFrameworkCore.Sqlite -v 9.0.16
dotnet add package Microsoft.EntityFrameworkCore.Design -v 9.0.16
```

# Створення моделі

Створіть класи контексту та сутностей.

Додайте клас Model.cs

```cs
using Microsoft.EntityFrameworkCore;

namespace EFGetStarted;

public class BloggingContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }

    public string DbPath { get; }

    public BloggingContext()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        DbPath = System.IO.Path.Join(path, "blogging.db");
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}

public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }

    public List<Post> Posts { get; } = new();
}

public class Post
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}
```
Рядки підключення не слід зберігати в коді для робочих програм. Ви також можете розділити кожен клас C# на окремий файл.
Не забудьте записати створений файл.

# Створення бази даних

Наступні кроки використовують міграції для створення бази даних.

```
dotnet ef migrations add InitialCreate
dotnet ef database update
```

# CRUD

Відкрийте Program.cs та замініть вміст наступним кодом:

```cs
using EFGetStarted;
using Microsoft.EntityFrameworkCore;

using var db = new BloggingContext();

// Note: This sample requires the database to be created before running.
Console.WriteLine($"Database path: {db.DbPath}.");

// Create
Console.WriteLine("Inserting a new blog");
db.Add(new Blog { Url = "http://blogs.msdn.com/adonet" });
await db.SaveChangesAsync();

// Read
Console.WriteLine("Querying for a blog");
var blog = await db.Blogs
    .OrderBy(b => b.BlogId)
    .FirstAsync();

// Update
Console.WriteLine("Updating the blog and adding a post");
blog.Url = "https://devblogs.microsoft.com/dotnet";
blog.Posts.Add(
    new Post { Title = "Hello World", Content = "I wrote an app using EF Core!" });
await db.SaveChangesAsync();

// Delete
Console.WriteLine("Delete the blog");
db.Remove(blog);
await db.SaveChangesAsync();
```
```
Database path: C:\Users\user\AppData\Local\blogging.db.
Inserting a new blog
Querying for a blog
Updating the blog and adding a post
Delete the blog
```
