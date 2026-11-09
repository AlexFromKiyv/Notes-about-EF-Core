# Додаткові функції відстеження змін

У цьому документі розглядаються різні функції та сценарії, пов'язані з відстеженням змін.

## Модель для прикладів

Types.cs
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

    public IList<Tag> Tags { get; } = new List<Tag>();
}

public class Tag
{
    public int Id { get; set; } // Primary key
    public string Text { get; set; }

    public IList<Post> Posts { get; } = new List<Post>(); // Collection navigation
}
```
ApplicationDbContext.cs
```cs

public class ApplicationDbContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
await DoAsync(CleanAndPopulateDatabase);
```
Helper.cs
```cs
partial class Program
{
    private static async Task CleanAndPopulateDatabase()
    {
        using var context = new ApplicationDbContextFactory().CreateDbContext(null);
        //Console.WriteLine(context.Model.ToDebugString());

        await CleanDatabase();
        await PopulateDatabase(context);
    }


    protected static async Task CleanDatabase()
    {
        using var context = new ApplicationDbContextFactory().CreateDbContext(null);

        Console.WriteLine("Deleting and re-creating database...");
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        Console.WriteLine("Done. Database is clean and fresh.");
    }

    public static async Task PopulateDatabase(ApplicationDbContext context)
    {

        context.AddRange(
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
                },
            },
            new Blog
            {
                Name = "Visual Studio Blog",
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


## Add чи AddAsync

Entity Framework Core (EF Core) надає асинхронні методи щоразу, коли використання цього методу може призвести до взаємодії з базою даних. Також передбачено синхронні методи, щоб уникнути накладних витрат під час використання баз даних, які не підтримують високопродуктивний асинхронний доступ.
DbContext.Add та DbSet\<TEntity\>.Add зазвичай не звертаються до бази даних, оскільки ці методи за своєю суттю просто починають відстежувати сутності. Однак деякі форми генерації значень можуть звертатися до бази даних для генерації значення ключа. Єдиний генератор значень, який виконує цю функцію та постачається з EF Core, – це HiLoValueGenerator\<TValue\>. Використання цього генератора є рідкістю; він ніколи не налаштовується за замовчуванням. Це означає, що переважна більшість програм повинні використовувати Add, а не AddAsync. 

Інші подібні методи, такі як Update, Attach та Remove, не мають асинхронних перевантажень, оскільки вони ніколи не генерують нові значення ключів і, отже, ніколи не потребують доступу до бази даних.

## AddRange, UpdateRange, AttachRange та RemoveRange

DbSet\<TEntity\> та DbContext пропонують альтернативні версії методів Add, Update, Attach та Remove, які приймають кілька екземплярів в одному виклику. Ці методи — AddRange, UpdateRange, AttachRange та RemoveRange відповідно.

Ці методи надаються для зручності. Використання методу "range" має таку ж функціональність, як і багаторазові виклики еквівалентного методу, що не є діапазоном. Між цими двома підходами немає суттєвої різниці в продуктивності.

    Примітка

    Це відрізняється від EF6, де AddRange та Add автоматично викликали DetectChanges, але багаторазовий виклик Add призводив до того, що DetectChanges викликався кілька разів замість одного. Це зробило AddRange ефективнішим в EF6. В EF Core жоден з цих методів не викликає DetectChanges автоматично.

## Методи DbContext чи DbSet

Багато методів, зокрема Add, Update, Attach і Remove, реалізовані як у DbSet<TEntity>, так і в DbContext. Для звичайних типів сутностей ці методи поводяться абсолютно однаково. Це пояснюється тим, що CLR-тип сутності зіставляється рівно з одним типом сутності в моделі EF Core. Таким чином, тип CLR повністю визначає, де сутність вписується в модель, і тому DbSet, який потрібно використовувати, можна визначити неявно.

Винятком із цього правила є використання типів сутностей спільного типу, які в основному використовуються для сутностей типу many-to-many. Під час використання типу сутності спільного типу спочатку необхідно створити набір баз даних (DbSet) для типу моделі EF Core, який використовується. Такі методи, як Add, Update, Attach та Remove, можна використовувати на DbSet без будь-якої неоднозначності щодо того, який тип моделі EF Core використовується.

Типи сутностей спільного типу використовуються за замовчуванням для сутностей об'єднання у зв'язках many-to-many. Наприклад, наведений нижче код налаштовує Dictionary\<string, int\> як тип сутності об'єднання:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder
        .SharedTypeEntity<Dictionary<string, int>>(
            "PostTag",
            b =>
            {
                b.IndexerProperty<int>("TagId");
                b.IndexerProperty<int>("PostId");
            });

    modelBuilder.Entity<Post>()
        .HasMany(p => p.Tags)
        .WithMany(p => p.Posts)
        .UsingEntity<Dictionary<string, int>>(
            "PostTag",
            j => j.HasOne<Tag>().WithMany(),
            j => j.HasOne<Post>().WithMany());
}
```
"Зміна зовнішніх ключів і навігаційних властивостей" демонструє, як пов’язати дві сутності шляхом відстеження нового екземпляра сполучної сутності. Наведений нижче код виконує це для типу сутності спільного типу Dictionary\<string, int\>, який використовується для сутності зв’язку:

```cs
//...
await DoAsync(DbContext_versus_DbSet_methods_1);

static async Task DbContext_versus_DbSet_methods_1() 
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var post = await context.Posts.SingleAsync(e => e.Id == 3);
    var tag = await context.Tags.SingleAsync(e => e.Id == 1);

    var joinEntitySet = context.Set<Dictionary<string, int>>("PostTag");
    var joinEntity = new Dictionary<string, int> { ["PostId"] = post.Id, ["TagId"] = tag.Id };
    joinEntitySet.Add(joinEntity);

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    await context.SaveChangesAsync();
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
Tag {Id: 1} Unchanged
    Id: 1 PK
    Text: '.NET'
  Posts: [{Id: 3}]
PostTag (Dictionary<string, int>) {PostId: 3, TagId: 1} Added
    PostId: 3 PK FK
    TagId: 1 PK FK
```
Зверніть увагу, що DbContext.Set<TEntity>(String) використовується для створення DbSet для типу сутності PostTag. Цей DbSet потім можна використовувати для виклику методу Add із новим екземпляром сутності зв’язку.

    Важливо

    Тип CLR, який за угодою використовується для типів сутностей об’єднання (join entity types), може змінитися в майбутніх випусках задля підвищення продуктивності. Не покладайтеся на жоден конкретний тип сутності об’єднання, якщо тільки він не був явно налаштований — як це зроблено для `Dictionary<string, int>` у наведеному вище коді.

## Доступ до властивостей vs. доступу до полів

Доступ до властивостей сутності за замовчуванням використовує поле резервного копіювання властивості. Це ефективно та дозволяє уникнути побічних ефектів від виклику методів отримання та встановлення властивостей. Наприклад, саме так «ліниве завантаження» (lazy-loading) дозволяє уникнути виникнення нескінченних циклів. Докладнішу інформацію про налаштування допоміжних полів (backing fields) у моделі див. у розділі «Допоміжні поля».

Іноді може бути доцільно, щоб EF Core викликав побічні ефекти під час зміни значень властивостей. Наприклад, під час прив’язки даних до сутностей встановлення значення властивості може генерувати сповіщення для інтерфейсу користувача (UI) — чого не відбувається у разі прямого встановлення значення поля. Цього можна досягти шляхом зміни параметра PropertyAccessMode для:
* Усі типи сутностей у моделі, що використовують ModelBuilder.UsePropertyAccessMode
* Усі властивості та навігаційні властивості для конкретного типу сутності, що налаштовуються за допомогою EntityTypeBuilder\<TEntity\>.UsePropertyAccessMode
* Конкретна властивість із використанням PropertyBuilder.UsePropertyAccessMode
* Конкретний варіант навігації з використанням NavigationBuilder.UsePropertyAccessMode

Режими доступу до властивостей Field і PreferField призводять до того, що EF Core звертається до значення властивості через її внутрішнє поле (backing field). Аналогічно, Property та PreferProperty призведуть до того, що EF Core звертатиметься до значення властивості через її геттер і сеттер.

Якщо використовуються поле або властивість, а EF Core не може отримати доступ до значення через це поле або через геттер/сеттер властивості, EF Core видасть виняток. Це гарантує, що EF Core завжди використовує доступ через поля або властивості саме тоді, коли ви розраховуєте на це.

З іншого боку, режими PreferField і PreferProperty передбачають використання властивості або відповідного допоміжного поля (backing field) у тих випадках, коли застосування бажаного способу доступу є неможливим. За замовчуванням використовується PreferField. Це означає, що EF Core використовуватиме поля, коли це можливо, але не зазнає невдачі, якщо доступ до властивості доведеться здійснювати через її геттер або сеттер.

Параметри FieldDuringConstruction і PreferFieldDuringConstruction налаштовують EF Core на використання внутрішніх полів (backing fields) лише під час створення екземплярів сутностей. Це дозволяє виконувати запити без побічних ефектів, пов’язаних із методами доступу (getter та setter), тоді як подальші зміни властивостей, що вносяться EF Core, спричинятимуть ці побічні ефекти.

Різні режими доступу до майна наведено в наступній таблиці:

|PropertyAccessMode|Перевага|Перевага створення сутності|Резервний варіант|Резервний варіант створення|
|---|---|---|---|---|
|Field|Field|Field|Throws|Throws|
|Property|Property|Property|Throws|Throws|
|PreferField|Field|Field|Property|Property|
|PreferProperty|Property|Property|Field|Field|
|FieldDuringConstruction|Property|Field|Field|Throws|
|PreferProperty|Property|Field|Field|Property|

## Тимчасові значення

Під час відстеження нових сутностей EF Core створює тимчасові значення ключів; справжні значення ключів для цих сутностей будуть згенеровані базою даних у момент виклику методу SaveChanges. Огляд того, як використовуються ці тимчасові значення, див. у розділі «Відстеження змін в EF Core».

### Доступ до тимчасових значень

Тимчасові значення зберігаються в механізмі відстеження змін (change tracker) і не записуються безпосередньо в екземпляри сутностей. Однак ці тимчасові значення стають доступними під час використання різноманітних механізмів доступу до відстежуваних сутностей. Наприклад, наведений нижче код звертається до тимчасового значення за допомогою EntityEntry.CurrentValues:

```cs
static async Task Temporary_values_1()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var blog = new Blog { Name = ".NET Blog" };

    context.Add(blog);

    Console.WriteLine($"Blog.Id set on entity is {blog.Id}");
    Console.WriteLine($"Blog.Id tracked by EF is {context.Entry(blog).Property(e => e.Id).CurrentValue}");
}
```
```
Blog.Id set on entity is 0
Blog.Id tracked by EF is -2147482645
```
PropertyEntry.IsTemporary можна використовувати для перевірки тимчасових значень.

### Маніпулювання тимчасовими значеннями

Іноді буває доцільно явно працювати з тимчасовими значеннями. Наприклад, на вебклієнті можна створити набір нових сутностей, а потім серіалізувати їх і передати назад на сервер. Значення зовнішніх ключів — це один зі способів встановлення зв’язків між цими сутностями. У наведеному нижче коді використовується цей підхід для пов’язування графа нових сутностей за допомогою зовнішнього ключа, при цьому дозволяючи генерувати фактичні значення ключів під час виклику методу SaveChanges.

```cs
static async Task Temporary_values_2()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var blogs = new List<Blog> 
    { 
        new Blog { Id = -1, Name = ".NET Blog" }, 
        new Blog { Id = -2, Name = "Visual Studio Blog" } 
    };

    var posts = new List<Post>
        {
            new Post
            {
                Id = -1,
                BlogId = -1,
                Title = "Announcing the Release of EF Core 5.0",
                Content = "Announcing the release of EF Core 5.0, a full featured cross-platform..."
            },
            new Post
            {
                Id = -2,
                BlogId = -2,
                Title = "Disassembly improvements for optimized managed debugging",
                Content = "If you are focused on squeezing out the last bits of performance for your .NET service or..."
            }
        };

    foreach (var blog in blogs)
    {
        context.Add(blog).Property(e => e.Id).IsTemporary = true;
    }

    foreach (var post in posts)
    {
        context.Add(post).Property(e => e.Id).IsTemporary = true;
    }

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    await context.SaveChangesAsync();

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
Зверніть увагу:

* Від’ємні числа використовуються як тимчасові значення ключів; це не є обов’язковим, але є поширеною практикою для запобігання конфліктам ключів.
* Властивості зовнішнього ключа (FK) Post.BlogId присвоюється те саме від’ємне значення, що й первинному ключу (PK) відповідного блогу.
* Значення первинних ключів позначаються як тимчасові шляхом встановлення властивості IsTemporary після того, як сутність потрапляє під відстеження. Це необхідно, оскільки будь-яке значення ключа, надане програмою, вважається реальним значенням ключа.

Якщо переглянути налагоджувальне подання відстежувача змін (change tracker) перед викликом SaveChanges, можна побачити, що значення первинних ключів (PK) позначені як тимчасові, а дописи пов’язані з відповідними блогами — включно з автоматичним коригуванням навігаційних властивостей:

```
Blog {Id: -2} Added
    Id: -2 PK Temporary
    Name: 'Visual Studio Blog'
  Posts: [{Id: -2}]
Blog {Id: -1} Added
    Id: -1 PK Temporary
    Name: '.NET Blog'
  Posts: [{Id: -1}]
Post {Id: -2} Added
    Id: -2 PK Temporary
    BlogId: -2 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: -2}
  Tags: []
Post {Id: -1} Added
    Id: -1 PK Temporary
    BlogId: -1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: -1}
  Tags: []
```
Після виклику SaveChanges ці тимчасові значення були замінені реальними значеннями, згенерованими базою даних:

```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: 'Visual Studio Blog'
  Posts: [{Id: 1}]
Blog {Id: 2} Unchanged
    Id: 2 PK
    Name: '.NET Blog'
  Posts: [{Id: 2}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 1}
  Tags: []
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 2 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 2}
  Tags: []
```

## Робота зі значеннями за замовчуванням

EF Core дозволяє властивості отримувати значення за замовчуванням із бази даних під час виклику методу SaveChanges. Як і у випадку зі згенерованими значеннями ключів, EF Core використовуватиме значення за замовчуванням із бази даних лише тоді, коли жодного значення не було задано явно. Наприклад, розглянемо наступний тип сутності:

Types.cs
```cs
public class Token
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime ValidFrom { get; set; }
}
```
Властивість ValidFrom налаштовано на отримання значення за замовчуванням з бази даних:

ApplicarionDB
```cs
public class ApplicationDbContext : DbContext
{
    //...

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<Token>()
            .Property(e => e.ValidFrom)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}

```
Це створює таблицю.

```sql
CREATE TABLE [dbo].[Token] (
    [Id]        INT            IDENTITY (1, 1) NOT NULL,
    [Name]      NVARCHAR (MAX) NOT NULL,
    [ValidFrom] DATETIME2 (7)  DEFAULT (getdate()) NOT NULL,
    CONSTRAINT [PK_Token] PRIMARY KEY CLUSTERED ([Id] ASC)
);
```



Під час вставки сутності цього типу EF Core дозволить базі даних згенерувати значення, якщо замість цього не було встановлено явне значення. Наприклад:

```cs
static async Task Working_with_default_values_1()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    context.AddRange(
        new Token { Name = "A" },
        new Token { Name = "B", ValidFrom = new DateTime(1111, 11, 11, 11, 11, 11) });

    await context.SaveChangesAsync();

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
Перегляд налагоджувального вигляду відстеження змін показує, що для першого токена база даних згенерувала значення ValidFrom, тоді як для другого токена використовувалося явно встановлене значення:

```
Token {Id: 1} Unchanged
    Id: 1 PK
    Name: 'A'
    ValidFrom: '06.11.2056 17:59:35'
Token {Id: 2} Unchanged
    Id: 2 PK
    Name: 'B'
    ValidFrom: '11.11.1111 11:11:11'
```

    Примітка

    Використання значень бази даних за замовчуванням вимагає, щоб для стовпця бази даних було налаштоване обмеження значення за замовчуванням. Це робиться автоматично міграціями EF Core під час використання HasDefaultValueSql або HasDefaultValue. Обов’язково створіть обмеження за замовчуванням для стовпця якимось іншим способом, якщо не використовуються міграції EF Core.


## Використання властивостей, що допускають значення null

EF Core може визначити, чи було встановлено властивість, порівнюючи значення властивості зі значенням за замовчуванням CLR для цього типу. Це добре працює в більшості випадків, але означає, що значення за замовчуванням CLR не можна явно вставити в базу даних. Наприклад, розглянемо сутність із цілочисельною властивістю:

```cs
public class Foo
{
    public int Id { get; set; }
    public int Count { get; set; }
}

```
Де для цієї властивості встановлено значення бази даних за замовчуванням -1:

```cs
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
        .Entity<Foo>()
        .Property(e => e.Count)
        .HasDefaultValue(-1);
    }
```
Мається на увазі, що значення за замовчуванням, -1, використовуватиметься щоразу, коли не встановлено явне значення. Однак встановлення значення 0 (стандартне значення CLR для цілих чисел) не відрізняється для EF Core від невстановлення жодного значення, це означає, що для цієї властивості неможливо вставити 0. Наприклад:

```cs
static async Task Working_with_default_values_2()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var fooA = new Foo { Count = 10 };
    var fooB = new Foo { Count = 0 };
    var fooC = new Foo();

    context.AddRange(fooA, fooB, fooC);
    await context.SaveChangesAsync();

    Console.WriteLine(fooA.Count);
    Console.WriteLine(fooB.Count); // Not what we want!
    Console.WriteLine(fooC.Count);
}
```
```
10
-1
-1
```
Зверніть увагу, що екземпляр, де властивість Count була явно встановлена ​​на 0, все одно отримує значення за замовчуванням з бази даних, що не є тим, що ми мали на увазі. Простий спосіб вирішити цю проблему – зробити властивість Count null-допустимою:

```cs
public class Foo
{
    public int Id { get; set; }
    public int? Count { get; set; }
}
```
Це робить CLR значенням за замовчуванням null замість 0, що означає, що тепер при явному встановленні буде вставлено 0:
```
10
0
-1
```

## Використання nullable резервних полів  

Проблема зі створенням властивості, що може мати значення null, полягає в тому, що вона може бути концептуально непридатною для використання в моделі предметної області. Примусове встановлення значення null для властивості ставить під загрозу модель.

Властивість можна залишити такою, що не допускає значення null, тоді як лише її допоміжне поле може допускати значення null. Наприклад:

```cs
public class Foo
{
    private int? count;

    public int Id { get; set; }
    public int Count 
    { 
        get => count ?? -1; 
        set => count = value; 
    }
}
```
Це дозволяє вставляти значення за замовчуванням CLR (0), якщо властивість явно має значення 0, при цьому не потрібно виставляти властивість як nullable в моделі домену. 
Той самий метод виводить:

```
10
0
-1
```

## Nullable допоміжні поля для властивостей типу bool

Цей шаблон особливо корисний у випадках, коли використовуються властивості типу bool зі значеннями за замовчуванням, що генеруються на рівні сховища даних. Оскільки значенням за замовчуванням для типу bool у CLR є false, це означає, що false неможливо явно вставити, використовуючи звичайний шаблон. Розглянемо, наприклад, тип сутності User:

```cs
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }

    private bool? _isAuthorized;

    public bool IsAuthorized
    {
        get => _isAuthorized ?? true;
        set => _isAuthorized = value;
    }
}
```
Для властивості IsAuthorized налаштовано значення за замовчуванням у базі даних — «true»:

```cs
modelBuilder
    .Entity<User>()
    .Property(e => e.IsAuthorized)
    .HasDefaultValue(true);
```
Властивості IsAuthorized можна явно надати значення true або false перед вставкою; також її можна залишити не встановленою — у такому разі буде використано значення, задане в базі даних за замовчуванням:

```cs
static async Task Working_with_default_values_3()
{
    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);

    var userA = new User { Name = "Mac" };
    var userB = new User { Name = "Alice", IsAuthorized = true };
    var userC = new User { Name = "Baxter", IsAuthorized = false }; // Always deny Baxter access!

    context.AddRange(userA, userB, userC);

    await context.SaveChangesAsync();
}
```
Результат виконання SaveChanges при використанні SQLite показує, що для Mac застосовується значення за замовчуванням, визначене в базі даних, тоді як для Alice і Baxter встановлено явні значення:

```sql
-- Executed DbCommand (0ms) [Parameters=[@p0='Mac' (Size = 3)], CommandType='Text', CommandTimeout='30']
INSERT INTO "User" ("Name")
VALUES (@p0);
SELECT "Id", "IsAuthorized"
FROM "User"
WHERE changes() = 1 AND "rowid" = last_insert_rowid();

-- Executed DbCommand (0ms) [Parameters=[@p0='True' (DbType = String), @p1='Alice' (Size = 5)], CommandType='Text', CommandTimeout='30']
INSERT INTO "User" ("IsAuthorized", "Name")
VALUES (@p0, @p1);
SELECT "Id"
FROM "User"
WHERE changes() = 1 AND "rowid" = last_insert_rowid();

-- Executed DbCommand (0ms) [Parameters=[@p0='False' (DbType = String), @p1='Baxter' (Size = 6)], CommandType='Text', CommandTimeout='30']
INSERT INTO "User" ("IsAuthorized", "Name")
VALUES (@p0, @p1);
SELECT "Id"
FROM "User"
WHERE changes() = 1 AND "rowid" = last_insert_rowid();
```

## Лише значення за замовчуванням у схемі

Іноді корисно мати значення за замовчуванням у схемі бази даних, створеній за допомогою міграцій EF Core, навіть якщо сам EF Core ніколи не використовуватиме ці значення під час операцій вставлення (insert). Цього можна досягти, налаштувавши властивість за допомогою PropertyBuilder.ValueGeneratedNever. Наприклад:

```cs
modelBuilder
    .Entity<Bar>()
    .Property(e => e.Count)
    .HasDefaultValue(-1)
    .ValueGeneratedNever();
```