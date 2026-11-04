# Розв’язання ідентичності в EF Core

DbContext може відстежувати лише один екземпляр сутності для кожного конкретного значення первинного ключа. Це означає, що кілька екземплярів сутності з однаковим значенням ключа мають бути зведені до одного екземпляра. Це називається «розв’язанням ідентичності» (identity resolution). Розв’язання ідентичності (identity resolution) гарантує, що Entity Framework Core (EF Core) відстежує узгоджений граф, у якому немає жодної неоднозначності щодо зв’язків або значень властивостей сутностей.

    Порада

    У цьому документі передбачається, що ви розумієте стани сутностей та основи відстеження змін в EF Core. Докладнішу інформацію з цих питань див. у розділі «Відстеження змін в EF Core». 


## Початкова модель для прикладів.

Types.cs
```cs
public class Blog
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Summary { get; set; } = string.Empty;

    public IList<Post> Posts { get; } = new List<Post>();
}

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int? BlogId { get; set; }
    public Blog Blog { get; set; }
}
```
```cs
public class ApplicationDbContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}
```
Допоміжні методи.

Helpers.cs
```cs
partial class Program
{
    private static async Task CleanAndPopulateDatabase()
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

    static async Task PopulateDatabase(ApplicationDbContext context)
    {
        context.Add(
            new Blog
            {
                Name = ".NET Blog",
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
                }
            });

        await context.SaveChangesAsync();
    }

    public static async Task ShowBlogs()
    {
        Console.WriteLine("\n\n");
        using var context = new ApplicationDbContextFactory().CreateDbContext(null);
        foreach (Blog blog in context.Blogs.Include(p => p.Posts)) 
        { 
            ShowBlog( blog );
            Console.WriteLine("\n");
        }
        Console.WriteLine("\n\n");
    }
    public static void ShowBlog(Blog blog)
    {
                Console.WriteLine($"\t{blog.Id}\t{blog.Name}\t{blog.Summary}\t{blog.Posts.Count}")
        foreach (var post in blog.Posts)
        {
            ShowPost(post);
        }
    }

    public static void ShowPost(Post post)
    {
        Console.WriteLine($"\t\t{post.Id}\t{post.Title}");
    }

}
```

Program.cs
```cs
static async Task DoAsync()
{
    await CleanAndPopulateDatabase();

    await ShowBlogs();
}
await DoAsync();
```

## Вступ

Наведений нижче код запитує сутність, а потім намагається приєднати інший екземпляр з тим самим значенням первинного ключа:
Program.cs
```cs
static async Task Identity_Resolution_in_EF_Core_1()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Blog blogA = await context.Blogs.SingleAsync(e => e.Id == 1);

    Blog blogB = new Blog { Id = 1, Name = ".NET Blog (All new!)" };

    try
    {
        context.Update(blogB); // This will throw
    }
    catch (Exception e)
    {
        Console.WriteLine($"{e.GetType().FullName}: {e.Message}");
    }
}
```
Program.cs
```cs
static async Task DoAsync(Func<Task> doIt)
{
    await CleanAndPopulateDatabase();

    await ShowBlogs();

    await doIt();
    
    await ShowBlogs();
}
await DoAsync(Identity_Resolution_in_EF_Core_1);
```
Виконання цього коду призводить до наступного винятку:
```
System.InvalidOperationException: The instance of entity type 'Blog' cannot be tracked because another instance with the same key value for {'Id'} is already being tracked. When attaching existing entities, ensure that only one entity instance with a given key value is attached. Consider using 'DbContextOptionsBuilder.EnableSensitiveDataLogging' to see the conflicting key values.
```
EF Core потребує одного екземпляра, оскільки:

* Значення властивостей можуть відрізнятися між кількома екземплярами. Під час оновлення бази даних EF Core потрібно знати, які значення властивостей використовувати.
* Відносини з іншими сутностями можуть відрізнятися між кількома примірниками. Наприклад, «blogA» може стосуватися іншої колекції дописів, ніж «blogB».

Вищезазначений виняток зазвичай зустрічається в таких ситуаціях:

* Під час спроби оновлення об'єкта
* Під час спроби відстеження серіалізованого графа сутностей
* Якщо не вдається встановити значення ключа, яке не генерується автоматично
* Під час повторного використання екземпляра DbContext для кількох одиниць роботи

Кожна з цих ситуацій обговорюється в наступних розділах.

## Оновлення сутності

Існує кілька різних підходів до оновлення сутності новими значеннями, як описано в розділах «Відстеження змін в EF Core» та «Явне відстеження сутностей». Ці підходи описані нижче в контексті розв'язання ідентифікаційних даних. Важливо зазначити, що кожен із підходів використовує або запит, або виклик одного з методів Update або Attach, але ніколи одночасно.

## Виклик Update

Часто сутність для оновлення не походить із запиту до DbContext, який ми збираємося використовувати для SaveChanges. Наприклад, у веб-застосунку екземпляр сутності може бути створений з інформації в POST-запиті. Найпростіший спосіб вирішити цю проблему — використовувати DbContext.Update або DbSet\<TEntity\>.Update. 
Наприклад:

```cs
static async Task UpdateFromHttpPost1(Blog blog)
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);

    context.Update(blog);

    await context.SaveChangesAsync();
}
```

У цьому випадку:

* Створюється лише один екземпляр сутності.
* Екземпляр сутності не запитується з бази даних під час оновлення.
* Усі значення властивостей будуть оновлені в базі даних, незалежно від того, чи вони фактично змінилися чи ні.
* Здійснюється один цикл обробки даних.


Використаємо метод.

```cs
static async Task Updating_an_entity_1()
{

    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);

    Blog blog = new Blog { Id = 1, Name = ".NET Blog (All new!)" };

    await UpdateFromHttpPost1(blog);
}
```
```
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (6ms) [Parameters=[@p2='1', @p0='.NET Blog (All new!)' (Nullable = false) (Size = 4000), @p1='' (Nullable = false) (Size = 4000)], CommandType='Text', CommandTimeout='30']
      SET IMPLICIT_TRANSACTIONS OFF;
      SET NOCOUNT ON;
      UPDATE [Blogs] SET [Name] = @p0, [Summary] = @p1
      OUTPUT 1
      WHERE [Id] = @p2;
```

## Запит, а потім застосування змін

Зазвичай невідомо, які значення властивостей фактично були змінені, коли сутність створюється з інформації в POST-запиті або подібному. Часто достатньо просто оновити всі значення в базі даних, як ми зробили в попередньому прикладі. Однак, якщо програма обробляє багато сутностей, і лише невелика кількість з них має фактичні зміни, тоді може бути корисним обмежити кількість надісланих оновлень. Цього можна досягти, виконавши запит для відстеження сутностей у їхньому поточному стані в базі даних, а потім застосувавши зміни до цих відстежуваних сутностей. Наприклад:

```cs
static async Task UpdateFromHttpPost2(Blog blog)
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);
    
    var trackedBlog = await context.Blogs.FindAsync(blog.Id);
    if (trackedBlog != null)
    {
        trackedBlog.Name = blog.Name;
        await context.SaveChangesAsync();
    }
}
```

У цьому випадку:
* Відстежується лише один екземпляр сутності; той, що повертається з бази даних запитом Find.
* Update, Attach, тощо не використовуються.
* Здійснюється два цикли обробки бази даних.

Використаємо.

```cs
static async Task Updating_an_entity_2()
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);

    Blog blog = new Blog { Id = 1, Name = ".NET Blog (All new!)" };

    await UpdateFromHttpPost2(blog);
}
```
```
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (14ms) [Parameters=[@__p_0='1'], CommandType='Text', CommandTimeout='30']
      SELECT TOP(1) [b].[Id], [b].[Name], [b].[Summary]
      FROM [Blogs] AS [b]
      WHERE [b].[Id] = @__p_0
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (3ms) [Parameters=[@p1='1', @p0='.NET Blog (All new!)' (Nullable = false) (Size = 4000)], CommandType='Text', CommandTimeout='30']
      SET IMPLICIT_TRANSACTIONS OFF;
      SET NOCOUNT ON;
      UPDATE [Blogs] SET [Name] = @p0
      OUTPUT 1
      WHERE [Id] = @p1;
```
Як видно оновлюється те шо дійсно потребує змін.

EF Core має кілька помічників для передачі значень властивостей у спосіб, як цей. Наприклад, PropertyValues.SetValues ​​скопіює всі значення з заданого об'єкта та встановить їх на відстежуваному об'єкті:

```cs
static async Task UpdateFromHttpPost3(Blog blog)
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);
    var trackedBlog = await context.Blogs.FindAsync(blog.Id);
    if (trackedBlog != null)
    {
        context.Entry(trackedBlog).CurrentValues.SetValues(blog);
        await context.SaveChangesAsync();
    }
}
```
```cs
static async Task Updating_an_entity_3()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Blog blog = new Blog { Id = 1, Name = ".NET Blog (All new!)" };

    await UpdateFromHttpPost3(blog);
}
```
```
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (15ms) [Parameters=[@__p_0='1'], CommandType='Text', CommandTimeout='30']
      SELECT TOP(1) [b].[Id], [b].[Name], [b].[Summary]
      FROM [Blogs] AS [b]
      WHERE [b].[Id] = @__p_0
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (6ms) [Parameters=[@p2='1', @p0='.NET Blog (All new!)' (Nullable = false) (Size = 4000), @p1='' (Nullable = false) (Size = 4000)], CommandType='Text', CommandTimeout='30']
      SET IMPLICIT_TRANSACTIONS OFF;
      SET NOCOUNT ON;
      UPDATE [Blogs] SET [Name] = @p0, [Summary] = @p1
      OUTPUT 1
      WHERE [Id] = @p2;
```


SetValues ​​приймає різні типи об'єктів, включаючи об'єкти передачі даних (DTO) з іменами властивостей, що відповідають властивостям типу сутності. Наприклад:

Types.cs
```cs
public class BlogDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Summary { get; set; } 
}
```
```cs
static async Task UpdateFromHttpPost4(BlogDto dto)
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);
    var trackedBlog = await context.Blogs.FindAsync(dto.Id);
    if (trackedBlog != null)
    {
        context.Entry(trackedBlog).CurrentValues.SetValues(dto);
        await context.SaveChangesAsync();
    }
}
```
```cs
static async Task Updating_an_entity_4()
{

    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    await UpdateFromHttpPost4(
        new BlogDto { Id = 1, Name = ".NET Blog (All new!)", Summary = "Posts about .NET" });
}
```
Або словник із записами імен/значень для значень властивостей:

```cs
static async Task UpdateFromHttpPost5(Dictionary<string, object> propertyValues)
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);
    var trackedBlog = await context.Blogs.FindAsync(propertyValues["Id"]);
    if (trackedBlog != null)
    {
        context.Entry(trackedBlog).CurrentValues.SetValues(propertyValues);
        await context.SaveChangesAsync();
    }
}
```
```cs
static async Task Updating_an_entity_5()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    await UpdateFromHttpPost5(
        new Dictionary<string, object> 
        { 
            ["Id"] = 1, 
            ["Name"] = ".NET Blog (All new!)", 
            ["Summary"] = "Posts about .NET" 
        });
}
```
Див. розділ "Доступ до відстежуваних сутностей" для отримання додаткової інформації про роботу зі значеннями властивостей, подібними до цього..

## Використання оригінальних значень

Досі кожен підхід або виконував запит перед оновленням, або оновив усі значення властивостей незалежно від того, чи змінилися вони. Щоб оновити лише ті значення, які змінилися, без запиту в рамках оновлення, потрібна конкретна інформація про те, які значення властивостей змінилися. Поширений спосіб отримати цю інформацію – надіслати назад як поточні, так і початкові значення в HTTP Post або подібному файлі. Наприклад:

```cs
static async Task UpdateFromHttpPost6(Blog blog, Dictionary<string, object> originalValues)
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);
   
    context.Attach(blog);
    context.Entry(blog).OriginalValues.SetValues(originalValues);

    await context.SaveChangesAsync();
}
```
У цьому коді спочатку приєднується сутність зі зміненими значеннями. Це призводить до того, що EF Core відстежуватиме сутність у стані Unchanged , тобто без значень властивостей, позначених як змінені. Потім до цієї відстежуваної сутності застосовується словник оригінальних значень. Це позначить як Modified властивості з різними поточними та початковими значеннями. Властивості, які мають однакові поточні та початкові значення, не будуть позначені як Modified.

У цьому випадку:

* Відстежується лише один екземпляр сутності за допомогою Attach.
* Екземпляр сутності не запитується з бази даних під час оновлення.
* Застосування вихідних значень гарантує, що в базі даних оновлюються лише значення властивостей, які фактично змінилися.
* Виконується один цикл обробки бази даних.

Як і у прикладах із попереднього розділу, орігінальні значення не обов’язково передавати у вигляді словника; також підійде екземпляр сутності або DTO.


```cs
static async Task Updating_an_entity_6()
{
    await CleanAndPopulateDatabase();

    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    await UpdateFromHttpPost6(
        new Blog 
        { 
            Id = 1, 
            Name = ".NET Blog (All new!)", 
            Summary = "Posts about .NET" 
        },
        new Dictionary<string, object> 
        { 
            ["Id"] = 1, 
            ["Name"] = ".NET Blog", 
            ["Summary"] = "Posts about .NET" 
        });
}
```
```
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (4ms) [Parameters=[@p1='1', @p0='.NET Blog (All new!)' (Nullable = false) (Size = 4000)], CommandType='Text', CommandTimeout='30']
      SET IMPLICIT_TRANSACTIONS OFF;
      SET NOCOUNT ON;
      UPDATE [Blogs] SET [Name] = @p0
      OUTPUT 1
      WHERE [Id] = @p1;
```

    Порада

    Хоча цей підхід має привабливі риси, він передбачає передачу початкових значень сутності до вебклієнта та назад. Ретельно зважте, чи виправдовують себе переваги цієї додаткової складності; для багатьох завдань більш прагматичним рішенням буде один із простіших підходів.

## Приєднання(Attaching) серіалізованого графа

EF Core працює з графами сутностей, з'єднаних через зовнішні ключі та властивості навігації, як описано в розділі "Зміна зовнішніх ключів та навігацій". Якщо такі графи створюються поза межами EF Core — наприклад, з JSON-файлу, — вони можуть містити кілька екземплярів однієї й тієї ж сутності. Ці дублікати необхідно звести до єдиних екземплярів, перш ніж можна буде відстежувати граф.

## Модель прикладів

Класи сутностей в Types.cs ті самі.

Додадкові методи в Hеlpers.cs змінено

```cs
    public static async Task PopulateDatabase()
    {
        using var context = new BlogsContext(quiet: true);

        context.AddRange(
            new Blog
            {
                Name = ".NET Blog",
                Summary = "Posts about .NET",
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
                Summary = "Posts about Visual Studio",
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
            });

        await context.SaveChangesAsync();
    }
```


## Графи без дублікатів

Перш ніж рухатися далі, важливо усвідомити, що:

* Серіалізатори часто мають опції для обробки циклів і дубльованих екземплярів у графі.
* Вибір об'єкта, що використовується як корінь графа, часто може допомогти зменшити кількість дублікатів або усунути їх.

За можливості використовуйте параметри серіалізації та обирайте кореневі елементи, які не призводять до появи дублікатів. Наприклад, у наведеному нижче коді використовується Json.NET для серіалізації списку блогів, кожен з яких містить пов’язані з ним дописи:

```cs
static async Task Attaching_a_serialized_graph_1() 
{
    await CleanAndPopulateDatabase();

    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var blogs = await context.Blogs.Include(e => e.Posts).ToListAsync();

    var serialized = JsonConvert.SerializeObject(blogs, 
        new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore, 
            Formatting = Formatting.Indented 
        });

    Console.WriteLine(serialized);

    //await UpdateBlogsFromJson(serialized);
}
```
JSON, згенерований за допомогою цього коду:

```json
[
  {
    "Id": 1,
    "Name": ".NET Blog",
    "Summary": "Posts about .NET",
    "Posts": [
      {
        "Id": 1,
        "Title": "Announcing the Release of EF Core 5.0",
        "Content": "Announcing the release of EF Core 5.0, a full featured cross-platform...",
        "BlogId": 1
      },
      {
        "Id": 2,
        "Title": "Announcing F# 5",
        "Content": "F# 5 is the latest version of F#, the functional programming language...",
        "BlogId": 1
      }
    ]
  },
  {
    "Id": 2,
    "Name": "Visual Studio Blog",
    "Summary": "Posts about Visual Studio",
    "Posts": [
      {
        "Id": 3,
        "Title": "Disassembly improvements for optimized managed debugging",
        "Content": "If you are focused on squeezing out the last bits of performance for your .NET service or...",
        "BlogId": 2
      },
      {
        "Id": 4,
        "Title": "Database Profiling with Visual Studio",
        "Content": "Examine when database queries were executed and measure how long the take using...",
        "BlogId": 2
      }
    ]
  }
]
```

Зверніть увагу, що в JSON немає дубльованих блогів або дописів. Це означає, що для оновлення цих сутностей у базі даних достатньо буде простих викликів методу Update:

```cs
static async Task UpdateBlogsFromJson(string json)
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var blogs = JsonConvert.DeserializeObject<List<Blog>>(json);

    foreach (var blog in blogs)
    {
        context.Update(blog);
    }

    await context.SaveChangesAsync();
}

static async Task Attaching_a_serialized_graph_1() 
{
    //...

    await UpdateBlogsFromJson(serialized);
}
```
В цьому прикладі json не змінюється з моменту зчитуваня з БД. Але оновленя відбуваеться всіх данних.

```sql
info: RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (31ms) [Parameters=[@p2='1', @p0='.NET Blog' (Nullable = false) (Size = 4000), @p1='Posts about .NET' (Nullable = false) (Size = 4000), @p5='2', @p3='Visual Studio Blog' (Nullable = false) (Size = 4000), @p4='Posts about Visual Studio' (Nullable = false) (Size = 4000), @p9='1', @p6='1' (Nullable = true), @p7='Announcing the release of EF Core 5.0, a full featured cross-platform...' (Nullable = false) (Size = 4000), @p8='Announcing the Release of EF Core 5.0' (Nullable = false) (Size = 4000), @p13='2', @p10='1' (Nullable = true), @p11='F# 5 is the latest version of F#, the functional programming language...' (Nullable = false) (Size = 4000), @p12='Announcing F# 5' (Nullable = false) (Size = 4000), @p17='3', @p14='2' (Nullable = true), @p15='If you are focused on squeezing out the last bits of performance for your .NET service or...' (Nullable = false) (Size = 4000), @p16='Disassembly improvements for optimized managed debugging' (Nullable = false) (Size = 4000), @p21='4', @p18='2' (Nullable = true), @p19='Examine when database queries were executed and measure how long the take using...' (Nullable = false) (Size = 4000), @p20='Database Profiling with Visual Studio' (Nullable = false) (Size = 4000)], CommandType='Text', CommandTimeout='30']
      SET NOCOUNT ON;
      UPDATE [Blogs] SET [Name] = @p0, [Summary] = @p1
      OUTPUT 1
      WHERE [Id] = @p2;
      UPDATE [Blogs] SET [Name] = @p3, [Summary] = @p4
      OUTPUT 1
      WHERE [Id] = @p5;
      UPDATE [Posts] SET [BlogId] = @p6, [Content] = @p7, [Title] = @p8
      OUTPUT 1
      WHERE [Id] = @p9;
      UPDATE [Posts] SET [BlogId] = @p10, [Content] = @p11, [Title] = @p12
      OUTPUT 1
      WHERE [Id] = @p13;
      UPDATE [Posts] SET [BlogId] = @p14, [Content] = @p15, [Title] = @p16
      OUTPUT 1
      WHERE [Id] = @p17;
      UPDATE [Posts] SET [BlogId] = @p18, [Content] = @p19, [Title] = @p20
      OUTPUT 1
      WHERE [Id] = @p21;
```


## Обробка дублікатів

У попередньому прикладі код серіалізував кожен блог разом із пов’язаними з ним дописами. Якщо змінити це так, щоб кожен допис серіалізувався разом із пов’язаним із ним блогом, то в серіалізованому JSON з’являться дублікати. Наприклад:

```cs
static async Task Attaching_a_serialized_graph_2()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var posts = await context.Posts.Include(e => e.Blog).ToListAsync();

    var serialized = JsonConvert.SerializeObject(
        posts,
        new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, Formatting = Formatting.Indented });

    Console.WriteLine(serialized);

    //await UpdatePostsFromJsonBad(serialized);
}
```
Серіалізований JSON тепер виглядає так:

```json
[
  {
    "Id": 1,
    "Title": "Announcing the Release of EF Core 5.0",
    "Content": "Announcing the release of EF Core 5.0, a full featured cross-platform...",
    "BlogId": 1,
    "Blog": {
      "Id": 1,
      "Name": ".NET Blog",
      "Summary": "Posts about .NET",
      "Posts": [
        {
          "Id": 2,
          "Title": "Announcing F# 5",
          "Content": "F# 5 is the latest version of F#, the functional programming language...",
          "BlogId": 1
        }
      ]
    }
  },
  {
    "Id": 2,
    "Title": "Announcing F# 5",
    "Content": "F# 5 is the latest version of F#, the functional programming language...",
    "BlogId": 1,
    "Blog": {
      "Id": 1,
      "Name": ".NET Blog",
      "Summary": "Posts about .NET",
      "Posts": [
        {
          "Id": 1,
          "Title": "Announcing the Release of EF Core 5.0",
          "Content": "Announcing the release of EF Core 5.0, a full featured cross-platform...",
          "BlogId": 1
        }
      ]
    }
  },
  {
    "Id": 3,
    "Title": "Disassembly improvements for optimized managed debugging",
    "Content": "If you are focused on squeezing out the last bits of performance for your .NET service or...",
    "BlogId": 2,
    "Blog": {
      "Id": 2,
      "Name": "Visual Studio Blog",
      "Summary": "Posts about Visual Studio",
      "Posts": [
        {
          "Id": 4,
          "Title": "Database Profiling with Visual Studio",
          "Content": "Examine when database queries were executed and measure how long the take using...",
          "BlogId": 2
        }
      ]
    }
  },
  {
    "Id": 4,
    "Title": "Database Profiling with Visual Studio",
    "Content": "Examine when database queries were executed and measure how long the take using...",
    "BlogId": 2,
    "Blog": {
      "Id": 2,
      "Name": "Visual Studio Blog",
      "Summary": "Posts about Visual Studio",
      "Posts": [
        {
          "Id": 3,
          "Title": "Disassembly improvements for optimized managed debugging",
          "Content": "If you are focused on squeezing out the last bits of performance for your .NET service or...",
          "BlogId": 2
        }
      ]
    }
  }
]
```
Зверніть увагу, що граф тепер містить кілька екземплярів Blog з однаковим значенням ключа, а також кілька екземплярів Post з однаковим значенням ключа. Спроба відстежити цей граф так само, як ми це робили в попередньому прикладі, призведе до появи помилки:

```cs
static async Task UpdatePostsFromJsonBad(string json)
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var posts = JsonConvert.DeserializeObject<List<Post>>(json);

    try
    {
        foreach (var post in posts)
        {
            context.Update(post); // Will throw
        }

        await context.SaveChangesAsync();
    }
    catch (Exception e)
    {
        Console.WriteLine($"{e.GetType().FullName}: {e.Message}");
    }
}
```
```
System.InvalidOperationException: The instance of entity type 'Post' cannot be tracked because another instance with the same key value for {'Id'} is already being tracked. When attaching existing entities, ensure that only one entity instance with a given key value is attached. Consider using 'DbContextOptionsBuilder.EnableSensitiveDataLogging' to see the conflicting key values.
```

Це можна виправити двома способами:

* Використати параметри серіалізації JSON, які зберігають посилання
* Виконуйте зіставлення ідентичностей під час відстеження графа

### Збереження посилань

Json.NET надає опцію PreserveReferencesHandling для вирішення цього завдання. Наприклад:

```cs
static async Task Attaching_a_serialized_graph_3()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var posts = await context.Posts.Include(e => e.Blog).ToListAsync();

    var serialized = JsonConvert.SerializeObject(posts,
        new JsonSerializerSettings
        {
            PreserveReferencesHandling = PreserveReferencesHandling.All,//!!!
            Formatting = Formatting.Indented
        });

    Console.WriteLine(serialized);

    //await UpdatePostsFromJson(serialized);
}
```
Отриманий JSON тепер виглядає так:

```json
{
  "$id": "1",
  "$values": [
    {
      "$id": "2",
      "Id": 1,
      "Title": "Announcing the Release of EF Core 5.0",
      "Content": "Announcing the release of EF Core 5.0, a full featured cross-platform...",
      "BlogId": 1,
      "Blog": {
        "$id": "3",
        "Id": 1,
        "Name": ".NET Blog",
        "Summary": "Posts about .NET",
        "Posts": [
          {
            "$ref": "2"
          },
          {
            "$id": "4",
            "Id": 2,
            "Title": "Announcing F# 5",
            "Content": "F# 5 is the latest version of F#, the functional programming language...",
            "BlogId": 1,
            "Blog": {
              "$ref": "3"
            }
          }
        ]
      }
    },
    {
      "$ref": "4"
    },
    {
      "$id": "5",
      "Id": 3,
      "Title": "Disassembly improvements for optimized managed debugging",
      "Content": "If you are focused on squeezing out the last bits of performance for your .NET service or...",
      "BlogId": 2,
      "Blog": {
        "$id": "6",
        "Id": 2,
        "Name": "Visual Studio Blog",
        "Summary": "Posts about Visual Studio",
        "Posts": [
          {
            "$ref": "5"
          },
          {
            "$id": "7",
            "Id": 4,
            "Title": "Database Profiling with Visual Studio",
            "Content": "Examine when database queries were executed and measure how long the take using...",
            "BlogId": 2,
            "Blog": {
              "$ref": "6"
            }
          }
        ]
      }
    },
    {
      "$ref": "7"
    }
  ]
}
```
Зверніть увагу, що в цьому JSON дублікати замінено посиланнями на кшталт "$ref": "5", які вказують на вже наявний у графі екземпляр. Цей граф знову можна відстежувати за допомогою простих викликів методу Update, як показано вище.

```cs
static async Task UpdatePostsFromJson(string json)
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);

    var posts = JsonConvert.DeserializeObject<List<Post>>(json);

    foreach (var post in posts)
    {
        context.Update(post);
    }

    await context.SaveChangesAsync();
}
```

У бібліотеках базових класів (BCL) .NET підтримка System.Text.Json має схожу опцію, яка забезпечує такий самий результат. Наприклад:

```cs
var serialized = JsonSerializer.Serialize(
    posts, new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve, WriteIndented = true });
```

### Обробка дублікатів

Якщо під час серіалізації неможливо усунути дублікати, то ChangeTracker.TrackGraph пропонує спосіб вирішення цієї проблеми. TrackGraph працює подібно до методів Add, Attach та Update, з тією різницею, що для кожного екземпляра сутності він генерує зворотний виклик (callback) перед початком відстеження. Цей зворотний виклик можна використовувати або для відстеження сутності, або для її ігнорування. Наприклад:

```cs
static async Task Attaching_a_serialized_graph_5()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var posts = await context.Posts.Include(e => e.Blog).ToListAsync();

    var serialized = JsonConvert.SerializeObject(posts,
        new JsonSerializerSettings 
        { 
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore, 
            Formatting = Formatting.Indented 
        });

    Console.WriteLine(serialized);

    Console.WriteLine();

    await UpdatePostsFromJsonWithIdentityResolution(serialized);
}
```

```cs
static async Task UpdatePostsFromJsonWithIdentityResolution(string json)
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var posts = JsonConvert.DeserializeObject<List<Post>>(json);

    foreach (var post in posts)
    {
        context.ChangeTracker.TrackGraph(
            post, node =>
            {
                var keyValue = node.Entry.Property("Id").CurrentValue;
                var entityType = node.Entry.Metadata;

                var existingEntity = node.Entry.Context.ChangeTracker.Entries()
                    .FirstOrDefault(
                        e => Equals(e.Metadata, entityType)
                             && Equals(e.Property("Id").CurrentValue, keyValue));

                if (existingEntity == null)
                {
                    Console.WriteLine($"Tracking {entityType.DisplayName()} entity with key value {keyValue}");

                    node.Entry.State = EntityState.Modified;
                }
                else
                {
                    Console.WriteLine($"Discarding duplicate {entityType.DisplayName()} entity with key value {keyValue}");
                }
            });
    }
    await context.SaveChangesAsync();
}
```
Для кожного об'єкта в графі цей код виконує такі дії:

* Визначає тип об'єкта та значення його ключа.
* Шукає об'єкт із цим ключем у механіку відстеження змін (change tracker).
    * Якщо об'єкт знайдено, жодних подальших дій не виконується, оскільки цей об'єкт є дублікатом.
    * Якщо об'єкт не знайдено, він береться на відстеження шляхом встановлення для нього стану Modified.

Результатом виконання цього коду є:
```
Tracking Post entity with key value 1
Tracking Blog entity with key value 1
Tracking Post entity with key value 2
Discarding duplicate Post entity with key value 2
Tracking Post entity with key value 3
Tracking Blog entity with key value 2
Tracking Post entity with key value 4
Discarding duplicate Post entity with key value 4
```
    Важливо

    Цей код передбачає, що всі дублікати є ідентичними. Завдяки цьому можна без побоювань обрати будь-який із дублікатів для відстеження, а решту — відкинути. Якщо ж дублікати можуть відрізнятися, коду доведеться вирішувати, який саме з них обрати, а також як об’єднати значення властивостей і навігаційні зв’язки.

    Примітка

    Для спрощення в цьому коді передбачається, що кожна сутність має властивість первинного ключа з назвою Id. Це можна оформити у вигляді абстрактного базового класу або інтерфейсу. Крім того, властивість (або властивості), що складає(-ють) первинний ключ, можна отримати з метаданих IEntityType — завдяки цьому такий код працюватиме з будь-яким типом сутності.

## Невдале встановлення значень ключів.

Для типів сутностей часто налаштовується використання автоматично згенерованих значень ключів. Це стандартна поведінка для властивостей цілочисельного типу або типу GUID, що входять до складу простих (нескладених) ключів. Однак, якщо тип сутності не налаштований на використання автоматично згенерованих значень ключів, то перед початком відстеження сутності необхідно явно задати значення ключа. Наприклад, використовуючи такий тип сутності:


Types.cs
```cs
public class Pet
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    public string Name { get; set; }
}
```
```cs
public class ApplicationDbContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<Pet> Pets { get; set; }

    //...
}
```

Розгляньмо код, який намагається відстежувати два нові екземпляри сутностей, встановлюючи значення ключів:

```cs
static void Failing_to_set_key_values_1()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    context.Add(new Pet { Name = "Smokey" });

    try
    {
        context.Add(new Pet { Name = "Clippy" }); // This will throw
    }
    catch (Exception e)
    {
        Console.WriteLine($"{e.GetType().FullName}: {e.Message}");
    }
}
```
Program.cs
```cs
static void Do()
{
    CleanAndPopulateDatabase().Wait();

    Failing_to_set_key_values_1();
}
Do();
```
```
System.InvalidOperationException: The instance of entity type 'Pet' cannot be tracked because another instance with the same key value for {'Id'} is already being tracked. When attaching existing entities, ensure that only one entity instance with a given key value is attached. Consider using 'DbContextOptionsBuilder.EnableSensitiveDataLogging' to see the conflicting key values.
```
Щоб вирішити цю проблему, можна або явно задати правільне значення ключів (які повині не співпадати), або налаштувати властивість ключа так, щоб вона використовувала згенеровані значення. Докладнішу інформацію див. у розділі «Згенеровані значення».

```cs
static void Failing_to_set_key_values_2()
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);

    context.Add(new Pet { Id = 1, Name = "Smokey" });

    try
    {
        context.Add(new Pet { Id = 2, Name = "Clippy" }); // This will throw
    }
    catch (Exception e)
    {
        Console.WriteLine($"{e.GetType().FullName}: {e.Message}");
    }
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```


## Надмірне використання одного екземпляра DbContext

Клас DbContext призначений для представлення короткотривалої «одиниці роботи» (unit-of-work), як описано в розділах «Ініціалізація та конфігурація DbContext» і детальніше розглянуто в розділі «Відстеження змін в EF Core». Недотримання цих рекомендацій може легко призвести до ситуацій, коли робиться спроба відстежувати кілька екземплярів одного й того самого об'єкта. Поширеними прикладами є:

* Використання одного й того самого екземпляра DbContext як для підготовки тестового стану, так і для безпосереднього виконання тесту часто призводить до того, що DbContext продовжує відстежувати екземпляр сутності, створений на етапі підготовки, тоді як під час виконання самого тесту робиться спроба приєднати новий екземпляр. Натомість використовуйте окремі екземпляри DbContext: один — для підготовки тестового стану, а інший — для виконання власне тестового коду.

* Використання спільного екземпляра DbContext у репозиторії або подібному коді. Натомість переконайтеся, що ваш репозиторій використовує окремий екземпляр DbContext для кожної одиниці роботи (unit-of-work).

## Розв’язання ідентичності та запити

Розпізнавання сутностей (identity resolution) відбувається автоматично, коли сутності відстежуються в результаті виконання запиту.Це означає, що якщо екземпляр сутності з певним значенням ключа вже відстежується, то замість створення нового екземпляра використовується вже наявний, який перебуває під наглядом. Це має важливий наслідок: якщо дані в базі даних змінилися, це не відобразиться в результатах запиту до контексту.Це вагома причина використовувати новий екземпляр DbContext для кожної одиниці роботи (unit-of-work) — як описано в розділі «Ініціалізація та конфігурація DbContext» і детальніше розглянуто в розділі «Відстеження змін в EF Core».

    Важливо

    Важливо розуміти, що EF Core завжди виконує LINQ-запит до DbSet на рівні бази даних і повертає результати, ґрунтуючись виключно на даних, що містяться в базі. Однак у випадку запиту з відстеженням, якщо повернуті сутності вже відстежуються, то замість створення екземплярів на основі даних із бази даних використовуються вже наявні відстежувані екземпляри.

Методи Reload() або GetDatabaseValues() можна використовувати, коли потрібно оновити відстежувані сутності, отримавши найактуальніші дані з бази даних. Докладнішу інформацію див. у розділі «Доступ до відстежуваних сутностей».

На відміну від запитів із відстеженням, запити без відстеження не виконують зіставлення сутностей (identity resolution). Це означає, що запити без відстеження (no-tracking queries) можуть повертати дублікати — так само, як у випадку із серіалізацією в JSON, описаному раніше. Зазвичай це не є проблемою, якщо результати запиту згодом будуть серіалізовані та надіслані клієнту.

    Порада

    Не варто систематично виконувати запити без відстеження (no-tracking queries), а потім приєднувати отримані сутності до того самого контексту. Це буде і повільніше, і складніше зробити правильно, ніж у випадку використання запиту для відстеження.

Запити без відстеження (no-tracking queries) не виконують зіставлення сутностей (identity resolution), оскільки це негативно впливає на продуктивність потокової обробки великої кількості сутностей, отриманих у результаті запиту. Це пояснюється тим, що для розпізнавання сутностей необхідно відстежувати кожен отриманий екземпляр, щоб його можна було використати повторно, замість того щоб згодом створювати дублікат.

Запити без відстеження можна змусити виконувати ідентифікацію за допомогою AsNoTrackingWithIdentityResolution\<TEntity\>(IQueryable\<TEntity\>). Після цього запит відстежуватиме повернуті екземпляри (не вдаючись до стандартного механізму відстеження) і гарантуватиме, що в результатах запиту не з’являться дублікати.

## Перевизначення рівності об'єктів

Під час порівняння екземплярів сутностей EF Core використовує порівняння за посиланням. Це справедливо навіть у тому випадку, якщо типи сутностей перевизначають метод Object.Equals(Object) або іншим чином змінюють логіку порівняння об'єктів на рівність. Однак існує один випадок, коли перевизначення рівності може вплинути на поведінку EF Core: коли навігаційні властивості, що представляють колекції, використовують перевизначену рівність замість рівності за посиланням, внаслідок чого кілька різних екземплярів сприймаються як один і той самий об'єкт.

З огляду на це, рекомендується уникати перевизначення рівності сутностей. Якщо це використовується, переконайтеся, що створено механізми навігації по колекціях, які забезпечують рівність за посиланням. Наприклад, створіть компаратор рівності, який використовує рівність за посиланням:

```cs
public sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    private ReferenceEqualityComparer()
    {
    }

    public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();

    bool IEqualityComparer<object>.Equals(object x, object y) => x == y;

    int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}
```

Цей компаратор потім можна використовувати під час створення навігації по колекціях. Наприклад:
```cs
public class Customer
{   
    //...
    public ICollection<Order> Orders { get; set; }
        = new HashSet<Order>(ReferenceEqualityComparer.Instance);
}
```

## Порівняння ключових властивостей

Окрім перевірки на рівність, значення ключів також повинні піддаватися впорядкуванню. Це важливо для уникнення взаємних блокувань (deadlocks) під час оновлення кількох сутностей в межах одного виклику методу SaveChanges. Усі типи, що використовуються для властивостей, які є первинними, альтернативними або зовнішніми ключами, а також ті, що застосовуються для унікальних індексів, повинні реалізовувати інтерфейси IComparable\<T\> та IEquatable\<T\>. Типи, які зазвичай використовуються як ключі (наприклад, `int`, `Guid`, `string` тощо), вже підтримують ці інтерфейси. Для власних типів ключів ці інтерфейси можна додати.