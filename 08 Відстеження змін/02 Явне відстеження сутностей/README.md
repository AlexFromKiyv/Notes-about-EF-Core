# Явне відстеження сутностей

Кожен екземпляр DbContext відстежує зміни, внесені до сутностей. Своєю чергою, ці відстежувані сутності зумовлюють зміни в базі даних під час виклику методу SaveChanges.

Механізм відстеження змін в Entity Framework Core (EF Core) працює найефективніше, коли для отримання сутностей і їх оновлення (шляхом виклику методу SaveChanges) використовується один і той самий екземпляр DbContext. Це зумовлено тим, що EF Core автоматично відстежує стан отриманих сутностей, а під час виклику `SaveChanges` виявляє будь-які внесені в них зміни. Цей підхід описано в розділі Огляд.

## Вступ
Сутності можна явно "attached"(приєднати) до DbContext, щоб контекст почав відстежувати їх. Це корисно, перш за все, коли:

1. Створення нових сутностей, які будуть внесені до бази даних.
2. Повторне приєднання від’єднаних сутностей, які раніше були отримані за допомогою іншого екземпляра DbContext.

Перший із цих варіантів знадобиться більшості застосунків, і його обробка здійснюється переважно за допомогою методів DbContext.Add. Другий варіант потрібен лише тим програмам, які змінюють сутності або зв’язки між ними в той час, коли ці сутності не відстежуються. Наприклад, вебзастосунок може надсилати сутності вебклієнту, де користувач вносить зміни й відправляє їх назад. Такі сутності називають «disconnected»(від’єднаними), оскільки спочатку їх було отримано за допомогою DbContext, але згодом, під час передачі клієнту, їх було від’єднано від цього контексту.

Тепер веб-застосунок має повторно приєднати ці сутності, щоб вони знову відстежувалися, і позначити внесені зміни, аби метод SaveChanges міг виконати відповідні оновлення в базі даних. За це насамперед відповідають методи DbContext.Attach та DbContext.Update.

    Порада

    Зазвичай немає потреби приєднувати сутності до того самого екземпляра DbContext, за допомогою якого вони були отримані. Не варто систематично виконувати запити без відстеження (no-tracking queries), а потім приєднувати отримані сутності до того самого контексту. Це працюватиме повільніше, ніж використання запиту з відстеженням, а також може призвести до проблем — наприклад, втрати значень тіньових властивостей, — що ускладнить правильну реалізацію.

## Згенеровані чи явні значення ключів

За замовчуванням властивості ключів типів integer та GUID налаштовані на використання автоматично згенерованих значень. Це має суттєву перевагу для відстеження змін: відсутність значення ключа вказує на те, що сутність є «новою». Під «новою» ми розуміємо таку, що ще не була додана до бази даних.

У наступних розділах використовуються дві моделі. Перша налаштована так, щоб не використовувати згенеровані значення ключів:

```cs
public class Blog
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    public string Name { get; set; }

    public IList<Post> Posts { get; } = new List<Post>();
}

public class Post
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    public string Title { get; set; }
    public string Content { get; set; }

    public int? BlogId { get; set; }
    public Blog Blog { get; set; }
}
```
У кожному прикладі насамперед наведено незгенеровані (тобто явно задані) значення ключів, оскільки такий підхід забезпечує максимальну чіткість і легкість сприйняття. Далі йде приклад, у якому використовуються згенеровані значення ключів:

```cs
public class Blog
{
    public int Id { get; set; }
    public string Name { get; set; }

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
Зауважте, що ключові властивості в цій моделі не потребують додаткового налаштування, оскільки використання згенерованих значень ключів є стандартною поведінкою для простих цілочисельних ключів.

## Вставлення нових сутностей

### Явні значення ключів

Щоб сутність була вставлена ​​під час виконання методу SaveChanges, вона повинна відстежуватися зі станом Added. Зазвичай сутності переводяться в стан Added шляхом виклику одного з методів DbContext.Add, DbContext.AddRange, DbContext.AddAsync, DbContext.AddRangeAsync або відповідних методів класу DbSet<TEntity>.

Наприклад, щоб почати відстежувати новий блог:

```cs
    context.Add(
        new Blog { Id = 1, Name = ".NET Blog", });
```

Перевірка режиму налагодження відстежувача змін після цього виклику показує, що контекст відстежує нову сутність зі станом Added:

```
Blog {Id: 1} Added
    Id: 1 PK
    Name: '.NET Blog'
  Posts: []
```

Повний приклад:

```cs
static async Task DoAsync()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);
    await CleanDatabase(context);
    await Inserting_new_entites_1(context);
}
await DoAsync();

static async Task Inserting_new_entites_1(ApplicationDbContext context)
{
    context.Add(
        new Blog { Id = 1, Name = ".NET Blog", });


    Console.WriteLine("Before SaveChanges:");
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);

    Console.WriteLine("After SaveChanges:");
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}

//Helper 
partial class Program
{
    protected static async Task CleanDatabase(DbContext context)
    {
        Console.WriteLine("Deleting and re-creating database...");
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        Console.WriteLine("Done. Database is clean and fresh.");
    }
}
```
```
Before SaveChanges:
Blog {Id: 1} Added
    Id: 1 PK
    Name: '.NET Blog'
  Posts: []

1
After SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: []
```

Однак методи Add працюють не лише з окремою сутністю. Вони фактично починають відстежувати цілий граф пов’язаних сутностей, переводячи їх усі в стан Added. Наприклад, щоб додати новий блог і пов’язані з ним нові дописи:

```cs
static async Task Inserting_new_entites_2(ApplicationDbContext context)
{
    await CleanDatabase(context);

    context.Add(
    new Blog
    {
        Id = 1,
        Name = ".NET Blog",
        Posts =
        {
                    new Post
                    {
                        Id = 1,
                        Title = "Announcing the Release of EF Core 5.0",
                        Content = "Announcing the release of EF Core 5.0, a full featured cross-platform..."
                    },
                    new Post
                    {
                        Id = 2,
                        Title = "Announcing F# 5",
                        Content = "F# 5 is the latest version of F#, the functional programming language..."
                    }
        }
    });

    Console.WriteLine("Before SaveChanges:");
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);

    Console.WriteLine("After SaveChanges:");
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
Тепер контекст відстежує всі ці сутності як Added:

```
Before SaveChanges:
Blog {Id: 1} Added
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Added
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Added
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}

3
After SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}
```

Зверніть увагу, що в наведених вище прикладах для властивостей ключа Id встановлено конкретні значення. Це зумовлено тим, що модель у цьому випадку налаштовано на використання явно заданих значень ключів, а не автоматично згенерованих. Якщо згенеровані ключі не використовуються, властивості ключа необхідно явно встановити перед викликом методу Add. Ці ключові значення потім вставляються під час виклику SaveChanges.

```sql
exec sp_executesql N'SET NOCOUNT ON;
INSERT INTO [Blogs] ([Id], [Name])
VALUES (@p0, @p1);
INSERT INTO [Posts] ([Id], [BlogId], [Content], [Title])
VALUES (@p2, @p3, @p4, @p5),
(@p6, @p7, @p8, @p9);
',N'@p0 int,@p1 nvarchar(4000),@p2 int,@p3 int,@p4 nvarchar(4000),@p5 nvarchar(4000),@p6 int,@p7 int,@p8 nvarchar(4000),@p9 nvarchar(4000)',@p0=1,@p1=N'.NET Blog',@p2=1,@p3=1,@p4=N'Announcing the release of EF Core 5.0, a full featured cross-platform...',@p5=N'Announcing the Release of EF Core 5.0',@p6=2,@p7=1,@p8=N'F# 5 is the latest version of F#, the functional programming language...',@p9=N'Announcing F# 5'
```
Після завершення виконання SaveChanges усі ці сутності відстежуються зі станом Unchanged, оскільки вони вже існують у базі даних:

```
After SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}
```

### Згенеровані значення ключів

Як зазначалося вище, властивості ключів цілочисельного типу та типу GUID за замовчуванням налаштовані на використання автоматично згенерованих значень. Це означає, що програма не повинна явно задавати значення ключа. Наприклад, щоб додати новий блог і дописи, для яких значення ключів генеруються автоматично:

```cs
static async Task Inserting_new_entites_3(ApplicationDbContext context)
{
    await CleanDatabase(context);

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
                    }
        }
    });

    Console.WriteLine("Before SaveChanges:");
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);

    Console.WriteLine("After SaveChanges:");
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
Як і у випадку з явними значеннями ключів, контекст тепер відстежує всі ці сутності як додані (Added):


```
Before SaveChanges:
Blog {Id: -2147482647} Added
    Id: -2147482647 PK Temporary
    Name: '.NET Blog'
  Posts: [{Id: -2147482647}, {Id: -2147482646}]
Post {Id: -2147482647} Added
    Id: -2147482647 PK Temporary
    BlogId: -2147482647 FK Temporary
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: -2147482647}
Post {Id: -2147482646} Added
    Id: -2147482646 PK Temporary
    BlogId: -2147482647 FK Temporary
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: -2147482647}

3
After SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}
```
Зверніть увагу, що в цьому випадку для кожної сутності було згенеровано тимчасові значення ключів. EF Core використовує ці значення до моменту виклику методу SaveChanges, після чого фактичні значення ключів зчитуються з бази даних.

```sql
exec sp_executesql N'SET IMPLICIT_TRANSACTIONS OFF;
SET NOCOUNT ON;
INSERT INTO [Blogs] ([Name])
OUTPUT INSERTED.[Id]
VALUES (@p0);
',N'@p0 nvarchar(4000)',@p0=N'.NET Blog'

exec sp_executesql N'SET IMPLICIT_TRANSACTIONS OFF;
SET NOCOUNT ON;
MERGE [Posts] USING (
VALUES (@p1, @p2, @p3, 0),
(@p4, @p5, @p6, 1)) AS i ([BlogId], [Content], [Title], _Position) ON 1=0
WHEN NOT MATCHED THEN
INSERT ([BlogId], [Content], [Title])
VALUES (i.[BlogId], i.[Content], i.[Title])
OUTPUT INSERTED.[Id], i._Position;
',N'@p1 int,@p2 nvarchar(4000),@p3 nvarchar(4000),@p4 int,@p5 nvarchar(4000),@p6 nvarchar(4000)',@p1=1,@p2=N'Announcing the release of EF Core 5.0, a full featured cross-platform...',@p3=N'Announcing the Release of EF Core 5.0',@p4=1,@p5=N'F# 5 is the latest version of F#, the functional programming language...',@p6=N'Announcing F# 5'
```
Після завершення виконання методу SaveChanges усі сутності оновлено фактичними значеннями ключів; вони відстежуються зі станом Unchanged (без змін), оскільки тепер відповідають стану в базі даних:

```
After SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}
```
Це точно такий самий кінцевий стан, як і в попередньому прикладі, де використовувалися явні значення ключів.

    Порада

    Явне значення ключа можна встановити навіть тоді, коли використовуються згенеровані значення ключів. Після цього EF Core спробує виконати вставку, використовуючи це значення ключа. Деякі конфігурації баз даних, зокрема SQL Server зі стовпцями типу Identity, не підтримують такі операції вставки й генеруватимуть помилку. 

## Приєднання наявних сутностей

### Явно задані значення ключів

Сутності, отримані в результаті виконання запитів, відстежуються зі станом Unchanged (без змін). Стан Unchanged (без змін) означає, що сутність не зазнала жодних змін із моменту її отримання (запиту).

Від’єднану сутність — наприклад, отриману від вебклієнта в HTTP-запиті — можна перевести в цей стан за допомогою методів DbContext.Attach чи DbContext.AttachRange або відповідних методів класу DbSet<TEntity>. Наприклад, щоб розпочати відстеження наявного блогу:

```cs
context.Attach(
    new Blog { Id = 1, Name = ".NET Blog", });
```

Перевірка режиму налагодження відстежувача змін після цього виклику показує, що сутність відстежується зі станом Unchanged (без змін):

```
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Posts: []
```

Так само, як і Add, метод Attach переводить цілий граф пов’язаних сутностей у стан Unchanged (без змін). Наприклад, щоб приєднати наявний блог і пов’язані з ним наявні дописи:

```cs
static async Task Attaching_existing_entities_1()
{
    Blog blog;

    // Create DB, add blog witn posts, get disconnected entity
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        await CleanDatabase(context);
        await PopulateDatabase1(context);
        blog = await GetBlog(context);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        context.Attach(blog);


        Console.WriteLine("Before SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        int count =  await context.SaveChangesAsync();
        Console.WriteLine(count);

        Console.WriteLine("After SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    }

}

static async Task PopulateDatabase1(ApplicationDbContext context)
{
    context.Add(
        new Blog
        {   
            Id = 1,
            Name = ".NET Blog",
            Posts =
            {
                    new Post
                    {
                        Id = 1,
                        Title = "Announcing the Release of EF Core 5.0",
                        Content = "Announcing the release of EF Core 5.0, a full featured cross-platform..."
                    },
                    new Post
                    {   
                        Id = 2,
                        Title = "Announcing F# 5",
                        Content = "F# 5 is the latest version of F#, the functional programming language..."
                    },
            }
        });

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}

static async Task<Blog> GetBlog(ApplicationDbContext context)
{
    return await context.Blogs.FirstAsync(b => b.Id == 1);
}
```
Тепер контекст відстежує всі ці сутності як такі, що не зазнали змін (Unchanged):

```
Before SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}

0
After SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}
```
Виклик SaveChanges на цьому етапі не матиме жодного ефекту. Усі сутності позначено як Unchanged, тому в базі даних нічого оновлювати.

### Згенеровані значення ключів

Як зазначалося вище, властивості ключів цілого типу (integer) та типу GUID за замовчуванням налаштовані на використання автоматично згенерованих значень. Це дає суттєву перевагу під час роботи з від’єднаними сутностями: відсутність значення ключа свідчить про те, що сутність ще не була додана до бази даних. Це дозволяє механізму відстеження змін автоматично виявляти нові сутності та переводити їх у стан Added. Наприклад, розгляньмо приєднання цього графа, що складається з блогу та дописів:

```cs
static async Task Attaching_existing_entities_2()
{
    Blog blog;

    // Create DB, add blog, get blog
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        await CleanDatabase(context);
        await PopulateDatabase2(context);
        blog = await GetBlog(context);
    }

    blog.Posts.Add(new Post
    {
        Title = "Announcing .NET 5.0",
        Content = ".NET 5.0 includes many enhancements, including single file applications, more..."
    });


    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        context.Attach(blog);

        Console.WriteLine("Before SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        int count = await context.SaveChangesAsync();
        Console.WriteLine(count);

        Console.WriteLine("After SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    }
}

```
```
Before SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: -2147482645}]
Post {Id: -2147482645} Added
    Id: -2147482645 PK Temporary
    BlogId: 1 FK
    Content: '.NET 5.0 includes many enhancements, including single file a...'
    Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}

1
After SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}
Post {Id: 3} Unchanged
    Id: 3 PK
    BlogId: 1 FK
    Content: '.NET 5.0 includes many enhancements, including single file a...'
    Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
```
Блог має значення ключа 1, що вказує на його наявність у базі даних. Два дописи також мають визначені значення ключів, тоді як третій — ні. EF Core сприйматиме це значення ключа як 0 — значення за замовчуванням для цілого числа в CLR. У результаті EF Core позначає нову сутність як Added (Додана) замість Unchanged (Без змін):

```
Post {Id: -2147482645} Added
    Id: -2147482645 PK Temporary
    BlogId: 1 FK
    Content: '.NET 5.0 includes many enhancements, including single file a...'
    Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
```
Виклик SaveChanges на цьому етапі не впливає на сутності зі станом Unchanged, але вставляє нову сутність у базу даних.

```sql
exec sp_executesql N'SET IMPLICIT_TRANSACTIONS OFF;
SET NOCOUNT ON;
INSERT INTO [Posts] ([BlogId], [Content], [Title])
OUTPUT INSERTED.[Id]
VALUES (@p0, @p1, @p2);
',N'@p0 int,@p1 nvarchar(4000),@p2 nvarchar(4000)',@p0=1,@p1=N'.NET 5.0 includes many enhancements, including single file applications, more...',@p2=N'Announcing .NET 5.0'
```
Важливо зауважити, що завдяки згенерованим значенням ключів EF Core здатний автоматично розрізняти нові та наявні сутності в від’єднаному графі. Словом, у разі використання згенерованих ключів EF Core завжди вставлятиме сутність, якщо для неї не встановлено значення ключа.

## Оновлення наявних сутностей

### Явне встановлення значень ключів

Методи DbContext.Update, DbContext.UpdateRange та відповідні методи в DbSet<TEntity> поводяться так само, як описані вище методи Attach, за винятком того, що сутності переводяться в стан Modified (змінено) замість Unchanged (без змін). Наприклад, щоб почати відстежувати наявний блог зі статусом Modified:

```cs
context.Update(
    new Blog { Id = 1, Name = ".NET Blog", });
```
Перевірка режиму налагодження відстежувача змін після цього виклику показує, що контекст відстежує цю сутність у стані Modified:

```
Blog {Id: 1} Modified
  Id: 1 PK
  Name: '.NET Blog' Modified
  Posts: []
```
Так само, як і у випадку з методами Add та Attach, метод Update фактично позначає весь граф пов’язаних сутностей як змінений (Modified). Наприклад, щоб позначити наявний блог і пов’язані з ним наявні дописи як змінені:

```cs
static async Task Updating_existing_entities_1()
{
    Blog blog;

    // Create DB, add blog, get blog
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        await CleanDatabase(context);
        await PopulateDatabase1(context);
        blog = await GetBlog(context);
    }

    blog.Name = ".NET Blog (Update)";

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        context.Update(blog);//!!!

        Console.WriteLine("Before SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        int count = await context.SaveChangesAsync();
        Console.WriteLine(count);

        Console.WriteLine("After SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    }
}
```
Тепер контекст відстежує всі ці сутності як змінені:

```cs
Before SaveChanges:
Blog {Id: 1} Modified
    Id: 1 PK
    Name: '.NET Blog (Update)' Modified
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Modified
    Id: 1 PK
    BlogId: 1 FK Modified
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...' Modified
    Title: 'Announcing the Release of EF Core 5.0' Modified
  Blog: {Id: 1}
Post {Id: 2} Modified
    Id: 2 PK
    BlogId: 1 FK Modified
    Content: 'F# 5 is the latest version of F#, the functional programming...' Modified
    Title: 'Announcing F# 5' Modified
  Blog: {Id: 1}

3
After SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog (Update)'
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}
```
Виклик SaveChanges на цьому етапі призведе до надсилання оновлень до бази даних для всіх цих сутностей.

```sql
exec sp_executesql N'SET NOCOUNT ON;
UPDATE [Blogs] SET [Name] = @p0
OUTPUT 1
WHERE [Id] = @p1;
UPDATE [Posts] SET [BlogId] = @p2, [Content] = @p3, [Title] = @p4
OUTPUT 1
WHERE [Id] = @p5;
UPDATE [Posts] SET [BlogId] = @p6, [Content] = @p7, [Title] = @p8
OUTPUT 1
WHERE [Id] = @p9;
',N'@p1 int,@p0 nvarchar(4000),@p5 int,@p2 int,@p3 nvarchar(4000),@p4 nvarchar(4000),@p9 int,@p6 int,@p7 nvarchar(4000),@p8 nvarchar(4000)',@p1=1,@p0=N'.NET Blog (Update)',@p5=1,@p2=1,@p3=N'Announcing the release of EF Core 5.0, a full featured cross-platform...',@p4=N'Announcing the Release of EF Core 5.0',@p9=2,@p6=1,@p7=N'F# 5 is the latest version of F#, the functional programming language...',@p8=N'Announcing F# 5'
```

### Згенеровані значення ключів

Як і у випадку з Attach, згенеровані значення ключів мають таку саму важливу перевагу для методу Update: відсутність значення ключа вказує на те, що сутність є новою і ще не була додана до бази даних. Як і у випадку з Attach, це дозволяє DbContext автоматично виявляти нові сутності та переводити їх у стан Added. Наприклад, розглянемо виклик Update для такого графа, що складається з блогу та дописів:

```cs
static async Task Updating_existing_entities_2()
{
    Blog blog;

    // Create DB, add blog, get blog
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        await CleanDatabase(context);
        await PopulateDatabase2(context);
        blog = await GetBlog(context);
    }

    blog.Posts.Add(new Post
    {
        Title = "Announcing .NET 5.0",
        Content = ".NET 5.0 includes many enhancements, including single file applications, more..."
    });

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        context.Update(blog);

        Console.WriteLine("Before SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        int count = await context.SaveChangesAsync();
        Console.WriteLine(count);

        Console.WriteLine("After SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    }
}

```
Як і у випадку з прикладом Attach, запис без значення ключа розпізнається як новий і переводиться в стан Added. Решта сутностей позначаються як Modified:
```
Before SaveChanges:
Blog {Id: 1} Modified
    Id: 1 PK
    Name: '.NET Blog' Modified
  Posts: [{Id: 1}, {Id: 2}, {Id: -2147482645}]
Post {Id: -2147482645} Added
    Id: -2147482645 PK Temporary
    BlogId: 1 FK
    Content: '.NET 5.0 includes many enhancements, including single file a...'
    Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
Post {Id: 1} Modified
    Id: 1 PK
    BlogId: 1 FK Modified
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...' Modified
    Title: 'Announcing the Release of EF Core 5.0' Modified
  Blog: {Id: 1}
Post {Id: 2} Modified
    Id: 2 PK
    BlogId: 1 FK Modified
    Content: 'F# 5 is the latest version of F#, the functional programming...' Modified
    Title: 'Announcing F# 5' Modified
  Blog: {Id: 1}

4
After SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}
Post {Id: 3} Unchanged
    Id: 3 PK
    BlogId: 1 FK
    Content: '.NET 5.0 includes many enhancements, including single file a...'
    Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
```
Виклик SaveChanges на цьому етапі призведе до надсилання оновлень до бази даних для всіх наявних сутностей, тоді як нову сутність буде вставлено.

```sql
exec sp_executesql N'SET NOCOUNT ON;
UPDATE [Blogs] SET [Name] = @p0
OUTPUT 1
WHERE [Id] = @p1;
UPDATE [Posts] SET [BlogId] = @p2, [Content] = @p3, [Title] = @p4
OUTPUT 1
WHERE [Id] = @p5;
UPDATE [Posts] SET [BlogId] = @p6, [Content] = @p7, [Title] = @p8
OUTPUT 1
WHERE [Id] = @p9;
INSERT INTO [Posts] ([BlogId], [Content], [Title])
OUTPUT INSERTED.[Id]
VALUES (@p10, @p11, @p12);
',N'@p1 int,@p0 nvarchar(4000),@p5 int,@p2 int,@p3 nvarchar(4000),@p4 nvarchar(4000),@p9 int,@p6 int,@p7 nvarchar(4000),@p8 nvarchar(4000),@p10 int,@p11 nvarchar(4000),@p12 nvarchar(4000)',@p1=1,@p0=N'.NET Blog',@p5=1,@p2=1,@p3=N'Announcing the release of EF Core 5.0, a full featured cross-platform...',@p4=N'Announcing the Release of EF Core 5.0',@p9=2,@p6=1,@p7=N'F# 5 is the latest version of F#, the functional programming language...',@p8=N'Announcing F# 5',@p10=1,@p11=N'.NET 5.0 includes many enhancements, including single file applications, more...',@p12=N'Announcing .NET 5.0'
```
Це дуже простий спосіб формування операцій оновлення та вставлення для від’єднаного графа об’єктів. Однак наслідком цього є надсилання до бази даних запитів на оновлення або вставлення для кожної властивості кожного відстежуваного об’єкта, навіть якщо значення окремих властивостей не змінилися. Не варто цього лякатися: для багатьох сценаріїв із невеликими графами об’єктів це може бути простим і прагматичним способом генерування оновлень. Водночас інші, складніші патерни іноді можуть забезпечувати ефективніше оновлення, як описано в розділі «Розпізнавання ідентичності в EF Core» (Identity Resolution in EF Core).

## Видалення наявних сутностей

Щоб сутність була видалена під час виконання методу SaveChanges, вона повинна відстежуватися зі станом Deleted. Сутності зазвичай переводяться в стан Deleted шляхом виклику методів DbContext.Remove, DbContext.RemoveRange або відповідних методів об’єкта DbSet\<TEntity\>. Наприклад, щоб позначити наявний допис як видалений:

```cs
static async Task Deleting_existing_entities_1()
{
    Blog blog;

    // Create DB, add blog, get blog
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        await CleanDatabase(context);
        await PopulateDatabase2(context);
        blog = await GetBlog(context);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        context.Remove(blog.Posts[1]);//!!!

        Console.WriteLine("Before SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        int count = await context.SaveChangesAsync();
        Console.WriteLine(count);

        Console.WriteLine("After SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    }
}
```
Перевірка режиму налагодження відстежувача змін після цього виклику показує, що контекст відстежує сутність у стані Deleted:
```
Before SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Deleted
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}

1
After SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
```
Цей об'єкт буде видалено під час виклику SaveChanges.

```sql
exec sp_executesql N'SET IMPLICIT_TRANSACTIONS OFF;
SET NOCOUNT ON;
DELETE FROM [Posts]
OUTPUT 1
WHERE [Id] = @p0;
',N'@p0 int',@p0=2
```
Після завершення виконання методу SaveChanges видалена сутність від’єднується від DbContext, оскільки вона більше не існує в базі даних. Тому режим налагодження (debug view) показує порожній результат, адже сутность не відстежується.

## Видалення залежних/дочірніх сутностей

Видалення залежних/дочірніх сутностей із графа є простішим завданням, ніж видалення головних/батьківських сутностей. Додаткову інформацію див. у наступному розділі, а також у розділі «Зміна зовнішніх ключів і навігаційних властивостей».

Зазвичай відстежують окрему сутність або граф пов’язаних сутностей, а потім викликають метод Remove для тих сутностей, які потрібно видалити. Такий граф відстежуваних сутностей зазвичай створюється одним із двох способів:

1. Виконання запиту для сутностей
2. Використання методів Attach або Update для графа від’єднаних сутностей, як описано в попередніх розділах.

Наприклад, код отримує допис від клієнта, а потім виконує щось на кшталт цього:

```cs
        context.Attach(post);//!!!
        context.Remove(post);//!!!
```
Повний приклад

```cs
static async Task Deleting_dependent_child_entities()
{
    // Create DB, add blog, get blog
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        await CleanDatabase(context);
        await PopulateDatabase2(context);
    }

    Post post = await GetDisconnectedPost();

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        context.Attach(post);//!!!
        context.Remove(post);//!!!

        Console.WriteLine("Before SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        int count = await context.SaveChangesAsync();
        Console.WriteLine(count);

        Console.WriteLine("After SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    }

    async Task<Post> GetDisconnectedPost()
    {
        using var tempContext = new ApplicationDbContextFactory().CreateDbContext(null);
        return await tempContext.Posts.FindAsync(2);
    }
}
```
```
Before SaveChanges:
Post {Id: 2} Deleted
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: <null>

1
After SaveChanges:
```

Також можна просто викликати метод Remove

```cs
        //context.Attach(post);
        context.Remove(post);//!!!
```
Це працює так само, як і в попередньому прикладі, оскільки виклик методу Remove для невідстежуваної сутності призводить до того, що вона спочатку приєднується, а потім позначається як видалена (Deleted).

У більш реалістичних прикладах спочатку додається граф сутностей, а потім деякі з цих сутностей позначаються як видалені.

```cs
// Attach a blog and associated posts
context.Attach(blog);

// Mark one post as Deleted
context.Remove(blog.Posts[1]);
```
Усі сутності позначено як Unchanged, за винятком тієї, для якої було викликано метод Remove. 

```
Before SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Deleted
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}

1
After SaveChanges:
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
```


Цей об'єкт буде видалено під час виклику SaveChanges.

```sql
exec sp_executesql N'SET IMPLICIT_TRANSACTIONS OFF;
SET NOCOUNT ON;
DELETE FROM [Posts]
OUTPUT 1
WHERE [Id] = @p0;
',N'@p0 int',@p0=2
```
Після завершення виконання методу SaveChanges видалена сутність від’єднується від DbContext, оскільки вона більше не існує в базі даних. Інші сутності залишаються в стані Unchanged.

## Видалення головних/батьківських сутностей

Кожен зв’язок між двома типами сутностей має головний (батьківський) і залежний (дочірній) бік. Залежна.дочірня сутність — це та, що містить властивість зовнішнього ключа. У зв’язку типу one-to-many основний (батьківський) елемент перебуває на боці «one», а залежний (дочірній) — на боці «many». Додаткову інформацію див. у розділі «Зв'язки сутностей».

У попередніх прикладах ми видаляли допис — залежну (дочірню) сутність у зв’язку one-to-many між блогами та дописами. Це відносно просто, оскільки видалення залежного (дочірнього) об'єкта не впливає на інші об'єкти. З іншого боку, видалення головного (батьківського) об’єкта має також впливати на залежні (дочірні) об’єкти. Якщо цього не зробити, значення зовнішнього ключа посилатиметься на значення первинного ключа, якого більше не існує. Це неприпустимий стан моделі, що в більшості баз даних призводить до помилки порушення посилальної цілісності.

Цей неприпустимий стан моделі можна обробити двома способами:

1. Встановлення значень зовнішнього ключа (FK) як null. Це означає, що залежні (дочірні) записи більше не пов’язані з жодним основним (батьківським) записом. Це стандартна поведінка для необов’язкових зв’язків, у яких зовнішній ключ може набувати значення NULL. Встановлення значення null для зовнішнього ключа (FK) є неприпустимим для обов’язкових зв’язків, у яких зовнішній ключ зазвичай не може набувати значення null.
2. Видалення залежних об'єктів (дочірніх елементів). Це стандартна поведінка для обов'язкових зв'язків; вона також застосовна до необов'язкових зв'язків.

Детальну інформацію про відстеження змін і зв’язки див. у розділі «Зміна зовнішніх ключів і навігаційних властивостей».

### Необов’язкові зв’язки

У моделі, яку ми використовували, властивість зовнішнього ключа Post.BlogId може набувати значення null. Це означає, що зв’язок є необов’язковим, тому стандартна поведінка EF Core полягає в тому, що під час видалення блогу властивості зовнішнього ключа BlogId отримують значення null. Наприклад:

```cs
static async Task Deleting_principal_parent_entities_1()
{
    // Create DB, add blog, get blog
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        await CleanDatabase(context);
        await PopulateDatabase2(context);
    }

    var blog = await GetDisconnectedBlogAndPosts();

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        context.Attach(blog);
        context.Remove(blog);

        Console.WriteLine("Before SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        int count = await context.SaveChangesAsync();
        Console.WriteLine(count);

        Console.WriteLine("After SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    }

    async Task<Blog> GetDisconnectedBlogAndPosts()
    {
        using var tempContext = new ApplicationDbContextFactory().CreateDbContext(null);
        return await tempContext.Blogs.Include(e => e.Posts).SingleAsync();
    }
}
```
Перевірка режиму налагодження відстежувача змін після виклику методу Remove показує, що, як і очікувалося, блог тепер позначено як Deleted:

```
Before SaveChanges:
Blog {Id: 1} Deleted
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Modified
    Id: 1 PK
    BlogId: <null> FK Modified Originally 1
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: <null>
Post {Id: 2} Modified
    Id: 2 PK
    BlogId: <null> FK Modified Originally 1
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: <null>

3
After SaveChanges:
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: <null> FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: <null>
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: <null> FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: <null>
```

Що цікавіше, усі пов’язані дописи тепер позначені як змінені. Це відбувається тому, що властивість зовнішнього ключа у кожній сутності встановлено значення null. Виклик SaveChanges оновлює значення зовнішнього ключа для кожного допису на null у базі даних, перш ніж видалити блог:

```sql
exec sp_executesql N'SET NOCOUNT ON;
UPDATE [Posts] SET [BlogId] = @p0
OUTPUT 1
WHERE [Id] = @p1;
UPDATE [Posts] SET [BlogId] = @p2
OUTPUT 1
WHERE [Id] = @p3;
DELETE FROM [Blogs]
OUTPUT 1
WHERE [Id] = @p4;
',N'@p1 int,@p0 int,@p3 int,@p2 int,@p4 int',@p1=1,@p0=NULL,@p3=2,@p2=NULL,@p4=1
```
Після завершення виконання методу SaveChanges видалена сутність від’єднується від DbContext, оскільки вона більше не існує в базі даних. Інші сутності тепер мають статус Unchanged (без змін) і значення зовнішніх ключів, що дорівнюють null, що відповідає стану бази даних.

### Обов’язкові зв’язки

Якщо властивість зовнішнього ключа Post.BlogId не допускає значення `null`, то зв’язок між блогами та дописами стає обов’язковим. У цій ситуації EF Core за замовчуванням видалятиме залежні (дочірні) сутності, коли видаляється головна (батьківська) сутність. Наприклад, видалення блогу разом із пов’язаними дописами, як у попередньому прикладі:

```cs
public class Post
{

    public int Id { get; set; }

    // ...

    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}
```
Метод видаленя без змін. 

```cs
        context.Attach(blog);
        context.Remove(blog);

        //or just
        //  context.Remove(blog);
```
Перевірка режиму налагодження відстежувача змін після виклику методу Remove показує, що блог, як і очікувалося, знову позначено як Deleted:

```
Before SaveChanges:
Blog {Id: 1} Deleted
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Deleted
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Deleted
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}

3
After SaveChanges:
```
Цікавіше в цьому випадку те, що всі пов’язані дописи також було позначено як видалені. Виклик методу SaveChanges призводить до видалення блогу та всіх пов’язаних із ним дописів із бази даних:

```sql
exec sp_executesql N'SET NOCOUNT ON;
DELETE FROM [Posts]
OUTPUT 1
WHERE [Id] = @p0;
DELETE FROM [Posts]
OUTPUT 1
WHERE [Id] = @p1;
DELETE FROM [Blogs]
OUTPUT 1
WHERE [Id] = @p2;
',N'@p0 int,@p1 int,@p2 int',@p0=1,@p1=2,@p2=1
```

Після завершення виконання методу SaveChanges усі видалені сутності від’єднуються від DbContext, оскільки вони більше не існують у базі даних. Тому вивід налагоджувального представлення (debug view) є порожнім.

    Примітка

    У цьому документі робота зі зв’язками в EF Core розглянута лише поверхнево. Докладнішу інформацію про моделювання зв’язків див. у розділі «Зв’язки» (Relationships), а відомості про оновлення чи видалення залежних (дочірніх) сутностей під час виклику методу SaveChanges — у розділі «Зміна зовнішніх ключів і навігаційних властивостей» (Changing Foreign Keys and Navigations).

## Налаштовуване відстеження за допомогою TrackGraph

Метод ChangeTracker.TrackGraph працює подібно до методів Add, Attach та Update, але з тією відмінністю, що він викликає зворотний виклик (callback) для кожного екземпляра сутності перед початком відстеження. Це дозволяє використовувати власну логіку для визначення того, як відстежувати окремі сутності в графі.

Наприклад, розгляньмо правило, яке EF Core використовує під час відстеження сутностей зі згенерованими значеннями ключів: якщо значення ключа дорівнює нулю, сутність вважається новою, і її слід вставити. Розширимо це правило: якщо значення ключа від’ємне, сутність слід видалити. Це дозволяє змінювати значення первинних ключів у сутностях від’єднаного графа, щоб позначати видалені сутності:

```cs
        blog.Posts.Add(
            new Post
            {
                Title = "Announcing .NET 5.0",
                Content = ".NET 5.0 includes many enhancements, including single file applications, more..."
            }
        );

        var toDelete = blog.Posts.Single(e => e.Title == "Announcing F# 5");
        toDelete.Id = -toDelete.Id;
```
Цей незв’язний граф можна відстежувати за допомогою TrackGraph:

```cs
static async Task Custom_tracking_with_TrackGraph_1()
{
    // Create DB, add blog, get blog
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        await CleanDatabase(context);
        await PopulateDatabase2(context);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        Blog blog = await context.Blogs.AsNoTracking().Include(e => e.Posts).SingleAsync(e => e.Name == ".NET Blog");

        blog.Posts.Add(
            new Post
            {
                Title = "Announcing .NET 5.0",
                Content = ".NET 5.0 includes many enhancements, including single file applications, more..."
            }
        );

        var toDelete = blog.Posts.Single(e => e.Title == "Announcing F# 5");
        toDelete.Id = -toDelete.Id;

        int count = await UpdateBlog(blog);
        Console.WriteLine(count);
    }
}

static async Task<int> UpdateBlog(Blog blog)
{
    var context = new ApplicationDbContextFactory().CreateDbContext(null);

    context.ChangeTracker.TrackGraph(
                blog, node =>
                {
                    var propertyEntry = node.Entry.Property("Id");
                    var keyValue = (int)propertyEntry.CurrentValue;

                    if (keyValue == 0)
                    {
                        node.Entry.State = EntityState.Added;
                    }
                    else if (keyValue < 0)
                    {
                        propertyEntry.CurrentValue = -keyValue;
                        node.Entry.State = EntityState.Deleted;
                    }
                    else
                    {
                        node.Entry.State = EntityState.Modified;
                    }

                    Console.WriteLine($"Tracking {node.Entry.Metadata.DisplayName()} with key value {keyValue} as {node.Entry.State}");
                });

    return await context.SaveChangesAsync();
}

```
Для кожного об'єкта в графі наведений вище код перевіряє значення первинного ключа перед початком відстеження цього об'єкта. Для невстановлених (нульових) значень ключа код виконує ті самі дії, що й стандартний механізм EF Core. Тобто, якщо ключ не встановлено, сутність позначається як Added. Якщо ключ встановлено, а значення є невід’ємним, сутність позначається як Modified. Однак, якщо виявлено від’ємне значення ключа, відновлюється його дійсне невід’ємне значення, а сутність позначається як Deleted.

```
Tracking Blog with key value 1 as Modified
Tracking Post with key value 1 as Modified
Tracking Post with key value -2 as Deleted
Tracking Post with key value 0 as Added
4
```

    Примітка

    Для спрощення в цьому коді передбачається, що кожна сутність має властивість первинного ключа цілочисельного типу з назвою Id. Це можна оформити у вигляді абстрактного базового класу або інтерфейсу. Як варіант, властивість (або властивості) первинного ключа можна отримати з метаданих IEntityType, завдяки чому цей код працюватиме з будь-яким типом сутності.

Метод TrackGraph має дві перевантажені версії. У наведеній вище простій перевантаженій версії методу EF Core визначає, коли припинити обхід графа. Зокрема, процес переходу до нових пов’язаних сутностей від заданої сутності припиняється, якщо ця сутність уже відстежується або якщо функція зворотного виклику не розпочинає її відстеження.

Розширена перевантажена версія методу ChangeTracker.TrackGraph\<TState\>(Object, TState, Func\<EntityEntryGraphNode\<TState\>\>, Boolean>) містить функцію зворотного виклику, яка повертає значення типу bool. Якщо ця функція повертає false, обхід графа припиняється; в іншому разі він продовжується. Під час використання цього методу слід бути уважним, щоб уникнути нескінченних циклів.