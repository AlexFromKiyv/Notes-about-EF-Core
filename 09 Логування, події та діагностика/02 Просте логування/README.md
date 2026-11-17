# Просте логування

Функцію простого логування в Entity Framework Core (EF Core) можна використовувати для зручного отримання логів під час розробки та налагодження програм. Цей спосіб ведення логів потребує мінімального налаштування й не вимагає додаткових пакетів NuGet.

EF Core також інтегрується з Microsoft.Extensions.Logging; це потребує додаткового налаштування, але часто краще підходить для ведення логів у продуктових застосунках.

## Початкові Model, похідний клас від DBContext та Helpers

```cs
public class Blog
{
    public int Id { get; set; }
    public string Name { get; set; }
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
```
```cs
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
            },
            new Blog
            {
                Name = "Visual Studio Blog",
            }
        };

        context.AddRange(blogs);
        await context.SaveChangesAsync();
    }
}
```
Program.cs
```cs
static async Task DoAsync(Func<Task> doIt)
{
    await CleanAndPopulateDatabase(); //to run only one time

    await doIt();
}
await DoAsync(ReadeBlog);


static async Task ReadeBlog()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Blog? blog = context.Blogs.Find(1);

    Console.WriteLine($"{blog?.Id}\t{blog?.Name}");
}
```

## Конфігурація

До логів EF Core можна отримати доступ із будь-якого типу застосунку, скориставшись методом LogTo під час налаштування екземпляра DbContext.

Зазвичай це налаштування виконується шляхом перевизначення методу DbContext.OnConfiguring або під час створення екземпляра DbContextOptions, який потім передається в конструктор DbContext.

```cs
public class ApplicationDbContext : DbContext
{
    //...

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.LogTo(Console.WriteLine);
}
```

```cs
static async Task DoAsync(Func<Task> doIt)
{
    //await CleanAndPopulateDatabase();  //to run only one time

    await doIt();
}
await DoAsync(ReadeBlog);
```
```
dbug: 16.11.2026 13:01:56.774 CoreEventId.ContextInitialized[10403] (Microsoft.EntityFrameworkCore.Infrastructure)
      Entity Framework Core 9.0.16 initialized 'ApplicationDbContext' using provider 'Microsoft.EntityFrameworkCore.SqlServer:9.0.16' with options: EngineType=SqlServer
dbug: 16.11.2026 13:01:56.961 CoreEventId.QueryCompilationStarting[10111] (Microsoft.EntityFrameworkCore.Query)
      Compiling query expression:
      'DbSet<Blog>()
          .FirstOrDefault(e => EF.Property<int>(e, "Id") == __p_0)'

...

```

Крім того, метод LogTo можна викликати в межах AddDbContext. 

    Порада

    Метод OnConfiguring викликається навіть тоді, коли використовується AddDbContext або коли в конструктор DbContext передається екземпляр DbContextOptions. Завдяки цьому він є ідеальним місцем для застосування конфігурації контексту, незалежно від того, як саме створюється DbContext.

## Спрямування логів

### Виведення логів у консоль

LogTo потребує делегата Action<T>, який приймає рядок. EF Core викликатиме цей делегат, передаючи йому рядок для кожного згенерованого повідомлення журналу. Далі вже делегат вирішує, що саме зробити з отриманим повідомленням.

Для цього делегата часто використовується метод Console.WriteLine, як показано вище. У результаті кожне повідомлення журналу записується в консоль.

```cs
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.LogTo(Console.WriteLine);
```
```
dbug: 13.11.2056 18:45:14.174 CoreEventId.ContextInitialized[10403] (Microsoft.EntityFrameworkCore.Infrastructure)
      Entity Framework Core 9.0.16 initialized 'ApplicationDbContext' using provider 'Microsoft.EntityFrameworkCore.SqlServer:9.0.16' with options: EngineType=SqlServer
dbug: 13.11.2056 18:45:14.226 RelationalEventId.ConnectionCreating[20005] (Microsoft.EntityFrameworkCore.Database.Connection)
      Creating DbConnection.
dbug: 13.11.2056 18:45:14.314 RelationalEventId.ConnectionCreated[20006] (Microsoft.EntityFrameworkCore.Database.Connection)
      Created DbConnection. (86ms).
dbug: 13.11.2056 18:45:14.322 RelationalEventId.ConnectionOpening[20000] (Microsoft.EntityFrameworkCore.Database.Connection)
      Opening connection to database 'MyDB' on server '(localdb)\mssqllocaldb'.

...

```
### Виведення даних у вікно налагодження

Debug.WriteLine можна використовувати для виведення даних у вікно налагодження (Debug) у Visual Studio або інших середовищах розробки (IDE). У цьому випадку необхідно використовувати лямбда-синтаксис, оскільки клас `Debug` виключається з релізних збірок. Наприклад:

```cs
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.LogTo(message => Debug.WriteLine(message));
```

### Запис у файл

Для запису у файл необхідно створити об'єкт StreamWriter (або аналогічний) для цього файлу. Метод WriteLine можна використовувати так само, як і в інших наведених вище прикладах. Не забудьте забезпечити коректне закриття файлу: для цього слід звільнити ресурс записувача (writer) під час завершення контексту. Наприклад:

```cs
public class ApplicationDbContext : DbContext
{
    private readonly StreamWriter _logStream = new StreamWriter("mylog.txt", append: true);
    public DbSet<Blog> Blogs { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.LogTo(_logStream.WriteLine);

    public override void Dispose()
    {
        base.Dispose();
        _logStream.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _logStream.DisposeAsync();
    }
}
```
При такому визначені потрібно буде використовувати лише один контекст.

Program.cs

```cs
static async Task DoAsync(Func<Task> doIt)
{
    //await CleanAndPopulateDatabase();

    await doIt();
}
await DoAsync(ReadeBlog);


static async Task ReadeBlog()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Blog? blog = context.Blogs.Find(1);

    Console.WriteLine($"{blog?.Id}\t{blog?.Name}");
}
```
Файл запишеться в bin\Debug\ ...

    Порада

    Для ведення логів у файли в продуктивних застосунках розгляньте можливість використання Microsoft.Extensions.Logging.

## Отримання детальних повідомлень

### Конфіденційні дані

За замовчуванням EF Core не включає значення даних у повідомлення про винятки. Це пояснюється тим, що такі дані можуть бути конфіденційними й можуть бути розкриті під час експлуатації в робочому середовищі, якщо виняток не буде належним чином оброблений.

```cs
public class ApplicationDbContext : DbContext
{
    //...

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.LogTo(Console.WriteLine);
}
```
```cs
//...
await DoAsync(ReadeBlog);

static async Task ReadeBlog()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Blog? blog = context.Blogs.Find(1);

    Console.WriteLine($"{blog?.Id}\t{blog?.Name}");
}
```
```
    ...
info: 16.11.2026 13:24:11.578 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (55ms) [Parameters=[@__p_0='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
      SELECT TOP(1) [b].[Id], [b].[Name]
      FROM [Blogs] AS [b]
      WHERE [b].[Id] = @__p_0
dbug: 16.11.2026 13:24:11.628 CoreEventId.StartedTracking[10806] (Microsoft.EntityFrameworkCore.ChangeTracking)
      Context 'ApplicationDbContext' started tracking 'Blog' entity. Consider using 'DbContextOptionsBuilder.EnableSensitiveDataLogging' to see key values.
    ...
```

Однак знання значень даних, особливо для ключів, може бути дуже корисним під час налагодження. В EF Core це можна ввімкнути, викликавши EnableSensitiveDataLogging().

```cs
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
        .LogTo(Console.WriteLine)
        .EnableSensitiveDataLogging();
```
```
info: 16.11.2026 13:23:19.445 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (87ms) [Parameters=[@__p_0='1'], CommandType='Text', CommandTimeout='30']
      SELECT TOP(1) [b].[Id], [b].[Name]
      FROM [Blogs] AS [b]
      WHERE [b].[Id] = @__p_0
dbug: 16.11.2026 13:23:19.509 CoreEventId.StartedTracking[10806] (Microsoft.EntityFrameworkCore.ChangeTracking)
      Context 'ApplicationDbContext' started tracking 'Blog' entity with key '{Id: 1}'.
```

### Деталізовані винятки під час виконання запитів.

З міркувань продуктивності EF Core не огортає кожен виклик для зчитування значення з постачальника бази даних у блок `try-catch`. Однак іноді це призводить до виникнення винятків, які важко діагностувати — особливо в тих випадках, коли база даних повертає значення NULL, хоча модель цього не допускає.

Увімкнення параметра EnableDetailedErrors призведе до того, що EF додасть блоки try-catch, завдяки чому надаватиме детальнішу інформацію про помилки. Наприклад:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder
        .LogTo(Console.WriteLine)
        .EnableDetailedErrors();
```

## Фільтрування

### Рівні логування

Кожному повідомленню журналу в EF Core присвоюється рівень, визначений перерахуванням LogLevel. У LogTo можна задати вищий мінімальний рівень, щоб відфільтрувати певні повідомлення. Наприклад, передача Information призводить до створення мінімального набору журналів, що обмежується даними про доступ до бази даних і деякими службовими повідомленнями.

```cs
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
        .LogTo(Console.WriteLine,LogLevel.Information);
```
```
info: 14.11.2026 15:07:46.052 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (131ms) [Parameters=[@__p_0='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
      SELECT TOP(1) [b].[Id], [b].[Name]
      FROM [Blogs] AS [b]
      WHERE [b].[Id] = @__p_0
1       .NET Blog
```

### Конкретні повідомлення

Кожному повідомленню в журналі присвоюється ідентифікатор події (EventId). До цих ідентифікаторів можна отримати доступ через клас CoreEventId або — для повідомлень, специфічних для реляційних баз даних, — через клас RelationalEventId. Постачальник бази даних також може мати власні ідентифікатори (ID) у подібному класі. Наприклад, SqlServerEventId для постачальника SQL Server.

LogTo можна налаштувати так, щоб реєструвалися лише повідомлення, пов’язані з одним або кількома ідентифікаторами подій. Наприклад, щоб реєструвати лише повідомлення, що стосуються ініціалізації або звільнення контексту:

```cs
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
        .LogTo(Console.WriteLine, new[] { CoreEventId.ContextDisposed, CoreEventId.ContextInitialized });
```
```
dbug: 14.11.2026 15:14:14.951 CoreEventId.ContextInitialized[10403] (Microsoft.EntityFrameworkCore.Infrastructure)
      Entity Framework Core 9.0.16 initialized 'ApplicationDbContext' using provider 'Microsoft.EntityFrameworkCore.SqlServer:9.0.16' with options: EngineType=SqlServer
1       .NET Blog
dbug: 14.11.2026 15:14:19.688 CoreEventId.ContextDisposed[10407] (Microsoft.EntityFrameworkCore.Infrastructure)
      'ApplicationDbContext' disposed.
```

### Категорії повідомлень

Кожному повідомленню призначається іменована ієрархічна категорія реєстратора. Ці категорії такі:

|Категорія|Повідомлення|
|---------|------------|
|Microsoft.EntityFrameworkCore|Усі повідомлення EF Core|
|Microsoft.EntityFrameworkCore.Database|Усі взаємодії з базою даних|
|Microsoft.EntityFrameworkCore.Database.Connection|Використання підключення до бази даних|
|Microsoft.EntityFrameworkCore.Database.Command|Використання команди бази даних|
|Microsoft.EntityFrameworkCore.Database.Transaction|Використання транзакцій бази даних|
|Microsoft.EntityFrameworkCore.Update|Збереження сутностей, за винятком взаємодій з базою даних|
|Microsoft.EntityFrameworkCore.Model|Усі взаємодії моделі та метаданих|
|Microsoft.EntityFrameworkCore.Model.Validation|Валідація моделі|
|Microsoft.EntityFrameworkCore.Query|Запити, за винятком взаємодій з базою даних|
|Microsoft.EntityFrameworkCore.Infrastructure|Загальні події, такі як створення контексту|
|Microsoft.EntityFrameworkCore.Scaffolding|Зворотне проектування бази даних|
|Microsoft.EntityFrameworkCore.Migrations|Міграції|
|Microsoft.EntityFrameworkCore.ChangeTracking|Відстеження змін у взаємодіях|

LogTo можна налаштувати так, щоб він реєстрував повідомлення лише з однієї або кількох категорій. Наприклад, щоб реєструвати лише взаємодії з базою даних:

```cs
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
            .LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Name });
```
Зверніть увагу, що клас DbLoggerCategory надає ієрархічний API для пошуку категорії та дозволяє уникнути необхідності жорстко прописувати рядки в коді.

Оскільки категорії є ієрархічними, цей приклад із використанням категорії «Database» охоплюватиме всі повідомлення для підкатегорій «Database.Connection», «Database.Command» та «Database.Transaction».

### Користувацькі фільтри

LogTo дозволяє використовувати власний фільтр у випадках, коли жодного з наведених вище варіантів фільтрації недостатньо. Наприклад, щоб реєструвати будь-які повідомлення рівня Information або вище, а також повідомлення про відкриття та закриття з’єднання:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder
        .LogTo(
            Console.WriteLine,
            (eventId, logLevel) => logLevel >= LogLevel.Information
                                   || eventId == RelationalEventId.ConnectionOpened
                                   || eventId == RelationalEventId.ConnectionClosed);
```
```
dbug: 15.11.2056 11:39:38.507 RelationalEventId.ConnectionOpened[20001] (Microsoft.EntityFrameworkCore.Database.Connection)
      Opened connection to database 'MyDB' on server '(localdb)\mssqllocaldb'.
info: 15.11.2056 11:39:38.711 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (126ms) [Parameters=[@__p_0='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
      SELECT TOP(1) [b].[Id], [b].[Name]
      FROM [Blogs] AS [b]
      WHERE [b].[Id] = @__p_0
dbug: 15.11.2056 11:39:38.810 RelationalEventId.ConnectionClosed[20003] (Microsoft.EntityFrameworkCore.Database.Connection)
      Closed connection to database 'MyDB' on server '(localdb)\mssqllocaldb' (6ms).
1       .NET Blog
```


    Підказка

    Фільтрування за допомогою користувацьких фільтрів або використання будь-яких інших наведених тут параметрів ефективніше, ніж фільтрування в делегаті LogTo. Це пояснюється тим, що якщо фільтр визначає, що повідомлення не слід реєструвати, то повідомлення журналу навіть не створюється.

## Налаштування для конкретних повідомлень

Налаштування для конкретних повідомлень
API ConfigureWarnings в EF Core дозволяє програмам змінювати поведінку системи у разі виникнення певної події. Це можна використовувати для того, щоб:

* Змінити рівень логування, на якому реєструється подія
* Пропустити реєстрацію події повністю
* Викинути виняток, коли відбувається подія

### Зміна рівня логування для події.

У попередньому прикладі використовувався спеціальний фільтр для реєстрації кожного повідомлення з рівнем LogLevel.Information, а також двох подій, визначених для рівня LogLevel.Debug. Цього ж результату можна досягти, змінивши рівень реєстрації для цих двох подій з Debug на Information:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder
        .ConfigureWarnings(
            b => b.Log(
                (RelationalEventId.ConnectionOpened, LogLevel.Information),
                (RelationalEventId.ConnectionClosed, LogLevel.Information)))
        .LogTo(Console.WriteLine, LogLevel.Information);
```

### Заборона реєстрації події

Аналогічним чином можна виключити окрему подію з реєстрації в журналі. Це особливо корисно для ігнорування попередження, яке було переглянуто та зрозуміле. Наприклад:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder
        .ConfigureWarnings(b => b.Ignore(CoreEventId.DetachedLazyLoadingWarning))
        .LogTo(Console.WriteLine);
```

### Викидання винятку для події

Нарешті, EF Core можна налаштувати так, щоб він генерував виняток у відповідь на певну подію. Це особливо корисно для перетворення попередження на помилку. (Дійсно, це було початковим призначенням методу ConfigureWarnings, звідси й назва.) Наприклад:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder
        .ConfigureWarnings(b => b.Throw(RelationalEventId.MultipleCollectionIncludeWarning))
        .LogTo(Console.WriteLine);
```

## Зміст та форматування повідомлення

Вміст LogTo за замовчуванням відформатовано в кілька рядків. Перший рядок містить метадані повідомлення:
* LogLevel як чотирисимвольний префікс
* Локальна позначка часу, відформатована для поточної культури
* Ідентифікатор події (EventId) у формі, яку можна скопіювати/вставити для отримання члена з CoreEventId або одного з інших класів EventId, плюс необроблене значення ідентифікатора.
* Категорія події, як описано вище.

```
info: 10/6/2020 10:52:45.581 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (0ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      CREATE TABLE "Blogs" (
          "Id" INTEGER NOT NULL CONSTRAINT "PK_Blogs" PRIMARY KEY AUTOINCREMENT,
          "Name" INTEGER NOT NULL
      );
dbug: 10/6/2020 10:52:45.582 RelationalEventId.TransactionCommitting[20210] (Microsoft.EntityFrameworkCore.Database.Transaction)
      Committing transaction.
dbug: 10/6/2020 10:52:45.585 RelationalEventId.TransactionCommitted[20202] (Microsoft.EntityFrameworkCore.Database.Transaction)
      Committed transaction.
```
Цей вміст можна налаштувати, передаючи значення з DbContextLoggerOptions, як показано в наступних розділах.

    Порада

    Розгляньте можливість використання Microsoft.Extensions.Logging для кращого контролю над форматуванням журналу.

### Використання часу UTC

За замовчуванням позначки часу призначені для локального використання під час налагодження. Використовуйте DbContextLoggerOptions.DefaultWithUtcTime, щоб застосовувати часові мітки у форматі UTC, які не залежать від регіональних налаштувань (культури), залишаючи при цьому все інше без змін. Наприклад:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.LogTo(
        Console.WriteLine,
        LogLevel.Debug,
        DbContextLoggerOptions.DefaultWithUtcTime);
```
Цей приклад призводить до такого форматування журналу:

```
dbug: 2026-11-16T10:30:07.8360437Z CoreEventId.ContextInitialized[10403] (Microsoft.EntityFrameworkCore.Infrastructure)
      Entity Framework Core 9.0.16 initialized 'ApplicationDbContext' using provider 'Microsoft.EntityFrameworkCore.SqlServer:9.0.16' with options: EngineType=SqlServer
dbug: 2026-11-16T10:30:08.5022732Z RelationalEventId.ConnectionCreating[20005] (Microsoft.EntityFrameworkCore.Database.Connection)
      Creating DbConnection.
```
### Однорядкове логування

Іноді корисно отримувати рівно один рядок для кожного повідомлення журналу. Це можна ввімкнути за допомогою DbContextLoggerOptions.SingleLine. Наприклад:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.LogTo(
        Console.WriteLine,
        LogLevel.Debug,
        DbContextLoggerOptions.DefaultWithLocalTime | DbContextLoggerOptions.SingleLine);
```

### Інші варіанти вмісту

Інші прапорці в параметрах логера DbContext можна використовувати для зменшення обсягу метаданих, що включаються до журналу. Це може бути корисним у поєднанні з однорядковим веденням журналу. Наприклад:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.LogTo(
        Console.WriteLine,
        LogLevel.Debug,
        DbContextLoggerOptions.UtcTime | DbContextLoggerOptions.SingleLine);
```