# Створення каркасів (зворотне проектування)

Зворотне проектування – це процес створення каркасів класів типів сутностей та класу DbContext на основі схеми існуючої бази даних. Це можна виконати за допомогою команди dotnet ef dbcontext scaffold з інструментів інтерфейсу командного рядка (CLI) .NET.

## Необхідні умови

* Перед створенням каркасу вам потрібно буде встановити або інструменти PMC, які працюють лише у Visual Studio, або інструменти .NET CLI, які працюють на всіх платформах, що підтримуються .NET.
* Встановіть пакет NuGet для Microsoft.EntityFrameworkCore.Design у проекті, до якого ви створюєте каркас.
* Інсталюйте пакет NuGet для постачальника бази даних, який орієнтований на схему бази даних, з якої потрібно створити каркас.

## Обов’язкові аргументи

Команди PMC та .NET CLI мають два обов’язкові аргументи: рядок підключення до бази даних та постачальника бази даних EF Core, який потрібно використовувати.

Першим аргументом команди є рядок підключення до бази даних. Інструменти використовують цей рядок підключення для зчитування схеми бази даних.

Спосіб взяття рядка підключення в лапки та екранування залежить від оболонки, яка використовується для виконання команди. Зверніться до документації оболонки. Наприклад, PowerShell вимагає екранування $, але не \\.

У наступному прикладі створюється каркас типів сутностей та DbContext з бази даних Chinook, розташованої на екземплярі SQL Server LocalDB комп'ютера, використовуючи постачальника бази даних Microsoft.EntityFrameworkCore.SqlServer.

```
dotnet ef dbcontext scaffold "Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=Chinook" Microsoft.EntityFrameworkCore.SqlServer
```

## Рядки підключення в коді scaffolding

За замовчуванням scaffolder включатиме рядок підключення в код scaffolding, але з попередженням.

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
    => optionsBuilder.UseSqlServer("Data Source=(LocalDb)\\MSSQLLocalDB;Database=AllTogetherNow");
```
Це зроблено для того, щоб згенерований код не аварійно завершував роботу під час першого використання, що було б дуже неефективним для навчання. Однак, як зазначено в попередженні, рядки підключення не повинні існувати у виробничому коді. Див. розділ «Час життя, конфігурація та ініціалізація DbContext», щоб дізнатися про різні способи керування рядками підключення.

Для придушення створення методу OnConfiguring, що містить рядок підключення, можна передати параметр --no-onconfiguring (.NET CLI).

## Ім'я постачальника

Другий аргумент – це ім'я постачальника. Ім'я постачальника зазвичай збігається з ім'ям пакета NuGet постачальника. Наприклад, для SQL Server або Azure SQL використовуйте Microsoft.EntityFrameworkCore.SqlServer.

## Параметри командного рядка

Процесом створення каркаса можна керувати за допомогою різних параметрів командного рядка.

### Визначення таблиць та представлень

За замовчуванням усі таблиці та представлення в схемі бази даних риштуються в типи сутностей. 

Ви можете обмежити, які таблиці та представлення будуть сформовані, вказавши схеми та таблиці. Аргумент --schema (.NET CLI) визначає схеми таблиць та представлень, для яких будуть згенеровані типи сутностей. Якщо цей аргумент пропущено, то всі схеми будуть включені. Якщо використовується ця опція, то всі таблиці та представлення у схемах будуть включені до моделі, навіть якщо вони не включені явно за допомогою --table.

Аргумент --table (.NET CLI) визначає таблиці та представлення, для яких будуть створені типи сутностей. Таблиці або подання в певній схемі можна включити за допомогою формату 'schema.table' або 'schema.view'. Якщо цей параметр пропущено, то включаються всі.

Наприклад, щоб створити каркас лише для таблиць Artists та Albums:

```
dotnet ef dbcontext scaffold ... --table Artist --table Album
```

Щоб створити каркас усіх таблиць та представлень зі схем Customer та Contractor:

```
dotnet ef dbcontext scaffold ... --schema Customer --schema Contractor
```
Наприклад, щоб створити каркас таблиці Purchases зі схеми Customer та таблиць Accounts та Contracts зі схеми  Contractor:

```
dotnet ef dbcontext scaffold ... --table Customer.Purchases --table Contractor.Accounts --table Contractor.Contracts
```

### Збереження імен баз даних

Назви таблиць і стовпців за замовчуванням виправлено, щоб краще відповідати правилам іменування типів і властивостей .NET. Вказівка ​​параметра -UseDatabaseNames (Visual Studio PMC) або --use-database-names (.NET CLI) вимкне цю поведінку, максимально зберігаючи оригінальні імена баз даних. Недійсні ідентифікатори .NET все одно будуть виправлені, а синтезовані імена, такі як властивості навігації, все ще відповідатимуть правилам іменування .NET.

Щоб вимкнути функцію множини під час створення каркасу за допомогою зворотного проектування моделей, використовуйте --no-pluralize (.NET CLI).

Наприклад, розглянемо наступні таблиці:

```sql
CREATE TABLE [BLOGS] (
    [ID] int NOT NULL IDENTITY,
    [Blog_Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Blogs] PRIMARY KEY ([ID]));

CREATE TABLE [posts] (
    [id] int NOT NULL IDENTITY,
    [postTitle] nvarchar(max) NOT NULL,
    [post content] nvarchar(max) NOT NULL,
    [1 PublishedON] datetime2 NOT NULL,
    [2 DeletedON] datetime2 NULL,
    [BlogID] int NOT NULL,
    CONSTRAINT [PK_Posts] PRIMARY KEY ([id]),
    CONSTRAINT [FK_Posts_Blogs_BlogId] FOREIGN KEY ([BlogID]) REFERENCES [Blogs] ([ID]) ON DELETE CASCADE);
```
За замовчуванням з цих таблиць будуть сформовані такі типи сутностей:

```cs
public partial class Blog
{
    public int Id { get; set; }
    public string BlogName { get; set; } = null!;
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
}

public partial class Post
{
    public int Id { get; set; }
    public string PostTitle { get; set; } = null!;
    public string PostContent { get; set; } = null!;
    public DateTime _1PublishedOn { get; set; }
    public DateTime? _2DeletedOn { get; set; }
    public int BlogId { get; set; }
    public virtual Blog Blog { get; set; } = null!;
    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
```
Однак, використання --use-database-names призводить до таких типів сутностей:

```cs
public partial class BLOG
{
    public int ID { get; set; }
    public string Blog_Name { get; set; } = null!;
    public virtual ICollection<post> posts { get; set; } = new List<post>();
}

public partial class post
{
    public int id { get; set; }
    public string postTitle { get; set; } = null!;
    public string post_content { get; set; } = null!;
    public DateTime _1_PublishedON { get; set; }
    public DateTime? _2_DeletedON { get; set; }
    public int BlogID { get; set; }
    public virtual BLOG Blog { get; set; } = null!;
```

### Використання атрибутів зіставлення (також відомих як анотації даних)

Типи сутностей налаштовуються за замовчуванням за допомогою API ModelBuilder в OnModelCreating. Вкажіть --data-annotations (.NET CLI), щоб замість цього використовувати атрибути зіставлення, коли це можливо.

Наприклад, використання Fluent API створить ось таку структуру:

```cs
entity.Property(e => e.Title)
    .IsRequired()
    .HasMaxLength(160);
```
Під час використання анотацій даних буде створено такий каркас:

```cs
[Required]
[StringLength(160)]
public string Title { get; set; }
```
Деякі аспекти моделі неможливо налаштувати за допомогою атрибутів відображення. Скаффолдер все одно використовуватиме API побудови моделі для обробки цих випадків.

### Ім'я DbContext

Ім'я каркасного класу DbContext буде іменем бази даних із суфіксом Context за замовчуванням. Щоб вказати інший, використовуйте --context .

### Цільові каталоги та простори імен

Класи сутностей та клас DbContext інтегровані в кореневий каталог проекту та використовують простір імен проекту за замовчуванням. 

Ви можете вказати каталог, де створюються класи, за допомогою --output-dir, а --context-dir можна використовувати для створення класу DbContext в окремий каталог від класів типів сутностей:

```
dotnet ef dbcontext scaffold ... --context-dir Data --output-dir Models
```
За замовчуванням простором імен буде кореневий простір імен плюс імена будь-яких підкаталогів у кореневому каталозі проєкту. Однак, ви можете змінити простір імен для всіх вихідних класів, використовуючи --namespace. Ви також можете змінити простір імен лише для класу DbContext, використовуючи --context-namespace:

```
dotnet ef dbcontext scaffold ... --namespace Your.Namespace --context-namespace Your.DbContext.Namespace
```

## Код каркасу

Результатом створення каркасу з існуючої бази даних є:

* Файл, що містить клас, що успадковується від DbContext
* Файл для кожного типу сутності

### Типи посилань C# з можливістю використання значення null

Скаффордер може створювати моделі EF та типи сутностей, які використовують типи посилань C# з можливістю використання значення null (NRT). Використання NRT автоматично формується, коли підтримка NRT увімкнена в проекті C#, в який формується код.

Наприклад, наступна таблиця «Теги» містить як стовпці рядків, що допускають значення null, так і стовпці рядків, що не допускають значення null:

```sql
CREATE TABLE [Tags] (
  [Id] int NOT NULL IDENTITY,
  [Name] nvarchar(max) NOT NULL,
  [Description] nvarchar(max) NULL,
  CONSTRAINT [PK_Tags] PRIMARY KEY ([Id]));
```
Це призводить до відповідних властивостей рядка, що допускають значення null, та властивостей, що не допускають значення null, у згенерованому класі:

```cs
public partial class Tag
{
    public Tag()
    {
        Posts = new HashSet<Post>();
    }

    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public virtual ICollection<Post> Posts { get; set; }
}
```

Аналогічно, наступні таблиці Posts містять обов’язковий зв’язок із таблицею  Blogs:

```sql
CREATE TABLE [Posts] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NOT NULL,
    [Contents] nvarchar(max) NOT NULL,
    [PostedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NULL,
    [BlogId] int NOT NULL,
    CONSTRAINT [PK_Posts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Posts_Blogs_BlogId] FOREIGN KEY ([BlogId]) REFERENCES [Blogs] ([Id]));
```
Це призводить до створення каркасу ненульового (обов'язкового) зв'язку між blog:

```cs
public partial class Blog
{
    public Blog()
    {
        Posts = new HashSet<Post>();
    }

    public int Id { get; set; }
    public string Name { get; set; } = null!;

    public virtual ICollection<Post> Posts { get; set; }
}
```
І post:

```cs
public partial class Post
{
    public Post()
    {
        Tags = new HashSet<Tag>();
    }

    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Contents { get; set; } = null!;
    public DateTime PostedOn { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public int BlogId { get; set; }

    public virtual Blog Blog { get; set; } = null!;

    public virtual ICollection<Tag> Tags { get; set; }
}
```

## Зв'язки  many-to-many

Процес створення каркасу виявляє прості таблиці об'єднань та автоматично генерує для них зіставлення  many-to-many.Наприклад, розглянемо таблиці для публікацій та тегів, а також таблицю об'єднань PostTag, яка їх з'єднує:

```sql
CREATE TABLE [Tags] (
  [Id] int NOT NULL IDENTITY,
  [Name] nvarchar(max) NOT NULL,
  [Description] nvarchar(max) NULL,
  CONSTRAINT [PK_Tags] PRIMARY KEY ([Id]));

CREATE TABLE [Posts] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NOT NULL,
    [Contents] nvarchar(max) NOT NULL,
    [PostedOn] datetime2 NOT NULL,
    [UpdatedOn] datetime2 NULL,
    CONSTRAINT [PK_Posts] PRIMARY KEY ([Id]));

CREATE TABLE [PostTag] (
    [PostsId] int NOT NULL,
    [TagsId] int NOT NULL,
    CONSTRAINT [PK_PostTag] PRIMARY KEY ([PostsId], [TagsId]),
    CONSTRAINT [FK_PostTag_Posts_TagsId] FOREIGN KEY ([TagsId]) REFERENCES [Tags] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PostTag_Tags_PostsId] FOREIGN KEY ([PostsId]) REFERENCES [Posts] ([Id]) ON DELETE CASCADE);
```
Після створення каркасу це призводить до створення класу для Post:

```cs
public partial class Post
{
    public Post()
    {
        Tags = new HashSet<Tag>();
    }

    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Contents { get; set; } = null!;
    public DateTime PostedOn { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public int BlogId { get; set; }

    public virtual Blog Blog { get; set; } = null!;

    public virtual ICollection<Tag> Tags { get; set; }
}
```
І клас для тегу:

```cs
public partial class Tag
{
    public Tag()
    {
        Posts = new HashSet<Post>();
    }

    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public virtual ICollection<Post> Posts { get; set; }
}
```
Але немає класу для таблиці PostTag. Натомість, конфігурація для зв'язку "багато-до-багатьох" побудована за допомогою scaffolding:

```cs
entity.HasMany(d => d.Tags)
    .WithMany(p => p.Posts)
    .UsingEntity<Dictionary<string, object>>(
        "PostTag",
        r => r.HasOne<Tag>().WithMany().HasForeignKey("TagsId"),
        l => l.HasOne<Post>().WithMany().HasForeignKey("PostsId"),
        j =>
            {
                j.HasKey("PostsId", "TagsId");
                j.ToTable("PostTag");
                j.HasIndex(new[] { "TagsId" }, "IX_PostTag_TagsId");
            });
```


## Створити каркас лише один раз

Завдяки такому підходу, сформований код забезпечує відправну точку для подальшого відображення на основі коду. Будь-які зміни до згенерованого коду можна вносити за бажанням – він стає звичайним кодом, як і будь-який інший код у вашому проєкті.

Синхронізацію бази даних та моделі EF можна здійснити одним із двох способів:

* Перейдіть на використання міграцій бази даних EF Core та використовуйте типи сутностей і конфігурацію моделі EF як джерело достовірних даних, використовуючи міграції для керування схемою.

* Вручну оновлюйте типи сутностей та конфігурацію EF, коли база даних змінюється. Наприклад, якщо до таблиці додається новий стовпець, тоді додайте властивість для стовпця до типу зіставленої сутності та додайте будь-яку необхідну конфігурацію за допомогою атрибутів зіставлення та/або коду в OnModelCreating. Це відносно легко, єдиною справжньою проблемою є процес, який гарантує, що зміни в базі даних будуть записані або виявлені певним чином, щоб розробник(ці), відповідальний(і) за код, міг(ли) відреагувати. 

## Повторне створення каркасу

Альтернативний підхід до одноразового створення каркасу полягає в повторному створенні каркасу щоразу, коли база даних змінюється. Це перезапише будь-який попередньо створений код, тобто будь-які зміни, внесені до типів сутностей або конфігурації EF у цьому коді, будуть втрачені.

За замовчуванням команди EF не перезаписують жодного існуючого коду для захисту від випадкової втрати коду. Аргумент --force (.NET CLI) можна використовувати для примусового перезапису існуючих файлів.

Оскільки риштований код буде перезаписано, краще не змінювати його безпосередньо, а натомість покладатися на часткові класи та методи, а також механізми в EF Core, які дозволяють перевизначати конфігурацію. Зокрема:

* Як клас DbContext, так і класи сутностей генеруються як часткові. Це дозволяє вводити додаткові члени та код в окремому файлі, який не буде перезаписано під час запуску scaffolding.
* Клас DbContext містить частковий метод під назвою OnModelCreatingPartial. Реалізацію цього методу можна додати до часткового класу для DbContext. Він буде викликаний після виклику OnModelCreating.
* Конфігурація моделі, виконана за допомогою API ModelBuilder, замінює будь-яку конфігурацію, виконану за допомогою домовленостей або атрибутів зіставлення, а також попередню конфігурацію, виконану в конструкторі моделей. Це означає, що код у OnModelCreatingPartial можна використовувати для заміни конфігурації, згенерованої процесом створення каркасу, без необхідності видалення цієї конфігурації.

Зрештою, пам’ятайте, що починаючи з EF7, шаблони T4, що використовуються для генерації коду, можна налаштовувати. Це часто ефективніший підхід, ніж створення каркасу з використанням значень за замовчуванням, а потім модифікація за допомогою часткових класів та/або методів.

## Як це працює

Зворотне проектування починається зі зчитування схеми бази даних. Воно зчитує інформацію про таблиці, стовпці, обмеження та індекси.

Далі, він використовує інформацію схеми для створення моделі EF Core. Таблиці використовуються для створення типів сутностей; стовпці використовуються для створення властивостей; а зовнішні ключі використовуються для створення зв'язків.

Зрештою, модель використовується для генерації коду. Відповідні класи типів сутностей, Fluent API та анотації даних формуються для повторного створення такої ж моделі з вашого застосунку.

## Обмеження

* Не все в моделі можна представити за допомогою схеми бази даних. Наприклад, інформація про ієрархії успадкування, власні типи та розділення таблиць відсутня в схемі бази даних. Через це ці конструкції ніколи не будуть сформовані.
* Крім того, деякі типи стовпців можуть не підтримуватися постачальником EF Core. Ці стовпці не будуть включені до моделі.
* Ви можете визначити токени паралельного використання в моделі EF Core, щоб запобігти одночасному оновленню однієї й тієї ж сутності двома користувачами. Деякі бази даних мають спеціальний тип для представлення цього типу стовпця (наприклад, rowversion у SQL Server), і в цьому випадку ми можемо виконати зворотне проектування цієї інформації; однак інші токени паралельного використання не будуть сформовані.