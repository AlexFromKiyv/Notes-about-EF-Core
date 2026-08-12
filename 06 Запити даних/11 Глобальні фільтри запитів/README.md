# Глобальні фільтри запитів

Глобальні фільтри запитів дозволяють прив’язати фільтр до типу сутності та застосовувати цей фільтр щоразу, коли виконується запит до цього типу сутності; уявіть їх як додатковий оператор LINQ Where, який додається щоразу, коли запитується тип сутності. Такі фільтри корисні в різних випадках.

## Базовий приклад – м’яке видалення

У деяких сценаріях, замість видалення рядка з бази даних, краще встановити прапорець IsDeleted, щоб позначити рядок як видалений; цей шаблон називається м’яким видаленням. М’яке видалення дозволяє відновити видалені рядки за потреби або зберегти журнал аудиту, де видалені рядки все ще доступні. Глобальні фільтри запитів можна використовувати для фільтрації рядків, видалених за замовчуванням, водночас дозволяючи вам отримувати до них доступ у певних місцях, вимкнувши фільтр для певного запиту.

Щоб увімкнути м’яке видалення, додамо властивість IsDeleted до нашого типу Blog:

```cs
public class Blog
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }

    public string Name { get; set; }
}
```
Тепер ми налаштовуємо глобальний фільтр запитів, використовуючи API HasQueryFilter в OnModelCreating:

```cs
modelBuilder.Entity<Blog>().HasQueryFilter(b => !b.IsDeleted);
```
Тепер ми можемо запитувати наші сутності блогу як завжди; налаштований фільтр гарантуватиме, що всі запити – за замовчуванням – фільтруватимуть усі випадки, коли IsDeleted має значення true.

Зверніть увагу, що на цьому етапі вам потрібно вручну встановити IsDeleted, щоб видалити сутність методом м’якого видалення. Для більш комплексного рішення ви можете перевизначити метод SaveChangesAsync вашого типу контексту, щоб додати логіку, яка обробляє всі видалені користувачем сутності та змінює їх на модифіковані, встановивши властивість IsDeleted у значення true:

```cs
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    ChangeTracker.DetectChanges();

    foreach (var item in ChangeTracker.Entries<Blog>().Where(e => e.State == EntityState.Deleted))
    {
        item.State = EntityState.Modified;
        item.CurrentValues["IsDeleted"] = true;
    }

    return await base.SaveChangesAsync(cancellationToken);
}
```
Це дозволяє використовувати EF API, які видаляють екземпляр сутності як завжди, а натомість видаляють його м’яко.

```cs
    await AddBlogs();
    await DeleteBlog(1);
```
```cs
static async Task AddBlogs()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);
    var blog1 = new Blog { Name = "Blog 1" };
    var blog2 = new Blog { Name = "Blog 2" };
    context.Blogs.Add(blog1);
    context.Blogs.Add(blog2);
    await context.SaveChangesAsync();
    Console.WriteLine("Blogs added to the database.");
}

static async Task DeleteBlog(int blogId)
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);
    var blog = await context.Blogs.FindAsync(blogId);
    if (blog != null)
    {
        context.Blogs.Remove(blog);
        await context.SaveChangesAsync();
        Console.WriteLine($"Blog with ID {blogId} marked as deleted.");
    }
    else
    {
        Console.WriteLine($"Blog with ID {blogId} not found.");
    }
}
```

```cs
    var query = context.Blogs;
    Console.WriteLine(query.ToQueryString());
    var result = await query.ToListAsync();

    foreach(var blog in result)
    {
        Console.WriteLine($"Blog ID: {blog.Id}, Name: {blog.Name}, IsDeleted: {blog.IsDeleted}");
    }
```

```sql
SELECT [b].[Id], [b].[IsDeleted], [b].[Name]
FROM [Blogs] AS [b]
WHERE [b].[IsDeleted] = CAST(0 AS bit)
Blog ID: 2, Name: Blog 2, IsDeleted: False
```

## Використання контекстних даних – багатокористувацька оренда

Ще одним поширеним сценарієм для глобальних фільтрів запитів є багатокористувацька оренда, де ваша програма зберігає дані, що належать різним користувачам, в одній таблиці. У таких випадках зазвичай є стовпець ідентифікатора орендаря, який пов’язує рядок із певним орендарем, а глобальні фільтри запитів можна використовувати для автоматичної фільтрації рядків поточного орендаря. Це забезпечує надійну ізоляцію клієнта для ваших запитів за замовчуванням, усуваючи необхідність думати про фільтрацію клієнта в кожному запиті.

На відміну від м’якого видалення, багатокористувацька оренда вимагає знання поточного ідентифікатора клієнта; це значення зазвичай визначається, наприклад, коли користувач автентифікується через Інтернет. Для цілей EF ідентифікатор орендаря має бути доступний в екземплярі контексту, щоб глобальний фільтр запитів міг посилатися на нього та використовувати його під час запитів. Давайте приймемо параметр tenantId у конструкторі нашого контекстного типу та посилатимемося на нього з нашого фільтра:

```cs
public class MultitenancyContext(string tenantId) : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>().HasQueryFilter(b => b.TenantId == tenantId);
    }
}
```

Це змушує будь-кого, хто створює контекст, вказувати пов'язаний з ним ідентифікатор клієнта, і гарантує, що за замовчуванням із запитів повертатимуться лише сутності блогу з цим ідентифікатором.

Примітка

У цьому прикладі показано лише основні концепції багатокористувацького користування, необхідні для демонстрації глобальних фільтрів запитів. Для отримання додаткової інформації про багатокористувацьке користування та EF див. розділ про багатокористувацьке користування в EF Core додатках.

```cs
public class Blog
{
    public int Id { get; set; }
    public required string TenantId { get; set; }

    public string Name { get; set; }
}
```

```cs
public class MultitenancyContext(string tenantId) : DbContext
{
    public DbSet<Blog> Blogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(
            @"Server=(localdb)\mssqllocaldb;Database=MyDB;Trusted_Connection=True;ConnectRetryCount=0");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>().HasQueryFilter(b => b.TenantId == tenantId);
    }
}
```

```cs
    await CreataDB();
    await AddBlogs();
    await GetAllBlogForMary();

```
```cs
static async Task CreataDB()
{
    using var context = new MultitenancyContext("John");

    await context.Database.EnsureDeletedAsync();
    await context.Database.EnsureCreatedAsync();
}
static async Task AddBlogs()
{
    using var context = new MultitenancyContext("John");
    var blog1 = new Blog { Name = "Blog 1", TenantId = "John" };
    var blog2 = new Blog { Name = "Blog 2", TenantId = "Mary" };
    context.Blogs.Add(blog1);
    context.Blogs.Add(blog2);
    await context.SaveChangesAsync();
    Console.WriteLine("Blogs added to the database.");
}

static async Task GetAllBlogForMary()
{
    using var context = new MultitenancyContext("Mary");

    var query = context.Blogs;
    Console.WriteLine(query.ToQueryString());

    var result = await query.ToListAsync();
    foreach (var blog in result)
    {
        Console.WriteLine(blog.Name+"\t"+blog.TenantId);
    }
}
```

```
Blogs added to the database.
DECLARE @__P_0 nvarchar(4000) = N'Mary';

SELECT [b].[Id], [b].[Name], [b].[TenantId]
FROM [Blogs] AS [b]
WHERE [b].[TenantId] = @__P_0
Blog 2  Mary
```

## Використання кількох фільтрів запитів

Виклик HasQueryFilter з простим фільтром перезаписує будь-який попередній фільтр, тому кілька фільтрів не можна визначити для одного типу сутності таким чином:

```cs
modelBuilder.Entity<Blog>().HasQueryFilter(b => !b.IsDeleted);
// The following overwrites the previous query filter:
modelBuilder.Entity<Blog>().HasQueryFilter(b => b.TenantId == tenantId);
```

Щоб визначити кілька фільтрів запитів для одного типу сутності, їх необхідно назвати:

```cs
modelBuilder.Entity<Blog>()
    .HasQueryFilter("SoftDeletionFilter", b => !b.IsDeleted)
    .HasQueryFilter("TenantFilter", b => b.TenantId == tenantId);
```
Це дає змогу керувати кожним фільтром окремо, зокрема вибірково вимикаючи один, але не інший.

До версії EF 10 ви могли прив’язати кілька фільтрів до типу сутності, викликавши HasQueryFilter один раз та об’єднавши свої фільтри за допомогою оператора &&:

```cs
modelBuilder.Entity<Blog>().HasQueryFilter(b => !b.IsDeleted && b.TenantId == tenantId);
```

## Вимкнення фільтрів

Фільтри можна вимкнути для окремих запитів LINQ за допомогою оператора IgnoreQueryFilters:

```cs
static async Task GetAllBlog()
{
    using var context = new MultitenancyContext("Mary");

    var query = context.Blogs.IgnoreQueryFilters();

    Console.WriteLine(query.ToQueryString());

    var result = await query.ToListAsync();
    foreach (var blog in result)
    {
        Console.WriteLine(blog.Name+"\t"+blog.TenantId);
    }
}
```
```
SELECT [b].[Id], [b].[Name], [b].[TenantId]
FROM [Blogs] AS [b]
Blog 1  John
Blog 2  Mary
```
Якщо налаштовано кілька іменованих фільтрів, це вимикає їх усі. Щоб вибірково вимкнути певні фільтри (починаючи з EF 10), передайте список імен фільтрів, які потрібно вимкнути:

```cs
var allBlogs = await context.Blogs.IgnoreQueryFilters(["SoftDeletionFilter"]).ToListAsync();
```

## Фільтри запитів та обов'язкові навігації

Увага

Використання обов'язкової навігації для доступу до сутності, у якій визначено глобальний фільтр запитів, може призвести до неочікуваних результатів.

Необхідні навігації в EF означають, що пов’язана сутність завжди присутня. Оскільки внутрішні об'єднання можуть використовуватися для отримання пов'язаних сутностей, якщо потрібну пов'язану сутність відфільтровує фільтр запиту, батьківська сутність також може бути відфільтрована. Це може призвести до неочікуваного отримання меншої кількості елементів, ніж очікувалося.

Щоб проілюструвати проблему, ми можемо використати сутності Blog та Post та налаштувати їх наступним чином:

```cs
//modelBuilder.Entity<Blog>().HasMany(b => b.Posts).WithOne(p => p.Blog).IsRequired();
modelBuilder.Entity<Blog>().HasQueryFilter(b => b.Url.Contains("fish"));
```
Модель може бути заповнена такими даними:

```cs
public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
    public List<Post> Posts { get; set; }
}
public class Post
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public Blog Blog { get; set; }
}
```

```cs
    context.Blogs.Add(
    new Blog
    {
        Url = "http://sample.com/blogs/fish",
        Posts =
        [
            new() { Title = "Fish care 101" },
            new() { Title = "Caring for tropical fish" },
            new() { Title = "Types of ornamental fish" }
        ]
    });

    context.Blogs.Add(
        new Blog
        {
            Url = "http://sample.com/blogs/cats",
            Posts =
            [
                new() { Title = "Cat care 101" },
            new() { Title = "Caring for tropical cats" },
            new() { Title = "Types of ornamental cats" }
            ]
        });
    await context.SaveChangesAsync();

```
Проблему можна спостерігати під час виконання наступних двох запитів:

```cs

    var query1 = context.Posts;
    var query2 = context.Posts.Include(p => p.Blog);

    Console.WriteLine(query1.ToQueryString());
    Console.WriteLine();
    Console.WriteLine(query2.ToQueryString());
    Console.WriteLine();

    var result = await query2.ToListAsync();

    foreach (var post in result)
    {
        Console.WriteLine($"{post.PostId}\t{post.Title}\t{post?.Blog?.Url}");
    }
```

З урахуванням вищезазначеної конфігурації перший запит повертає всі 6 екземплярів Post, але другий запит повертає лише 3. Ця невідповідність виникає, оскільки метод Include у другому запиті завантажує пов'язані сутності Blog. Оскільки потрібна навігація між Blog та Post, EF Core використовує INNER JOIN під час побудови запиту.

```sql
SELECT [p].[PostId], [p].[BlogId], [p].[Title]
FROM [Posts] AS [p]

SELECT [p].[PostId], [p].[BlogId], [p].[Title], [b0].[BlogId], [b0].[Url]
FROM [Posts] AS [p]
INNER JOIN (
    SELECT [b].[BlogId], [b].[Url]
    FROM [Blogs] AS [b]
    WHERE [b].[Url] LIKE N'%fish%'
) AS [b0] ON [p].[BlogId] = [b0].[BlogId]

1       Fish care 101   http://sample.com/blogs/fish
2       Caring for tropical fish        http://sample.com/blogs/fish
3       Types of ornamental fish        http://sample.com/blogs/fish
```
Використання INNER JOIN фільтрує всі рядки публікацій, пов'язані з якими рядки блогу були відфільтровані фільтром запиту. Цю проблему можна вирішити, налаштувавши навігацію як необов'язкову, що призведе до генерації LEFT JOIN замість INNER JOIN:

```cs
modelBuilder.Entity<Blog>().HasMany(b => b.Posts).WithOne(p => p.Blog).IsRequired(false);
modelBuilder.Entity<Blog>().HasQueryFilter(b => b.Url.Contains("fish"));
```
або

```cs
    public Blog? Blog { get; set; }
```

```sql
SELECT [p].[PostId], [p].[BlogId], [p].[Title], [b0].[BlogId], [b0].[Url]
FROM [Posts] AS [p]
LEFT JOIN (
    SELECT [b].[BlogId], [b].[Url]
    FROM [Blogs] AS [b]
    WHERE [b].[Url] LIKE N'%fish%'
) AS [b0] ON [p].[BlogId] = [b0].[BlogId]

1       Fish care 101   http://sample.com/blogs/fish
2       Caring for tropical fish        http://sample.com/blogs/fish
3       Types of ornamental fish        http://sample.com/blogs/fish
4       Cat care 101
5       Caring for tropical cats
6       Types of ornamental cats
```
Альтернативний підхід полягає у визначенні узгоджених фільтрів для обох типів сутностей: Блог та Публікація; після застосування відповідних фільтрів до Блогу та Публікації рядки Публікації, які могли опинитися в неочікуваному стані, видаляються, і обидва запити повертають 3 результати.

```cs
modelBuilder.Entity<Blog>().HasMany(b => b.Posts).WithOne(p => p.Blog).IsRequired();
modelBuilder.Entity<Blog>().HasQueryFilter(b => b.Url.Contains("fish"));
modelBuilder.Entity<Post>().HasQueryFilter(p => p.Blog.Url.Contains("fish"));
```
```sql
SELECT [p].[PostId], [p].[BlogId], [p].[Title]
FROM [Posts] AS [p]
INNER JOIN (
    SELECT [b].[BlogId], [b].[Url]
    FROM [Blogs] AS [b]
    WHERE [b].[Url] LIKE N'%fish%'
) AS [b0] ON [p].[BlogId] = [b0].[BlogId]
WHERE [b0].[Url] LIKE N'%fish%'

SELECT [p].[PostId], [p].[BlogId], [p].[Title], [b0].[BlogId], [b0].[Url]
FROM [Posts] AS [p]
INNER JOIN (
    SELECT [b].[BlogId], [b].[Url]
    FROM [Blogs] AS [b]
    WHERE [b].[Url] LIKE N'%fish%'
) AS [b0] ON [p].[BlogId] = [b0].[BlogId]
WHERE [b0].[Url] LIKE N'%fish%'

1       Fish care 101
2       Caring for tropical fish
3       Types of ornamental fish
```

## Фільтри запитів та IEntityTypeConfiguration

Якщо вашому фільтру запитів потрібен доступ до tenant ID  або подібної контекстної інформації, IEntityTypeConfiguration\<TEntity\> може створювати додаткові труднощі, оскільки, на відміну від OnModelCreating, немає екземпляра вашого типу контексту, на який можна було б легко посилатися з фільтра запитів. Як тимчасове рішення, додайте фіктивний контекст до вашого типу конфігурації та посилайтеся на нього наступним чином:

```cs
private sealed class CustomerEntityConfiguration : IEntityTypeConfiguration<Customer>
{
    private readonly SomeDbContext _context = null!;

    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasQueryFilter(d => d.TenantId == _context.TenantId);
    }
}
```

## Обмеження

Глобальні фільтри запитів мають такі обмеження:

* Фільтри можна визначити лише для кореневого типу сутності ієрархії успадкування.
* Наразі EF Core не виявляє цикли у визначеннях глобальних фільтрів запитів, тому слід бути обережним під час їх визначення. Якщо вказати цикли неправильно, вони можуть призвести до нескінченних зациклень під час перетворення запитів.
