# Виявлення змін і сповіщення

Кожен екземпляр DbContext відстежує зміни, внесені в сутності. Ці відстежувані сутності, своєю чергою, зумовлюють зміни в базі даних під час виклику методу SaveChanges. Це питання розглядається в розділі Відстеження змін > Огляд , і в цьому документі передбачається, що читач розуміє стани сутностей та основи відстеження змін в Entity Framework Core (EF Core).

Для відстеження змін у властивостях і зв’язках необхідно, щоб DbContext був здатний виявляти ці зміни. У цьому документі описано, як відбувається таке виявлення, а також як використовувати сповіщення про зміни властивостей або проксі-об'єкти для відстеження змін, щоб забезпечити негайне виявлення цих змін.

## Модель прикладів

Types.cs
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
    public IList<Tag> Tags { get; } = new List<Tag>(); // Skip collection navigation
}

public class Tag
{
    public int Id { get; set; }
    public string Text { get; set; }

    public IList<Post> Posts { get; } = new List<Post>(); // Skip collection navigation
}


public class PostTag
{
    public int PostId { get; set; } // First part of composite PK; FK to Post
    public int TagId { get; set; } // Second part of composite PK; FK to Tag

    public DateTime TaggedOn { get; set; } // Auto-generated payload property
    public string TaggedBy { get; set; } // Not-generated payload property
}
```

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
        modelBuilder.Entity<Post>()
            .HasMany(p => p.Tags)
            .WithMany(p => p.Posts)
            .UsingEntity<PostTag>(
                j => j.HasOne<Tag>().WithMany(),
                j => j.HasOne<Post>().WithMany());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entityEntry in ChangeTracker.Entries<PostTag>())
        {
            if (entityEntry.State == EntityState.Added)
            {
                entityEntry.Entity.TaggedBy = "Alex";
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```
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

# Відстеження змін за допомогою знімків

За замовчуванням EF Core створює знімок значень властивостей кожної сутності в той момент, коли вона вперше потрапляє під відстеження екземпляром DbContext. Значення, збережені в цьому знімку, потім порівнюються з поточними значеннями сутності, щоб визначити, які значення властивостей змінилися. Виявлення змін відбувається під час виклику методу SaveChanges, щоб гарантувати, що всі змінені значення будуть зафіксовані перед надсиланням оновлень до бази даних. Однак виявлення змін відбувається й в інші моменти, щоб гарантувати, що застосунок працює з актуальною інформацією відстеження. Виявлення змін можна ініціювати в будь-який момент, викликавши ChangeTracker.DetectChanges().

## Коли потрібне виявлення змін

Виявлення змін необхідне в тих випадках, коли властивість або навігаційна властивість були змінені без використання EF Core для внесення цих змін. Наприклад, розгляньмо завантаження блогів і дописів, а потім внесення змін до цих сутностей:

```cs
static async Task Snapshot_change_tracking_1()
{
    await CleanAndPopulateDatabase();

    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Blog? blog = await context.Blogs.Include(e => e.Posts).FirstAsync(e => e.Name == ".NET Blog");

    // Change a property value
    blog.Name = ".NET Blog (Updated!)";

    // Add a new entity to a navigation
    blog.Posts.Add(
        new Post
        {
            Title = "What’s next for System.Text.Json?",
            Content = ".NET 5.0 was released recently and has come with many..."
        });

    Console.WriteLine("\n\nBefore DetectChanges:\n");
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
   
    context.ChangeTracker.DetectChanges();
   
    Console.WriteLine("\n\nAfter DetectChanges:\n");
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```

Якщо переглянути налагоджувальне подання відстежувача змін (change tracker) перед викликом ChangeTracker.DetectChanges(), можна побачити, що внесені зміни ще не виявлені, а отже, не відображені в станах сутностей і даних про змінені властивості:

```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog (Updated!)' Originally '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, <not found>]
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
Зокрема, стан запису в блозі залишається Unchanged, а новий запис не відображається як відстежуваний об'єкт. (Проникливий користувач помітить, що властивості повідомляють про свої нові значення, навіть якщо ці зміни ще не були виявлені EF Core. Це тому, що налагоджувальне подання зчитує поточні значення безпосередньо з екземпляра сутності.)

Порівняйте це з виглядом у режимі налагодження після виклику DetectChanges:

```
Blog {Id: 1} Modified
    Id: 1 PK
    Name: '.NET Blog (Updated!)' Modified Originally '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: -2147482643}]
Post {Id: -2147482643} Added
    Id: -2147482643 PK Temporary
    BlogId: 1 FK
    Content: '.NET 5.0 was released recently and has come with many...'
    Title: 'What's next for System.Text.Json?'
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
```
Тепер блог правильно позначено як Modified, а новий допис виявлено й відстежується як Added.

На початку цього розділу ми зазначали, що виявлення змін необхідне в тих випадках, коли внесення змін відбувається без участі EF Core. Ось що відбувається в наведеному вище коді. Тобто зміни властивостей і навігаційних властивостей вносяться безпосередньо в екземпляри сутностей, а не за допомогою будь-яких методів EF Core.

Порівняйте це з наведеним нижче кодом, який змінює сутності таким самим чином, але цього разу — з використанням методів EF Core:

```cs
static async Task Snapshot_change_tracking_2()
{
    await CleanAndPopulateDatabase();
 
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Blog? blog = await context.Blogs.Include(e => e.Posts).FirstAsync(e => e.Name == ".NET Blog");

    // Change a property value
    context.Entry(blog).Property(e => e.Name).CurrentValue = ".NET Blog (Updated!)";

    // Add a new entity to the DbContext
    context.Add(
        new Post
        {
            Blog = blog,
            Title = "What’s next for System.Text.Json?",
            Content = ".NET 5.0 was released recently and has come with many..."
        });

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
```
Blog {Id: 1} Modified
    Id: 1 PK
    Name: '.NET Blog (Updated!)' Modified Originally '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: -2147482643}]
Post {Id: -2147482643} Added
    Id: -2147482643 PK Temporary
    BlogId: 1 FK
    Content: '.NET 5.0 was released recently and has come with many...'
    Title: 'What's next for System.Text.Json?'
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
```

У цьому випадку налагоджувальне подання відстежувача змін (change tracker) показує, що всі стани сутностей і зміни властивостей відомі, хоча фактичного процесу виявлення змін ще не відбулося. Це пояснюється тим, що PropertyEntry.CurrentValue — це метод EF Core; отже, EF Core одразу дізнається про зміни, внесені за допомогою цього методу. Аналогічно, виклик DbContext.Add дозволяє EF Core миттєво дізнатися про нову сутність і належним чином відстежувати її.

    Порада

    Не намагайтеся уникнути механізму виявлення змін, постійно використовуючи методи EF Core для внесення змін до сутностей. Такий підхід часто є більш громіздким і менш ефективним, ніж внесення змін до сутностей у звичний спосіб. Мета цього документа — роз’яснити, у яких випадках виявлення змін є необхідним, а в яких — ні. Документ не має на меті заохочувати відмову від виявлення змін.

## Методи, що автоматично виявляють зміни

Метод DetectChanges() викликається автоматично тими методами, виконання яких може вплинути на результати. До таких методів належать:

* DbContext.SaveChanges і DbContext.SaveChangesAsync — щоб гарантувати, що всі зміни будуть виявлені перед оновленням бази даних.
* ChangeTracker.Entries() та ChangeTracker.Entries\<TEntity\>() — для забезпечення актуальності станів сутностей і змінених властивостей.
* ChangeTracker.HasChanges(), щоб гарантувати точність результату.
* ChangeTracker.CascadeChanges() — для забезпечення коректних станів сутностей (головних/батьківських сутностей) перед виконанням каскадних змін.
* DbSet<TEntity>.Local — щоб гарантувати, що відстежуваний граф є актуальним.

Також існують випадки, коли виявлення змін відбувається лише для окремого екземпляра сутності, а не для всього графа відстежуваних сутностей. До таких випадків належать:
* Під час використання DbContext.Entry — щоб гарантувати, що стан сутності та змінені властивості є актуальними.
* Під час використання методів EntityEntry, таких як Property, Collection, Reference або Member, — щоб гарантувати, що інформація про зміни властивостей, поточні значення тощо є актуальною.
* Випадок, коли залежна сутність (дочірній об'єкт) підлягає видаленню через розірвання обов'язкового зв'язку. Це дозволяє виявити випадки, коли сутність не слід видаляти, оскільки вона була перепідпорядкована.
* Локальне виявлення змін для окремої сутності можна ініціювати явно шляхом виклику EntityEntry.DetectChanges().

    Примітка

    Локальне виявлення змін може пропустити деякі зміни, які були б виявлені під час повного сканування. Це відбувається, коли каскадні дії, спричинені невідстеженими змінами в інших сутностях, впливають на розглядувану сутність. У таких ситуаціях програмі може знадобитися примусово виконати повне сканування всіх сутностей шляхом явного виклику ChangeTracker.DetectChanges().

## Вимкнення автоматичного виявлення змін

Процес виявлення змін зазвичай не стає «вузьким місцем» для більшості застосунків. Однак виявлення змін може негативно вплинути на продуктивність деяких застосунків, які відстежують тисячі сутностей.(Точна кількість залежатиме від багатьох факторів, зокрема від кількості властивостей в об'єкті.) З цієї причини автоматичне виявлення змін можна вимкнути за допомогою властивості ChangeTracker.AutoDetectChangesEnabled. Розглянемо, наприклад, обробку сутностей зв’язку (join entities) у відношенні «багато-до-багатьох», які містять додаткові дані (payloads):

Зміни в моделі.


```cs
public class ApplicationDbContext : DbContext
{

    //...

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entityEntry in ChangeTracker.Entries<PostTag>()) // Detects changes automatically
        {
            if (entityEntry.State == EntityState.Added)
            {
                entityEntry.Entity.TaggedBy = "Alex";
            }
        }

        try
        {
            ChangeTracker.AutoDetectChangesEnabled = false;
            return await base.SaveChangesAsync(cancellationToken); // Avoid automatically detecting changes again here
        }
        finally
        {
            ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }
}
```

Як відомо з попереднього розділу, і ChangeTracker.Entries\<TEntity\>(), і DbContext.SaveChanges автоматично виявляють зміни. Однак після виклику Entries код не вносить жодних змін у стан сутностей або властивостей. (Встановлення звичайних значень властивостей для сутностей зі станом Added не призводить до зміни їхнього стану.) Тому під час виклику базового методу SaveChanges код вимикає непотрібне автоматичне виявлення змін. Код також використовує блок try/finally, щоб гарантувати відновлення налаштування за замовчуванням, навіть якщо виконання SaveChanges завершиться невдачею.

```cs
static async Task Disabling_auto_detect_changes()
{
    await CleanAndPopulateDatabase();

    using var context = new ApplicationDbContextFactory(false).CreateDbContext(null);

    Tag? tag = await context.Tags.FindAsync(1);

    var posts = context.Posts;

    foreach (var post in posts)
    {
        post.Tags.Add(tag!);
    }
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    await context.SaveChangesAsync();
}
```
```
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: <null>
  Tags: [{Id: 1}]
Post {Id: 2} Unchanged
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: <null>
  Tags: [{Id: 1}]
Post {Id: 3} Unchanged
    Id: 3 PK
    BlogId: 2 FK
    Content: 'If you are focused on squeezing out the last bits of perform...'
    Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  Tags: [{Id: 1}]
Post {Id: 4} Unchanged
    Id: 4 PK
    BlogId: 2 FK
    Content: 'Examine when database queries were executed and measure how ...'
    Title: 'Database Profiling with Visual Studio'
  Blog: <null>
  Tags: [{Id: 1}]
Tag {Id: 1} Unchanged
    Id: 1 PK
    Text: '.NET'
  Posts: []

info: 24.10.2056 13:25:37.133 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (19ms) [Parameters=[@p0='1', @p1='1', @p2='Alex' (Nullable = false) (Size = 4000), @p3='2', @p4='1', @p5='Alex' (Nullable = false) (Size = 4000), @p6='3', @p7='1', @p8='Alex' (Nullable = false) (Size = 4000), @p9='4', @p10='1', @p11='Alex' (Nullable = false) (Size = 4000)], CommandType='Text', CommandTimeout='30']
      SET IMPLICIT_TRANSACTIONS OFF;
      SET NOCOUNT ON;
      INSERT INTO [PostTag] ([PostId], [TagId], [TaggedBy])
      VALUES (@p0, @p1, @p2),
      (@p3, @p4, @p5),
      (@p6, @p7, @p8),
      (@p9, @p10, @p11);
```
Як видно до процессу запису в БД у всіх Post виправлена навігація Tags: [{Id: 1}]. Це означає що сховані сутності PostTag вже має необхідний стан Added.


    Порада

    Не варто вважати, що для забезпечення високої продуктивності ваш код обов’язково має вимикати автоматичне виявлення змін. Це потрібно лише в тих випадках, коли профілювання програми, яка відстежує велику кількість сутностей, показує, що саме процес виявлення змін стає проблемою для продуктивності.

## Виявлення змін і перетворення значень

Щоб використовувати відстеження змін за допомогою знімків (snapshot change tracking) для певного типу сутності, EF Core має бути здатний:

* Створюйте знімок значення кожної властивості в момент, коли сутність стає об'єктом відстеження.
* Порівняйте це значення з поточним значенням
* Згенерувати хеш-код для значення

EF Core виконує це автоматично для типів, які можна безпосередньо зіставити з базою даних. Однак, якщо для зіставлення властивості використовується перетворювач значень (value converter), то цей перетворювач має визначати, як саме виконувати ці дії. Це реалізується за допомогою засобу порівняння значень (value comparer) і детально описано в документації щодо засобів порівняння значень.(Value Comparers)

# Сутності з механізмом сповіщення

Для більшості застосунків рекомендується використовувати відстеження змін на основі знімків (snapshot change tracking). Однак для застосунків, які відстежують велику кількість сутностей та/або вносять до них численні зміни, може бути доцільним реалізувати сутності, що автоматично сповіщають EF Core про зміну значень своїх властивостей або навігаційних властивостей. Такі сутності називаються «сутностями з механізмом сповіщення» (notification entities).

## Реалізація сутностей із механізмом сповіщень

Сутності, що підтримують сповіщення, використовують інтерфейси INotifyPropertyChanging та INotifyPropertyChanged, які є частиною бібліотеки базових класів .NET base class library (BCL). Ці інтерфейси визначають події, які мають генеруватися до та після зміни значення властивості. Наприклад:

```cs
public class Blog : INotifyPropertyChanging, INotifyPropertyChanged
{
    public event PropertyChangingEventHandler PropertyChanging;
    public event PropertyChangedEventHandler PropertyChanged;

    private int _id;

    public int Id
    {
        get => _id;
        set
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Id)));
            _id = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Id)));
        }
    }

    private string _name;

    public string Name
    {
        get => _name;
        set
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Name)));
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public IList<Post> Posts { get; } = new ObservableCollection<Post>();
}
```

Крім того, будь-які навігаційні властивості, що представляють колекції, повинні реалізовувати інтерфейс INotifyCollectionChanged; у наведеному вище прикладі це забезпечується використанням ObservableCollection\<T\> для публікацій. До складу EF Core також входить реалізація ObservableHashSet\<T\>, яка забезпечує ефективніший пошук, але ціною втрати стабільного порядку елементів.

Більшу частину цього коду сповіщень зазвичай переносять у базовий клас, який не відображається (unmapped base class). Наприклад:

```cs
public class Blog : NotifyingEntity
{
    private int _id;

    public int Id
    {
        get => _id;
        set => SetWithNotify(value, out _id);
    }

    private string _name;

    public string Name
    {
        get => _name;
        set => SetWithNotify(value, out _name);
    }

    public IList<Post> Posts { get; } = new ObservableCollection<Post>();
}

public abstract class NotifyingEntity : INotifyPropertyChanging, INotifyPropertyChanged
{
    protected void SetWithNotify<T>(T value, out T field, [CallerMemberName] string propertyName = "")
    {
        NotifyChanging(propertyName);
        field = value;
        NotifyChanged(propertyName);
    }

    public event PropertyChangingEventHandler PropertyChanging;
    public event PropertyChangedEventHandler PropertyChanged;

    private void NotifyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void NotifyChanging(string propertyName)
        => PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
}
```

## Налаштування сутностей із підтримкою сповіщень

EF Core не має змоги перевірити, чи інтерфейси INotifyPropertyChanging або INotifyPropertyChanged реалізовані в повному обсязі, необхідному для роботи з EF Core. Зокрема, у деяких випадках використання цих інтерфейсів сповіщення генеруються лише для певних властивостей, а не для всіх властивостей (включно з навігаційними), як того вимагає EF Core. З цієї причини EF Core не підключається до цих подій автоматично.

Натомість EF Core потрібно налаштувати на використання цих сутностей, що підтримують сповіщення. Зазвичай це робиться для всіх типів сутностей шляхом виклику ModelBuilder.HasChangeTrackingStrategy. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangingAndChangedNotifications);
}
```
(Стратегію також можна налаштувати по-різному для різних типів сутностей за допомогою EntityTypeBuilder.HasChangeTrackingStrategy, проте зазвичай це недоцільно, оскільки для типів, які не є сутностями з механізмом сповіщень, все одно потрібен виклик DetectChanges.)

Для повноцінного відстеження змін через механізм сповіщень необхідно реалізувати обидва інтерфейси: INotifyPropertyChanging та INotifyPropertyChanged. Це дозволяє зберегти початкові значення безпосередньо перед зміною значення властивості, завдяки чому EF Core не потрібно створювати знімок (snapshot) під час відстеження сутності. Типи сутностей, які реалізують лише інтерфейс INotifyPropertyChanged, також можна використовувати з EF Core. У цьому випадку EF все одно створює знімок під час відстеження сутності, щоб зберігати початкові значення, але потім використовує сповіщення для негайного виявлення змін — замість того, щоб потребувати виклику методу DetectChanges.

Різні значення ChangeTrackingStrategy узагальнено в наведеній нижче таблиці.

|ChangeTrackingStrategy|Потрібні інтерфейси|Потребує DetectChanges|Snapshots original values|
|----------------------|-------------------|----------------------|--------------|
|Snapshot|None|Yes|Yes|
|ChangedNotifications|INotifyPropertyChanged|No|Yes|
|ChangingAndChangedNotifications|INotifyPropertyChanged and INotifyPropertyChanging|No|No|
|ChangingAndChangedNotificationsWithOriginalValues|INotifyPropertyChanged and INotifyPropertyChanging|No|Yes|

## Використання сутностей із механізмом сповіщень

Налаштуємо сутності з оповіщенням і контекст.

Types.cs
```cs

public class Blog : INotifyPropertyChanging, INotifyPropertyChanged
{
    public event PropertyChangingEventHandler PropertyChanging;
    public event PropertyChangedEventHandler PropertyChanged;

    private int _id;

    public int Id
    {
        get => _id;
        set
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Id)));
            _id = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Id)));
        }
    }

    private string _name;

    public string Name
    {
        get => _name;
        set
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Name)));
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public IList<Post> Posts { get; } = new ObservableCollection<Post>();
}

public class Post : INotifyPropertyChanging, INotifyPropertyChanged
{
    public event PropertyChangingEventHandler PropertyChanging;
    public event PropertyChangedEventHandler PropertyChanged;

    private int _id;

    public int Id
    {
        get => _id;
        set
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Id)));
            _id = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Id)));
        }
    }

    private string _title;

    public string Title
    {
        get => _title;
        set
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Title)));
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    private string _content;

    public string Content
    {
        get => _content;
        set
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Content)));
            _content = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
        }
    }

    private int? _blogId;

    public int? BlogId
    {
        get => _blogId;
        set
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(BlogId)));
            _blogId = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BlogId)));
        }
    }

    private Blog _blog;

    public Blog Blog
    {
        get => _blog;
        set
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Blog)));
            _blog = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Blog)));
        }
    }
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
        modelBuilder.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangingAndChangedNotifications);
    }
}
```
Відповідно змінемо процедуру заповнювання.

```cs
    protected static async Task PopulateDatabase(ApplicationDbContext context)
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
        });
        await context.SaveChangesAsync();
    }
```
Сутності-сповіщувачі (notification entities) поводяться так само, як і будь-які інші сутності, за одним винятком: внесення змін до екземплярів таких сутностей не потребує виклику ChangeTracker.DetectChanges() для виявлення цих змін. Наприклад:

Program.cs
```cs
tatic async Task Notification_entities_1()
{
    await CleanAndPopulateDatabase();

    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Blog? blog = await context.Blogs.Include(e => e.Posts).FirstAsync(e => e.Name == ".NET Blog");

    // Change a property value
    blog.Name = ".NET Blog (Updated!)";

    // Add a new entity to a navigation
    blog.Posts.Add(
        new Post
        {
            Title = "What’s next for System.Text.Json?",
            Content = ".NET 5.0 was released recently and has come with many..."
        });

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```

У випадку зі звичайними сутностями налагоджувальне подання відстежувача змін (change tracker) показувало, що ці зміни не виявлялися, доки не було викликано метод DetectChanges. Натомість, якщо використовуються сутності з підтримкою сповіщень (notification entities), налагоджувальне подання свідчить про те, що ці зміни виявляються миттєво:

```
Blog {Id: 1} Modified
    Id: 1 PK
    Name: '.NET Blog (Updated!)' Modified
  Posts: [{Id: 1}, {Id: 2}, {Id: -2147482643}]
Post {Id: -2147482643} Added
    Id: -2147482643 PK Temporary
    BlogId: 1 FK
    Content: '.NET 5.0 was released recently and has come with many...'
    Title: 'What's next for System.Text.Json?'
  Blog: {Id: 1}

    ...
```

## Проксі-об'єкти для відстеження змін

EF Core може динамічно генерувати типи проксі-об'єктів, які реалізують інтерфейси INotifyPropertyChanging та INotifyPropertyChanged. Для цього потрібно встановити NuGet-пакет Microsoft.EntityFrameworkCore.Proxies 

```cs
  <ItemGroup>
    ...
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.16" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Proxies" Version="9.0.16" />
  </ItemGroup>
```


Ввімкнути проксі-об'єкти для відстеження змін за допомогою методу UseChangeTrackingProxies. Наприклад:

```cs
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    //...

    public ApplicationDbContext CreateDbContext(string[]? args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        string connectionString = @"Server=(localdb)\mssqllocaldb;Database=MyDB;Trusted_Connection=True;ConnectRetryCount=0";

        optionsBuilder
            .UseChangeTrackingProxies();

        if (_quiet)
        {
            optionsBuilder
                .UseSqlServer(connectionString);
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
Створення динамічного проксі-об'єкта передбачає формування нового динамічного типу .NET (з використанням реалізації проксі-об'єктів із бібліотеки Castle.Core), який успадковується від типу сутності та перевизначає всі методи встановлення значень властивостей (сеттери). Тому типи сутностей, для яких створюються проксі-об'єкти, повинні допускати успадкування, а також мати властивості, які можна перевизначити. Крім того, навігаційні властивості, що представляють колекції та створені явно, повинні реалізовувати інтерфейс INotifyCollectionChanged. Наприклад:

```cs
public class Blog
{
    public virtual int Id { get; set; }
    public virtual string Name { get; set; }

    public virtual IList<Post> Posts { get; } = new ObservableCollection<Post>();
}

public class Post
{
    public virtual int Id { get; set; }
    public virtual string Title { get; set; }
    public virtual string Content { get; set; }

    public virtual int BlogId { get; set; }
    public virtual Blog Blog { get; set; }
}
```
Одним із суттєвих недоліків проксі-об'єктів для відстеження змін є те, що EF Core має завжди відстежувати саме екземпляри проксі-об'єктів, а не екземпляри базового типу сутності. Це пояснюється тим, що екземпляри базового типу сутності не генерують сповіщень, а отже, зміни, внесені до таких сутностей, залишаться непоміченими.

Під час виконання запитів до бази даних EF Core автоматично створює екземпляри-проксі, тому цей недолік, як правило, стосується лише відстеження нових екземплярів сутностей. Такі екземпляри необхідно створювати за допомогою методів розширення CreateProxy, а не звичним способом — через оператор new. Це означає, що в коді з попередніх прикладів тепер слід використовувати CreateProxy:

```cs
    static async Task PopulateDatabase(ApplicationDbContext context)
    {
        context.AddRange(
            context.CreateProxy<Blog>(
                b =>
                {
                    b.Name = ".NET Blog";
                    b.Posts.Add(
                        context.CreateProxy<Post>(
                            p =>
                            {
                                p.Title = "Announcing the Release of EF Core 5.0";
                                p.Content = "Announcing the release of EF Core 5.0, a full featured cross-platform...";
                            }));
                    b.Posts.Add(
                        context.CreateProxy<Post>(
                            p =>
                            {
                                p.Title = "Announcing F# 5";
                                p.Content = "F# 5 is the latest version of F#, the functional programming language...";
                            }));
                }),
            context.CreateProxy<Blog>(
                b =>
                {
                    b.Name = "Visual Studio Blog";
                    b.Posts.Add(
                        context.CreateProxy<Post>(
                            p =>
                            {
                                p.Title = "Disassembly improvements for optimized managed debugging";
                                p.Content =
                                    "If you are focused on squeezing out the last bits of performance for your .NET service or...";
                            }));
                    b.Posts.Add(
                        context.CreateProxy<Post>(
                            p =>
                            {
                                p.Title = "Database Profiling with Visual Studio";
                                p.Content = "Examine when database queries were executed and measure how long the take using...";
                            }));
                }));

        await context.SaveChangesAsync();
    }
```
```cs
static async Task Change_tracking_proxies_1()
{
    await CleanAndPopulateDatabase();

    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var blog = await context.Blogs.Include(e => e.Posts).FirstAsync(e => e.Name == ".NET Blog");

    // Change a property value
    blog.Name = ".NET Blog (Updated!)";

    // Add a new entity to a navigation
    blog.Posts.Add(
        context.CreateProxy<Post>(
            p =>
            {
                p.Title = "What’s next for System.Text.Json?";
                p.Content = ".NET 5.0 was released recently and has come with many...";
            }));

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
```
Blog {Id: 1} Modified
    Id: 1 PK
    Name: '.NET Blog (Updated!)' Modified
  Posts: [{Id: 1}, {Id: 2}, {Id: -2147482643}]
Post {Id: -2147482643} Added
    Id: -2147482643 PK Temporary
    BlogId: 1 FK
    Content: '.NET 5.0 was released recently and has come with many...'
    Title: 'What's next for System.Text.Json?'
  Blog: {Id: 1}

    ...
```


## Події відстеження змін

EF Core генерує подію ChangeTracker.Tracked, коли сутність починає відстежуватися вперше. Подальші зміни стану сутності призводять до виникнення подій ChangeTracker.StateChanged. Докладнішу інформацію див. у розділі «Події .NET в EF Core».

    Примітка

    Подія StateChanged не генерується, коли сутність починає відстежуватися вперше, хоча її стан при цьому змінюється з Detached на один із інших станів. Переконайтеся, що ви відстежуєте як подію StateChanged, так і подію Tracked, щоб отримувати всі відповідні сповіщення.