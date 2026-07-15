# API створення та видалення

Методи EnsureCreatedAsync та EnsureDeletedAsync пропонують спрощену альтернативу міграціям для керування схемою бази даних. Ці методи корисні в сценаріях, коли дані тимчасові та можуть бути видалені під час зміни схеми. Наприклад, під час створення прототипів, у тестах або для локальних кешів.

Деякі провайдери (особливо нереляційні) не підтримують міграції. Для цих провайдерів EnsureCreatedAsync часто є найпростішим способом ініціалізації схеми бази даних.

EnsureCreatedAsync та Migrations погано працюють разом. Якщо ви використовуєте Migrations, не використовуйте EnsureCreatedAsync для ініціалізації схеми.

Перехід від EnsureCreatedAsync до Migrations не є безпроблемним. Найпростіший спосіб зробити це – видалити базу даних та повторно створити її за допомогою Migrations. Якщо ви плануєте використовувати міграції в майбутньому, краще просто почати з Migrations, а не використовувати EnsureCreatedAsync.

## EnsureCreatedAsync

EnsureCreatedAsync створить базу даних, якщо вона не існує, та ініціалізує схему бази даних. Якщо існують будь-які таблиці (включно з таблицями для іншого класу DbContext), схема не буде ініціалізована.

```cs
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);
    Console.WriteLine(await context.Database.CanConnectAsync());
    // Create the database if it doesn't exist
    await context.Database.EnsureCreatedAsync();
    Console.WriteLine(await context.Database.CanConnectAsync());
```


## EnsureDeletedAsync

Метод EnsureDeletedAsync видаляє базу даних, якщо вона існує. Якщо у вас немає відповідних дозволів, виникає виняток.

```cs
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);
    Console.WriteLine(await context.Database.CanConnectAsync());
    // Drop the database if it exists
    await context.Database.EnsureDeletedAsync();
    Console.WriteLine(await context.Database.CanConnectAsync());
```

## Оба методи разом

```cs
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

## SQL-скрипт

Щоб отримати SQL, який використовується EnsureCreatedAsync, можна скористатися методом GenerateCreateScript.

```cs
    var sql = context.Database.GenerateCreateScript();
    Console.WriteLine(sql);
```

## Кілька класів DbContext

EnsureCreated працює лише тоді, коли в базі даних немає таблиць. За потреби ви можете написати власну перевірку, щоб побачити, чи потрібно ініціалізувати схему, та використати базову службу IRelationalDatabaseCreator для ініціалізації схеми.

```cs
// TODO: Check whether the schema needs to be initialized

// Initialize the schema for this DbContext
var databaseCreator = dbContext.GetService<IRelationalDatabaseCreator>();
databaseCreator.CreateTables();
```