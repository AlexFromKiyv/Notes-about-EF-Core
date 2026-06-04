# Типи сутностей

Включення DbSet певного типу до вашого контексту означає, що він включено до моделі EF Core; зазвичай ми називаємо такий тип сутністю. EF Core може зчитувати та записувати екземпляри сутностей з бази даних/в неї, а якщо ви використовуєте реляційну базу даних, EF Core може створювати таблиці для ваших сутностей за допомогою міграцій.

## Включення типів до моделі

За домовленістю, типи, що визначені як властивості DbSet у вашому контексті, включаються до моделі як сутності. Також включено типи сутностей, зазначені в методі OnModelCreating, а також будь-які типи, знайдені шляхом рекурсивного дослідження властивостей навігації інших виявлених типів сутностей.

У наведеному нижче прикладі коду включено всі типи:

1. Blog включено, оскільки він відображається у властивості DbSet контексту.
2. Post включено, оскільки його виявлено через властивість навігації Blog.Posts.
3. AuditEntry, оскільки він вказаний у OnModelCreating.

```cs
internal class MyContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEntry>();
    }
}

public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }

    public List<Post> Posts { get; set; }
}

public class Post
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public Blog Blog { get; set; }
}

public class AuditEntry
{
    public int AuditEntryId { get; set; }
    public string Username { get; set; }
    public string Action { get; set; }
}
```

## Виключення типів з моделі

Якщо ви не хочете, щоб певний тип було включено до моделі, ви можете виключити його:

```cs
[NotMapped]
public class BlogMetadata
{
    public DateTime LoadedFromDatabase { get; set; }
}
```
```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Ignore<BlogMetadata>();
}
```

## Виключення з міграцій

Іноді корисно мати один і той самий тип сутності, визначений у кількох типах DbContext. Це особливо актуально під час використання обмежених контекстів, для яких зазвичай використовується різний тип DbContext для кожного обмеженого контексту.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<IdentityUser>()
        .ToTable("AspNetUsers", t => t.ExcludeFromMigrations());
}
```
З цією конфігурацією міграції не створюватимуть таблицю AspNetUsers, але IdentityUser все ще буде включено до моделі та може використовуватися звичайним чином.

Якщо вам потрібно знову почати керувати таблицею за допомогою міграцій, слід створити нову міграцію, де AspNetUsers не виключається. Наступна міграція тепер міститиме всі зміни, внесені до таблиці.

## Ім'я таблиці

За домовленістю, кожен тип сутності буде налаштовано на зіставлення з таблицею бази даних з тим самим ім'ям, що й властивість DbSet, яка надає доступ до сутності. Якщо для заданої сутності не існує DbSet, використовується ім'я класу. 

Ви можете вручну налаштувати назву таблиці:

```cs
[Table("Blogs")]
public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
}
```
```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .ToTable("Blogs");
}
```

## Схема таблиці

Під час використання реляційної бази даних таблиці за домовленістю створюються за схемою вашої бази даних за замовчуванням. Наприклад, Microsoft SQL Server використовуватиме схему dbo (SQLite не підтримує схеми). Ви можете налаштувати створення таблиць у певній схемі наступним чином:

```cs
[Table("blogs", Schema = "blogging")]
public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .ToTable("blogs", schema: "blogging");
}
```
Замість того, щоб вказувати схему для кожної таблиці, ви також можете визначити схему за замовчуванням на рівні моделі за допомогою Fluent API:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema("blogging");
}
```
Зверніть увагу, що встановлення схеми за замовчуванням також вплине на інші об'єкти бази даних, такі як послідовності.

## Зіставлення представлень

Типи сутностей можна зіставити з представленнями бази даних за допомогою Fluent API.
EF не створює view в БД автоматично під час міграцій. Він вважає що воно вже створено.

```cs
modelBuilder.Entity<Blog>()
    .ToView("blogsView", schema: "blogging");
```
Зіставлення з виглядом видалить зіставлення таблиці за замовчуванням, але тип сутності також можна явно зіставити з таблицею. У цьому випадку зіставлення вигляду використовуватиметься для запитів, а зіставлення таблиці – для оновлень.

## Зіставлення з table-valued function

Можна зіставити тип сутності з table-valued функцією (TVF) замість таблиці в базі даних. Щоб проілюструвати це, давайте визначимо ще одну сутність, яка представляє блог з кількома публікаціями. У цьому прикладі сутність не має ключа, але це не обов'язково має бути так.

```cs
public class BlogWithMultiplePosts
{
    public string Url { get; set; }
    public int PostCount { get; set; }
}
```
Далі створіть у базі даних таку функцію, що повертає значення таблиці та повертає лише блоги з кількома публікаціями, а також кількість публікацій, пов’язаних з кожним із цих блогів:

```sql
CREATE FUNCTION dbo.BlogsWithMultiplePosts()
RETURNS TABLE
AS
RETURN
(
    SELECT b.Url, COUNT(p.Id) AS PostCount
    FROM Blogs AS b
    JOIN Posts AS p ON b.Id = p.Id
    GROUP BY b.Id, b.Url
    HAVING COUNT(p.Id) > 1
)
```
Тепер сутність BlogWithMultiplePosts можна зіставити з цією функцією наступним чином:

```cs
        modelBuilder.Entity<BlogWithMultiplePosts>()
            .HasNoKey()
            .ToFunction("BlogsWithMultiplePosts");
```
Щоб зіставити сутність з функцією, що повертає табличні значення, функція повинна бути без параметрів.

Зазвичай властивості сутності будуть зіставлені з відповідними стовпцями, що повертаються TVF. Якщо стовпці, що повертаються TVF, мають інші назви, ніж властивість сутності, то стовпці сутності можна налаштувати за допомогою методу HasColumnName, як і під час зіставлення зі звичайною таблицею.

Коли тип сутності зіставляється з функцією, що повертає табличні значення, запит:

```cs
var query = from b in context.Set<BlogWithMultiplePosts>()
            where b.PostCount > 3
            select new { b.Url, b.PostCount };
```
Створює наступний SQL-код:

```sql
SELECT [b].[Url], [b].[PostCount]
FROM [dbo].[BlogsWithMultiplePosts]() AS [b]
WHERE [b].[PostCount] > 3
```

## Коментарі до таблиці

Ви можете встановити довільний текстовий коментар, який буде встановлено до таблиці бази даних, що дозволить вам задокументувати вашу схему в базі даних:

```cs
[Comment("Blogs managed on the website")]
public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
}
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>().ToTable(
        tableBuilder => tableBuilder.HasComment("Blogs managed on the website"));
}
```

## Типи сутностей спільного типу

Типи сутностей, які використовують один і той самий тип CLR, відомі як типи сутностей спільного типу. Ці типи сутностей потрібно налаштувати з унікальним ім'ям, яке необхідно вказувати щоразу, коли використовується тип сутності спільного типу, на додаток до типу CLR. Це означає, що відповідна властивість DbSet має бути реалізована за допомогою виклику Set.

```cs
internal class MyContext : DbContext
{
    public DbSet<Dictionary<string, object>> Blogs => Set<Dictionary<string, object>>("Blog");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.SharedTypeEntity<Dictionary<string, object>>(
            "Blog", bb =>
            {
                bb.Property<int>("BlogId");
                bb.Property<string>("Url");
                bb.Property<DateTime>("LastUpdated");
            });
    }
}
```