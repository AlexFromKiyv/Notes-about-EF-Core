# Каскадне видалення

Entity Framework Core (EF Core) представляє зв'язки за допомогою зовнішніх ключів. Сутність із зовнішнім ключем є дочірньою або залежною сутністю у зв'язку. Значення зовнішнього ключа цієї сутності має збігатися зі значенням первинного ключа (або альтернативним значенням ключа) пов'язаної головної/батьківської сутності.

Якщо основну/батьківську сутність видалити, значення зовнішніх ключів залежних/дочірніх об'єктів більше не збігатимуться з первинним або альтернативним ключем будь-якого основного/батьківського об'єкта. Це недійсний стан, який призведе до порушення посилальних обмежень у більшості баз даних.

Існує два варіанти уникнути цього порушення посилального обмеження:

1. Встановіти значення FK на null
2. Видаліти залежні/дочірні об'єкти

Перший варіант дійсний лише для необов'язкових зв'язків, де властивість зовнішнього ключа (і стовпець бази даних, на який вона зіставлена) має бути null-допустимим.

Другий варіант дійсний для будь-якого типу зв'язку та відомий як «каскадне видалення».

# Коли відбувається каскадна поведінка

Каскадні видалення потрібні, коли залежний/дочірній об'єкт більше не може бути пов'язаний з його поточним принципалом/батьківським об'єктом. Це може статися, тому що принципал/батьківський об'єкт видалено, або це може статися, коли принципал/батьківський об'єкт все ще існує, але залежний/дочірній об'єкт більше не пов'язаний з ним.

## Видалення принципала/батьківського елемента

Розглянемо цю просту модель, де Blog є принципалом/батьківським елементом у зв'язку з Post, який є залежним/дочірнім елементом. Post.BlogId – це властивість зовнішнього ключа, значення якої має збігатися з первинним ключем Blog.Id блогу, до якого належить публікація.

```cs
public class Blog
{
    public int Id { get; set; }

    public string Name { get; set; }

    public IList<Post> Posts { get; set; } = new List<Post>();
}

public class Post
{
    public int Id { get; set; }

    public string Title { get; set; }
    public string Content { get; set; }

    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}
```
```cs
static async Task PopulateDatabase(ApplicationDbContext context) 
{
    context.Blogs.Add(new Blog
    {
        Name = "My Blog",
        Posts = new List<Post>
        {
            new Post { Title = "My First Post", Content = "This is my first post." },
            new Post { Title = "My Second Post", Content = "This is my second post." }
        }
    });
    context.Blogs.Add(new Blog { 
        Name = "My Second Blog",
        Posts = new List<Post>
        {
            new Post { Title = "Another Post", Content = "This is another post." }
        }
    });

    var countAdded = await context.SaveChangesAsync();
    Console.WriteLine(countAdded);
}

```

За домовленістю, цей зв'язок налаштовано як обов'язковий, оскільки властивість зовнішнього ключа Post.BlogId не може мати значення null. Обов'язкові зв'язки налаштовані на використання каскадних видалень за замовчуванням. Див. розділ "Зв'язки" для отримання додаткової інформації про моделювання зв'язків.

Під час видалення блогу всі публікації видаляються каскадно.

```cs
static async Task DeletePrincipial(ApplicationDbContext context)
{
    Blog blog = await context.Blogs.OrderBy(e => e.Name).Include(e => e.Posts).FirstAsync();

    context.Blogs.Remove(blog);

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}
```
```
3
```
Команда SaveChanges генерує наступний SQL-код, використовуючи SQL Server як приклад:

```sql
-- Executed DbCommand (1ms) [Parameters=[@p0='1'], CommandType='Text', CommandTimeout='30']
SET NOCOUNT ON;
DELETE FROM [Posts]
WHERE [Id] = @p0;
SELECT @@ROWCOUNT;

-- Executed DbCommand (0ms) [Parameters=[@p0='2'], CommandType='Text', CommandTimeout='30']
SET NOCOUNT ON;
DELETE FROM [Posts]
WHERE [Id] = @p0;
SELECT @@ROWCOUNT;

-- Executed DbCommand (2ms) [Parameters=[@p1='1'], CommandType='Text', CommandTimeout='30']
SET NOCOUNT ON;
DELETE FROM [Blogs]
WHERE [Id] = @p1;
SELECT @@ROWCOUNT;
```

## Розрив зв'язку

Замість видалення блогу, ми могли б розірвати зв'язок між кожною публікацією та її блогом. Це можна зробити, встановивши для навігації посилань Post.Blog значення null для кожного допису:

```cs
static async Task SeveringARelationship1(ApplicationDbContext context)
{
    Blog blog = await context.Blogs.OrderBy(e => e.Name).Include(e => e.Posts).FirstAsync();

    foreach(var post in blog.Posts)
    {
        post.Blog = null;
    }

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}
```
```
2
```
Зв’язок також можна розірвати, видаливши кожну публікацію з навігації колекції Blog.Posts:

```cs
static async Task SeveringARelationship2(ApplicationDbContext context)
{
    Blog blog = await context.Blogs.OrderBy(e => e.Name).Include(e => e.Posts).FirstAsync();

    blog.Posts.Clear();

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}
```
```
2
```
В обох випадках результат однаковий: блог не видаляється, але видаляються публікації, які більше не пов’язані з жодним блогом:

```sql
-- Executed DbCommand (1ms) [Parameters=[@p0='1'], CommandType='Text', CommandTimeout='30']
SET NOCOUNT ON;
DELETE FROM [Posts]
WHERE [Id] = @p0;
SELECT @@ROWCOUNT;

-- Executed DbCommand (0ms) [Parameters=[@p0='2'], CommandType='Text', CommandTimeout='30']
SET NOCOUNT ON;
DELETE FROM [Posts]
WHERE [Id] = @p0;
SELECT @@ROWCOUNT;
```
Видалення сутностей, які більше не пов'язані з жодним принципалом/залежним, називається «видаленням сиріт».

Порада

Каскадне видалення та видалення сиріт тісно пов'язані. Обидва варіанти призводять до видалення залежних/дочірніх сутностей, коли зв'язок з необхідним їм головним/батьківським об'єктом розривається. Для каскадного видалення це розділення відбувається тому, що головний/батьківський об'єкт сам видаляється. Для осиротілих об'єктів головна/батьківська сутність все ще існує, але більше не пов'язана із залежними/дочірніми сутностями.

# Де відбувається каскадна поведінка

Каскадна поведінка може бути застосована до:

* Сутності, що відстежуються поточним DbContext
* Сутності в базі даних, які не були завантажені в контекст

## Каскадне видалення відстежуваних сутностей

EF Core завжди застосовує налаштовані каскадні поведінки до відстежуваних сутностей. Це означає, що якщо програма завантажує всі відповідні залежні/дочірні сутності в DbContext, як показано у наведених вище прикладах, то каскадна поведінка буде застосована правильно незалежно від того, як налаштовано базу даних.

Порада

Точний час каскадної поведінки відстежуваних об’єктів можна контролювати за допомогою ChangeTracker.CascadeDeleteTiming та ChangeTracker.DeleteOrphansTiming. Див. Зміна зовнішніх ключів та навігації для отримання додаткової інформації.

## Каскадне видалення в базі даних

Багато систем баз даних також пропонують каскадні поведінки, які спрацьовують, коли сутність видаляється в базі даних. EF Core налаштовує ці моделі поведінки на основі каскадного видалення в моделі EF Core, коли база даних створюється за допомогою EnsureCreated або міграцій EF Core. Наприклад, використовуючи наведену вище модель, для публікацій під час використання SQL Server створюється наступна таблиця:

```sql
CREATE TABLE [Posts] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NULL,
    [Content] nvarchar(max) NULL,
    [BlogId] int NOT NULL,
    CONSTRAINT [PK_Posts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Posts_Blogs_BlogId] FOREIGN KEY ([BlogId]) REFERENCES [Blogs] ([Id]) ON DELETE CASCADE
);
```
Зверніть увагу, що обмеження зовнішнього ключа, яке визначає зв'язок між блогами та публікаціями, налаштовано на ON DELETE CASCADE.

Якщо ми знаємо, що база даних налаштована таким чином, то ми можемо видалити блог без попереднього завантаження публікацій, і база даних подбає про видалення всіх публікацій, пов’язаних із цим блогом.

```cs
static async Task CascadeDeleteInTheDatabase(ApplicationDbContext context)
{
    Blog blog = await context.Blogs.OrderBy(e => e.Name).FirstAsync();
    
    context.Blogs.Remove(blog);

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}
```
```
1
```
Зверніть увагу, що для Post немає опції Include, тому вони не завантажуються. У цьому випадку команда SaveChanges видалить лише блог, оскільки це єдина відстежувана сутність:

```sql
-- Executed DbCommand (6ms) [Parameters=[@p0='1'], CommandType='Text', CommandTimeout='30']
SET NOCOUNT ON;
DELETE FROM [Blogs]
WHERE [Id] = @p0;
SELECT @@ROWCOUNT;
```
Це призведе до винятку, якщо обмеження зовнішнього ключа в базі даних не налаштовано для каскадного видалення. Однак у цьому випадку записи видаляються базою даних, оскільки під час створення бази даних було налаштовано на ON DELETE CASCADE.

    Примітка

    Бази даних зазвичай не мають можливості автоматично видаляти сироти. Це пояснюється тим, що хоча EF Core представляє зв'язки, використовуючи навігації, а також зовнішні ключі, бази даних мають лише зовнішні ключі та не мають навігації. Це означає, що зазвичай неможливо розірвати зв'язок, не завантажуючи обидві сторони в DbContext.

    Примітка

    База даних EF Core, що зберігається в пам'яті, наразі не підтримує каскадні видалення в базі даних.

    Попередження

    Не налаштовуйте каскадне видалення в базі даних під час м’якого видалення об’єктів. Це може призвести до випадкового фактичного видалення об’єктів, а не їхнього м’якого видалення.

# Обмеження на каскадну поведінку базою даних

Деякі бази даних, зокрема SQL Server, мають обмеження на каскадну поведінку, яка формує цикли. Наприклад, розглянемо таку модель:

```cs
public class Blog
{
    public int Id { get; set; }
    public string Name { get; set; }

    public IList<Post> Posts { get; } = new List<Post>();

    public int OwnerId { get; set; }
    public Person Owner { get; set; }
}

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int BlogId { get; set; }
    public Blog Blog { get; set; }

    public int AuthorId { get; set; }
    public Person Author { get; set; }
}

public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }

    public IList<Post> Posts { get; } = new List<Post>();

    public Blog OwnedBlog { get; set; }
}
```

Ця модель має три зв'язки, всі з яких обов'язкові та тому налаштовані на каскадне видалення за домовленістю:

* Видалення блогу призведе до каскадного видалення всіх пов'язаних публікацій
* Видалення автора публікацій призведе до каскадного видалення цих публікацій
* Видалення власника блогу призведе до каскадного видалення блогу

Це все розумно (хоча й трохи драконівськи в політиках управління блогами!), але спроба створити базу даних SQL Server з цими налаштованими каскадами призводить до наступного винятку:

```
Microsoft.Data.SqlClient.SqlException (0x80131904): Introducing FOREIGN KEY constraint 'FK_Posts_Person_AuthorId' on table 'Posts' may cause cycles or multiple cascade paths. Specify ON DELETE NO ACTION or ON UPDATE NO ACTION, or modify other FOREIGN KEY constraints.
```

Існує два способи вирішення цієї ситуації:

1. Змініть один або декілька зв'язків, щоб вони не видалялися каскадно.
2. Налаштуйте базу даних без одного або кількох із цих каскадних видалень, а потім переконайтеся, що всі залежні сутності завантажено, щоб EF Core міг виконувати каскадну поведінку.

Використовуючи перший підхід у нашому прикладі, ми могли б зробити зв'язок post-blog необов'язковим, надавши йому властивість зовнішнього ключа, що може мати значення null:

```cs
    public int? BlogId { get; set; }
    public Blog? Blog { get; set; }
```
Необов’язковий зв’язок дозволяє публікації існувати без блогу, а це означає, що каскадне видалення більше не буде налаштовано за замовчуванням. Це означає, що більше немає циклу в каскадних діях, і базу даних можна створити без помилок на SQL Server.

```sql
CREATE TABLE [dbo].[Posts] (
    [Id]       INT            IDENTITY (1, 1) NOT NULL,
    [Title]    NVARCHAR (MAX) NOT NULL,
    [Content]  NVARCHAR (MAX) NOT NULL,
    [BlogId]   INT            NULL,
    [AuthorId] INT            NOT NULL,
    CONSTRAINT [PK_Posts] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Posts_Blogs_BlogId] FOREIGN KEY ([BlogId]) REFERENCES [dbo].[Blogs] ([Id]),
    CONSTRAINT [FK_Posts_People_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [dbo].[People] ([Id]) ON DELETE CASCADE
);
```
Використовуючи другий підхід, ми можемо залишити зв'язок blog-owner обов'язковим та налаштованим для каскадного видалення, але зробити так, щоб ця конфігурація застосовувалася лише до відстежуваних сутностей, а не до бази даних: це означає, що більше немає циклу в каскадних діях, і базу даних можна створювати без помилок на SQL Server.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder
        .Entity<Blog>()
        .HasOne(e => e.Owner)
        .WithOne(e => e.OwnedBlog)
        .OnDelete(DeleteBehavior.ClientCascade);
}
```
В таблиці Blog обмеження:

```sql
    CONSTRAINT [FK_Blogs_People_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [dbo].[People] ([Id]) ON DELETE CASCADE
```

міняеться на 

```sql
    CONSTRAINT [FK_Blogs_People_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [dbo].[People] ([Id])
```
Додамо данні
```cs
static async Task PopulateDatabase(ApplicationDbContext context)
{
    Person person1 = new Person { Name = "Alex" };
    context.People.Add(person1);
    Person person2 = new Person { Name = "Julia" };
    context.People.Add(person2);

    context.Blogs.Add(new Blog
    {
        Name = "My Blog",
        Owner = person1,
        Posts = new List<Post>
            {
                new Post { Title = "My First Post", Content = "This is my first post.", Author = person2 },
                new Post { Title = "My Second Post", Content = "This is my second post.", Author = person1 }
            }
    });
    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}
```
Що станеться, якщо ми завантажимо і людину, і блог, яким вона володіє, а потім видалимо цю людину?
```cs
static async Task DeleteAuthor(ApplicationDbContext context)
{
    Person owner = await context.People.SingleAsync(p => p.Name == "Alex");
    Blog blog = await context.Blogs.SingleAsync( b=>b.Owner == owner);

    context.Remove(owner);

    var count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}
```
```
2
```
EF Core каскадно видалятиме власника, щоб блог також було видалено. А база даних видалить публікації:

```sql
-- Executed DbCommand (8ms) [Parameters=[@p0='1'], CommandType='Text', CommandTimeout='30']
SET NOCOUNT ON;
DELETE FROM [Blogs]
WHERE [Id] = @p0;
SELECT @@ROWCOUNT;

-- Executed DbCommand (2ms) [Parameters=[@p1='1'], CommandType='Text', CommandTimeout='30']
SET NOCOUNT ON;
DELETE FROM [People]
WHERE [Id] = @p1;
SELECT @@ROWCOUNT;
```
Однак, якщо блог не тідстежується контекстом коли видаляється власник:

```cs
    context.ChangeTracker.Clear();
    await DeleteAuthor(context);

static async Task DeleteAuthor(ApplicationDbContext context)
{
    Person owner = await context.People.SingleAsync(p => p.Name == "Alex");
    //Blog blog = await context.Blogs.SingleAsync( b=>b.Owner == owner);

    context.Remove(owner);

    var count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}
```
Тоді буде викинуто виняток через порушення обмеження зовнішнього ключа в базі даних:

```
Microsoft.Data.SqlClient.SqlException: The DELETE statement conflicted with the REFERENCE constraint "FK_Blogs_People_OwnerId". The conflict occurred in database "Scratch", table "dbo.Blogs", column 'OwnerId'. The statement has been terminated.
```

# Каскадування null-значень

Необов'язкові зв'язки мають властивості зовнішнього ключа, що допускають значення null, зіставлені зі стовпцями бази даних, що допускають значення null. Це означає, що значення зовнішнього ключа може бути встановлено на null, коли поточний принципал/батьківський об'єкт видаляється або відокремлюється від залежного/дочірнього об'єкта.

Давайте ще раз розглянемо приклади з розділу «Коли трапляється каскадна поведінка», але цього разу з необов'язковим зв'язком, представленим властивістю зовнішнього ключа Post.BlogId, яка може мати значення null:

```cs
public int? BlogId { get; set; }
```
Ця властивість зовнішнього ключа буде встановлена ​​на значення null для кожної публікації, коли пов’язаний з нею блог буде видалено. Наприклад, цей код, який такий самий, як і раніше:

```cs
static async Task DeletePrincipial(ApplicationDbContext context)
{
    Blog? blog = await context.Blogs.OrderBy(e => e.Name).Include(e => e.Posts).FirstAsync();

    context.Remove(blog);

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}
```
Тепер виклик SaveChanges призведе до таких оновлень бази даних:

```sql
-- Executed DbCommand (2ms) [Parameters=[@p1='1', @p0=NULL (DbType = Int32)], CommandType='Text', CommandTimeout='30']
SET NOCOUNT ON;
UPDATE [Posts] SET [BlogId] = @p0
WHERE [Id] = @p1;
SELECT @@ROWCOUNT;

-- Executed DbCommand (0ms) [Parameters=[@p1='2', @p0=NULL (DbType = Int32)], CommandType='Text', CommandTimeout='30']
SET NOCOUNT ON;
UPDATE [Posts] SET [BlogId] = @p0
WHERE [Id] = @p1;
SELECT @@ROWCOUNT;

-- Executed DbCommand (1ms) [Parameters=[@p2='1'], CommandType='Text', CommandTimeout='30']
SET NOCOUNT ON;
DELETE FROM [Blogs]
WHERE [Id] = @p2;
SELECT @@ROWCOUNT;
```

Аналогічно, якщо стосунки розірвано, використовуючи будь-який із наведених вище прикладів:

```cs
static async Task SeveringARelationship1(ApplicationDbContext context)
{
    Blog blog = await context.Blogs.OrderBy(e => e.Name).Include(e => e.Posts).FirstAsync();

    foreach (var post in blog.Posts)
    {
        post.Blog = null;
    }

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}

static async Task SeveringARelationship2(ApplicationDbContext context)
{
    Blog blog = await context.Blogs.OrderBy(e => e.Name).Include(e => e.Posts).FirstAsync();

    blog.Posts.Clear();

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}

```
Потім, коли викликається SaveChanges, записи оновлюються значеннями зовнішнього ключа null:

```sql
-- Executed DbCommand (2ms) [Parameters=[@p1='1', @p0=NULL (DbType = Int32)], CommandType='Text', CommandTimeout='30']
SET NOCOUNT ON;
UPDATE [Posts] SET [BlogId] = @p0
WHERE [Id] = @p1;
SELECT @@ROWCOUNT;

-- Executed DbCommand (0ms) [Parameters=[@p1='2', @p0=NULL (DbType = Int32)], CommandType='Text', CommandTimeout='30']
SET NOCOUNT ON;
UPDATE [Posts] SET [BlogId] = @p0
WHERE [Id] = @p1;
SELECT @@ROWCOUNT;
```
Див. статтю Зміна зовнішніх ключів та навігацій для отримання додаткових відомостей про те, як EF Core керує зовнішніми ключами та навігаціями під час зміни їхніх значень.

    Примітка

    Виправлення таких зв'язків було поведінкою за замовчуванням Entity Framework з першої версії у 2008 році. До EF Core воно не мало назви та не могло бути змінено. Тепер воно відоме як ClientSetNull, як описано в наступному розділі.

Бази даних також можна налаштувати на каскадування нульових значень, подібних до цього, коли видаляється принципал/батьківський об'єкт у необов'язковому зв'язку. Однак це трапляється набагато рідше, ніж використання каскадних видалень у базі даних. Використання каскадних видалень та каскадних нульових значень у базі даних одночасно майже завжди призведе до циклів зв'язків під час використання SQL Server. Дивіться наступний розділ для отримання додаткової інформації про налаштування каскадних нульових значень.

# Налаштування каскадних поведінок

    Порада

    Обов’язково прочитайте розділи вище, перш ніж переходити сюди. Налаштування каскадних поведінок, ймовірно, не матимуть сенсу, якщо ви не зрозумієте попередній матеріал.

Каскадна поведінка налаштовується для кожного зв'язку за допомогою методу OnDelete в OnModelCreating. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder
        .Entity<Blog>()
        .HasOne(e => e.Owner)
        .WithOne(e => e.OwnedBlog)
        .OnDelete(DeleteBehavior.ClientCascade);
}
```
Див. розділ «Зв’язки» для отримання додаткової інформації про налаштування зв’язків між типами сутностей.

OnDelete приймає значення з переліку DeleteBehavior, що, як визнаємо, є заплутаним. Цей перелік визначає як поведінку EF Core для відстежуваних сутностей, так і конфігурацію каскадного видалення в базі даних, коли EF використовується для створення схеми.

## Вплив на схему бази даних

У наступній таблиці показано результат кожного значення OnDelete для обмеження зовнішнього ключа, створеного міграціями EF Core або EnsureCreated.

|DeleteBehavior|Вплив на схему бази даних|
|--------------|-------------------------|
|Cascade|ON DELETE CASCADE|
|Restrict|ON DELETE RESTRICT|
|NoAction|database default|
|SetNull|ON DELETE SET NULL|
|ClientSetNull|database default|
|ClientCascade|database default|
|ClientNoAction|database default|

Поведінка ON DELETE NO ACTION (значення бази даних за замовчуванням) та ON DELETE RESTRICT у реляційних базах даних зазвичай ідентична або дуже схожа. Незважаючи на те, що може означати NO ACTION (БЕЗ ДІЇ), обидва ці варіанти призводять до застосування посилальних обмежень. Різниця, якщо така є, полягає в перевірці обмежень базою даних. Перегляньте документацію до бази даних, щоб дізнатися про конкретні відмінності між ON DELETE NO ACTION та ON DELETE RESTRICT у вашій системі баз даних.

SQL Server не підтримує ON DELETE RESTRICT, тому замість цього використовується ON DELETE NO ACTION. Єдині значення, які призведуть до каскадної поведінки в базі даних, це Cascade та SetNull. Усі інші значення налаштують базу даних так, щоб вона не каскадувала жодних змін.

## Вплив на поведінку функції SaveChanges

У таблицях у наступних розділах описано, що відбувається із залежними/дочірніми сутностями, коли головний/батьківський об'єкт видаляється або його зв'язок із залежними/дочірніми сутностями розривається. Кожна таблиця охоплює одне з:

* Необов'язкові (FK, що допускає нульове значення) та обов'язкові (FK, що не допускає нульове значення) зв'язки
* Коли залежні/дочірні елементи завантажуються та відстежуються DbContext і коли вони існують лише в базі даних

### Обов'язковий зв'язок із завантаженими dependents/children.

|DeleteBehavior|При видаленні principal/parent | Про від'єднання від principal/parent|
|--------------|-------------------------------|-------------------------------------|
|Cascade|Залежні, видалені EF Core|Залежні, видалені EF Core|
|Restrict|InvalidOperationException|InvalidOperationException|
|NoAction|InvalidOperationException|InvalidOperationException|
|SetNull|SqlException під час створення бази даних|SqlException під час створення бази даних|
|ClientSetNull|InvalidOperationException|InvalidOperationException|
|ClientCascade|Dependents deleted by EF Core|Dependents deleted by EF Core|
|ClientNoAction|DbUpdateException|InvalidOperationException|

Примітки:

* За замовчуванням для таких обов'язкових зв'язків використовується Cascade.
* Використання будь-чого, крім каскадного видалення, для обов'язкових зв'язків призведе до винятку під час виклику SaveChanges.
    * Зазвичай це виняток InvalidOperationException з EF Core, оскільки недійсний стан виявлено в завантажених дочірніх/залежних елементах.
    * ClientNoAction змушує EF Core не перевіряти залежні елементи виправлення перед їх надсиланням до бази даних, тому в цьому випадку база даних викидає виняток, який потім обгортається в DbUpdateException методом SaveChanges.
    * SetNull відхиляється під час створення бази даних, оскільки стовпець зовнішнього ключа не може мати значення null.
Оскільки залежні/дочірні елементи завантажуються, вони завжди видаляються EF Core і ніколи не залишаються для видалення базою даних.

### Обов'язковий зв'язок із не завантаженими dependents/children.

|DeleteBehavior|При видаленні principal/parent | Про від'єднання від principal/parent|
|--------------|-------------------------------|-------------------------------------|
|Cascade|Залежні видалені базою даних|N/A|
|Restrict|DbUpdateException|N/A|
|NoAction|DbUpdateException|N/A|
|SetNull|SqlException під час створення бази даних|N/A|
|ClientSetNull|DbUpdateException|N/A|
|ClientCascade|DbUpdateException|N/A|
|ClientNoAction|DbUpdateException|N/A|

Примітки:

* Розрив стосунків тут недійсний, оскільки dependents/children не завантажуються.
* За замовчуванням для таких обов'язкових зв'язків використовується Cascade.
* Використання будь-чого, крім каскадного видалення, для обов'язкових зв'язків призведе до винятку під час виклику SaveChanges.
    * Зазвичай це виняток DbUpdateException, оскільки залежні/дочірні елементи не завантажуються, і тому недійсний стан може бути виявлений лише базою даних. Потім SaveChanges обгортає виняток бази даних у виняток DbUpdateException.
    * SetNull відхиляється під час створення бази даних, оскільки стовпець зовнішнього ключа не може мати значення null.

### Необов'язковий зв'язок із завантаженими dependents/children.

|DeleteBehavior|При видаленні principal/parent | Про від'єднання від principal/parent|
|--------------|-------------------------------|-------------------------------------|
|Cascade|Залежні, видалені EF Core|Залежні, видалені EF Core|
|Restrict|Залежні FK, встановлені EF Core на null|Залежні FK, встановлені EF Core на null|
|NoAction|Залежні FK, встановлені EF Core на null|Залежні FK, встановлені EF Core на null|
|SetNull|Залежні FK, встановлені EF Core на null|Залежні FK, встановлені EF Core на null|
|ClientSetNull|Залежні FK, встановлені EF Core на null|Залежні FK, встановлені EF Core на null|
|ClientCascade|Залежні FK, встановлені EF Core на null|Залежні FK, встановлені EF Core на null|
|ClientNoAction|DbUpdateException|Залежні FK, встановлені EF Core на null|

Примітки:

* Значення за замовчуванням для таких необов'язкових зв'язків – ClientSetNull. 
* Залежні/дочірні елементи ніколи не видаляються, якщо не налаштовано Cascade або ClientCascade.
* Усі інші значення призводять до того, що залежні FK встановлюються в значення null EF Core...
    * окрім ClientNoAction, який повідомляє EF Core не торкатися зовнішніх ключів залежних/дочірніх об'єктів під час видалення головних/батьківських об'єктів. Тому база даних викидає виняток, який SaveChanges обгортає як DbUpdateException.

### Необов'язковий зв'язок із не завантаженими dependents/children.

|DeleteBehavior|При видаленні principal/parent | Про від'єднання від principal/parent|
|--------------|-------------------------------|-------------------------------------|
|Cascade|Залежні видалені базою даних|N/A|
|Restrict|DbUpdateException|N/A|
|NoAction|DbUpdateException|N/A|
|SetNull|Залежні FK встановлені базою даних на null|N/A|
|ClientSetNull|DbUpdateException|N/A|
|ClientCascade|DbUpdateException|N/A|
|ClientNoAction|DbUpdateException|N/A|

Примітки:

* Розрив стосунків тут недійсний, оскільки dependents/children не завантажуються.
* Значення за замовчуванням для таких необов'язкових зв'язків — ClientSetNull.
* Залежні/дочірні елементи необхідно завантажити, щоб уникнути винятку бази даних, якщо база даних не налаштована на каскадування видалень або нулів.