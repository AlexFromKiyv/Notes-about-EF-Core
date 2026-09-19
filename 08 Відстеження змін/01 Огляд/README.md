# Огляд

Кожен екземпляр DbContext відстежує зміни, внесені до сутностей. Ці відстежувані об'єкти, у свою чергу, зумовлюють зміни до бази даних, коли викликається SaveChanges. У цьому документі наведено огляд механізму відстеження змін в Entity Framework Core (EF Core) та його взаємозв’язку із запитами й оновленнями.

## Як відстежувати сутності

Екземпляри сутностей починають відстежуватися, коли вони:

* Повернуто в результаті запиту до бази даних
* Явно приєднано до DbContext за допомогою методів Add, Attach, Update або подібних.
* Виявлено як нові сутності, пов’язані з уже відстежуваними сутностями.

Відстеження екземплярів сутностей припиняється, коли:

* Об'єкт DbContext звільнено.
* Журнал відстеження змін очищено.
* Суб'єкти є явно відокремлені.

DbContext призначений для представлення короткотривалого шаблону «Одиниця роботи» (Unit of Work), як описано в розділі «Ініціалізація та конфігурація DbContext». Це означає, що звільнення ресурсів DbContext — це звичайний спосіб припинити відстеження сутностей. Іншими словами, життєвий цикл DbContext має бути таким:

1. Створіть екземпляр DbContext
2. Відстежуйте деякі сутності
3. Внесіть зміни до сутностей.
4. Викличте SaveChanges, щоб оновити базу даних.
5. Звільніть екземпляр DbContext.

    Порада

    За такого підходу не обов’язково очищувати change tracker або явно від’єднувати екземпляри сутностей. Однак, якщо вам усе ж потрібно від’єднати сутності, виклик методу ChangeTracker.Clear є ефективнішим, ніж від’єднання сутностей по одній.

## Стани сутності

Кожна сутність пов’язана з певним станом сутності (EntityState):

* Detached(відокремлені) сутності не відстежуються контекстом DbContext.
* Added(додані) сутності є новими й ще не були внесені до бази даних. Це означає, що їх буде вставлено під час виклику методу SaveChanges.
* Unchanged(без змін) Сутності зі статусом unchanged не зазнали жодних змін відтоді, як були отримані з бази даних. Усі сутності, отримані в результаті запитів, спочатку перебувають саме в цьому стані.
* Modified(модифіковані) сутності зазнали змін після того, як їх було отримано з бази даних. Це означає, що вони будуть оновлені під час виклику методу SaveChanges.
* Deleted(видалені) сутності існують у базі даних, але позначені для видалення, коли викликається метод SaveChanges.

EF Core відстежує зміни на рівні властивостей. Наприклад, якщо змінюється лише одне значення властивості, то під час оновлення бази даних зміниться тільки це значення. Однак властивості можуть бути позначені як змінені лише тоді, коли сама сутність перебуває у стані Modified. (Або, якщо поглянути інакше: стан Modified означає, що принаймні одне значення властивості було позначено як змінене.)

У наведеній нижче таблиці узагальнено різні стани:

|Стан сутності|Відстежується DbContext|Існує в базі даних|Змінені властивості|Дія під час SaveChanges|
|-------------|-----------------------|------------------|-------------------|-----------------------|
|Detached|Ні|-|-|-|
|Added|Так|Ні|-|Insert|
|Unchanged|Так|Так|Ні|-|
|Modified|Так|Так|Так|Update|
|Deleted|Так|Так|-|Delete|

    Примітка

    Для більшої зрозумілості в цьому тексті використовуються терміни, характерні для реляційних баз даних. Бази даних NoSQL зазвичай підтримують схожі операції, хоча й можуть використовувати для них інші назви. Додаткову інформацію можна знайти в документації постачальника вашої бази даних.

## Відстеження на основі запитів

Відстеження змін EF Core працює найкраще коли один і той самий екземпляр DbContext використовується як для запиту для сутностей так і для їх оновлення, викликавши SaveChanges. Це відбувається тому, що EF Core автоматично відстежує стан отриманих сутностей, а потім виявляє будь-які зміни, внесені до них, під час виклику методу SaveChanges.

Цей підхід має кілька переваг порівняно з явним відстеженням екземплярів сутностей:

* Усе просто. Стани сутностей рідко потребують явного керування — EF Core сам опікується змінами стану.
* Оновлення обмежуються лише тими значеннями, які фактично змінилися.
* Значення тіньових властивостей зберігаються та використовуються за потреби. Це особливо актуально, коли зовнішні ключі зберігаються в тіньовому стані.
* Початкові значення властивостей зберігаються автоматично та використовуються для ефективного оновлення.

## Простий запит і оновлення

Наприклад, розгляньмо просту модель:

```cs
public class Blog
{
    public int Id { get; set; }
    public string Name { get; set; }
    public IList<Post> Posts { get; } = new List<Post>();
    public override string? ToString()
    {
        return $"{Id}\t{Name}\t{Posts.Count}";
    }

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
static async Task DoAsync()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    //Console.WriteLine(context.Model.ToDebugString());

    // Clean the database
    await CleanDatabase(context);
    await PopulateDatabase(context);

}
await DoAsync();

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

Ми можемо використовувати цю модель для виконання запитів до блогів і дописів, а потім вносити зміни до бази даних:

```cs
static async Task QueryAndUpadteBlog(ApplicationDbContext context)
{
    var blog = await context.Blogs.Include(e => e.Posts).FirstAsync(e => e.Name == ".NET Blog");

    blog.Name = ".NET Blog (Updated!)";

    IList<Post> posts = blog.Posts
        .Where(e => !e.Title.Contains("5.0"))
        .ToList();

    foreach (var post in posts)
    {
        post.Title = post.Title.Replace("5", "5.0");
    }

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}

```
Виклик SaveChanges призводить до таких оновлень бази даних :

```sql
exec sp_executesql N'SET NOCOUNT ON;
UPDATE [Blogs] SET [Name] = @p0
OUTPUT 1
WHERE [Id] = @p1;
UPDATE [Posts] SET [Title] = @p2
OUTPUT 1
WHERE [Id] = @p3;
',N'@p1 int,@p0 nvarchar(4000),@p3 int,@p2 nvarchar(4000)',@p1=1,@p0=N'.NET Blog (Updated!)',@p3=2,@p2=N'Announcing F# 5.0'
```

Режим налагодження відстеження змін(the change tracker debug view) — це чудовий спосіб візуалізувати, які сутності відстежуються та в якому стані вони перебувають. Наприклад, якщо вставити наведений нижче код у приклад вище перед викликом SaveChanges:

```cs
    context.ChangeTracker.DetectChanges();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
```
Генерує такий вивід:
```
Blog {Id: 1} Modified
    Id: 1 PK
    Name: '.NET Blog (Updated!)' Modified Originally '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}]
Post {Id: 1} Unchanged
    Id: 1 PK
    BlogId: 1 FK
    Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
    Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Modified
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5.0' Modified Originally 'Announcing F# 5'
  Blog: {Id: 1}
```
Зверніть особливу увагу на:

* Властивість Blog.Name позначено як Modified (Name: '.NET Blog (Updated!)' Modified Originally '.NET Blog'), внаслідок чого блог переходить у стан Modified.
* Властивість Post.Title для допису 2 позначено як Modified (Title: 'Announcing F# 5.0' Modified Originally 'Announcing F# 5'), внаслідок чого цей допис переходить у стан Modified.
* Інші значення властивостей запису 2 не змінилися, а тому не позначені як змінені. Саме тому ці значення не включаються до оновлення бази даних.
* Інший запис не зазнав жодних змін. Саме тому він досі перебуває в стані Unchanged(без змін) і не включається до оновлення бази даних.

## Виконання запиту, а потім операцій вставки, оновлення та видалення

Оновлення, подібні до тих, що наведено в попередньому прикладі, можна поєднувати з операціями вставлення та видалення в межах однієї одиниці роботи.

```cs
static async Task QueryAndUpadteBlog(ApplicationDbContext context)
{
    var blog = await context.Blogs
        .Include(e => e.Posts)
        .FirstAsync(e => e.Name == ".NET Blog");

    blog.Name = ".NET Blog (Updated!)";

    IList<Post> posts = blog.Posts
        .Where(e => !e.Title.Contains("5.0"))
        .ToList();

    foreach (var post in posts)
    {
        post.Title = post.Title.Replace("5", "5.0");
    }

    context.ChangeTracker.DetectChanges();
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}
```
У цьому прикладі:

* Блог та пов’язані з ним публікації запитуються з бази даних та відстежуються.
* Властивість Blog.Name змінено.
* До колекції наявних дописів блогу додається новий допис.
* Наявний допис позначається для видалення шляхом виклику DbContext.Remove.

Якщо знову переглянути налагоджувальне представлення відстежувача змін перед викликом SaveChanges, можна побачити, як EF Core відстежує ці зміни:

```
Blog {Id: 1} Modified
    Id: 1 PK
    Name: '.NET Blog (Updated!)' Modified Originally '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: -2147482645}]
Post {Id: -2147482645} Added
    Id: -2147482645 PK Temporary
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
Post {Id: 2} Deleted
    Id: 2 PK
    BlogId: 1 FK
    Content: 'F# 5 is the latest version of F#, the functional programming...'
    Title: 'Announcing F# 5'
  Blog: {Id: 1}

3
```


Зверніть увагу:

* Блог позначено як змінений. Це призведе до оновлення.
* Допис 2 позначено як видалений. Це призведе до видалення запису з бази даних.
* Новий допис із тимчасовим ідентифікатором пов’язується з блогом 1 і позначається як Added. Це ініціює операцію вставлення даних у базу.

Це призводить до виконання таких команд бази даних під час виклику SaveChanges:

```sql
exec sp_executesql N'SET NOCOUNT ON;
UPDATE [Blogs] SET [Name] = @p0
OUTPUT 1
WHERE [Id] = @p1;
DELETE FROM [Posts]
OUTPUT 1
WHERE [Id] = @p2;
INSERT INTO [Posts] ([BlogId], [Content], [Title])
OUTPUT INSERTED.[Id]
VALUES (@p3, @p4, @p5);
',N'@p1 int,@p0 nvarchar(4000),@p2 int,@p3 int,@p4 nvarchar(4000),@p5 nvarchar(4000)',@p1=1,@p0=N'.NET Blog (Updated!)',@p2=2,@p3=1,@p4=N'.NET 5.0 was released recently and has come with many...',@p5=N'What’s next for System.Text.Json?'
```

Докладнішу інформацію про вставлення та видалення сутностей див. у розділі «Явне відстеження сутностей». Докладнішу інформацію про те, як EF Core автоматично виявляє такі зміни, див. у розділі «Виявлення змін і сповіщення».

    Порада

    Викличте ChangeTracker.HasChanges(), щоб визначити, чи було внесено зміни, які призведуть до оновлення бази даних під час виконання SaveChanges. Якщо HasChanges повертає false, то SaveChanges не виконуватиме жодних дій.

