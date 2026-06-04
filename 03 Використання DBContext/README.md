# Використання DBContext

## Час життя DbContext

Час життя DbContext починається з моменту створення екземпляра та закінчується з моменту його видалення. Екземпляр DbContext розроблений для використання в одній одиниці роботи. Це означає, що час життя екземпляра DbContext зазвичай дуже короткий.

Типова одиниця роботи під час використання Entity Framework Core (EF Core) включає:

1. Створення екземпляра DbContext
2. Відстеження екземплярів сутностей контекстом. 
    Сутності відстежуються шляхом:
    1. Повернення із запиту 
    2. Додавання або приєднання до контексту
3. Зміни вносяться до відстежуваних сутностей за потреби для реалізації бізнес-правила.
4. Викликається SaveChanges або SaveChangesAsync. EF Core виявляє внесені зміни та записує їх у базу даних.
5. Екземпляр DbContext утилізується

Важливо утилізувати DbContext після використання. Це гарантує:

1. Некеровані ресурси звільняються.
2. Події або інші перехоплювачі не реєструються. Скасування реєстрації запобігає витокам пам'яті, коли на екземпляр залишається посилання.

DbContext не є потокобезпечним. Не розподіляйте контексти між потоками. Обов'язково зачекайте на всі асинхронні виклики, перш ніж продовжувати використовувати екземпляр контексту.

## DbContext у впровадженні залежностей для ASP.NET Core

У багатьох веб-застосунках кожен HTTP-запит відповідає одній одиниці роботи. Через це прив’язка часу життя контексту до часу життя запиту є гарним налаштуванням за замовчуванням для веб-застосунків. 

Програми ASP.NET Core налаштовуються за допомогою впровадження залежностей. EF Core можна додати до цієї конфігурації за допомогою AddDbContext у Program.cs. Наприклад:

```cs
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string"
        + "'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
```
У попередньому коді реєструється ApplicationDbContext, підклас DbContext, як служба з обмеженою областю видимості у постачальнику служби додатків ASP.NET Core. Постачальник послуг також відомий як контейнер впровадження залежностей. Контекст налаштовано на використання постачальника бази даних SQL Server та зчитує рядок підключення з конфігурації ASP.NET Core. 

Клас ApplicationDbContext повинен надавати публічний конструктор з параметром DbContextOptions\<ApplicationDbContext\>. Ось як конфігурація контексту з AddDbContext передається до базового класу DbContext.

```cs
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}
```
ApplicationDbContext можна використовувати в контролерах ASP.NET Core або інших сервісах через ін'єкцію конструктора:

```cs
public class MyController
{
    private readonly ApplicationDbContext _context;

    public MyController(ApplicationDbContext context)
    {
        _context = context;
    }
}
```
Кінцевим результатом є екземпляр ApplicationDbContext, створений для кожного запиту та переданий контролеру для виконання одиниці роботи перед тим, як його буде утилізовано після завершення запиту.

## Базова ініціалізація DbContext за допомогою 'new'

Екземпляри DbContext можна створювати за допомогою new у C#. Конфігурацію можна виконати шляхом перевизначення методу OnConfiguring або передачі опцій конструктору.

```cs
public class ApplicationDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(
            @"Server=(localdb)\mssqllocaldb;Database=Test;ConnectRetryCount=0");
    }
}
```
Цей шаблон також спрощує передачу конфігурації, такої як рядок підключення, через конструктор DbContext.

```cs
public class ApplicationDbContext : DbContext
{
    private readonly string _connectionString;

    public ApplicationDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(_connectionString);
    }
}
```

Або ж DbContextOptionsBuilder можна використовувати для створення об'єкта DbContextOptions, який потім передається конструктору DbContext. Це дозволяє також явно створювати DbContext, налаштований для впровадження залежностей. Наприклад, під час використання ApplicationDbContext, визначеного для веб-застосунків ASP.NET Core вище:

```cs
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}
```
DbContextOptions можна створити, а конструктор викликати явно:

```cs
var contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=Test;ConnectRetryCount=0")
    .Options;

using var context = new ApplicationDbContext(contextOptions);
```

## Використання фабрики DbContext

Деякі типи програм (наприклад, ASP.NET Core Blazor) використовують впровадження залежностей, але не створюють область дії сервісу, яка відповідає бажаному часу життя DbContext. Навіть там, де таке узгодження існує, застосунку може знадобитися виконати кілька одиниць роботи в межах цієї області дії. Наприклад, кілька одиниць роботи в одному HTTP-запиті.

У цих випадках AddDbContextFactory можна використовувати для реєстрації фабрики для створення екземплярів DbContext.

```cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddDbContextFactory<ApplicationDbContext>(
        options => options.UseSqlServer(
            @"Server=(localdb)\mssqllocaldb;Database=Test;ConnectRetryCount=0"));
}
```
Клас ApplicationDbContext повинен надавати публічний конструктор з параметром DbContextOptions\<ApplicationDbContext\>. Це той самий шаблон, що використовується в традиційному розділі ASP.NET Core вище.

```cs
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}
```

Фабрику DbContextFactory потім можна використовувати в інших сервісах за допомогою ін'єкції конструктора.

```cs
private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

public MyController(IDbContextFactory<ApplicationDbContext> contextFactory)
{
    _contextFactory = contextFactory;
}
```
Введену фабрику потім можна використовувати для створення екземплярів DbContext у коді сервісу.

```cs
public async Task DoSomething()
{
    using (var context = _contextFactory.CreateDbContext())
    {
        // ...
    }
}
```
Зверніть увагу, що екземпляри DbContext, створені таким чином, не керуються постачальником послуг застосунку, і тому їх має утилізувати застосунок.


## DbContextOptions

Відправною точкою для всіх налаштувань DbContext є DbContextOptionsBuilder. Існує три способи отримати цього створювача:

1. У AddDbContext та пов'язаних методах
2. У OnConfiguring
3. Створено явно з new

Приклади кожного з них наведено в попередніх розділах. Одну й ту саму конфігурацію можна застосовувати незалежно від того, звідки береться створювач. Крім того, OnConfiguring завжди викликається незалежно від того, як побудовано контекст. Це означає, що OnConfiguring можна використовувати для виконання додаткової конфігурації навіть під час використання AddDbContext.

## Налаштування постачальника бази даних

Кожен екземпляр DbContext має бути налаштований на використання одного і лише одного постачальника бази даних. (Різні екземпляри підтипу DbContext можна використовувати з різними постачальниками баз даних, але один екземпляр повинен використовувати лише один.). Постачальник бази даних налаштовується за допомогою спеціального виклику Use*. Наприклад, щоб використовувати постачальника бази даних SQL Server:

```cs
public class ApplicationDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(
            @"Server=(localdb)\mssqllocaldb;Database=Test;ConnectRetryCount=0");
    }
}
```
Ці методи Use* є методами розширення, реалізованими постачальником бази даних. Це означає, що пакет NuGet постачальника бази даних має бути встановлений перед використанням методу розширення.

Приклади поширених постачальників баз даних: SQL Server or Azure SQL(.UseSqlServer(connectionString)), Azure Cosmos DB(.UseCosmos(connectionString, databaseName)), PostgreSQL*(.UseNpgsql(connectionString)), MySQL/MariaDB*(.UseMySql(connectionString)), Oracle*(.UseOracle(connectionString)), EF Core in-memory database, SQLite(.UseSqlite(connectionString))

Додаткове налаштування, специфічне для постачальника бази даних, виконується в додатковому створювачі, специфічному для постачальника. Наприклад, використання EnableRetryOnFailure для налаштування повторних спроб для забезпечення стійкості підключення під час підключення до Azure SQL:

```cs
public class ApplicationDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlServer(
                @"Server=(localdb)\mssqllocaldb;Database=Test",
                providerOptions => { providerOptions.EnableRetryOnFailure(); });
    }
}
```

## Інша конфігурація DbContext

Інші конфігурації DbContext можна об'єднати до або після (не має значення який саме) виклику Use*. Наприклад, щоб увімкнути ведення журналу конфіденційних даних:

```cs
public class ApplicationDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .EnableSensitiveDataLogging()
            .UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=Test;ConnectRetryCount=0");
    }
}
```


## Конфігурація DbContext під час розробки

Інструменти часу розробки EF Core, такі як ті, що використовуються для міграції EF Core, повинні мати можливість виявляти та створювати робочий екземпляр типу DbContext, щоб збирати відомості про типи сутностей програми та про те, як вони зіставляються зі схемою бази даних. Цей процес може бути автоматичним, якщо інструмент може легко створити DbContext таким чином, щоб він був налаштований аналогічно тому, як він був би налаштований під час виконання. Хоча будь-який шаблон, який надає необхідну конфігураційну інформацію DbContext, може працювати під час виконання, інструменти, які потребують використання DbContext під час проектування, можуть працювати лише з обмеженою кількістю шаблонів.


## Використання конструктора без параметрів

Якщо DbContext неможливо отримати від постачальника послуг застосунку, інструменти шукають похідний тип DbContext усередині проекту. Потім вони намагаються створити екземпляр за допомогою конструктора без параметрів. Це може бути конструктор за замовчуванням, якщо DbContext налаштовано за допомогою методу OnConfiguring.

## З фабрики часу проектування

Ви також можете вказати інструментам, як створити ваш DbContext, реалізувавши інтерфейс Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory\<TContext\>: Якщо клас, що реалізує цей інтерфейс, знаходиться або в тому ж проекті, що й похідний DbContext, або в проекті запуску програми, інструменти обходять інші способи створення DbContext і використовують замість цього фабрику часу проектування.

```cs
public class BloggingContextFactory : IDesignTimeDbContextFactory<BloggingContext>
{
    public BloggingContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BloggingContext>();
        optionsBuilder.UseSqlite("Data Source=blog.db");

        return new BloggingContext(optionsBuilder.Options);
    }
}
```
Фабрика часу проектування може бути особливо корисною, якщо вам потрібно налаштувати DbContext інакше для часу проектування та під час виконання, якщо конструктор DbContext приймає додаткові параметри, які не зареєстровані в DI, якщо ви взагалі не використовуєте DI або якщо з якоїсь причини ви не бажаєте мати метод CreateHostBuilder у класі Main вашої програми ASP.NET Core.

## Уникнення проблем із потоками DbContext

Entity Framework Core не підтримує виконання кількох паралельних операцій в одному екземплярі DbContext. Це включає як паралельне виконання асинхронних запитів, так і будь-яке явне одночасне використання з кількох потоків. Тому завжди негайно очікуйте асинхронних викликів або використовуйте окремі екземпляри DbContext для операцій, що виконуються паралельно.