# Тіньові властивості та властивості індексатора

Тіньові властивості – це властивості, які не визначені у вашому класі сутностей .NET, але визначені для цього типу сутностей у моделі EF Core. Значення та стан цих властивостей зберігаються виключно у відстежувачі змін (Change Tracker). Тіньові властивості корисні, коли в базі даних є дані, які не повинні бути доступні для відображених типів сутностей.

Властивості індексатора – це властивості типу сутності, які підтримуються індексатором у класі сутності .NET. Доступ до них можна отримати за допомогою індексатора на екземплярах класу .NET. Це також дозволяє додавати додаткові властивості до типу сутності без зміни класу CLR.

## Тіньові властивості зовнішнього ключа

Тіньові властивості найчастіше використовуються для властивостей зовнішнього ключа, де вони додаються до моделі за домовленістю, коли жодна властивість зовнішнього ключа не була знайдена за домовленістю або налаштована явно. Зв'язок представлений властивостями навігації, але в базі даних він забезпечується обмеженням зовнішнього ключа, а значення стовпця зовнішнього ключа зберігається у відповідній тіньовій властивості.

Властивість матиме назву \<navigation property name\>\<principal key property name\> (для найменування використовується навігація на залежній сутності, яка вказує на головну сутність). Якщо назва властивості головного ключа починається з назви властивості навігації, то назва буде просто \<principal key property name\>. Якщо для залежної сутності немає властивості навігації, то замість неї використовується ім'я основного типу, об'єднане з ім'ям властивості основного або альтернативного ключа \<principal type name\>\<principal key property name\>.

Наприклад, наступний код призведе до додавання властивості BlogId до сутності Post:

```cs
internal class MyContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
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

    // Since there is no CLR property which holds the foreign
    // key for this relationship, a shadow property is created.
    public Blog Blog { get; set; }
}
```
```cs
static void ShowModelPost()
{
    var context = new ApplicationDbContextFactory().CreateDbContext(null);
    Console.WriteLine(context.Model.FindEntityType(typeof(Post))?.ToDebugString());
}
ShowModelPost();
```
```
EntityType: Post
  Properties:
    Id (int) Required PK AfterSave:Throw ValueGenerated.OnAdd
    BlogId (no field, int) Shadow Required FK Index
    Content (string) Required
    Title (string) Required
  Navigations:
    Blog (Blog) ToPrincipal Blog Inverse: Posts
  Keys:
    Id PK
  Foreign keys:
    Post {'BlogId'} -> Blog {'Id'} Required Cascade ToDependent: Posts ToPrincipal: Blog
  Indexes:
    BlogId
```

## Налаштування тіньових властивостей.

Ви можете використовувати Fluent API для налаштування тіньових властивостей. Після виклику перевантаження рядка Property\<TProperty\>(String) можна об'єднати в ланцюжок будь-які виклики конфігурації, які ви використовували б для інших властивостей. У наступному прикладі, оскільки Blog не має властивості CLR з назвою LastUpdated, створюється тіньова властивість:

```cs
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>()
            .Property<DateTime>("LastUpdated");
    }
```
```
EntityType: Blog
  Properties:
    Id (int) Required PK AfterSave:Throw ValueGenerated.OnAdd
    LastUpdated (no field, DateTime) Shadow Required
    Url (string) Required
  Navigations:
    Posts (List<Post>) Collection ToDependent Post Inverse: Blog
  Keys:
    Id PK
```
Якщо ім'я, передане методу Property, збігається з ім'ям існуючої властивості (тіньової властивості або властивості, визначеної в класі сутності), то код налаштує цю існуючу властивість, а не введе нову тіньову властивість.

## Доступ до тіньових властивостей.

Значення тіньових властивостей можна отримати та змінити через API ChangeTracker:

```cs
    context.Entry(myBlog).Property("LastUpdated").CurrentValue = DateTime.Now;
```
На властивості тіней можна посилатися в запитах LINQ через статичний метод EF.Property:

```cs
var blogs = context.Blogs
    .OrderBy(b => EF.Property<DateTime>(b, "LastUpdated"));
```
До тіньових властивостей не можна отримати доступ після запиту без відстеження, оскільки повернуті сутності не відстежуються системою відстеження змін.

## Налаштування властивостей індексатора

Ви можете використовувати Fluent API для налаштування властивостей індексатора. Після виклику методу IndexerProperty ви можете об'єднати будь-які виклики конфігурації, які ви використовували б для інших властивостей. У наступному прикладі Blog має визначений індексатор, і він буде використаний для створення властивості індексатора.

```cs
internal class MyContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>().IndexerProperty<DateTime>("LastUpdated");
    }
}

public class Blog
{
    private readonly Dictionary<string, object> _data = new Dictionary<string, object>();
    public int BlogId { get; set; }

    public object this[string key]
    {
        get => _data[key];
        set => _data[key] = value;
    }
}
```
Якщо ім’я, надане методу IndexerProperty, збігається з ім’ям наявної властивості індексатора, тоді код налаштує цю наявну властивість. Якщо тип сутності має властивість, яка підтримується властивістю класу сутності, тоді створюється виняток, оскільки доступ до властивостей індексатора можна отримати лише через індексатор.

На властивості індексатора можна посилатися в запитах LINQ через статичний метод EF.Property, як показано вище, або за допомогою властивості індексатора CLR.

## Типи сутностей пакета властивостей

Типи сутностей, що містять лише властивості індексатора, відомі як типи сутностей пакета властивостей. Ці типи сутностей не мають тіньових властивостей, і EF замість них створює властивості індексатора. Наразі як тип сутності пакета властивостей підтримується лише Dictionary\<string, object\>. Він має бути налаштований як тип сутності спільного типу з унікальним ім'ям, а відповідна властивість DbSet має бути реалізована за допомогою виклику Set.

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