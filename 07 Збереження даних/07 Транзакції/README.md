# Транзакції

Транзакції дозволяють обробляти кілька операцій з базою даних атомарним чином. Якщо транзакцію зафіксовано (commit), усі операції успішно застосовуються до бази даних. Якщо транзакцію відкатують(rollback), жодна з операцій не застосовується до бази даних.

## Поведінка транзакцій за замовчуванням

За замовчуванням, якщо постачальник бази даних підтримує транзакції, всі зміни в одному виклику SaveChanges застосовуються в транзакції. Якщо будь-яка зі змін завершується невдачею, транзакцію скасовують, і жодні зміни не застосовуються до бази даних. Це означає, що команда SaveChanges гарантовано завершиться успішно або залишить базу даних без змін у разі виникнення помилки.

```cs
public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; } = string.Empty;
    public List<Post> Posts { get; set; }

    public override string? ToString()
    {
        return $"{BlogId}\t{Url}";
    }
}

public class Post
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}
```

```cs
static async Task AddBlogWithPost(ApplicationDbContext context)
{
    try
    {
        context.Blogs.Add(
            new Blog
            {
                Url = "https://example.com/blog1",
                Posts = new List<Post>
                {
                new Post {PostId = 1, Title = "Post 1", Content = "Content 1"},
                new Post { Title = "Post 2", Content = "Content 2"},
                new Post { Title = "Post 3", Content = "Content 3"},
                }
            });
        int count = await context.SaveChangesAsync();
        Console.WriteLine(count);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.InnerException.Message);
    }

    context.ChangeTracker.Clear();

    int countOfBlogs = context.Blogs.Count();
    Console.WriteLine(countOfBlogs);
}

```


```
Cannot insert explicit value for identity column in table 'Posts' when IDENTITY_INSERT is set to OFF.
0
```


Для більшості програм такої поведінки за замовчуванням достатньо. Вам слід вручну контролювати транзакції лише тоді, коли вимоги вашої програми вважають це необхідним.

## Керування автоматичними транзакціями

Ви можете контролювати, чи EF автоматично створює транзакцію під час виклику SaveChanges, якщо транзакції користувача не існує. Встановіть для AutoTransactionBehavior одне з наступних значень:

* WhenNeeded (за замовчуванням): EF створює транзакцію лише за потреби. Наприклад, більшість окремих SQL-інструкцій виконуються в транзакції неявно, тому EF не створює явної транзакції.
* Always: EF завжди створює транзакцію, якщо транзакцій користувача не існує.
* Never: EF ніколи не створює транзакцію автоматично.

Наприклад, налаштуйте EF так, щоб він завжди створював транзакцію перед викликом SaveChanges:

```cs
    context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
```


Always може бути корисним, якщо код застосунку залежить від зворотних викликів створення транзакцій IDbTransactionInterceptor, які викликаються, коли SaveChanges не використовує транзакцію користувача.

    Попередження

    Використовуйте AutoTransactionBehavior.Never з обережністю. Якщо SaveChanges потрібно виконати кілька команд і виникає помилка, попередні команди, можливо, вже були зафіксовані, залишивши часткові зміни в базі даних.

    ```cs
            context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
            int count = await context.SaveChangesAsync();
            Console.WriteLine(count);
    ```
    ```
    The instance of entity type 'Post' cannot be tracked because another instance with the same key value for {'PostId'} is already being tracked. When attaching existing entities, ensure that only one entity instance with a given key value is attached. Consider using 'DbContextOptionsBuilder.EnableSensitiveDataLogging' to see the conflicting key values.
    1
    ```
    Таким чином Blog додався а Список Post частково.

## Керування транзакціями

Ви можете використовувати API DbContext.Database для початку, фіксації та відкату транзакцій.

```cs
public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
}
```

```cs
static async Task ControllingTransactionAsync(ApplicationDbContext context)
{
    using var transaction = await context.Database.BeginTransactionAsync();
    try
    {
        context.Blogs.Add(new Blog { Url = "http://blogs.msdn.com/dotnet" });
        await context.SaveChangesAsync();

        context.Blogs.Add(new Blog { Url = "http://blogs.msdn.com/visualstudio" });
        await context.SaveChangesAsync();

        var blogs = await context.Blogs
            .OrderBy(b => b.Url)
            .ToListAsync();

        foreach (var blog in blogs)
        {
            Console.WriteLine($"Blog: {blog.BlogId} - {blog.Url}");
        }

        //Blog blog1 = await context.Blogs.SingleAsync(b => b.BlogId == 10000);

        // Commit transaction if all commands succeed, transaction will auto-rollback
        // when disposed if either commands fails
        await transaction.CommitAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
        // Rollback the transaction in case of an error
        await transaction.RollbackAsync();
    }
}

```
```
Blog: 1 - http://blogs.msdn.com/dotnet
Blog: 2 - http://blogs.msdn.com/visualstudio
```

Хоча всі постачальники реляційних баз даних підтримують транзакції, інші типи постачальників можуть викидати або не виконувати операції під час виклику API транзакцій.

    Примітка

    Ручне керування транзакціями таким чином несумісне з неявно викликаними стратегіями повторного виконання. Див. розділ «Стійкість з’єднання» для отримання додаткової інформації.

## Точки збереження

Коли викликається SaveChanges, а транзакція вже виконується в контексті, EF автоматично створює точку збереження перед збереженням будь-яких даних. Точки збереження – це точки в транзакції бази даних, до яких пізніше можна повернутися у разі виникнення помилки або з будь-якої іншої причини. Якщо SaveChanges виявляє помилку, вона автоматично повертає транзакцію до точки збереження, залишаючи транзакцію в тому ж стані, ніби вона ніколи не починалася. Це дозволяє вам виправляти проблеми та повторювати спробу збереження, зокрема, коли виникають проблеми з оптимістичним паралельним виконанням.

    Попередження

    Точки збереження несумісні з множинними активними наборами результатів Multiple Active Result Sets  (MARS) SQL Server. Точки збереження не будуть створені EF, коли MARS увімкнено для з’єднання, навіть якщо MARS не використовується активно. Якщо під час SaveChanges виникає помилка, транзакція може залишитися в невідомому стані.

Також можливо керувати точками збереження вручну, як і з транзакціями. У наступному прикладі створюється точка збереження всередині транзакції та виконується відкат до неї у разі невдачі:

```cs
static async Task ControllingSavePointAsync(ApplicationDbContext context)
{
    await using var transaction = await context.Database.BeginTransactionAsync();

    try
    {
        context.Blogs.Add(new Blog { Url = "https://devblogs.microsoft.com/dotnet/" });
        await context.SaveChangesAsync();

        await transaction.CreateSavepointAsync("BeforeMoreBlogs");

        context.Blogs.Add(new Blog { Url = "https://devblogs.microsoft.com/visualstudio/" });
        context.Blogs.Add(new Blog { Url = "https://devblogs.microsoft.com/aspnet/" });
        await context.SaveChangesAsync();

        await transaction.CommitAsync();
    }
    catch (Exception)
    {
        // If a failure occurred, we rollback to the savepoint and can continue the transaction
        await transaction.RollbackToSavepointAsync("BeforeMoreBlogs");

        // TODO: Handle failure, possibly retry inserting blogs
    }
}
```

## Міжконтекстна транзакція

Ви також можете спільно використовувати транзакцію між кількома екземплярами контексту. Ця функціональність доступна лише під час використання постачальника реляційної бази даних, оскільки вона вимагає використання DbTransaction та DbConnection, які є специфічними для реляційних баз даних. Щоб спільно використовувати транзакцію, контексти повинні спільно використовувати як DbConnection, так і DbTransaction.

### Дозволити зовнішнє надання з'єднання

Спільне використання DbConnection вимагає можливості передачі з'єднання в контекст під час його створення. Найпростіший спосіб дозволити зовнішнє надання DbConnection – це припинити використання методу DbContext.OnConfiguring для налаштування контексту та створити зовнішні DbContextOptions та передати їх конструктору контексту.

    Порада

    DbContextOptionsBuilder – це API, який ви використовували в DbContext.OnConfiguring для налаштування контексту, тепер ви збираєтеся використовувати його зовні для створення DbContextOptions.

```cs
public class BloggingContext : DbContext
{
    public BloggingContext(DbContextOptions<BloggingContext> options)
        : base(options)
    {
    }

    public DbSet<Blog> Blogs { get; set; }
}
```
Альтернативою є продовження використання DbContext.OnConfiguring, але прийняття DbConnection, який зберігається, а потім використовується в DbContext.OnConfiguring.

```cs
public class BloggingContext : DbContext
{
    private DbConnection _connection;

    public BloggingContext(DbConnection connection)
    {
      _connection = connection;
    }

    public DbSet<Blog> Blogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(_connection);
    }
}
```
### Поділитися підключенням і транзакцією

Тепер ви можете створювати кілька екземплярів контексту, які використовують одне й те саме з'єднання. Потім скористайтеся API DbContext.Database.UseTransaction(DbTransaction), щоб зареєструвати обидва контексти в одній транзакції.

```cs
    string? connectionString = context.Database.GetConnectionString();
    await ShareConnectioAndTransaction(connectionString!);

static async Task ShareConnectioAndTransaction(string connectionString) 
{
    using var connection = new SqlConnection(connectionString);
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer(connection)
        .Options;

    using var context1 = new ApplicationDbContext(options);
    await using var transaction = await context1.Database.BeginTransactionAsync();
    try
    {
        context1.Blogs.Add(new Blog { Url = "http://blogs.msdn.com/dotnet" });
        await context1.SaveChangesAsync();

        using (var context2 = new ApplicationDbContext(options))
        {
            await context2.Database.UseTransactionAsync(transaction.GetDbTransaction());

            var blogs = await context2.Blogs
                .OrderBy(b => b.Url)
                .ToListAsync();

            context2.Blogs.Add(new Blog { Url = "http://dot.net" });
            await context2.SaveChangesAsync();
        }
        await transaction.CommitAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
    }
}

```

### Використання зовнішніх DbTransactions (лише для реляційних баз даних)

Якщо ви використовуєте кілька технологій доступу до даних для доступу до реляційної бази даних, вам може знадобитися спільний доступ до транзакції між операціями, що виконуються цими різними технологіями.

У наступному прикладі показано, як виконати операцію ADO.NET SqlClient та операцію Entity Framework Core в одній транзакції.

```cs
    string? connectionString = context.Database.GetConnectionString();
    await UsingExternalDbTransactions(connectionString);

static async Task UsingExternalDbTransactions(string connectionString)
{
    using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
    try
    {
        // Run raw ADO.NET command in the transaction
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM dbo.Blogs";
        command.ExecuteNonQuery();

        // Run an EF Core command in the transaction
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection)
            .Options;

        using (var context = new ApplicationDbContext(options))
        {
            await context.Database.UseTransactionAsync(transaction);
            context.Blogs.Add(new Blog { Url = "http://blogs.msdn.com/dotnet" });
            await context.SaveChangesAsync();
        }

        // Commit transaction if all commands succeed, transaction will auto-rollback
        // when disposed if either commands fails
        await transaction.CommitAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
    }
}
```
### Використання System.Transactions

Можна використовувати амбієнтні транзакції, якщо потрібно координувати дії в більшій області застосування.



```cs

    string? connectionString = context.Database.GetConnectionString();
    await UsingSystemTransactions(connectionString!);


static async Task UsingSystemTransactions(string connectionString)
{
    using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.ReadCommitted,
                },
                TransactionScopeAsyncFlowOption.Enabled))
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        try
        {
            // Run raw ADO.NET command in the transaction
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM dbo.Blogs";
            await command.ExecuteNonQueryAsync();

            // Run an EF Core command in the transaction
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connection)
                .Options;

            using (var context = new ApplicationDbContext(options))
            {
                context.Blogs.Add(new Blog { Url = "http://blogs.msdn.com/dotnet" });
                await context.SaveChangesAsync();
            }

            // Commit transaction if all commands succeed, transaction will auto-rollback
            // when disposed if either commands fails
            scope.Complete();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
```
Також можливо залучити до явної транзакції.

```cs
    string? connectionString = context.Database.GetConnectionString();
    await UsingExplicitTransactions(connectionString!);

static async Task UsingExplicitTransactions(string connectionString)
{
    using (var transaction = new CommittableTransaction(
           new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }))
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connection)
                .Options;

            using (var context = new ApplicationDbContext(options))
            {
                await context.Database.OpenConnectionAsync();
                context.Database.EnlistTransaction(transaction);

                // Run raw ADO.NET command in the transaction
                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM dbo.Blogs";
                await command.ExecuteNonQueryAsync();

                // Run an EF Core command in the transaction
                context.Blogs.Add(new Blog { Url = "http://blogs.msdn.com/dotnet" });
                await context.SaveChangesAsync();
                await context.Database.CloseConnectionAsync();
            }

            // Commit transaction if all commands succeed, transaction will auto-rollback
            // when disposed if either commands fails
            transaction.Commit();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
```

### Обмеження System.Transactions

1. EF Core покладається на постачальників баз даних для реалізації підтримки System.Transactions. Якщо постачальник не реалізує підтримку System.Transactions, можливо, що виклики цих API будуть повністю проігноровані. SqlClient підтримує це.

    Важливо

    Рекомендується перевірити, чи правильно API працює з вашим постачальником, перш ніж покладатися на нього для керування транзакціями. Якщо це не так, рекомендується звернутися до розробника бази даних.

2. Підтримку розподілених транзакцій у System.Transactions було додано до .NET 7.0 лише для Windows. Будь-яка спроба використовувати розподілені транзакції на старіших версіях .NET або на платформах, відмінних від Windows, буде невдалою.

3. TransactionScope не підтримує асинхронне підтвердження/відкат; це означає, що його синхронне видалення блокує виконуваний потік, доки операція не буде завершена.