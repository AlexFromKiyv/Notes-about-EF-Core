# Налагодження механізму відстеження змін

Механізм відстеження змін в Entity Framework Core (EF Core) генерує два типи вихідних даних, які допомагають під час налагодження:

* ChangeTracker.DebugView забезпечує зручне для читання представлення всіх сутностей, що відстежуються.
* Повідомлення журналу рівня налагодження (debug) генеруються, коли механізм відстеження змін виявляє стан і коригує зв’язки.

## Модель прикладу

Types.cs
```cs
public class Blog
{
    public int Id { get; set; } // Primary key
    public Guid AssetsId { get; set; } // Alternate key
    public string Name { get; set; }

    public IList<Post> Posts { get; } = new List<Post>(); // Collection navigation
    public BlogAssets Assets { get; set; } // Reference navigation
}

public class BlogAssets
{
    public Guid Id { get; set; } // Primary key and foreign key
    public byte[] Banner { get; set; }

    public Blog Blog { get; set; } // Reference navigation
}

public class Post
{
    public int Id { get; set; } // Primary key
    public string Title { get; set; }
    public string Content { get; set; }

    public int BlogId { get; set; } // Foreign key
    public Blog Blog { get; set; } // Reference navigation

    public IList<Tag> Tags { get; } = new List<Tag>(); // Skip collection navigation
}

public class Tag
{
    public int Id { get; set; } // Primary key
    public string Text { get; set; }

    public IList<Post> Posts { get; } = new List<Post>(); // Skip collection navigation
}
```
```cs
public class ApplicationDbContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<BlogAssets> Assets { get; set; }
    public DbSet<Tag> Tags { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder
            .Entity<Blog>()
            .Property(e => e.AssetsId)
            .ValueGeneratedOnAdd();

        modelBuilder
            .Entity<BlogAssets>()
            .HasOne(e => e.Blog)
            .WithOne(e => e.Assets)
            .HasForeignKey<BlogAssets>(e => e.Id)
            .HasPrincipalKey<Blog>(e => e.AssetsId);
    }
}
```
```cs
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private readonly LogLevel _logLevel;

    public ApplicationDbContextFactory(LogLevel logLevel = LogLevel.Error) 
    {
        _logLevel = logLevel;
    }

    public ApplicationDbContext CreateDbContext(string[]? args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        string connectionString = @"Server=(localdb)\mssqllocaldb;Database=MyDB;Trusted_Connection=True;ConnectRetryCount=0";
        
        optionsBuilder
            .LogTo(Console.WriteLine, _logLevel)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.MultipleCollectionIncludeWarning))
            .EnableSensitiveDataLogging()
            .UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
```
Helpers.cs
```cs
partial class Program
{
    private static async Task CleanAndPopulateDatabase()
    {
        using var context = new ApplicationDbContextFactory().CreateDbContext(null);
        //Console.WriteLine(context.Model.ToDebugString());

        await CleanDatabase();
        await PopulateDatabase();
    }


    protected static async Task CleanDatabase()
    {
        using var context = new ApplicationDbContextFactory().CreateDbContext(null);

        Console.WriteLine("Deleting and re-creating database...");
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        Console.WriteLine("Done. Database is clean and fresh.");
    }

    public static async Task PopulateDatabase()
    {
        using var context = new ApplicationDbContextFactory().CreateDbContext(null);

        var blogs = new[]
        {
            new Blog
            {
                Name = ".NET Blog",
                Assets = new BlogAssets() {Banner = Array.Empty<byte>()},
                Posts =
                {
                    new Post
                    {
                        Title = "Announcing the Release of EF Core 5.0",
                        Content = "Announcing the release of EF Core 5.0, a full featured cross-platform..."
                    },
                    new Post
                    {
                        Title = "Announcing F# 5",
                        Content = "F# 5 is the latest version of F#, the functional programming language..."
                    },
                },
            },
            new Blog
            {
                Name = "Visual Studio Blog",
                Assets = new BlogAssets() {Banner = Array.Empty<byte>()},
                Posts =
                {
                    new Post
                    {
                        Title = "Disassembly improvements for optimized managed debugging",
                        Content =
                            "If you are focused on squeezing out the last bits of performance for your .NET service or..."
                    },
                    new Post
                    {
                        Title = "Database Profiling with Visual Studio",
                        Content = "Examine when database queries were executed and measure how long the take using..."
                    },
                }
            }
        };

        var tags = new[]
        {
            new Tag { Text = ".NET", Posts = { blogs[0].Posts[0], blogs[0].Posts[1] } },
            new Tag { Text = "Visual Studio", Posts = { blogs[1].Posts[0], blogs[1].Posts[1] } },
            new Tag { Text = "EF Core", Posts = { blogs[0].Posts[0] } }
        };

        context.AddRange(blogs);
        context.AddRange(tags);

        await context.SaveChangesAsync();
    }
}
```
Program.cs
```cs
static async Task DoAsync(Func<Task> doIt)
{
    await CleanAndPopulateDatabase();

    await doIt();

}
await DoAsync(LoadBlogs);


static async Task LoadBlogs()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var blogs = await context.Blogs.ToListAsync();

    foreach (var blog in blogs)
    {
        Console.WriteLine($"{blog.Id}\t{blog.Name}\t{blog.Posts.Count}");
    }
}
```

## Режим налагодження відстежувача змін

До налагоджувального подання відстежувача змін можна отримати доступ у налагоджувачі вашого середовища розробки (IDE). Для цього треба встановити точку остановки (Наприклад в  LoadBlogsWithPosts на foreach ), адалі подмвитисть стан context > ChangeTracker > DebugView > ShortView > View > Text Visualizer

До нього також можна звернутися безпосередньо з коду — наприклад, щоб вивести відлагоджувальне представлення в консоль:

```cs
static async Task LoadBlogs()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var blogs = await context.Blogs.Include(b=>b.Posts).ToListAsync();

    Console.WriteLine(context.ChangeTracker.DebugView.ShortView);
}
```
```
Blog {Id: 1} Unchanged AK {AssetsId: 6ecf4d85-b252-418b-c10b-08df40932e5a}
Blog {Id: 2} Unchanged AK {AssetsId: 99f8d4ab-b97d-4727-c10c-08df40932e5a}

1       .NET Blog       0
2       Visual Studio Blog      0
```
Режим налагодження має короткий і розгорнутий формати. Короткий формат відображає відстежувані сутності, їхній стан і ключові значення. Розгорнутий формат додатково містить усі значення властивостей і навігаційних зв’язків, а також стан.

### Short view

Розгляньмо приклад режиму налагодження (debug view), використовуючи модель. 
Спершу ми відстежимо кілька сутностей і переведемо їх у різні стани — це забезпечить нас наочними даними про зміни, які можна буде проаналізувати:

```cs
static async Task Change_tracker_debug_view_1()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var blogs = await context.Blogs
    .Include(e => e.Posts).ThenInclude(e => e.Tags)
    .Include(e => e.Assets)
    .ToListAsync();

    // Mark something Added
    blogs[0].Posts.Add(
        new Post
        {
            Title = "What’s next for System.Text.Json?",
            Content = ".NET 5.0 was released recently and has come with many new features and..."
        });

    // Mark something Deleted
    blogs[1].Posts.Remove(blogs[1].Posts[1]);

    // Make something Modified
    blogs[0].Name = ".NET Blog (All new!)";

    context.ChangeTracker.DetectChanges();

    Console.WriteLine(context.ChangeTracker.DebugView.ShortView);
}
```
Виведення короткого представлення на цьому етапі, як показано вище, дає такий результат:
```
Blog {Id: 1} Modified AK {AssetsId: c3258daf-2229-477f-f0be-08df3f0ba228}
Blog {Id: 2} Unchanged AK {AssetsId: 95a9ec16-4f9a-4152-f0bf-08df3f0ba228}
BlogAssets {Id: 95a9ec16-4f9a-4152-f0bf-08df3f0ba228} Unchanged FK {Id: 95a9ec16-4f9a-4152-f0bf-08df3f0ba228}
BlogAssets {Id: c3258daf-2229-477f-f0be-08df3f0ba228} Unchanged FK {Id: c3258daf-2229-477f-f0be-08df3f0ba228}
Post {Id: -2147482643} Added FK {BlogId: 1}
Post {Id: 1} Unchanged FK {BlogId: 1}
Post {Id: 2} Unchanged FK {BlogId: 1}
Post {Id: 3} Unchanged FK {BlogId: 2}
Post {Id: 4} Deleted FK {BlogId: 2}
Tag {Id: 1} Unchanged
Tag {Id: 2} Unchanged
Tag {Id: 3} Unchanged
PostTag (Dictionary<string, object>) {PostsId: 1, TagsId: 1} Unchanged FK {PostsId: 1} FK {TagsId: 1}
PostTag (Dictionary<string, object>) {PostsId: 1, TagsId: 3} Unchanged FK {PostsId: 1} FK {TagsId: 3}
PostTag (Dictionary<string, object>) {PostsId: 2, TagsId: 1} Unchanged FK {PostsId: 2} FK {TagsId: 1}
PostTag (Dictionary<string, object>) {PostsId: 3, TagsId: 2} Unchanged FK {PostsId: 3} FK {TagsId: 2}
PostTag (Dictionary<string, object>) {PostsId: 4, TagsId: 2} Deleted FK {PostsId: 4} FK {TagsId: 2}
```
Примітка:
* Each tracked entity is listed with its primary key (PK) value. For example, Blog {Id: 1}.
* Якщо сутність належить до типу сутностей спільного типу (shared-type entity type), то також відображається її тип CLR. Наприклад, PostTag (Dictionary\<string, object\>).
* Далі показано EntityState. Він може набувати одного з таких значень: Unchanged, Added, Modified або Deleted.
* Далі наведено значення для будь-яких альтернативних ключів (AK). Наприклад: AK {AssetsId: ed727978-1ffe-4709-baee-73913e8e44a0}.
* Нарешті, відображаються значення для будь-яких зовнішніх ключів (FK). Наприклад: FK {PostsId: 4}, FK {TagsId: 2}.

###  Long view

Розширений вигляд можна вивести в консоль так само, як і короткий вигляд:

```cs
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
```
Результат для того самого стану, що й у короткому поданні вище, — це:

```
Blog {Id: 1} Modified
    Id: 1 PK
    AssetsId: '8b205504-28b1-4b25-a006-08df3f0d80b3' AK
    Name: '.NET Blog (All new!)' Modified Originally '.NET Blog'
  Assets: {Id: 8b205504-28b1-4b25-a006-08df3f0d80b3}
  Posts: [{Id: 1}, {Id: 2}, {Id: -2147482643}]
Blog {Id: 2} Unchanged
    Id: 2 PK
    AssetsId: '917c18b8-5e87-4b44-a007-08df3f0d80b3' AK
    Name: 'Visual Studio Blog'
  Assets: {Id: 917c18b8-5e87-4b44-a007-08df3f0d80b3}
  Posts: [{Id: 3}]
BlogAssets {Id: 8b205504-28b1-4b25-a006-08df3f0d80b3} Unchanged
    Id: '8b205504-28b1-4b25-a006-08df3f0d80b3' PK FK
    Banner: '0x'
  Blog: {Id: 1}
BlogAssets {Id: 917c18b8-5e87-4b44-a007-08df3f0d80b3} Unchanged
    Id: '917c18b8-5e87-4b44-a007-08df3f0d80b3' PK FK
    Banner: '0x'
  Blog: {Id: 2}
Post {Id: -2147482643} Added
    Id: -2147482643 PK Temporary
    BlogId: 1 FK
    Content: '.NET 5.0 was released recently and has come with many new fe...'
    Title: 'What's next for System.Text.Json?'
  Blog: {Id: 1}
  Tags: []
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
  Tags: [{Id: 1}, {Id: 3}]
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}
  Tags: [{Id: 1}]
Post {Id: 3} Unchanged
    Id: 3 PK
    BlogId: 2 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 2}
  Tags: [{Id: 2}]
Post {Id: 4} Deleted
    Id: 4 PK
    BlogId: 2 FK
    Content: 'Examine when database queries were executed and measure how ...'
    Title: 'Database Profiling with Visual Studio'
  Blog: <null>
  Tags: [{Id: 2}]
Tag {Id: 1} Unchanged
    Id: 1 PK
    Text: '.NET'
  Posts: [{Id: 1}, {Id: 2}]
Tag {Id: 2} Unchanged
    Id: 2 PK
    Text: 'Visual Studio'
  Posts: [{Id: 3}, {Id: 4}]
Tag {Id: 3} Unchanged
    Id: 3 PK
    Text: 'EF Core'
  Posts: [{Id: 1}]
PostTag (Dictionary<string, object>) {PostsId: 1, TagsId: 1} Unchanged
    PostsId: 1 PK FK
    TagsId: 1 PK FK
PostTag (Dictionary<string, object>) {PostsId: 1, TagsId: 3} Unchanged
    PostsId: 1 PK FK
    TagsId: 3 PK FK
PostTag (Dictionary<string, object>) {PostsId: 2, TagsId: 1} Unchanged
    PostsId: 2 PK FK
    TagsId: 1 PK FK
PostTag (Dictionary<string, object>) {PostsId: 3, TagsId: 2} Unchanged
    PostsId: 3 PK FK
    TagsId: 2 PK FK
PostTag (Dictionary<string, object>) {PostsId: 4, TagsId: 2} Deleted
    PostsId: 4 PK FK
    TagsId: 2 PK FK
```
Кожен відстежуваний об'єкт і його стан відображаються, як і раніше. Однак у розширеному поданні також відображаються значення властивостей і навігаційні значення.

### Значеня властивостей

Для кожної властивості в розширеному поданні відображається, чи є вона частиною первинного ключа (PK), альтернативного ключа (AK) або зовнішнього ключа (FK).

* Blog.Id — це властивість, що є первинним ключем: Id: 1 PK
* Blog.AssetsId — це властивість, що є альтернативним ключем: AssetsId: 'ed727978-1ffe-4709-baee-73913e8e44a0' AK

* Post.BlogId — це властивість зовнішнього ключа: BlogId: 2 FK
* BlogAssets.Id є водночас первинним ключем і зовнішнім ключем: Id: '3a54b880-2b9d-486b-9403-dc2e52d36d65' PK FK
* Змінені значення властивостей позначені відповідним чином; також відображається початкове значення властивості. Наприклад: «Name: '.NET Blog (All new!)' Modified Originally '.NET Blog'"».

Нарешті, додані сутності з тимчасовими значеннями ключів вказують на те, що це значення є тимчасовим. Наприклад: Id: -2147482643 PK Temporary.

### Навігаційні значення

Значення навігації відображаються з використанням значень первинних ключів сутностей, на які посилаються ці навігаційні зв’язки. Наприклад, у наведеному вище результаті допис 3 пов’язаний із блогом 2. Це означає, що навігаційна властивість Post.Blog вказує на екземпляр Blog з ідентифікатором 2. Це відображається як Blog: {Id: 2}.
Те саме відбувається і з навігаційними властивостями, що представляють колекції, за винятком того, що в цьому випадку пов’язаних сутностей може бути кілька. Наприклад, навігаційна колекція Blog.Posts містить три сутності зі значеннями ключів 1, 2 та -2147482643 відповідно. Це відображається як [{Id: 1}, {Id: 2}, {Id: -2147482643}].

## Ведення журналу відстежувачем змін (logging)

Механізм відстеження змін (Change Tracker) записує повідомлення на рівні журналювання Debug щоразу, коли виявляє зміни у властивостях або навігаційних зв’язках. 

Наприклад, коли в коді на початку цього документа викликається метод ChangeTracker.DetectChanges() і ввімкнено журналювання для налагодження, генеруються такі записи журналу:

```
dbug: 10.11.2026 17:24:25.907 CoreEventId.DetectChangesStarting[10800] (Microsoft.EntityFrameworkCore.ChangeTracking)
      DetectChanges starting for 'ApplicationDbContext'.
dbug: 10.11.2026 17:24:25.910 CoreEventId.PropertyChangeDetected[10802] (Microsoft.EntityFrameworkCore.ChangeTracking)
      The unchanged property 'Blog.Name' was detected as changed from '.NET Blog' to '.NET Blog (All new!)' and will be marked as modified for entity with key '{Id: 1}'.
dbug: 10.11.2026 17:24:25.915 CoreEventId.StateChanged[10807] (Microsoft.EntityFrameworkCore.ChangeTracking)
      The 'Blog' entity with key '{Id: 1}' tracked by 'ApplicationDbContext' changed state from 'Unchanged' to 'Modified'.
dbug: 10.11.2026 17:24:25.920 CoreEventId.CollectionChangeDetected[10804] (Microsoft.EntityFrameworkCore.ChangeTracking)
      1 entities were added and 0 entities were removed from navigation 'Blog.Posts' on entity with key '{Id: 1}'.
dbug: 10.11.2026 17:24:25.929 CoreEventId.ValueGenerated[10808] (Microsoft.EntityFrameworkCore.ChangeTracking)
      'ApplicationDbContext' generated temporary value '-2147482643' for the property 'Post.Id'.
dbug: 10.11.2026 17:24:25.933 CoreEventId.ReferenceChangeDetected[10805] (Microsoft.EntityFrameworkCore.ChangeTracking)
      The navigation 'Post.Blog' for entity with key '{Id: -2147482643}' was detected as changed.
dbug: 10.11.2026 17:24:25.934 CoreEventId.StartedTracking[10806] (Microsoft.EntityFrameworkCore.ChangeTracking)
      Context 'ApplicationDbContext' started tracking 'Post' entity with key '{Id: -2147482643}'.
dbug: 10.11.2026 17:24:25.936 CoreEventId.CollectionChangeDetected[10804] (Microsoft.EntityFrameworkCore.ChangeTracking)
      0 entities were added and 1 entities were removed from navigation 'Blog.Posts' on entity with key '{Id: 2}'.
dbug: 10.11.2026 17:24:25.939 CoreEventId.StateChanged[10807] (Microsoft.EntityFrameworkCore.ChangeTracking)
      The 'Post' entity with key '{Id: 4}' tracked by 'ApplicationDbContext' changed state from 'Unchanged' to 'Modified'.
dbug: 10.11.2026 17:24:25.947 CoreEventId.CascadeDeleteOrphan[10003] (Microsoft.EntityFrameworkCore.Update)
      An entity of type 'Post' with key '{Id: 4}' changed to 'Deleted' state due to severed required relationship to its parent entity of type 'Blog'.
dbug: 10.11.2026 17:24:25.949 CoreEventId.StateChanged[10807] (Microsoft.EntityFrameworkCore.ChangeTracking)
      The 'Post' entity with key '{Id: 4}' tracked by 'ApplicationDbContext' changed state from 'Modified' to 'Deleted'.
dbug: 10.11.2026 17:24:25.959 CoreEventId.CascadeDelete[10002] (Microsoft.EntityFrameworkCore.Update)
      A cascade state change of an entity of type 'PostTag' with key '{PostsId: 4, TagsId: 2}' to 'Deleted' occurred due to the deletion of its parent entity of type 'Post' with key '{Id: 4}'.
dbug: 10.11.2026 17:24:25.965 CoreEventId.StateChanged[10807] (Microsoft.EntityFrameworkCore.ChangeTracking)
      The 'PostTag' entity with key '{PostsId: 4, TagsId: 2}' tracked by 'ApplicationDbContext' changed state from 'Unchanged' to 'Deleted'.
dbug: 10.11.2026 17:24:25.967 CoreEventId.ReferenceChangeDetected[10805] (Microsoft.EntityFrameworkCore.ChangeTracking)
      The navigation 'Post.Blog' for entity with key '{Id: 4}' was detected as changed.
dbug: 10.11.2026 17:24:25.969 CoreEventId.DetectChangesCompleted[10801] (Microsoft.EntityFrameworkCore.ChangeTracking)
      DetectChanges completed for 'ApplicationDbContext'.
```

У наведеній нижче таблиці узагальнено повідомлення журналу відстеження змін:

|Event ID|Description|
|--------|-----------|
|CoreEventId.DetectChangesStarting|DetectChanges() розпочинається|
|CoreEventId.DetectChangesCompleted|DetectChanges() завершено|
|CoreEventId.PropertyChangeDetected|Значення звичайної властивості змінилося|
|CoreEventId.ForeignKeyChangeDetected|Значення властивості зовнішнього ключа змінилося|
|CoreEventId.CollectionChangeDetected|До навігації колекції, що не підлягає пропуску, було додано пов’язані сутності або їх було з неї видалено.|
|CoreEventId.ReferenceChangeDetected|Навігаційну властивість-посилання було змінено: вона тепер вказує на іншу сутність або має значення null.|
|CoreEventId.StartedTracking|EF Core почав відстежувати сутність|
|CoreEventId.StateChanged|EntityState сутності змінився|
|CoreEventId.ValueGenerated|Для властивості було згенеровано значення|
|CoreEventId.SkipCollectionChangeDetected|До пропускну навігацію коллекції (skip collection) було додано пов’язані сутності або їх було видалено.|


