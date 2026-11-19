# Microsoft.Extensions.Logging

Microsoft.Extensions.Logging — це розширюваний механізм журналювання, що підтримує підключаємі провайдери для багатьох поширених систем журналювання. Як плагіни, що надаються Microsoft (наприклад, Microsoft.Extensions.Logging.Console), так і плагіни від сторонніх розробників (наприклад, Serilog.Extensions.Logging) доступні у вигляді пакетів NuGet.

Entity Framework Core (EF Core) повністю інтегрується з Microsoft.Extensions.Logging. Однак розгляньте можливість використання простого логування — це простіший спосіб ведення логів, особливо для застосунків, у яких не використовується впровадження залежностей.

## Застосунки ASP.NET Core

У програмах ASP.NET Core за замовчуванням використовується Microsoft.Extensions.Logging. Виклик AddDbContext або AddDbContextPool призводить до того, що EF Core автоматично використовує налаштування журналювання, задані за допомогою стандартного механізму ASP.NET.

## Інші типи застосунків

Інші типи застосунків можуть використовувати GenericHost, щоб застосовувати ті самі патерни впровадження залежностей, що й в ASP.NET Core. Тоді можна використовувати AddDbContext або AddDbContextPool — так само, як у програмах ASP.NET Core.

Microsoft.Extensions.Logging можна використовувати й у програмах, які не застосовують впровадження залежностей, хоча налаштувати просте логування може бути легше.

Microsoft.Extensions.Logging вимагає створення LoggerFactory. Цю фабрику слід десь зберігати як статичний або глобальний екземпляр і використовувати щоразу, коли створюється DbContext. Наприклад, часто фабрику логерів зберігають як статичну властивість у DbContext.

```cs
public static readonly ILoggerFactory MyLoggerFactory
    = LoggerFactory.Create(builder => { builder.AddConsole(); });
```

Цей екземпляр-синглтон (або глобальний екземпляр) слід зареєструвати в EF Core за допомогою DbContextOptionsBuilder. Наприклад:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder
        .UseLoggerFactory(MyLoggerFactory)
        .UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=EFLogging;Trusted_Connection=True;ConnectRetryCount=0");
```

## Отримання детальних повідомлень

    Порада

    Метод OnConfiguring викликається навіть тоді, коли використовується AddDbContext або коли в конструктор DbContext передається екземпляр DbContextOptions. Завдяки цьому він є ідеальним місцем для застосування конфігурації контексту, незалежно від того, як саме створюється DbContext.

### Конфіденційні дані

За замовчуванням EF Core не включає значення даних у повідомлення про винятки. Це зумовлено тим, що такі дані можуть бути конфіденційними, і в разі необробленого винятку вони можуть стати доступними під час експлуатації програми в робочому середовищі.

Однак знання значень даних, особливо для ключів, може бути дуже корисним під час налагодження. В EF Core це можна ввімкнути, викликавши метод EnableSensitiveDataLogging(). Наприклад:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.EnableSensitiveDataLogging();
```

### Деталізовані винятки під час виконання запитів

З міркувань продуктивності EF Core не огортає кожен виклик для зчитування значення з постачальника бази даних у блок try-catch. Однак іноді це призводить до винятків, які важко діагностувати — особливо коли база даних повертає NULL у випадках, коли модель цього не допускає.

Увімкнення параметра EnableDetailedErrors призведе до того, що EF додасть блоки try-catch, завдяки чому надаватиме детальнішу інформацію про помилки. Наприклад:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.EnableDetailedErrors();
```

## Налаштування для конкретних повідомлень

API ConfigureWarnings в EF Core дозволяє програмам змінювати поведінку системи у відповідь на виникнення певної події.Це можна використовувати для того, щоб:

* Змінити рівень журналювання, з яким фіксується подія
* Повністю пропустити журналювання події
* Викликати виняток, коли відбувається подія

### Зміна рівня журналювання для події

Іноді може бути корисно змінити попередньо визначений рівень журналювання для певної події. Наприклад, це можна використати, щоб підвищити рівень двох додаткових подій з LogLevel.Debug до LogLevel.Information:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder
        .ConfigureWarnings(
            b => b.Log(
                (RelationalEventId.ConnectionOpened, LogLevel.Information),
                (RelationalEventId.ConnectionClosed, LogLevel.Information)));
```

### Заборона реєстрації події

Аналогічним чином можна заборонити реєстрацію окремої події. Це особливо корисно, якщо потрібно ігнорувати попередження, яке вже було розглянуто та зрозуміле. Наприклад:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder
        .ConfigureWarnings(b => b.Ignore(CoreEventId.DetachedLazyLoadingWarning));
```

### Викидання винятку для події

Нарешті, EF Core можна налаштувати так, щоб він викидав виняток у разі виникнення певної події. Це особливо корисно для перетворення попередження на помилку. (Власне, саме для цього спочатку й призначався метод ConfigureWarnings — звідси й назва.) Наприклад:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder
        .ConfigureWarnings(b => b.Throw(RelationalEventId.QueryPossibleUnintendedUseOfEqualsWarning));
```

## Фільтрація та інше налаштування

Див. розділ «Ведення журналів у .NET» (Logging in .NET), щоб дізнатися про фільтрацію журналів та інші параметри конфігурації.

Події журналювання в EF Core визначені в одному з таких місць:

* Події журналювання в EF Core визначені в одному з таких місць:

* CoreEventId — для подій, спільних для всіх постачальників баз даних в EF Core;
* RelationalEventId — для подій, спільних для всіх постачальників реляційних баз даних;
* Аналогічний клас — для подій, характерних для конкретного постачальника бази даних (наприклад, SqlServerEventId для постачальника SQL Server).

Ці визначення містять ідентифікатори подій, рівні журналювання та категорії для кожної події — відповідно до того, як це реалізовано в Microsoft.Extensions.Logging.