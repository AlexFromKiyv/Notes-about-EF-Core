# Події

Entity Framework Core (EF Core) надає події .NET, які діють як зворотні виклики (callbacks) у відповідь на певні події в коді EF Core. Події простіші за перехоплювачі (interceptors) і дозволяють гнучкіше налаштовувати реєстрацію. Однак вони є суто синхронними, тому не можуть виконувати неблокувальні асинхронні операції введення-виведення (I/O).

Події реєструються для кожного екземпляра DbContext. Використовуйте діагностичний слухач (diagnostic listener), щоб отримати ту саму інформацію, але для всіх екземплярів DbContext у межах процесу.

## Події, що генеруються EF Core

EF Core генерує такі події:

|Подія|Коли відбувається|
|-----|-----------------|
|DbContext.SavingChanges|На початку SaveChanges або SaveChangesAsync|
|DbContext.SavedChanges|Після успішного виконання SaveChanges або SaveChangesAsync|
|DbContext.SaveChangesFailed|Після невдалої операції SaveChanges або SaveChangesAsync|
|ChangeTracker.Tracked|Коли сутність відстежується контекстом|
|ChangeTracker.StateChanged|Коли відстежувана сутність змінює свій стан|

## Модель прикладу

Types.cs

```cs

public interface IHasTimestamps
{
    DateTime? Added { get; set; }
    DateTime? Deleted { get; set; }
    DateTime? Modified { get; set; }
}

public class Blog : IHasTimestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    public string Name { get; set; }

    public ICollection<Post> Posts { get; } = new List<Post>();

    public DateTime? Added { get; set; }
    public DateTime? Deleted { get; set; }
    public DateTime? Modified { get; set; }

    public override string ToString()
        => $"Blog {Id}{this.ToStampString()}";
}

public class Post : IHasTimestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    public string Title { get; set; }

    public Blog Blog { get; set; }

    public DateTime? Added { get; set; }
    public DateTime? Deleted { get; set; }
    public DateTime? Modified { get; set; }

    public override string ToString()
        => $"Post {Id}{this.ToStampString()}";
}

public static class HasTimestampsExtensions
{
    public static string ToStampString(this IHasTimestamps entity)
    {
        return $"{GetStamp("Added", entity.Added)}{GetStamp("Modified", entity.Modified)}{GetStamp("Deleted", entity.Deleted)}";

        string GetStamp(string state, DateTime? dateTime)
            => dateTime == null ? "" : $" {state} on: {dateTime}";
    }
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
}

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{

    public ApplicationDbContext CreateDbContext(string[]? args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        string connectionString = @"Server=(localdb)\mssqllocaldb;Database=MyDB;Trusted_Connection=True;ConnectRetryCount=0";
        
        optionsBuilder
            .UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
```

Helpers.cs

```cs
partial class Program
{
    protected static async Task CleanDatabase()
    {
        using var context = new ApplicationDbContextFactory().CreateDbContext(null);

        Console.WriteLine("Deleting and re-creating database...");
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        Console.WriteLine("Done. Database is clean and fresh.");
    }

}
```
Program.cs

```cs
static async Task DoAsync(Func<Task> doIt)
{
    await doIt();
}
await DoAsync(CleanDatabase);
```

## Приклад: Фіксація часу зміни стану

Кожна сутність, що відстежується об'єктом DbContext, має стан (EntityState). Наприклад, стан Added вказує на те, що сутність буде вставлена ​​в базу даних.

У цьому прикладі використовуються події Tracked і StateChanged для виявлення моментів, коли сутність змінює свій стан. Після цього сутність позначається поточним моментом часу, що вказує, коли відбулася ця зміна. У результаті створюються мітки часу, які показують, коли об’єкт було додано, видалено та/або востаннє оновлено.

Типи сутностей у цьому прикладі реалізують інтерфейс, який визначає властивості часових міток:

```cs
public interface IHasTimestamps
{
    DateTime? Added { get; set; }
    DateTime? Deleted { get; set; }
    DateTime? Modified { get; set; }
}
```

Після цього метод у DbContext застосунку може встановлювати мітки часу для будь-якої сутності, що реалізує цей інтерфейс:

```cs
public class ApplicationDbContext : DbContext
{

    //...

    private static void UpdateTimestamps(object? sender, EntityEntryEventArgs e)
    {
        if (e.Entry.Entity is IHasTimestamps entityWithTimestamps)
        {
            switch (e.Entry.State)
            {
                case EntityState.Deleted:
                    entityWithTimestamps.Deleted = DateTime.UtcNow;
                    Console.WriteLine($"Stamped for delete: {e.Entry.Entity}");
                    break;
                case EntityState.Modified:
                    entityWithTimestamps.Modified = DateTime.UtcNow;
                    Console.WriteLine($"Stamped for update: {e.Entry.Entity}");
                    break;
                case EntityState.Added:
                    entityWithTimestamps.Added = DateTime.UtcNow;
                    Console.WriteLine($"Stamped for insert: {e.Entry.Entity}");
                    break;
            }
        }
    }
}
```

Цей метод має відповідну сигнатуру для використання як обробник подій — як для події Tracked, так і для події StateChanged. Обробник реєструється для обох подій у конструкторі DbContext. Зауважте, що події можна прив’язати до DbContext у будь-який момент; це не обов’язково робити в конструкторі контексту.

```cs
public class ApplicationDbContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
        ChangeTracker.StateChanged += UpdateTimestamps;
        ChangeTracker.Tracked += UpdateTimestamps;
    }

    //...

}
```
Обидві події потрібні, оскільки нові сутності запускають події Tracked під час першого відстеження. Події StateChanged спрацьовують лише для сутностей, стан яких змінюється, поки вони вже відстежуються.

Зразок для цього прикладу містить просту консольну програму, яка вносить зміни до бази даних блогу:

```cs
static async Task DoAsync(Func<Task> doIt)
{
    await CleanDatabase();

    await doIt();
}
await DoAsync(AddAndChangeBlog);

static async Task AddAndChangeBlog()
{
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        context.Add(
            new Blog
            {
                Id = 1,
                Name = "EF Blog",
                Posts = 
                { 
                    new Post { Id = 1, Title = "EF Core 3.1!" }, 
                    new Post { Id = 2, Title = "EF Core 5.0!" } 
                }
            });

        await context.SaveChangesAsync();
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var blog = await context.Blogs.Include(e => e.Posts).SingleAsync();

        blog.Name = "EF Core Blog";
        context.Remove(blog.Posts.First());
        blog.Posts.Add(new Post { Id = 3, Title = "EF Core 6.0!" });

        context.ChangeTracker.DetectChanges();
        await context.SaveChangesAsync();
    }
}

```
Вивід цього коду показує зміни стану, що відбуваються, та застосування часових позначок:
```
Stamped for insert: Blog 1 Added on: 18.11.2026 15:40:42
Stamped for insert: Post 1 Added on: 18.11.2026 15:40:42
Stamped for insert: Post 2 Added on: 18.11.2026 15:40:42
Stamped for delete: Post 1 Added on: 18.11.2026 15:40:42 Deleted on: 18.11.2026 15:40:43
Stamped for update: Blog 1 Added on: 18.11.2026 15:40:42 Modified on: 18.11.2026 15:40:43
Stamped for insert: Post 3 Added on: 18.11.2026 15:40:43
```
