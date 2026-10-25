# Зміна зовнішніх ключів і навігаційних властивостей

# Огляд зовнішніх ключів і навігаційних властивостей

Зв’язки в моделі Entity Framework Core (EF Core) реалізуються за допомогою зовнішніх ключів (FK). Зовнішній ключ (FK) складається з однієї або кількох властивостей залежної (або дочірньої) сутності у зв’язку. Ця залежна (дочірня) сутність пов’язана з відповідною головною (батьківською) сутністю, коли значення властивостей зовнішнього ключа залежної (дочірньої) сутності збігаються зі значеннями властивостей альтернативного або первинного ключа (PK) головної (батьківської) сутності.

Зовнішні ключі — це ефективний спосіб зберігання та керування зв’язками в базі даних, однак вони не надто зручні для роботи з кількома пов’язаними сутностями в коді застосунку. Тому більшість моделей EF Core також накладають «навігаційні властивості» (navigations) поверх представлення зовнішніх ключів. Навігаційні властивості формують C#/.NET зв’язки між екземплярами сутностей, що відображають асоціації, виявлені шляхом зіставлення значень зовнішніх ключів зі значеннями первинних або альтернативних ключів.

Навігацію можна використовувати на обох сторонах зв’язку, лише на одній стороні або не використовувати взагалі, залишаючи лише властивість FK. Властивість зовнішнього ключа (FK) можна приховати, зробивши її тіньовою властивістю. Докладнішу інформацію про моделювання зв’язків див. у розділі «Зв’язки» (Relationships).

# Модель для прикладів

Наведена нижче модель містить чотири типи сутностей із взаємозв’язками між ними. Коментарі в коді вказують, які властивості є зовнішніми ключами, первинними ключами та навігаційними властивостями.

```cs
public class Blog
{
    public int Id { get; set; } // Primary key
    public string Name { get; set; }

    public IList<Post> Posts { get; } = new List<Post>(); // Collection navigation
    public BlogAssets Assets { get; set; } // Reference navigation
}

public class BlogAssets
{
    public int Id { get; set; } // Primary key
    public byte[] Banner { get; set; } 

    public int? BlogId { get; set; } // Foreign key
    public Blog Blog { get; set; } // Reference navigation
}

public class Post
{
    public int Id { get; set; } // Primary key
    public string Title { get; set; }
    public string Content { get; set; }

    public int? BlogId { get; set; } // Foreign key
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
Три взаємозв’язки в цій моделі — це:

* Кожен блог може мати багато дописів (one-to-many):

    * Blog є основним (батьківським principal/parent).
    * Post є залежний (дочірній dependent/child). Він містить властивість зовнішнього ключа (FK) Post.BlogId, значення якої має збігатися зі значенням первинного ключа (PK) Blog.Id відповідного блогу. 
    * Post.Blog — це навігаційне посилання від допису до відповідного блогу. Post.Blog є зворотною навігацією для Blog.Posts.
    * Blog.Posts — це навігаційна колекція, що веде від блогу до всіх пов’язаних із ним дописів. Blog.Posts є зворотною навігацією для Post.Blog.

* Кожен блог може мати один Assets (актив) (one-to-one):
    * Blog є основним (батьківським principal/parent).
    * BlogAssets є залежний (дочірній dependent/child). Він містить властивість зовнішнього ключа (FK) BlogAssets.BlogId, значення якої має збігатися зі значенням первинного ключа (PK) Blog.Id відповідного блогу.
    * BlogAssets.Blog — це навігаційне посилання від активів до відповідного блогу.BlogAssets.Blog — це зворотна навігація для Blog.Assets.
    * Blog.Assets — це навігаційне посилання від блогу до пов’язаних із ним ресурсів (assets). Blog.Assets є зворотною навігацією для BlogAssets.Blog.

* Кожен допис може мати багато тегів, а кожен тег — багато дописів (many-to-many):
    * Зв’язки типу Many-to-many — це додатковий рівень, що базується на двох зв’язках типу one-to-many. Детальніше про зв’язки Many-to-many ідеться далі в цьому документі.
    * Post.Tags — це навігація від публікації до всіх пов’язаних із нею тегів. Post.Tags є зворотною навігацією відносно Tag.Posts.
    * Tag.Posts — це навігація від тегу до всіх пов’язаних із ним дописів. Tag.Posts є зворотною навігацією відносно Post.Tags.

Докладнішу інформацію про моделювання та налаштування зв’язків див. у розділі «Зв’язки сутностей».

Додадково для прикладу потрібне наступне.

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
      modelBuilder.Entity<PostTag>().HasKey(e => new { e.PostId, e.TagId });
    }
}
```
```cs
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private readonly bool _quiet;

    public ApplicationDbContextFactory(bool quiet = true) 
    {
        _quiet = quiet;
    }

    public ApplicationDbContext CreateDbContext(string[]? args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        string connectionString = @"Server=(localdb)\mssqllocaldb;Database=MyDB;Trusted_Connection=True;ConnectRetryCount=0";
        
        
        if(_quiet)
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
        else
        {
            optionsBuilder
                .EnableSensitiveDataLogging()
                .UseSqlServer(connectionString);

            optionsBuilder.LogTo(Console.WriteLine, [RelationalEventId.CommandExecuted]);
        }
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
```
Programm.cs
```cs
static async Task DoAsync()
{
    await CleanAndPopulateDatabase();
}
await DoAsync();



partial class Program
{
    protected static async Task CleanAndPopulateDatabase()
    {
        using var context = new ApplicationDbContextFactory().CreateDbContext(null);
        //Console.WriteLine(context.Model.ToDebugString());

        await CleanDatabase(context);
        await PopulateDatabase(context);
    }


    protected static async Task CleanDatabase(DbContext context)
    {
        Console.WriteLine("Deleting and re-creating database...");
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        Console.WriteLine("Done. Database is clean and fresh.");
    }

    protected static async Task PopulateDatabase(ApplicationDbContext context)
    {
        context.AddRange(
        new Blog
        {
            Name = ".NET Blog",
            Assets = new BlogAssets() { Banner = Array.Empty<byte>() },
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
            Assets = new BlogAssets() { Banner = Array.Empty<byte>() },
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
        },
        new Tag { Text = ".NET" },
        new Tag { Text = "Visual Studio" },
        new Tag { Text = "EF Core" });

        await context.SaveChangesAsync();
    }
}
```

# Коригуванням зв’язків

EF Core підтримує узгодженість між навігаційними властивостями та значеннями зовнішніх ключів (і навпаки). Тобто, якщо значення зовнішнього ключа змінюється так, що тепер воно посилається на іншу головну (батьківську) сутність, то навігаційні властивості оновлюються відповідно до цієї зміни. Аналогічно, якщо змінюється навігаційна властивість, значення зовнішніх ключів відповідних сутностей оновлюються, щоб відобразити цю зміну. ​​Цей процес називається «коригуванням зв’язків» (relationship fixup).

## Коригування на основі запиту

Процес узгодження зв’язків (fixup) вперше відбувається під час отримання сутностей із бази даних. У базі даних містяться лише значення зовнішніх ключів, тому, коли EF Core створює екземпляр сутності на основі даних із бази, він використовує ці значення для налаштування навігаційних властивостей-посилань і додавання сутностей до навігаційних властивостей-колекцій (за потреби). Наприклад, розгляньмо запит щодо блогів та пов’язаних із ними дописів і ресурсів:

Programm.cs

```cs
static async Task DoAsync()
{
    //await CleanAndPopulateDatabase();
    await Relationship_fixup_0();
}
await DoAsync();


static async Task Relationship_fixup_0()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var blogs = await context.Blogs
    .ToListAsync();

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: <null>
  Posts: []
Blog {Id: 2} Unchanged
    Id: 2 PK
    Name: 'Visual Studio Blog'
  Assets: <null>
  Posts: []
```
Разом з Blog завантажимо Posts і Assets.

```cs
static async Task Relationship_fixup_1()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var blogs = await context.Blogs
    .Include(e => e.Posts)
    .Include(e => e.Assets)
    .ToListAsync();

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```

Для кожного блогу EF Core спочатку створює екземпляр Blog. Далі, під час завантаження кожного допису з бази даних, його навігаційна властивість Post.Blog налаштовується так, щоб вказувати на відповідний блог. Так само публікація додається до навігації колекції Blog.Posts. Те саме відбувається з BlogAssets, за винятком того, що в цьому випадку обидва навігаційні властивості є посиланнями. Навігацію Blog.Assets налаштовано так, щоб вона вказувала на екземпляр Assets, а навігацію BlogAsserts.Blog — на екземпляр Blog.

Перегляд налагоджувальної інформації відстежувача змін після виконання цього запиту показує два блоги; для кожного з них відстежується один актив і два дописи:

```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: {Id: 1}
  Posts: [{Id: 1}, {Id: 2}]
Blog {Id: 2} Unchanged
    Id: 2 PK
    Name: 'Visual Studio Blog'
  Assets: {Id: 2}
  Posts: [{Id: 3}, {Id: 4}]
BlogAssets {Id: 1} Unchanged
    Id: 1 PK
    Banner: '0x'
    BlogId: 1 FK
  Blog: {Id: 1}
BlogAssets {Id: 2} Unchanged
    Id: 2 PK
    Banner: '0x'
    BlogId: 2 FK
  Blog: {Id: 2}
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
  Tags: []
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}
  Tags: []
Post {Id: 3} Unchanged
    Id: 3 PK
    BlogId: 2 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 2}
  Tags: []
Post {Id: 4} Unchanged
    Id: 4 PK
    BlogId: 2 FK
    Content: 'Examine when database queries were executed and measure how ...'
    Title: 'Database Profiling with Visual Studio'
  Blog: {Id: 2}
  Tags: []
```
У режимі налагодження відображаються як значення ключів, так і навігаційні зв’язки. Навігаційні зв’язки відображаються з використанням значень первинних ключів пов’язаних сутностей. Наприклад, запис Posts: [{Id: 1}, {Id: 2}] у наведеному вище виводі вказує на те, що навігаційна властивість колекції Blog.Posts містить два пов’язані дописи з первинними ключами 1 та 2 відповідно. Аналогічно, для кожного допису, пов’язаного з першим блогом, рядок Blog: {Id: 1} вказує на те, що навігаційна властивість Post.Blog посилається на блог із первинним ключем 1.

## Коригування зв’язків для сутностей, що відстежуються локально

Узгодження зв’язків також відбувається між сутностями, отриманими в результаті запиту з відстеженням, і сутностями, які вже відстежуються контекстом DbContext. Наприклад, розгляньте можливість виконання трьох окремих запитів для блогів, дописів і медіаресурсів:

```cs
static async Task Relationship_fixup_2() 
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);
 
    var blogs = await context.Blogs.ToListAsync();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    Console.WriteLine("\n\n");

    var assets = await context.Assets.ToListAsync();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    Console.WriteLine("\n\n");

    var posts = await context.Posts.ToListAsync();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
Якщо знову поглянути на вікна налагодження, то після першого запиту відстежуються лише два блоги:

```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: <null>
  Posts: []
Blog {Id: 2} Unchanged
    Id: 2 PK
    Name: 'Visual Studio Blog'
  Assets: <null>
  Posts: []
```
Навігаційні властивості-посилання Blog.Assets мають значення null, а навігаційні властивості-колекції Blog.Posts є порожніми, оскільки контекст наразі не відстежує жодних пов’язаних сутностей.

Після виконання другого запиту навігаційні посилання Blogs.Assets було скориговано так, щоб вони вказували на щойно відстежувані екземпляри BlogAsset. Аналогічним чином, посилання навігації BlogAssets.Blog налаштовані так, щоб вказувати на відповідний екземпляр Blog, який вже відстежується.

```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: {Id: 1}
  Posts: []
Blog {Id: 2} Unchanged
    Id: 2 PK
    Name: 'Visual Studio Blog'
  Assets: {Id: 2}
  Posts: []
BlogAssets {Id: 1} Unchanged
    Id: 1 PK
    Banner: '0x'
    BlogId: 1 FK
  Blog: {Id: 1}
BlogAssets {Id: 2} Unchanged
    Id: 2 PK
    Banner: '0x'
    BlogId: 2 FK
  Blog: {Id: 2}
```

Нарешті, після третього запиту, навігаційні властивості колекції Blog.Posts містять усі пов’язані дописи, а посилання Post.Blog вказують на відповідний екземпляр Blog:

```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: {Id: 1}
  Posts: [{Id: 1}, {Id: 2}]
Blog {Id: 2} Unchanged
    Id: 2 PK
    Name: 'Visual Studio Blog'
  Assets: {Id: 2}
  Posts: [{Id: 3}, {Id: 4}]
BlogAssets {Id: 1} Unchanged
    Id: 1 PK
    Banner: '0x'
    BlogId: 1 FK
  Blog: {Id: 1}
BlogAssets {Id: 2} Unchanged
    Id: 2 PK
    Banner: '0x'
    BlogId: 2 FK
  Blog: {Id: 2}
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
  Tags: []
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}
  Tags: []
Post {Id: 3} Unchanged
    Id: 3 PK
    BlogId: 2 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 2}
  Tags: []
Post {Id: 4} Unchanged
    Id: 4 PK
    BlogId: 2 FK
    Content: 'Examine when database queries were executed and measure how ...'
    Title: 'Database Profiling with Visual Studio'
  Blog: {Id: 2}
  Tags: []
```
Це той самий кінцевий стан, який було досягнуто з оригінальним єдиним запитом, оскільки EF Core виправляв навігацію під час відстеження об’єктів, навіть якщо вони надходили з кількох різних запитів.

    Примітка

    Процес узгодження посилань (fixup) ніколи не призводить до отримання додаткових даних із бази даних. Це з’єднує лише ті сутності, які вже повернуто запитом або які вже відстежуються контекстом DbContext. Див. розділ «Розпізнавання ідентичності в EF Core» (Identity Resolution in EF Core), щоб дізнатися про обробку дублікатів під час серіалізації сутностей.


# Зміна зв’язків за допомогою навігаційних властивостей

Найпростіший спосіб змінити зв’язок між двома сутностями — це змінити навігаційну властивість, дозволивши EF Core належним чином оновити зворотну навігаційну властивість і значення зовнішнього ключа (FK). Це можна зробити шляхом:

* Додавання сутності до навігаційної властивості колекції або її видалення звідти.
* Зміна навігаційної властивості-посилання так, щоб вона вказувала на іншу сутність, або встановлення для неї значення null.

## Додавання до навігаційних колекцій або видалення з них

Наприклад, перемістімо одну зі статей із блогу Visual Studio до блогу .NET. Для цього потрібно спочатку завантажити блоги й дописи, а потім перемістити допис із колекції навігації одного блогу до колекції навігації іншого:

```cs
static async Task Changing_relationships_using_navigations_1()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);
    await CleanAndPopulateDatabase();

    var dotNetBlog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Name == ".NET Blog");
    var vsBlog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Name == "Visual Studio Blog");

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    Console.WriteLine("\n\n");

    var post = vsBlog.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));
    vsBlog.Posts.Remove(post);
    dotNetBlog.Posts.Add(post);

    context.ChangeTracker.DetectChanges();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    await context.SaveChangesAsync();
}
```

    Порада

    Тут необхідно викликати ChangeTracker.DetectChanges(), оскільки звернення до режиму налагоджувального перегляду (debug view) не призводить до автоматичного виявлення змін.

Ось відлагоджувальне представлення, виведене після виконання DetectChanges() наведеного вище коду. Тут тільки відзначено шо змінило наш код:

```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: <null>
  Posts: [{Id: 1}, {Id: 2}] !!!
Blog {Id: 2} Unchanged
    Id: 2 PK
    Name: 'Visual Studio Blog'
  Assets: <null>
  Posts: [{Id: 3}, {Id: 4}] !!!

Post {Id: 3} Unchanged
    Id: 3 PK
    BlogId: 2 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 2}
  Tags: [] !!!

  ... After DetectChanges()

Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: <null>
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}] !!!
Blog {Id: 2} Unchanged
    Id: 2 PK
    Name: 'Visual Studio Blog'
  Assets: <null>
  Posts: [{Id: 4}] !!!

  ...

Post {Id: 3} Modified
    Id: 3 PK
    BlogId: 1 FK Modified Originally 2 !!!
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 1} !!!
  Tags: []

  ...

```
Навігаційна властивість Blog.Posts у блозі .NET тепер містить три дописи (Posts: [{Id: 1}, {Id: 2}, {Id: 3}]). Так само, навігація Blog.Posts у блозі Visual Studio містить лише один допис (Posts: [{Id: 4}]). Це цілком очікувано, оскільки код явним чином змінив ці колекції.

Що цікавіше, хоча код не змінював навігацію Post.Blog явним чином, її було скориговано так, щоб вона вказувала на блог Visual Studio (Blog: {Id: 1}). Крім того, значення зовнішнього ключа Post.BlogId було оновлено відповідно до значення первинного ключа блогу .NET. Ця зміна значення зовнішнього ключа (FK) зберігається в базі даних після виклику SaveChanges:

```sql

info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (8ms) [Parameters=[@p1='3', @p0='1' (Nullable = true)], CommandType='Text', CommandTimeout='30']
      SET IMPLICIT_TRANSACTIONS OFF;
      SET NOCOUNT ON;
      UPDATE [Posts] SET [BlogId] = @p0
      OUTPUT 1
      WHERE [Id] = @p1;
```
Якшо данні не візуалізуються, саму зміну в БД можа досягти не заванатзуючи всі Posts.

```cs
  
  var dotNetBlog = await context.Blogs.SingleAsync(e => e.Name == ".NET Blog");
  var vsBlog = await context.Blogs.SingleAsync(e => e.Name == "Visual Studio Blog");
  
  var post = context.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));
  
  vsBlog.Posts.Remove(post);
  dotNetBlog.Posts.Add(post);

```
```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: <null>
  Posts: [{Id: 3}]
Blog {Id: 2} Unchanged
    Id: 2 PK
    Name: 'Visual Studio Blog'
  Assets: <null>
  Posts: []
Post {Id: 3} Modified
    Id: 3 PK
    BlogId: 1 FK Modified Originally 2
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 1}
  Tags: []
```
```sql
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (9ms) [Parameters=[@p1='3', @p0='1' (Nullable = true)], CommandType='Text', CommandTimeout='30']
      SET IMPLICIT_TRANSACTIONS OFF;
      SET NOCOUNT ON;
      UPDATE [Posts] SET [BlogId] = @p0
      OUTPUT 1
      WHERE [Id] = @p1;
```

## Зміна посилання-навігації 

У попередньому прикладі допис було переміщено з одного блогу до іншого шляхом зміни навігації в колекції дописів кожного з блогів. Того самого результату можна досягти, змінивши посилання навігації Post.Blog так, щоб воно вказувало на новий блог.

```cs
static async Task Changing_relationships_using_navigations_2()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    await CleanAndPopulateDatabase();

    var dotNetBlog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Name == ".NET Blog");
    var vsBlog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Name == "Visual Studio Blog");

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    Console.WriteLine("\n\n");

    var post = vsBlog.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));
    
    // vsBlog.Posts.Remove(post);
    // dotNetBlog.Posts.Add(post);
        
    post.Blog = dotNetBlog;

    context.ChangeTracker.DetectChanges();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    await context.SaveChangesAsync();
}
```
Після цієї зміни вигляд у режимі налагодження залишається точно таким самим, як і в попередньому прикладі. Це пояснюється тим, що EF Core виявив зміну навігаційної властивості-посилання, а потім відповідним чином скоригував навігаційні властивості-колекції та значення зовнішнього ключа.

## Зміна зв’язків за допомогою значень зовнішніх ключів

У попередньому розділі ми керували зв’язками через навігаційні властивості, дозволяючи значенням зовнішніх ключів оновлюватися автоматично. Це рекомендований спосіб керування зв’язками в EF Core. Однак значення зовнішніх ключів (FK) можна також змінювати безпосередньо. Наприклад, ми можемо перемістити допис з одного блога до іншого, змінивши значення зовнішнього ключа Post.BlogId:

```cs
static async Task Changing_relationships_using_navigations_3()
{
    //...
    //vsBlog.Posts.Remove(post);
    //dotNetBlog.Posts.Add(post);

    //post.Blog = dotNetBlog;

    post.BlogId = dotNetBlog.Id;
    //...
}
```
Зверніть увагу, наскільки це схоже на зміну навігації за посиланням, як показано в попередньому прикладі. Після цієї зміни вигляд у режимі налагодження знову такий самий, як і в двох попередніх прикладах. Це пояснюється тим, що EF Core виявив зміну значення зовнішнього ключа (FK), а потім відповідним чином скоригував навігаційні властивості як для посилання, так і для колекції.

```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: <null>
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]

...

Post {Id: 3} Modified
    Id: 3 PK
    BlogId: 1 FK Modified Originally 2
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 1}
  Tags: []

...
```

    Порада

    Не пишіть код для керування всіма навігаційними властивостями та значеннями зовнішніх ключів щоразу, коли змінюється зв’язок. Такий код є складнішим і повинен у кожному випадку забезпечувати узгоджені зміни зовнішніх ключів і навігаційних властивостей. За можливості змінюйте лише одну навігаційну властивість або, можливо, обидві. За потреби змінюйте лише значення зовнішніх ключів (FK). Уникайте одночасної зміни навігаційних властивостей і значень зовнішніх ключів.

Також можна завантажувати в контекст тільки необхідне

```cs
static async Task Changing_relationships_using_navigations_4()
{
    //...
 
    var dotNetBlog = await context.Blogs.SingleAsync(e => e.Name == ".NET Blog");
    var post = context.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));

    post.BlogId = dotNetBlog.Id;

    context.ChangeTracker.DetectChanges();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    await context.SaveChangesAsync();
}
```


# Коригування для доданих або видалених сутностей

## Додавання до навігаційної властивості-колекції

EF Core виконує такі дії, коли виявляє, що до навігаційної властивості-колекції було додано нову залежну (дочірню) сутність:

    * Якщо сутність не відстежується, то вона починає відстежуватися. (Зазвичай сутність перебуватиме в стані Added. Однак, якщо тип сутності налаштовано на використання згенерованих ключів і встановлено значення первинного ключа, то сутність відстежуватиметься в стані Unchanged.)
    * Якщо суб'єкт пов'язаний з іншою головною, цей зв'язок розривається.
    * Сутність пов’язується з основним об’єктом (або батьківським об’єктом), якому належить навігація за колекцією.
    * Значення навігаційних властивостей та зовнішніх ключів коригуються для всіх залучених сутностей.

З огляду на це бачимо, що для переміщення допису з одного блогу до іншого нам насправді не потрібно видаляти його зі старої навігації колекції перед додаванням до нової. Тож код із наведеного вище прикладу можна змінити з:

```cs
static async Task Fixup_for_added_or_deleted_entities_1()
{
    //...

    var dotNetBlog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Name == ".NET Blog");
    var vsBlog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Name == "Visual Studio Blog");

    var post = vsBlog.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));

    vsBlog.Posts.Remove(post);
    dotNetBlog.Posts.Add(post);
  
  //...
}
```
На 

```cs
static async Task Fixup_for_added_or_deleted_entities_2()
{
  //...
    var dotNetBlog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Name == ".NET Blog");
    var vsBlog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Name == "Visual Studio Blog");

    var post = vsBlog.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));

    dotNetBlog.Posts.Add(post);
  //...
}
```
EF Core бачить, що допис було додано до нового блогу, і автоматично видаляє його з колекції першого блогу.

## Видалення з навігаційної колекції

Видалення залежної/дочірньої сутності з навігації колекції принципала/батьківського об'єкта призводить до розриву зв'язку з цим принципалом/батьківським об'єктом. Далішній розвиток подій залежить від того, чи є зв'язок необов'язковим, чи обов'язковим.

### Необов’язкові зв’язки

За замовчуванням для необов’язкових зв’язків значення зовнішнього ключа встановлюється як null. Це означає, що залежний об'єкт (дочірній елемент) більше не пов'язаний із жодним основним об'єктом (батьківським елементом). Наприклад, завантажмо блог і дописи, а потім видалімо один із дописів із навігаційної колекції Blog.Posts:

```cs
static async Task Fixup_for_added_or_deleted_entities_3()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    await CleanAndPopulateDatabase();

    var dotNetBlog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Name == ".NET Blog");

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    Console.WriteLine("\n\n");

    var post = dotNetBlog.Posts.Single(e => e.Title == "Announcing F# 5");
    dotNetBlog.Posts.Remove(post);

    context.ChangeTracker.DetectChanges();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    await context.SaveChangesAsync();
}

```
```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: <null>
  Posts: [{Id: 1}]

  ...

Post {Id: 2} Modified
    Id: 2 PK
    BlogId: <null> FK Modified Originally 1
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: <null>
  Tags: []
```
Перегляд налагоджувальної інформації про відстеження змін після внесення цієї зміни показує, що:

* Для зовнішнього ключа Post.BlogId встановлено значення null (BlogId: <null>; початкове значення FK: 1)
* Для навігаційної властивості Post.Blog встановлено значення null (Blog: <null>)
* Допис видалено з навігаційної колекції Blog.Posts (Posts: [{Id: 1}])

Зверніть увагу, що запис не позначено як Deleted. Він позначений як Modified, щоб під час виклику SaveChanges значення зовнішнього ключа (FK) у базі даних було встановлено в null.

### Обов’язкові зв’язки

Для обов’язкових зв’язків встановлення значення зовнішнього ключа (FK) у null заборонено (і зазвичай неможливо). Отже, розрив обов’язкового зв’язку означає, що залежний об’єкт (дочірня сутність) має бути або перепризначений новому головному об’єкту (батьківській сутності), або видалений із бази даних під час виклику методу SaveChanges, щоб уникнути порушення обмеження цілісності посилань. Це називається «видаленням сиріт» (deleting orphans) і є стандартною поведінкою в EF Core для обов’язкових зв’язків.

Наприклад, змінимо зв’язок між блогом і дописами на обов’язковий, а потім виконаємо той самий код, що й у попередньому прикладі:

```cs
public class Post
{
    //...
    public int BlogId { get; set; } // Foreign key //!!!
    public Blog Blog { get; set; } // Reference navigation
    //...
}
```
```cs
static async Task Fixup_for_added_or_deleted_entities_4()
{
   //...

    var post = dotNetBlog.Posts.Single(e => e.Title == "Announcing F# 5");
    dotNetBlog.Posts.Remove(post);

    //...
}
```

```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: <null>
  Posts: [{Id: 1}]

  ...

Post {Id: 2} Deleted
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: <null>
  Tags: []
```
Перегляд режиму налагодження після цієї зміни показує, що:

* Допис позначено як видалений, тож його буде видалено з бази даних під час виклику SaveChanges.
* Навігацію за посиланням Post.Blog встановлено в значення null (Blog: \<null\>).
* Допис видалено з навігації колекції Blog.Posts (Posts: [{Id: 1}]).

Зверніть увагу, що Post.BlogId залишається незмінним, оскільки для обов’язкового зв’язку йому не можна присвоїти значення null.

Виклик SaveChanges призводить до видалення «сирітського» допису:

```sql
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (13ms) [Parameters=[@p0='2'], CommandType='Text', CommandTimeout='30']
      SET IMPLICIT_TRANSACTIONS OFF;
      SET NOCOUNT ON;
      DELETE FROM [Posts]
      OUTPUT 1
      WHERE [Id] = @p0;
```

### Налаштування моменту видалення «сиріт» та зміни батьківського об'єкта

За замовчуванням сутності-сироти позначаються як видалені одразу після виявлення зміни зв’язку. Однак цей процес можна відкласти до моменту безпосереднього виклику методу SaveChanges. Це може бути корисно, щоб уникнути перетворення на «сиріт» тих сутностей, які було видалено з одного головного об'єкта (або батьківської сутності), але які будуть прив'язані до нового головного об'єкта (або батьківської сутності) до виклику методу SaveChanges. Для налаштування моменту виконання цієї операції використовується властивість ChangeTracker.DeleteOrphansTiming. 

```cs
static async Task Fixup_for_added_or_deleted_entities_5()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    await CleanAndPopulateDatabase();

    var dotNetBlog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Name == ".NET Blog");
    var vsBlog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Name == "Visual Studio Blog");

    context.ChangeTracker.DeleteOrphansTiming = CascadeTiming.OnSaveChanges;

    var post = vsBlog.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));
    vsBlog.Posts.Remove(post);

    context.ChangeTracker.DetectChanges();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    Console.WriteLine("\n\n");

    dotNetBlog.Posts.Add(post);

    context.ChangeTracker.DetectChanges();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    await context.SaveChangesAsync();
}
```
Після видалення запису з колекції об'єкт не позначається як Deleted, як це було в попередньому прикладі. Натомість EF Core відстежує розрив зв’язку, попри те, що це обов’язковий зв’язок. (Значення FK вважається null-версією EF Core, хоча насправді воно не може бути null-версією, оскільки тип не може мати значення null. Це називається "концептуальним null-версією".)

```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: <null>
  Posts: [{Id: 1}, {Id: 2}]
Blog {Id: 2} Unchanged
    Id: 2 PK
    Name: 'Visual Studio Blog'
  Assets: <null>
  Posts: [{Id: 4}]

    ...

Post {Id: 3} Modified
    Id: 3 PK
    BlogId: <null> FK Modified Originally 2
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  Tags: []

    ...

```
Виклик SaveChanges у цей час призведе до видалення осиротілого допису. Однак, якщо, як у наведеному вище прикладі, до виклику методу SaveChanges об’єкт post пов’язується з новим блогом, то його буде належним чином прив’язано до цього нового блогу, і він більше не вважатиметься «сиротою»:

```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: <null>
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
Blog {Id: 2} Unchanged
    Id: 2 PK
    Name: 'Visual Studio Blog'
  Assets: <null>
  Posts: [{Id: 4}]

    ...

Post {Id: 3} Modified
    Id: 3 PK
    BlogId: 1 FK Modified Originally 2
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 1}
  Tags: []

    ...
```
Виклик SaveChanges на цьому етапі оновить запис у базі даних, а не видалить його.

```sql
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (5ms) [Parameters=[@p1='3', @p0='1'], CommandType='Text', CommandTimeout='30']
      SET IMPLICIT_TRANSACTIONS OFF;
      SET NOCOUNT ON;
      UPDATE [Posts] SET [BlogId] = @p0
      OUTPUT 1
      WHERE [Id] = @p1;
```

Також можна вимкнути автоматичне видалення «сиріт» (orphan entities). Це призведе до виникнення винятку, якщо метод SaveChanges буде викликано під час відстеження такого об'єкта. Наприклад, цей код:

```cs
static async Task Fixup_for_added_or_deleted_entities_6()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    await CleanAndPopulateDatabase();

    try
    {
        var dotNetBlog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Name == ".NET Blog");

        context.ChangeTracker.DeleteOrphansTiming = CascadeTiming.Never;

        var post = dotNetBlog.Posts.Single(e => e.Title == "Announcing F# 5");

        dotNetBlog.Posts.Remove(post);

        context.ChangeTracker.DetectChanges();
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        await context.SaveChangesAsync();
    }
    catch (Exception e)
    {
        Console.WriteLine($"{e.GetType().FullName}: {e.Message}");
    }
}
```
Викличе цей виняток
```
System.InvalidOperationException: The association between entity types 'Blog' and 'Post' has been severed, but the relationship is either marked as required or is implicitly required because the foreign key is not nullable. If the dependent/child entity should be deleted when a required relationship is severed, configure the relationship to use cascade deletes. Consider using 'DbContextOptionsBuilder.EnableSensitiveDataLogging' to see the key values.
```
Видалення «сиріт» (orphans), а також каскадне видалення, можна примусово виконати в будь-який момент шляхом виклику ChangeTracker.CascadeChanges(). Поєднання цього з установленням параметра видалення сирітських записів (delete orphan timing) у значення «Never» гарантує, що такі записи ніколи не видалятимуться, доки EF Core не отримає відповідної явної вказівки.

```cs
static async Task Fixup_for_added_or_deleted_entities_6()
{
  //...
        var post = dotNetBlog.Posts.Single(e => e.Title == "Announcing F# 5");

        dotNetBlog.Posts.Remove(post);

        context.ChangeTracker.DetectChanges();
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        
        context.ChangeTracker.CascadeChanges();

        context.ChangeTracker.DetectChanges();
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        await context.SaveChangesAsync();
  //...
}
```

## Зміна навігаційної властивості-посилання

Зміна навігаційної властивості-посилання у зв’язку типу one-to-many має такий самий ефект, як і зміна навігаційної властивості-колекції на протилежному боці зв’язку. Встановлення значення навігації посилання залежного/дочірнього об'єкта на null еквівалентно видаленню сутності з навігації колекції принципала/батьківського об'єкта. Усі операції з виправлення та зміни в базі даних виконуються так, як описано в попередньому розділі, зокрема перетворення сутності на «сироту» (orphan), якщо зв’язок є обов’язковим.

### Необов’язкові зв’язки one-to-one

У разі зв’язків one-to-one зміна навігаційної властивості-посилання призводить до розриву попереднього зв’язку. У випадку необов’язкових зв’язків це означає, що значення зовнішнього ключа (FK) у залежному (дочірньому) об’єкті, з яким раніше було встановлено зв’язок, набуває значення null.

```cs
public class BlogAssets
{
    //...
    public int? BlogId { get; set; } // Foreign key
    //...
}
```
```cs
static async Task Fixup_for_added_or_deleted_entities_7()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    await CleanAndPopulateDatabase();

    var dotNetBlog = await context.Blogs.Include(e => e.Assets).SingleAsync(e => e.Name == ".NET Blog");
  
    //dotNetBlog.Assets = null; // for an experiment
    dotNetBlog.Assets = new BlogAssets() { Banner = Array.Empty<byte>() };


    context.ChangeTracker.DetectChanges();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    await context.SaveChangesAsync();

    Console.WriteLine();
}
```
У режимі налагодження перед викликом SaveChanges видно, що новий BlogAssets замінили наявний, який тепер позначений як Modified і мають значення null у зовнішньому ключі BlogAssets.BlogId:

```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: {Id: -2147482645}
  Posts: []
BlogAssets {Id: -2147482645} Added
    Id: -2147482645 PK Temporary
    Banner: '0x'
    BlogId: 1 FK
  Blog: {Id: 1}
BlogAssets {Id: 1} Modified  !!!
    Id: 1 PK
    Banner: '0x'
    BlogId: <null> FK Modified Originally 1 !!!
  Blog: <null>
```
Це призводить до виконання операцій оновлення та вставки під час виклику SaveChanges:

```sql
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (16ms) [Parameters=[@p1='1', @p0=NULL (DbType = Int32), @p2='0x' (Nullable = false) (Size = 8000), @p3='1' (Nullable = true)], CommandType='Text', CommandTimeout='30']
      SET NOCOUNT ON;
      UPDATE [Assets] SET [BlogId] = @p0
      OUTPUT 1
      WHERE [Id] = @p1;
      INSERT INTO [Assets] ([Banner], [BlogId])
      OUTPUT INSERTED.[Id]
      VALUES (@p2, @p3);
```

### Обов’язкові зв’язки one-to-one

Виконання того самого коду, що й у попередньому прикладі, але цього разу з обов’язковим зв’язком «один до одного», демонструє, що раніше пов’язаний об’єкт `BlogAssets` тепер позначається як видалений (Deleted), оскільки він стає «сиротою» після того, як його місце займає новий `BlogAssets`:

```cs
public class BlogAssets
{
  //...
    public int BlogId { get; set; } // Foreign key
  //...
}
```
```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Assets: {Id: -2147482647}
  Posts: []
BlogAssets {Id: -2147482647} Added
    Id: -2147482647 PK Temporary
    Banner: '0x'
    BlogId: 1 FK
  Blog: {Id: 1}
BlogAssets {Id: 1} Deleted
    Id: 1 PK
    Banner: '0x'
    BlogId: 1 FK
  Blog: <null>
```
У результаті, під час виклику SaveChanges виконуються операції видалення та вставлення:

```sql
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (11ms) [Parameters=[@p0='1', @p1='0x' (Nullable = false) (Size = 8000), @p2='1'], CommandType='Text', CommandTimeout='30']
      SET NOCOUNT ON;
      DELETE FROM [Assets]
      OUTPUT 1
      WHERE [Id] = @p0;
      INSERT INTO [Assets] ([Banner], [BlogId])
      OUTPUT INSERTED.[Id]
      VALUES (@p1, @p2);
```

Момент позначення «сирітських» записів як видалених можна змінити так само, як це показано для навігації в колекціях; це має такі самі наслідки.

## Видалення сутності

### Необов’язкові зв’язки

Коли сутність позначається як видалена (наприклад, шляхом виклику DbContext.Remove), посилання на неї видаляються з навігаційних властивостей інших сутностей. 

Наприклад, позначимо блог Visual Studio як видалений:

```cs
public class Post
{
  //...
  public int? BlogId { get; set; } // Foreign key
  //...
}
public class BlogAssets
{
  //...
  public int? BlogId { get; set; } // Foreign key
  //...
}
```
```CS
static async Task Deleting_an_entity_1()
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);

    await CleanAndPopulateDatabase();

    var vsBlog = await context.Blogs
    .Include(e => e.Posts)
    .Include(e => e.Assets)
    .SingleAsync(e => e.Name == "Visual Studio Blog");

    context.Remove(vsBlog);

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    await context.SaveChangesAsync();
}
```
Перегляд налагоджувальної інформації відстежувача змін перед викликом SaveChanges показує:
```
Blog {Id: 2} Deleted
    Id: 2 PK
    Name: 'Visual Studio Blog'
  Assets: {Id: 2}
  Posts: [{Id: 3}, {Id: 4}]
BlogAssets {Id: 2} Modified
    Id: 2 PK
    Banner: '0x'
    BlogId: <null> FK Modified Originally 2
  Blog: <null>
Post {Id: 3} Modified
    Id: 3 PK
    BlogId: <null> FK Modified Originally 2
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  Tags: []
Post {Id: 4} Modified
    Id: 4 PK
    BlogId: <null> FK Modified Originally 2
    Content: 'Examine when database queries were executed and measure how ...'
    Title: 'Database Profiling with Visual Studio'
  Blog: <null>
  Tags: []
```
Зверніть увагу, що:

* Blog позначено як Deleted. 
* BlogAssets, пов’язані з видаленим блогом, мають значення зовнішнього ключа null (BlogId: \<null\>, FK Modified Originally: 2) і навігаційну властивість-посилання зі значенням null (Blog: \<null\>).
* Кожен запис, пов’язаний із видаленим блогом, має нульове значення зовнішнього ключа (BlogId: \<null\>, FK Modified Originally 2) і навігаційне посилання null (Blog: \<null\>).

### Обов'язкові зв'язки

Поведінка виправлення для обов'язкових зв'язків така ж, як і для необов'язкових зв'язків, за винятком того, що залежні/дочірні сутності позначені як Deleted, оскільки вони не можуть існувати без принципала/батьківського об'єкта та мають бути видалені з бази даних під час виклику SaveChanges, щоб уникнути винятку обмеження посилання. Це називається «каскадним видаленням» (cascade delete) і є стандартною поведінкою в EF Core для обов’язкових зв’язків. Наприклад, виконання того самого коду, що й у попередньому прикладі, але з обов’язковим зв’язком, призводить до такого вигляду в режимі налагодження перед викликом методу SaveChanges:

```cs
public class Post
{
  //...
  public int BlogId { get; set; } // Foreign key
  //...
}
public class BlogAssets
{
  //...
  public int BlogId { get; set; } // Foreign key
  //...
}
```
```
Blog {Id: 2} Deleted
    Id: 2 PK
    Name: 'Visual Studio Blog'
  Assets: {Id: 2}
  Posts: [{Id: 3}, {Id: 4}]
BlogAssets {Id: 2} Deleted
    Id: 2 PK
    Banner: '0x'
    BlogId: 2 FK
  Blog: {Id: 2}
Post {Id: 3} Deleted
    Id: 3 PK
    BlogId: 2 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 2}
  Tags: []
Post {Id: 4} Deleted
    Id: 4 PK
    BlogId: 2 FK
    Content: 'Examine when database queries were executed and measure how ...'
    Title: 'Database Profiling with Visual Studio'
  Blog: {Id: 2}
  Tags: []
```
Як і очікувалося, залежні об'єкти (дочірні елементи) тепер позначено як Deleted. Однак зверніть увагу, що навігаційні зв'язки цих видалених об'єктів залишилися незмінними. Це може здатися дивним, але такий підхід дозволяє уникнути повного руйнування графа видалених сутностей завдяки очищенню всіх навігаційних зв’язків. Тобто блог, ресурс і дописи й далі утворюють граф сутностей навіть після видалення. Завдяки цьому відновити такий граф сутностей набагато простіше, ніж в EF6, де граф фактично «розбивався» на окремі елементи.

### Час виконання каскадного видалення та зміна батьківського об'єкта

За замовчуванням каскадне видалення відбувається одразу після того, як батьківський (основний) об'єкт позначається як видалений. Це працює так само, як і у випадку видалення «сиріт» (orphans), описаному раніше. Як і у випадку з видаленням «сиріт» (orphans), цей процес можна відкласти до моменту виклику методу SaveChanges або навіть повністю вимкнути, відповідним чином налаштувавши властивість ChangeTracker.CascadeDeleteTiming. Це корисно так само, як і у випадку видалення «сирітських» записів, зокрема для перепризначення батьківського об’єкта (або основного запису) для дочірніх елементів чи залежних записів після видалення відповідного батьківського об’єкта.

Каскадне видалення (зокрема й видалення «сирітських» записів) можна ініціювати в будь-який момент, викликавши метод ChangeTracker.CascadeChanges(). Поєднання цього з установленням моменту виконання каскадного видалення в значення Never гарантує, що каскадне видалення не відбуватиметься, доки EF Core не отримає відповідну явну вказівку.

  Порада

  Каскадне видалення та видалення «сиріт» (orphan entities) тісно пов’язані між собою. В обох випадках відбувається видалення залежних (дочірніх) сутностей, коли розривається зв’язок із відповідною головною (батьківською) сутністю. У разі каскадного видалення цей зв’язок розривається через видалення самої головної (батьківської) сутності. У ситуації з «сиротами» головна (батьківська) сутність продовжує існувати, але більше не пов’язана із залежними (дочірніми) сутностями.

# Зв’язки many-to-many

В EF Core зв’язки «багато-до-багатьох» реалізуються за допомогою проміжної сутності (join entity). Кожна зі сторін зв’язку many-to-many пов’язана з цією проміжною сутністю через зв’язок one-to-many. Цю сполучну сутність можна визначити та відобразити явно, або ж вона може бути створена неявно й залишатися прихованою. В обох випадках основний механізм поведінки є однаковим. Спершу ми розглянемо цей базовий механізм, щоб зрозуміти, як працює відстеження зв’язків many-to-many.

## Як працюють зв’язки many-to-many

Розгляньте цю модель EF Core, яка створює зв’язок «багато-до-багатьох» між дописами та тегами з використанням явно визначеного типу сутності зв’язку:

```cs

public class Blog
{
    public int Id { get; set; } // Primary key
    public string Name { get; set; }

    public IList<Post> Posts { get; } = new List<Post>(); // Collection navigation
}

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int? BlogId { get; set; }
    public Blog Blog { get; set; }

    public IList<PostTag> PostTags { get; } = new List<PostTag>(); // Collection navigation
}

public class Tag
{
    public int Id { get; set; }
    public string Text { get; set; }

    public IList<PostTag> PostTags { get; } = new List<PostTag>(); // Collection navigation
}

[PrimaryKey(nameof(PostId),nameof(TagId))]
public class PostTag
{
    public int PostId { get; set; } // First part of composite PK; FK to Post
    public int TagId { get; set; } // Second part of composite PK; FK to Tag

    public Post Post { get; set; } // Reference navigation
    public Tag Tag { get; set; } // Reference navigation
}
```
Первиний ключ для сутності PostTag можна всановити інакше 

```cs
public class ApplicationDbContext : DbContext
{
  //...
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PostTag>().HasKey(e => new { e.PostId, e.TagId });
    }
}
```
Зверніть увагу, що тип сутності зв’язку PostTag містить дві властивості зовнішнього ключа. У цій моделі для того, щоб допис був пов’язаний із тегом, має існувати сполучна сутність PostTag, у якій значення зовнішнього ключа PostTag.PostId збігається зі значенням первинного ключа Post.Id, а значення зовнішнього ключа PostTag.TagId збігається зі значенням первинного ключа Tag.Id. 

```cs
static async Task Many_to_many_relationships_1()
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);

    await CleanAndPopulateDatabase();

    var post = await context.Posts.SingleAsync(e => e.Id == 3);
    var tag = await context.Tags.SingleAsync(e => e.Id == 1);

    context.Add(new PostTag { PostId = post.Id, TagId = tag.Id });

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    await context.SaveChangesAsync();
}
```
Якщо після виконання цього коду переглянути налагоджувальне представлення відстежувача змін, можна побачити, що допис і тег пов’язані через нову сполучну сутність PostTag:

```
Post {Id: 3} Unchanged
    Id: 3 PK
    BlogId: 2 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  PostTags: [{PostId: 3, TagId: 1}]
PostTag {PostId: 3, TagId: 1} Added
    PostId: 3 PK FK
    TagId: 1 PK FK
  Post: {Id: 3}
  Tag: {Id: 1}
Tag {Id: 1} Unchanged
    Id: 1 PK
    Text: '.NET'
  PostTags: [{PostId: 3, TagId: 1}]
```
```sql
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (15ms) [Parameters=[@p0='3', @p1='1'], CommandType='Text', CommandTimeout='30']
      SET IMPLICIT_TRANSACTIONS OFF;
      SET NOCOUNT ON;
      INSERT INTO [PostTag] ([PostId], [TagId])
      VALUES (@p0, @p1);
```
Зверніть увагу, що навігаційні властивості колекцій у сутностях Post і Tag, а також навігаційні властивості посилань у PostTag, були належним чином налаштовані. Цими зв’язками можна керувати за допомогою навігаційних властивостей замість значень зовнішніх ключів (FK) — так само, як і в усіх попередніх прикладах. Наприклад, наведений вище код можна змінити для додавання зв’язку, встановивши навігаційні посилання в сутності об’єднання:



```cs
static async Task Many_to_many_relationships_2()
{
    // ... the same code

    context.Add(new PostTag { Post = post, Tag = tag });

    // ... the same code
}
```
Це призводить до точно таких самих змін у зовнішніх ключах і навігаційних властивостях, як і в попередньому прикладі.

## Навігація з пропуском 

Ручне керування проміжною таблицею може бути громіздким. Зв’язками типу many-to-many можна керувати безпосередньо за допомогою спеціальних навігаційних властивостей колекцій, які «пропускають» проміжну сутність. Наприклад, до наведеної вище моделі можна додати два переходи для швидкої навігації: один — від публікації до тегів, а інший — від тегу до публікацій:

Types.cs
```cs
public class Post
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int? BlogId { get; set; }
    public Blog Blog { get; set; }

    public IList<Tag> Tags { get; } = new List<Tag>(); // Skip collection navigation
    public IList<PostTag> PostTags { get; } = new List<PostTag>(); // Collection navigation
}

public class Tag
{
    public int Id { get; set; }
    public string Text { get; set; }

    public IList<Post> Posts { get; } = new List<Post>(); // Skip collection navigation
    public IList<PostTag> PostTags { get; } = new List<PostTag>(); // Collection navigation
}

// The entity TPostTag remains unchanged.
```

Цей зв’язок many-to-many потребує такої конфігурації, щоб гарантувати, що як пропускаючі навігаційні властивості, так і звичайні навігаційні властивості використовуватимуться для одного й того самого зв’язку many-to-many :

```cs
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PostTag>().HasKey(e => new { e.PostId, e.TagId });

        modelBuilder.Entity<Post>()
        .HasMany(p => p.Tags)
        .WithMany(p => p.Posts)
        .UsingEntity<PostTag>(
            j => j.HasOne(t => t.Tag).WithMany(p => p.PostTags),
            j => j.HasOne(t => t.Post).WithMany(p => p.PostTags));

    }
```
Додаткову інформацію про відображення зв’язків many-to-many див. у розділі «Зв’язки сутностей».

Пропускаючи навігаційні властивості виглядають і поводяться так само, як звичайні навігаційні властивості колекцій. Однак спосіб роботи зі значеннями зовнішніх ключів тут інший. Пов’яжімо допис із тегом, але цього разу скористаймося пропускаючею навігацією (skip navigation): 

```cs
static async Task Many_to_many_relationships_3()
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);

    await CleanAndPopulateDatabase();

    var post = await context.Posts.SingleAsync(e => e.Id == 3);
    var tag = await context.Tags.SingleAsync(e => e.Id == 1);

    post.Tags.Add(tag);

    context.ChangeTracker.DetectChanges();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    await context.SaveChangesAsync();
}

```
Зверніть увагу, що цей код не використовує проміжну сутність. Натомість він просто додає сутність до навігаційної колекції так само, як це робиться у випадку зв’язку one-to-many. Отримане представлення для налагодження по суті таке саме, як і раніше:
```
Post {Id: 3} Unchanged
    Id: 3 PK
    BlogId: 2 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  PostTags: [{PostId: 3, TagId: 1}]
  Tags: [{Id: 1}]
PostTag {PostId: 3, TagId: 1} Added
    PostId: 3 PK FK
    TagId: 1 PK FK
  Post: {Id: 3}
  Tag: {Id: 1}
Tag {Id: 1} Unchanged
    Id: 1 PK
    Text: '.NET'
  PostTags: [{PostId: 3, TagId: 1}]
  Posts: [{Id: 3}]

info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (10ms) [Parameters=[@p0='3', @p1='1'], CommandType='Text', CommandTimeout='30']
      SET IMPLICIT_TRANSACTIONS OFF;
      SET NOCOUNT ON;
      INSERT INTO [PostTag] ([PostId], [TagId])
      VALUES (@p0, @p1);
```
Зверніть увагу, що екземпляр сутності зв’язку PostTag було створено автоматично, а значення його зовнішніх ключів (FK) встановлено відповідно до значень первинних ключів (PK) пов’язаних між собою тегу та допису. Усі стандартні навігаційні властивості для посилань і колекцій було скориговано відповідно до цих значень зовнішніх ключів (FK). Крім того, оскільки ця модель містить перехідну навігацію, їх також було скориговано. Зокрема, хоча ми й додали тег до колекції Post.Tags, зворотний зв’язок Tag.Posts з іншого боку цього відношення також було оновлено, щоб він містив відповідний допис.

Варто зазначити, що базовими зв’язками «багато-до-багатьох» усе ще можна керувати безпосередньо, навіть якщо поверх них додано навігаційні властивості типу skip navigation. Наприклад, тег і допис можна пов’язати так само, як ми це робили до впровадження засобів навігації з можливістю пропуску:

```cs
static async Task Many_to_many_relationships_4()
{
  //...

  context.Add(new PostTag { Post = post, Tag = tag });
  
  //...
}
```
Або з використанням значень зовнішніх ключів (FK):

```cs
static async Task Many_to_many_relationships_5()
{
  //...

 context.Add(new PostTag { PostId = post.Id, TagId = tag.Id });
  
  //...
}

```

Це все одно забезпечить належне налаштування пропускної навігації и, що дасть такий самий вивід у режимі налагодження, як і в попередньому прикладі.

## Тільки пропускні навігаційні властивості

У попередньому розділі ми додали навігаційні властивості пропуску на додаток до повного визначення двох базових зв’язків типу one-to-many. Це корисно для ілюстрації того, що відбувається зі значеннями зовнішніх ключів (FK), але часто є зайвим. Натомість, зв'язок many-to-many можна визначити, використовуючи лише пропускні навігації.

```cs
public class Post
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int? BlogId { get; set; }
    public Blog Blog { get; set; }

    public IList<Tag> Tags { get; } = new List<Tag>(); // Skip collection navigation
}

public class Tag
{
    public int Id { get; set; }
    public string Text { get; set; }

    public IList<Post> Posts { get; } = new List<Post>(); // Skip collection navigation
}
```
Використовуючи цю модель, ми можемо знову пов’язати допис (Post) і тег (Tag), додавши допис до колекції Tag.Posts (або, як варіант, додавши тег до колекції Post.Tags):

```cs
static async Task Many_to_many_relationships_6()
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);

    await CleanAndPopulateDatabase();

    var post = await context.Posts.SingleAsync(e => e.Id == 3);
    var tag = await context.Tags.SingleAsync(e => e.Id == 1);

    post.Tags.Add(tag);

    context.ChangeTracker.DetectChanges();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    await context.SaveChangesAsync();
}
```
Перегляд налагоджувальної інформації після внесення цієї зміни показує, що EF Core створив екземпляр Dictionary\<string, object\> для представлення сутності зв’язку. Ця сполучна сутність містить властивості зовнішніх ключів PostsId та TagsId, значення яких встановлено відповідно до значень первинних ключів (PK) пов’язаних публікації та тегу. 

```
Post {Id: 3} Unchanged
    Id: 3 PK
    BlogId: 2 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  Tags: [{Id: 1}]
Tag {Id: 1} Unchanged
    Id: 1 PK
    Text: '.NET'
  Posts: [{Id: 3}]
PostTag (Dictionary<string, object>) {PostsId: 3, TagsId: 1} Added
    PostsId: 3 PK FK
    TagsId: 1 PK FK
```
```sql
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (19ms) [Parameters=[@p0='3', @p1='1'], CommandType='Text', CommandTimeout='30']
      SET IMPLICIT_TRANSACTIONS OFF;
      SET NOCOUNT ON;
      INSERT INTO [PostTag] ([PostsId], [TagsId])
      VALUES (@p0, @p1);
```
Додаткову інформацію про сутності неявного об’єднання (implicit join entities) та використання типів сутностей Dictionary\<string, object\> див. у розділі «Зв’язки сутностей».

  Важливо

  Тип CLR, що за замовчуванням використовується для типів сутностей об'єднання (join entity types), може змінитися в майбутніх випусках задля підвищення продуктивності. Не варто покладатися на те, що типом об'єднання буде Dictionary\<string, object\>, якщо це не було явно налаштовано.

## Об’єднання сутностей із додатковими даними (payloads)

Досі в усіх прикладах використовувався тип сутності для об’єднання (явний або неявний), що містив лише дві властивості зовнішніх ключів, необхідні для реалізації зв’язку many-to-many. Жодне з цих значень зовнішнього ключа (FK) не потрібно задавати явно в програмі під час роботи зі зв’язками, оскільки вони беруться зі властивостей первинного ключа пов’язаних сутностей. Це дозволяє EF Core створювати екземпляри сутності зв’язку без втрати даних.

## Корисне навантаження зі згенерованими значеннями

EF Core підтримує додавання додаткових властивостей до типу сутності зв'язку. Це називають додаванням «корисного навантаження» (payload) до сутності зв'язку. Наприклад, додамо властивість TaggedOn до сутності зв'язку PostTag:

```cs
public class PostTag
{
    public int PostId { get; set; } // First part of composite PK; FK to Post
    public int TagId { get; set; } // Second part of composite PK; FK to Tag

    public DateTime TaggedOn { get; set; } // Payload
}
```
Ця властивість корисного навантаження не встановлюватиметься, коли EF Core створюватиме екземпляр сутності зв’язку. Найпоширеніший спосіб вирішення цього питання — використання властивостей із автоматично згенерованими значеннями. Наприклад, властивість TaggedOn можна налаштувати так, щоб вона використовувала мітку часу, згенеровану на рівні сховища даних, під час вставлення кожної нової сутності:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(p => p.Tags)
        .WithMany(p => p.Posts)
        .UsingEntity<PostTag>(
            j => j.HasOne<Tag>().WithMany(),
            j => j.HasOne<Post>().WithMany(),
            j => j.Property(e => e.TaggedOn).HasDefaultValueSql("CURRENT_TIMESTAMP"));
}
```

Тепер публікацію можна позначити так само, як і раніше:

```cs
static async Task Many_to_many_relationships_7()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    await CleanAndPopulateDatabase();

    var post = await context.Posts.SingleAsync(e => e.Id == 3);
    var tag = await context.Tags.SingleAsync(e => e.Id == 1);

    post.Tags.Add(tag);

    await context.SaveChangesAsync();

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
Перегляд налагоджувальної інформації відстежувача змін після виклику SaveChanges показує, що властивість TaggedOn була встановлена ​​належним чином:

```
Post {Id: 3} Unchanged
    Id: 3 PK
    BlogId: 2 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  Tags: [{Id: 1}]
PostTag {PostId: 3, TagId: 1} Unchanged
    PostId: 3 PK FK
    TagId: 1 PK FK
    TaggedOn: '13.10.2050 12:19:28'
Tag {Id: 1} Unchanged
    Id: 1 PK
    Text: '.NET'
  Posts: [{Id: 3}]
```

## Явне задання значень корисного навантаження

Продовжуючи попередній приклад, додамо властивість корисного навантаження, яка не використовує автоматично згенероване значення:

```cs
public class PostTag
{
    public int PostId { get; set; } // First part of composite PK; FK to Post
    public int TagId { get; set; } // Second part of composite PK; FK to Tag

    public DateTime TaggedOn { get; set; } // Auto-generated payload property
    public string TaggedBy { get; set; } // Not-generated payload property
}
```

Тепер публікацію можна позначати тегами так само, як і раніше, а проміжну сутність (join entity) і надалі буде створено автоматично. До цього об’єкта можна отримати доступ за допомогою одного з механізмів, описаних у розділі «Доступ до відстежуваних сутностей». Наприклад, у наведеному нижче коді використовується метод DbSet\<TEntity\>.Find для доступу до екземпляра сутності зв’язку:

```cs
static async Task Many_to_many_relationships_8()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    await CleanAndPopulateDatabase();

    var post = await context.Posts.SingleAsync(e => e.Id == 3);
    var tag = await context.Tags.SingleAsync(e => e.Id == 1);

    post.Tags.Add(tag);

    context.ChangeTracker.DetectChanges();

    PostTag? joinEntity = await context.Set<PostTag>().FindAsync(post.Id, tag.Id);

    if (joinEntity is not null)
    {
        joinEntity.TaggedOn = DateTime.Now;
        joinEntity.TaggedBy = "ajcvickers";
    }
    else
    {
        throw new InvalidOperationException($"Join entity for PostId={post.Id} and TagId={tag.Id} was not found.");
    }

    await context.SaveChangesAsync();

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
Після того як об'єкт зв'язку знайдено, з ним можна працювати у звичний спосіб — наприклад, щоб встановити властивість корисного навантаження TaggedBy перед викликом SaveChanges.


```
Post {Id: 3} Unchanged
    Id: 3 PK
    BlogId: 2 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  Tags: [{Id: 1}]
PostTag {PostId: 3, TagId: 1} Unchanged
    PostId: 3 PK FK
    TagId: 1 PK FK
    TaggedBy: 'ajcvickers'
    TaggedOn: '13.10.2050 12:57:06'
Tag {Id: 1} Unchanged
    Id: 1 PK
    Text: '.NET'
  Posts: [{Id: 3}]
```
  Примітка

  Зауважте, що тут необхідно викликати ChangeTracker.DetectChanges(), щоб дозволити EF Core виявити зміну навігаційної властивості та створити екземпляр сутності зв’язку перед використанням методу Find. Докладнішу інформацію див. у розділі «Виявлення змін і сповіщення».

Як варіант, сутність зв’язку можна створити явно, щоб пов’язати допис із тегом.

```cs
static async Task Many_to_many_relationships_9()
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);
    Console.WriteLine(context.Model.ToDebugString());
   
    await CleanAndPopulateDatabase();

    var post = await context.Posts.SingleAsync(e => e.Id == 3);
    var tag = await context.Tags.SingleAsync(e => e.Id == 1);

    context.Add(
        new PostTag
        {
            PostId = post.Id,
            TagId = tag.Id,
            TaggedBy = "ajcvickers"
        });

    await context.SaveChangesAsync();

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
Нарешті, ще один спосіб задати дані корисного навантаження — це перевизначити метод `SaveChanges` або скористатися подією DbContext.SavingChanges для обробки сутностей перед оновленням бази даних.

AplicationsDBContext.cs
```cs
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entityEntry in ChangeTracker.Entries<PostTag>())
        {
            if (entityEntry.State == EntityState.Added)
            {
                entityEntry.Entity.TaggedBy = "Alex";
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
```
```cs
static async Task Many_to_many_relationships_10()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    await CleanAndPopulateDatabase();

    var post = await context.Posts.SingleAsync(e => e.Id == 3);
    var tag = await context.Tags.SingleAsync(e => e.Id == 1);

    post.Tags.Add(tag);

    await context.SaveChangesAsync();

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
```
Post {Id: 3} Unchanged
    Id: 3 PK
    BlogId: 2 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  Tags: [{Id: 1}]
PostTag {PostId: 3, TagId: 1} Unchanged
    PostId: 3 PK FK
    TagId: 1 PK FK
    TaggedBy: 'Alex'
    TaggedOn: '22.10.2056 11:49:34'
Tag {Id: 1} Unchanged
    Id: 1 PK
    Text: '.NET'
  Posts: [{Id: 3}]
```