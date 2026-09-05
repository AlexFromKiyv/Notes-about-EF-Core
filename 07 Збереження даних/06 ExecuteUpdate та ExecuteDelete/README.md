# ExecuteUpdate та ExecuteDelete

ExecuteUpdate та ExecuteDelete – це спосіб збереження даних у базі даних без використання традиційного відстеження змін EF та методу SaveChanges(). Для вступного порівняння цих двох методів див. сторінку «Швидкий огляд» про збереження даних.

## ExecuteDelete

Розлянемо наступну модель.

```cs
public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int Rating { get; set; }
    public bool IsVisible { get; set; } = true;
    public List<Post> Posts { get; set; }

    [Timestamp]
    public byte[] Version { get; set; }
    public override string? ToString()
    {
        return $"{BlogId}\t{Url}\t{Rating}\t{IsVisible}\t{Convert.ToHexString(Version)}\t";
    }
}

public class Post
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public int Rating { get; set; }
    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}
```
```cs
static async Task PopulateDatabase(ApplicationDbContext context)
{
    context.Blogs.AddRange(
        new Blog { Url = "https://example.com/blog1", Rating = 5, IsVisible = true,
            Posts = new List<Post>
            {
                new Post { Title = "Post 1", Content = "Content 1", Rating = 3 },
                new Post { Title = "Post 2", Content = "Content 2", Rating = 2 },
                new Post { Title = "Post 3", Content = "Content 3", Rating = 3 },
            }   
        },
        new Blog { Url = "https://example.com/blog2", Rating = 1, IsVisible = true,
            Posts = new List<Post>
            {
                new Post { Title = "Post 4", Content = "Content 4", Rating = 4 },
                new Post { Title = "Post 5", Content = "Content 5", Rating = 2 },
            }
        },  
        new Blog { Url = "https://example.com/blog3", Rating = 1, IsVisible = true,
            Posts = new List<Post>
            {
                new Post { Title = "Post 6", Content = "Content 6", Rating = 3 },
                new Post { Title = "Post 7", Content = "Content 7", Rating = 5 },
            }
        }

    );
    var countAdded = await context.SaveChangesAsync();
    Console.WriteLine(countAdded);
}
```

Припустимо, що вам потрібно видалити всі блоги з рейтингом нижче певного порогу. Традиційний підхід SaveChanges() вимагає від вас виконання наступного:

```cs
static async Task DeleteWithSaveChanges(ApplicationDbContext context)
{
    await foreach (var blog in context.Blogs.Where(b => b.Rating < 3).AsAsyncEnumerable())
    {
        context.Blogs.Remove(blog);
    }

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}
```
```
2
```
Це досить неефективний спосіб виконання цього завдання: ми запитуємо базу даних щодо всіх блогів, що відповідають нашому фільтру, а потім запитуємо, матеріалізуємо та відстежуємо всі ці екземпляри; кількість відповідних сутностей може бути величезною. Потім ми повідомляємо трекеру змін EF, що кожен блог потрібно видалити, і застосовуємо ці зміни, викликаючи SaveChanges(), який генерує інструкцію DELETE для кожного з них.

Ось те саме завдання, яке виконується через API ExecuteDelete:

```cs
static async Task DeleteWithExecuteDelete(ApplicationDbContext context)
{
    int count = await context.Blogs.Where(b => b.Rating < 3).ExecuteDeleteAsync();
    Console.WriteLine(count);
}
```
Це використовує знайомі оператори LINQ для визначення того, на які блоги слід вплинути — так само, як якщо б ми запитували їх — а потім повідомляє EF виконати SQL DELETE для бази даних:

```sql
DELETE FROM [b]
FROM [Blogs] AS [b]
WHERE [b].[Rating] < 3
```
Окрім того, що це простіше та коротше, це дуже ефективно виконується в базі даних, без завантаження будь-яких даних з бази даних або залучення відстеження змін EF. Зверніть увагу, що ви можете використовувати довільні оператори LINQ, щоб вибрати блоги, які потрібно видалити — вони перетворюються на SQL для виконання в базі даних, так само, як якщо б ви запитували ці блоги.

## ExecuteUpdate

Замість видалення цих блогів, що, якби ми хотіли змінити властивість, щоб вказати, що вони мають бути приховані? ExecuteUpdate надає подібний спосіб вираження оператора SQL UPDATE:

```cs
static async Task UpdateWithExecuteUpdate(ApplicationDbContext context)
{
    await ShowBlogs(context);

    int count = await context.Blogs
        .Where(b => b.Rating < 3)
        .ExecuteUpdateAsync(setters => 
        setters.SetProperty(blog => blog.IsVisible, false));
    Console.WriteLine(count);

    context.ChangeTracker.Clear(); Console.WriteLine();
    await ShowBlogs(context);

}
static async Task ShowBlogs(ApplicationDbContext context)
{
    foreach (var blog in context.Blogs)
    {
        Console.WriteLine(blog);
    }
}
```
```
1       https://example.com/blog1       5       True    00000000000007D1
2       https://example.com/blog2       1       True    00000000000007D2
3       https://example.com/blog3       1       True    00000000000007D3
2

1       https://example.com/blog1       5       True    00000000000007D1
2       https://example.com/blog2       1       False   00000000000007D4
3       https://example.com/blog3       1       False   00000000000007D5
```

Як і у випадку з ExecuteDelete, ми спочатку використовуємо LINQ, щоб визначити, які блоги слід змінити; але у випадку з ExecuteUpdate нам також потрібно виразити зміну, яку потрібно застосувати до відповідних блогів. Це робиться шляхом виклику SetProperty у виклику ExecuteUpdate та надання йому двох аргументів: властивості, яку потрібно змінити (IsVisible), та нового значення, яке вона повинна мати (false). Це призводить до виконання наступного SQL-запиту:

```sql
UPDATE [b]
SET [b].[IsVisible] = CAST(0 AS bit)
FROM [Blogs] AS [b]
WHERE [b].[Rating] < 3

```

### Оновлення кількох властивостей

ExecuteUpdate дозволяє оновлювати кілька властивостей за один виклик. Наприклад, щоб встановити IsVisible як false, так і Rating як zero, просто об'єднайте додаткові виклики SetProperty разом:

```cs
static async Task MultipayUpdateWithExecuteUpdate(ApplicationDbContext context)
{
    await ShowBlogs(context);

    int count = await context.
        Blogs.Where(b => b.Rating < 3).
        ExecuteUpdateAsync(setters => setters
        .SetProperty(blog => blog.IsVisible, false)
        .SetProperty(blog => blog.Rating, 0)
        );
    Console.WriteLine(count);

    context.ChangeTracker.Clear(); Console.WriteLine();
    await ShowBlogs(context);
}
```
```
1       https://example.com/blog1       5       True    00000000000007D1
2       https://example.com/blog2       1       True    00000000000007D2
3       https://example.com/blog3       1       True    00000000000007D3
2

1       https://example.com/blog1       5       True    00000000000007D1
2       https://example.com/blog2       0       False   00000000000007D4
3       https://example.com/blog3       0       False   00000000000007D5
```

Це виконує наступний SQL-запит:

```sql
UPDATE [b]
SET [b].[Rating] = 0,
    [b].[IsVisible] = CAST(0 AS bit)
FROM [Blogs] AS [b]
WHERE [b].[Rating] < 3
```

### Посилання на існуюче значення властивості

У наведених вище прикладах властивість оновлено до нового постійного значення. ExecuteUpdate також дозволяє посилатися на існуюче значення властивості під час обчислення нового значення; наприклад, щоб збільшити рейтинг усіх відповідних блогів на один, використовуйте наступне:

```cs
static async Task UpdateWithReferenceToLocalVariable(ApplicationDbContext context)
{
    await ShowBlogs(context);

    int count = await context.Blogs
        .Where(b => b.Rating < 3)
        .ExecuteUpdateAsync(setters => setters.SetProperty(blog => blog.Rating, b => b.Rating + 1));
    Console.WriteLine(count);

    context.ChangeTracker.Clear(); Console.WriteLine();
    await ShowBlogs(context);
}
```

```
1       https://example.com/blog1       5       True    00000000000007D1
2       https://example.com/blog2       1       True    00000000000007D2
3       https://example.com/blog3       1       True    00000000000007D3
2

1       https://example.com/blog1       5       True    00000000000007D1
2       https://example.com/blog2       2       True    00000000000007D4
3       https://example.com/blog3       2       True    00000000000007D5
```
Зверніть увагу, що другий аргумент SetProperty тепер є лямбда-функцією, а не константою, як раніше. Його параметр b представляє оновлюваний блог; в межах цієї лямбда-функції b.Rating, таким чином, містить рейтинг до будь-яких змін. Це виконує наступний SQL-запит:

```sql
UPDATE [b]
SET [b].[Rating] = [b].[Rating] + 1
FROM [Blogs] AS [b]
WHERE [b].[Rating] < 3
```

## Навігація та пов'язані сутності

ExecuteUpdate наразі не підтримує посилання на навігацію в лямбда-виразі SetProperty. Наприклад, припустимо, що ми хочемо оновити рейтинги всіх блогів, щоб новий рейтинг кожного блогу був середнім значенням рейтингів усіх його публікацій. Ми можемо спробувати використати ExecuteUpdate наступним чином:

```cs
static async Task AttemptUpdate(ApplicationDbContext context)
{
    int count = await context.Blogs.ExecuteUpdateAsync(
    setters => setters.SetProperty(b => b.Rating, b => b.Posts.Average(p => p.Rating)));
    Console.WriteLine(count);
}

```
Однак, EF дозволяє виконувати цю операцію, спочатку використовуючи Select для обчислення середнього рейтингу та проектуючи його на анонімний тип, а потім використовуючи ExecuteUpdate для цього:

```cs
static async Task UpdateWithSelect(ApplicationDbContext context)
{
    await ShowBlogs(context);

    int count = await context.Blogs
    .Select(b => new { Blog = b, NewRating = b.Posts.Average(p => p.Rating) })
    .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Blog.Rating, b => b.NewRating));
    Console.WriteLine(count);

    context.ChangeTracker.Clear();

    await ShowBlogs(context);
}

```
```
1       https://example.com/blog1       5       True    00000000000007D1
2       https://example.com/blog2       1       True    00000000000007D2
3       https://example.com/blog3       1       True    00000000000007D3
3
1       https://example.com/blog1       2       True    00000000000007D4
2       https://example.com/blog2       3       True    00000000000007D5
3       https://example.com/blog3       4       True    00000000000007D6
```

Це виконує наступний SQL-запит:

```sql
UPDATE [b]
SET [b].[Rating] = CAST((
    SELECT AVG(CAST([p].[Rating] AS float))
    FROM [Post] AS [p]
    WHERE [b].[Id] = [p].[BlogId]) AS int)
FROM [Blogs] AS [b]
```

## Відстеження змін

Користувачі, знайомі з SaveChanges, звикли виконувати кілька змін, а потім викликати SaveChanges для застосування всіх цих змін до бази даних; це стало можливим завдяки трекеру змін EF, який накопичує – або відстежує – ці зміни.

Функції ExecuteUpdate та ExecuteDelete працюють зовсім по-різному: вони набувають чинності негайно, в момент їх виклику. Це означає, що хоча одна операція ExecuteUpdate або ExecuteDelete може впливати на багато рядків, неможливо накопичити кілька таких операцій та застосувати їх одночасно, наприклад, під час виклику SaveChanges. Фактично, ці функції абсолютно не знають про трекер змін EF і взагалі не взаємодіють з ним.

Фактично, ці функції абсолютно не знають про трекер змін EF і взагалі не взаємодіють з ним. Це має кілька важливих наслідків.

Розглянемо наступний код:

```cs
static async Task HowItChangeTracking(ApplicationDbContext context)
{
    await ShowBlogs(context);
    context.ChangeTracker.Clear();


    // 1. Query the blog. Since EF queries are tracking by default, the Blog is now tracked by EF's change tracker.
    var blog = await context.Blogs.SingleAsync(b => b.BlogId == 1);

    // 2. Increase the rating of all blogs in the database by one. This executes immediately.
    await context.Blogs.ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Rating, b => b.Rating + 1));

    // 3. Increase the rating of `SomeBlog` by two. This modifies the .NET `Rating` property and is not yet persisted to the database.
    blog.Rating += 2;

    // 4. Persist tracked changes to the database.
    await context.SaveChangesAsync();

    context.ChangeTracker.Clear();Console.WriteLine();
    await ShowBlogs(context);
}

```
```
1       https://example.com/blog1       5       True
2       https://example.com/blog2       1       True
3       https://example.com/blog3       1       True

1       https://example.com/blog1       7       True
2       https://example.com/blog2       2       True
3       https://example.com/blog3       2       True
```
Найважливіше те, що коли викликається ExecuteUpdate і всі блоги оновлюються в базі даних, трекер змін EF не оновлюється, і відстежуваний екземпляр .NET все ще має своє початкове значення рейтингу з моменту, коли його було запитано. Припустимо, що спочатку рейтинг блогу був 5; Після виконання третього рядка рейтинг у базі даних тепер становить 6 (через ExecuteUpdate), тоді як рейтинг у відстежуваному екземплярі .NET становить 7. Коли викликається SaveChanges, EF виявляє, що нове значення 7 відрізняється від початкового значення 5, і зберігає цю зміну. ​​Зміна, виконана ExecuteUpdate, перезаписується і не враховується.

Як наслідок, зазвичай гарною ідеєю є уникати змішування як відстежуваних модифікацій SaveChanges, так і невідстежуваних модифікацій через ExecuteUpdate/ExecuteDelete.

Якшо до сутності Blog додати токен паралельності тоді при визові SaveChange виникне виняток до сутність змінилась з часу запиту.


## Транзакції

Продовжуючи вищесказане, важливо розуміти, що ExecuteUpdate та ExecuteDelete неявно не запускають транзакцію під час їх виклику. Розглянемо наступний код:

```cs
static async Task HowAboutTransaction(ApplicationDbContext context)
{
    await ShowBlogs(context);

    await context.Blogs.ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Rating, b => b.Rating + 2));
    await context.Blogs.ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Rating, b => b.Rating - 1));

    var blog = await context.Blogs.SingleAsync(b => b.BlogId == 1);
    blog.Rating += 2;
    await context.SaveChangesAsync();

    context.ChangeTracker.Clear(); Console.WriteLine();
    await ShowBlogs(context);
}
```
```
1       https://example.com/blog1       5       True    Draft.Models.Blog
2       https://example.com/blog2       1       True    Draft.Models.Blog
3       https://example.com/blog3       1       True    Draft.Models.Blog

1       https://example.com/blog1       7       True    Draft.Models.Blog
2       https://example.com/blog2       2       True    Draft.Models.Blog
3       https://example.com/blog3       2       True    Draft.Models.Blog
```
Кожен виклик ExecuteUpdate призводить до надсилання одного SQL UPDATE до бази даних. Оскільки жодної транзакції не створюється, якщо будь-який збій заважає успішному завершенню другої ExecuteUpdate, наслідки першої все одно зберігаються в базі даних. Фактично, чотири операції, описані вище – два виклики ExecuteUpdate, запит і SaveChanges – виконуються кожна в межах власної транзакції. Щоб об’єднати кілька операцій в одну транзакцію, явно запустіть транзакцію за допомогою DatabaseFacade:

```cs
static async Task UpdateWithTransaction(ApplicationDbContext context)
{
    await ShowBlogs(context);

    using (var transaction = context.Database.BeginTransaction())
    {
        await context.Blogs.
        ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Rating, b => b.Rating + 2));
        await context.Blogs.
        ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Rating, b => b.Rating - 1));

        transaction.Commit();
    }

    context.ChangeTracker.Clear();

    using (var transaction = context.Database.BeginTransaction())
    {
        var blog = await context.Blogs.SingleAsync(b => b.BlogId == 1);
        blog.Rating += 2;
        await context.SaveChangesAsync();

        transaction.Commit();
    }

    context.ChangeTracker.Clear(); Console.WriteLine();
    await ShowBlogs(context);
}

```
```
1       https://example.com/blog1       5       True    00000000000007D1
2       https://example.com/blog2       1       True    00000000000007D2
3       https://example.com/blog3       1       True    00000000000007D3

1       https://example.com/blog1       8       True    00000000000007DA
2       https://example.com/blog2       2       True    00000000000007D8
3       https://example.com/blog3       2       True    00000000000007D9
```

## Контроль паралельності і рядки шо зазналм змін

SaveChanges забезпечує автоматичний контроль паралельності, використовуючи токен паралельності, щоб гарантувати, що рядок не змінювався між моментом його завантаження та моментом збереження змін. Оскільки ExecuteUpdate та ExecuteDelete не взаємодіють із системою відстеження змін, вони не можуть автоматично застосовувати керування паралельністю.

Однак, обидва ці методи повертають кількість рядків, на які вплинула операція; це може бути особливо корисним для самостійної реалізації контролю паралельності:

```cs
static async Task ConcurrencyControlAndRowsAffected(ApplicationDbContext context)
{
    Blog blog = await context.Blogs.SingleAsync(b=>b.BlogId == 1);

    context.Database.ExecuteSqlRawAsync("Update Blogs Set Rating = 1");

    var numberUpdate = await context.Blogs
        .Where(b => b.BlogId == blog.BlogId && b.Version == blog.Version)
        .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.Rating, b => b.Rating + 1));
    if (numberUpdate == 0)
    {
        Console.WriteLine("Update failed!\n");
        throw new Exception("Update failed!");
    }
}
```
```
Update failed!

Unhandled exception. System.Exception: Update failed!
   at Program.<<Main>$>g__ConcurrencyControlAndRowsAffected|0
```

У цьому коді ми використовуємо оператор LINQ Where для застосування оновлення до певного блогу, і лише якщо його токен паралелізму має певне значення (наприклад, те, яке ми бачили під час запиту блогу з бази даних). Потім ми перевіряємо, скільки рядків було фактично оновлено за допомогою ExecuteUpdate; якщо результат дорівнює нулю, то жодних рядків не було оновлено, і токен паралельності, ймовірно, було змінено в результаті паралельного оновлення.

## Обмеження

* Наразі підтримується лише оновлення та видалення; вставлення має виконуватися через DbSet<TEntity>.Add і SaveChanges().
* Хоча оператори SQL UPDATE і DELETE дозволяють отримувати вихідні значення стовпців для рядків, на які впливає, це наразі не підтримується ExecuteUpdate і ExecuteDelete.
* Кілька викликів цих методів не можуть бути пакетними. Кожен виклик виконує власний цикл обробки даних.
* Бази даних зазвичай дозволяють змінювати лише одну таблицю за допомогою UPDATE або DELETE.
* Ці методи наразі працюють лише з постачальниками реляційних баз даних.
