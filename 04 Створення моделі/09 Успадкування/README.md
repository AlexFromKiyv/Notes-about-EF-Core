# Успадкування

EF може зіставити ієрархію типів .NET з базою даних. Це дозволяє вам писати ваші .NET-сутності в коді, як завжди, використовуючи базові та похідні типи, а також EF безперешкодно створювати відповідну схему бази даних, видавати запити тощо. Фактичні деталі того, як відображається ієрархія типів, залежать від постачальника; тут описано підтримку успадкування в контексті реляційної бази даних.

## Відображення ієрархії типів сутностей

За домовленістю, EF не скануватиме автоматично базові або похідні типи; це означає, що якщо ви хочете, щоб тип CLR у вашій ієрархії було зіставлено, ви повинні явно вказати цей тип у вашій моделі. Наприклад, якщо вказати лише базовий тип ієрархії, EF Core не обов'язково включатиме всі її підтипи.

У наступному прикладі показано набір баз даних (DbSet) для блогу (Blog) та його підкласу RssBlog. Якщо Blog має будь-який інший підклас, він не буде включено до моделі.

```cs
internal class MyContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<RssBlog> RssBlogs { get; set; }
}

public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
}

public class RssBlog : Blog
{
    public string RssUrl { get; set; }
}
```

Стовпці бази даних автоматично робляться nullable за потреби під час використання зіставлення TPH. Наприклад, стовпець RssUrl є nullable, оскільки звичайні екземпляри Blog не мають цієї властивості.

Якщо ви не хочете надавати доступ до DbSet для однієї або кількох сутностей в ієрархії, ви також можете скористатися Fluent API, щоб переконатися, що вони включені до моделі.

Якщо ви не покладаєтеся на домовленості, ви можете явно вказати базовий тип за допомогою HasBaseType. Ви також можете використовувати .HasBaseType((Type)null) для видалення типу сутності з ієрархії.

## Конфігурація таблиці на ієрархію(Table-per-hierarchy) та дискримінатора

За замовчуванням EF відображає успадкування за шаблоном таблиці на ієрархію (TPH). TPH використовує одну таблицю для зберігання даних для всіх типів в ієрархії, а стовпець дискримінатора використовується для визначення того, який тип представляє кожен рядок. За замовчуванням EF відображає успадкування, використовуючи шаблон таблиця-на-ієрархію (TPH). TPH використовує одну таблицю для зберігання даних усіх типів в ієрархії, а стовпець-дискримінатор використовується для визначення того, який тип представляє кожен рядок.

Наведена вище модель відображається в наступній схемі бази даних (зверніть увагу на неявно створений стовпець Дискримінатор, який визначає, який тип блогу зберігається в кожному рядку).

```sql
CREATE TABLE [dbo].[Blogs] (
    [BlogId]        INT            IDENTITY (1, 1) NOT NULL,
    [Url]           NVARCHAR (MAX) NOT NULL,
    [Discriminator] NVARCHAR (8)   NOT NULL,
    [RssUrl]        NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_Blogs] PRIMARY KEY CLUSTERED ([BlogId] ASC)
);
```
Ви можете налаштувати ім'я та тип стовпця дискримінатора, а також значення, які використовуються для ідентифікації кожного типу в ієрархії:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasDiscriminator<string>("blog_type")
        .HasValue<Blog>("blog_base")
        .HasValue<RssBlog>("blog_rss");
}
```
У наведених вище прикладах EF неявно додав дискримінатор як властивість тіні для базової сутності ієрархії. Цю властивість можна налаштувати як будь-яку іншу:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .Property("blog_type")
        .HasMaxLength(200);
}
```
Зрештою, дискримінатор також можна зіставити зі звичайною властивістю .NET у вашій сутності:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasDiscriminator(b => b.BlogType);

    modelBuilder.Entity<Blog>()
        .Property(e => e.BlogType)
        .HasMaxLength(200)
        .HasColumnName("blog_type");
        
    modelBuilder.Entity<RssBlog>();
}
```

Під час запитів похідних сутностей, які використовують шаблон TPH, EF Core додає пердікат поверх стовпця дискримінатора в запиті. Цей фільтр гарантує, що ми не отримаємо жодних додаткових рядків для базових типів або типів-споріднених типів, яких немає в результаті. Цей предикат фільтра пропускається для базового типу сутності, оскільки запит для базової сутності отримає результати для всіх сутностей в ієрархії. Під час матеріалізації результатів запиту, якщо ми стикаємося зі значенням дискримінатора, яке не зіставлене з жодним типом сутності в моделі, ми викидаємо виняток, оскільки не знаємо, як матеріалізувати результати. Ця помилка виникає лише тоді, коли ваша база даних містить рядки зі значеннями дискримінатора, які не відображені в моделі EF. Якщо у вас є такі дані, ви можете позначити зіставлення дискримінатора в моделі EF Core як неповне, щоб вказати, що нам завжди слід додавати предикат фільтра для запитів будь-якого типу в ієрархії. Виклик IsComplete(false) у конфігурації дискримінатора позначає відображення як неповне. 

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasDiscriminator()
        .IsComplete(false);
}
```

## Спільні стовпці

За замовчуванням, коли два споріднені типи сутностей в ієрархії мають властивість з однаковою назвою, вони будуть зіставлені з двома окремими стовпцями. Однак, якщо їхній тип ідентичний, їх можна зіставити з одним стовпцем бази даних:

```cs
public class MyContext : DbContext
{
    public DbSet<BlogBase> Blogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>()
            .Property(b => b.Url)
            .HasColumnName("Url");

        modelBuilder.Entity<RssBlog>()
            .Property(b => b.Url)
            .HasColumnName("Url");
    }
}

public abstract class BlogBase
{
    public int BlogId { get; set; }
}

public class Blog : BlogBase
{
    public string Url { get; set; }
}

public class RssBlog : BlogBase
{
    public string Url { get; set; }
}
```
Постачальники реляційних баз даних, такі як SQL Server, не використовуватимуть предикат дискримінатора автоматично під час запитів спільних стовпців із використанням приведення типів. Запит Url = (blog as RssBlog).Url також повертатиме значення Url для рядків типу Blog. Щоб обмежити запит сутностями RssBlog, потрібно вручну додати фільтр до дискримінатора, наприклад Url = blog is RssBlog ? (blog as RssBlog).Url : null.

## Конфігурація "таблиця за типом" (Table-per-type)

У шаблоні зіставлення TPT усі типи зіставляються з окремими таблицями. Властивості, що належать виключно базовому або похідному типу, зберігаються в таблиці, яка відповідає цьому типу. Властивості, що належать виключно базовому або похідному типу, зберігаються в таблиці, яка відповідає цьому типу. Таблиці, що відповідають похідним типам, також зберігають зовнішній ключ, який з'єднує похідну таблицю з базовою таблицею.

```cs
modelBuilder.Entity<Blog>().ToTable("Blogs");
modelBuilder.Entity<RssBlog>().ToTable("RssBlogs");
```
Замість виклику ToTable для кожного типу сутності, ви можете викликати modelBuilder.Entity<Blog>().UseTptMappingStrategy() для кожного кореневого типу сутності, і назви таблиць будуть згенеровані EF.

EF створить наступну схему бази даних для наведеної вище моделі.

```cs
CREATE TABLE [Blogs] (
    [BlogId] int NOT NULL IDENTITY,
    [Url] nvarchar(max) NULL,
    CONSTRAINT [PK_Blogs] PRIMARY KEY ([BlogId])
);

CREATE TABLE [RssBlogs] (
    [BlogId] int NOT NULL,
    [RssUrl] nvarchar(max) NULL,
    CONSTRAINT [PK_RssBlogs] PRIMARY KEY ([BlogId]),
    CONSTRAINT [FK_RssBlogs_Blogs_BlogId] FOREIGN KEY ([BlogId]) REFERENCES [Blogs] ([BlogId]) ON DELETE NO ACTION
);
```

Якщо ви використовуєте масове налаштування, ви можете отримати назву стовпця для певної таблиці, викликавши GetColumnName(IProperty, StoreObjectIdentifier).

```cs
foreach (var entityType in modelBuilder.Model.GetEntityTypes())
{
    var tableIdentifier = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);

    Console.WriteLine($"{entityType.DisplayName()}\t\t{tableIdentifier}");
    Console.WriteLine(" Property\tColumn");

    foreach (var property in entityType.GetProperties())
    {
        var columnName = property.GetColumnName(tableIdentifier.Value);
        Console.WriteLine($" {property.Name,-10}\t{columnName}");
    }

    Console.WriteLine();
}
```
У багатьох випадках TPT демонструє гірші показники продуктивності порівняно з THT.

Стовпці для похідного типу зіставляються з різними таблицями, тому складені FK-обмеження та індекси, які використовують як успадковані, так і оголошені властивості, не можуть бути створені в базі даних.

## Конфігурація "Таблиця на конкретний тип" (Table-per-concrete-type)

У шаблоні зіставлення TPC всі типи зіставляються з окремими таблицями. Кожна таблиця містить стовпці для всіх властивостей відповідного типу сутності. Це вирішує деякі поширені проблеми продуктивності стратегії TPT.

```cs
modelBuilder.Entity<Blog>().UseTpcMappingStrategy()
    .ToTable("Blogs");
modelBuilder.Entity<RssBlog>()
    .ToTable("RssBlogs");
```

EF створить наступну схему бази даних для наведеної вище моделі.

```sql
CREATE TABLE [Blogs] (
    [BlogId] int NOT NULL DEFAULT (NEXT VALUE FOR [BlogSequence]),
    [Url] nvarchar(max) NULL,
    CONSTRAINT [PK_Blogs] PRIMARY KEY ([BlogId])
);

CREATE TABLE [RssBlogs] (
    [BlogId] int NOT NULL DEFAULT (NEXT VALUE FOR [BlogSequence]),
    [Url] nvarchar(max) NULL,
    [RssUrl] nvarchar(max) NULL,
    CONSTRAINT [PK_RssBlogs] PRIMARY KEY ([BlogId])
);
```
## Схема бази даних TPC

Стратегія TPC подібна до стратегії TPT, за винятком того, що для кожного конкретного типу в ієрархії створюється окрема таблиця, але таблиці не створюються для абстрактних типів – звідси й назва «таблиця на конкретний тип»(table-per-concrete-type). Як і у випадку з TPT, сама таблиця вказує тип збереженого об'єкта. Однак, на відміну від TPT-відображення, кожна таблиця містить стовпці для кожної властивості конкретного типу та його базових типів. Схеми бази даних TPC денормалізовані.

Наприклад, розглянемо відображення цієї ієрархії:

```cs
public abstract class Animal
{
    protected Animal(string name)
    {
        Name = name;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public abstract string Species { get; }

    public Food? Food { get; set; }
}

public abstract class Pet : Animal
{
    protected Pet(string name)
        : base(name)
    {
    }

    public string? Vet { get; set; }

    public ICollection<Human> Humans { get; } = new List<Human>();
}

public class FarmAnimal : Animal
{
    public FarmAnimal(string name, string species)
        : base(name)
    {
        Species = species;
    }

    public override string Species { get; }

    [Precision(18, 2)]
    public decimal Value { get; set; }

    public override string ToString()
        => $"Farm animal '{Name}' ({Species}/{Id}) worth {Value:C} eats {Food?.ToString() ?? "<Unknown>"}";
}

public class Cat : Pet
{
    public Cat(string name, string educationLevel)
        : base(name)
    {
        EducationLevel = educationLevel;
    }

    public string EducationLevel { get; set; }
    public override string Species => "Felis catus";

    public override string ToString()
        => $"Cat '{Name}' ({Species}/{Id}) with education '{EducationLevel}' eats {Food?.ToString() ?? "<Unknown>"}";
}

public class Dog : Pet
{
    public Dog(string name, string favoriteToy)
        : base(name)
    {
        FavoriteToy = favoriteToy;
    }

    public string FavoriteToy { get; set; }
    public override string Species => "Canis familiaris";

    public override string ToString()
        => $"Dog '{Name}' ({Species}/{Id}) with favorite toy '{FavoriteToy}' eats {Food?.ToString() ?? "<Unknown>"}";
}

public class Human : Animal
{
    public Human(string name)
        : base(name)
    {
    }

    public override string Species => "Homo sapiens";

    public Animal? FavoriteAnimal { get; set; }
    public ICollection<Pet> Pets { get; } = new List<Pet>();

    public override string ToString()
        => $"Human '{Name}' ({Species}/{Id}) with favorite animal '{FavoriteAnimal?.Name ?? "<Unknown>"}'" +
           $" eats {Food?.ToString() ?? "<Unknown>"}";
}


```
Під час використання SQL Server для цієї ієрархії створюються такі таблиці:

```sql
CREATE TABLE [Cats] (
    [Id] int NOT NULL DEFAULT (NEXT VALUE FOR [AnimalSequence]),
    [Name] nvarchar(max) NOT NULL,
    [FoodId] uniqueidentifier NULL,
    [Vet] nvarchar(max) NULL,
    [EducationLevel] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Cats] PRIMARY KEY ([Id]));

CREATE TABLE [Dogs] (
    [Id] int NOT NULL DEFAULT (NEXT VALUE FOR [AnimalSequence]),
    [Name] nvarchar(max) NOT NULL,
    [FoodId] uniqueidentifier NULL,
    [Vet] nvarchar(max) NULL,
    [FavoriteToy] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Dogs] PRIMARY KEY ([Id]));

CREATE TABLE [FarmAnimals] (
    [Id] int NOT NULL DEFAULT (NEXT VALUE FOR [AnimalSequence]),
    [Name] nvarchar(max) NOT NULL,
    [FoodId] uniqueidentifier NULL,
    [Value] decimal(18,2) NOT NULL,
    [Species] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_FarmAnimals] PRIMARY KEY ([Id]));

CREATE TABLE [Humans] (
    [Id] int NOT NULL DEFAULT (NEXT VALUE FOR [AnimalSequence]),
    [Name] nvarchar(max) NOT NULL,
    [FoodId] uniqueidentifier NULL,
    [FavoriteAnimalId] int NULL,
    CONSTRAINT [PK_Humans] PRIMARY KEY ([Id]));
```

Зверніть увагу, що:

* Немає таблиць для типів Animal або Pet, оскільки вони є абстрактними в об'єктній моделі. Пам’ятайте, що C# не дозволяє створювати екземпляри абстрактних типів, і тому немає ситуації, коли екземпляр абстрактного типу буде збережено в базі даних.
* Зіставлення властивостей у базових типах повторюється для кожного конкретного типу. Наприклад, кожна таблиця має стовпець «Name», а таблиця «Cats» та «Dogs» має стовпець «Vet».

Збереження деяких даних у цій базі даних призводить до наступного:

Cats

Id	Name	FoodId	Vet	EducationLevel
1	Alice	99ca3e98-b26d-4a0c-d4ae-08da7aca624f	Pengelly	MBA
2	Mac	99ca3e98-b26d-4a0c-d4ae-08da7aca624f	Pengelly	Preschool
8	Baxter	5dc5019e-6f72-454b-d4b0-08da7aca624f	Bothell Pet Hospital	BSc

Dogs 

Id	Name	FoodId	Vet	FavoriteToy
3	Toast	011aaf6f-d588-4fad-d4ac-08da7aca624f	Pengelly	Mr. Squirrel

FarmAnimals table

Id	Name	FoodId	Value	Species
4	Clyde	1d495075-f527-4498-d4af-08da7aca624f	100.00	Equus africanus asinus

Humans 
Id	Name	FoodId	FavoriteAnimalId
5	Wendy	5418fd81-7660-432f-d4b1-08da7aca624f	2
6	Arthur	59b495d4-0414-46bf-d4ad-08da7aca624f	1
9	Katie	null	8

Зверніть увагу, що на відміну від зіставлення TPT, вся інформація для одного об'єкта міститься в одній таблиці. І, на відміну від зіставлення TPH, у жодній таблиці немає комбінації стовпця та рядка, яку модель ніколи не використовувала б. Нижче ми побачимо, як ці характеристики можуть бути важливими для запитів та сховища.

## Генерація ключів

Обрана стратегія відображення успадкування має наслідки для того, як генеруються та керуються значення первинних ключів. Ключі в TPH прості, оскільки кожен екземпляр сутності представлений одним рядком в одній таблиці. Можна використовувати будь-який вид генерації ключів-значень, і жодних додаткових обмежень не потрібно. 

Для стратегії TPT у таблиці завжди є рядок, що відповідає базовому типу ієрархії. Для цього рядка можна використовувати будь-який вид генерації ключів, а ключі для інших таблиць пов'язані з цією таблицею за допомогою обмежень зовнішнього ключа.

Для TPC справи стають дещо складнішими. По-перше, важливо розуміти, що EF Core вимагає, щоб усі сутності в ієрархії мали унікальне значення ключа, навіть якщо сутності мають різні типи. Наприклад, використовуючи нашу модель-приклад, Собака не може мати те саме значення ключа Id, що й Кішка. По-друге, на відміну від TPT, немає єдиної таблиці, яка могла б слугувати єдиним місцем, де зберігаються та можуть бути згенеровані ключові значення. Це означає, що простий стовпець Identity використовувати не можна.

Для баз даних, що підтримують послідовності, значення ключів можна генерувати, використовуючи одну послідовність, на яку посилається обмеження за замовчуванням для кожної таблиці. Ця стратегія використовується в таблицях TPC, показаних вище, де кожна таблиця має наступне:

```sql
[Id] int NOT NULL DEFAULT (NEXT VALUE FOR [AnimalSequence])
```

AnimalSequence – це послідовність бази даних, створена EF Core. Ця стратегія використовується за замовчуванням для ієрархій TPC під час використання постачальника баз даних EF Core для SQL Server. Постачальники баз даних для інших баз даних, що підтримують послідовності, повинні мати аналогічне значення за замовчуванням. Інші стратегії генерації ключів, які використовують послідовності, такі як шаблони Hi-Lo, також можуть використовуватися з TPC.

Хоча стандартні стовпці Identity не працюють із TPC, їх можна використовувати, якщо кожну таблицю налаштовано з відповідним початковим значенням та приростом, щоб значення, згенеровані для кожної таблиці, ніколи не конфліктували. Наприклад:

```cs
modelBuilder.Entity<Cat>().ToTable("Cats", tb => tb.Property(e => e.Id).UseIdentityColumn(1, 4));
modelBuilder.Entity<Dog>().ToTable("Dogs", tb => tb.Property(e => e.Id).UseIdentityColumn(2, 4));
modelBuilder.Entity<FarmAnimal>().ToTable("FarmAnimals", tb => tb.Property(e => e.Id).UseIdentityColumn(3, 4));
modelBuilder.Entity<Human>().ToTable("Humans", tb => tb.Property(e => e.Id).UseIdentityColumn(4, 4));
```
Використання цієї стратегії ускладнює додавання похідних типів пізніше, оскільки вимагає попереднього знання загальної кількості типів в ієрархії.

## Обмеження зовнішнього ключа

Стратегія зіставлення TPC створює денормалізовану схему SQL – це одна з причин, чому деякі прихильники баз даних виступають проти неї. Наприклад, розглянемо стовпець зовнішнього ключа FavoriteAnimalId. Значення в цьому стовпці має збігатися зі значенням первинного ключа якоїсь тварини. Це можна забезпечити в базі даних за допомогою простого обмеження FK при використанні TPH або TPT.

```sql
CONSTRAINT [FK_Animals_Animals_FavoriteAnimalId] FOREIGN KEY ([FavoriteAnimalId]) REFERENCES [Animals] ([Id])
```
Але під час використання TPC первинний ключ для будь-якої тварини зберігається в таблиці, що відповідає конкретному типу цієї тварини. Наприклад, первинний ключ кота зберігається у стовпці Cats.Id, тоді як первинний ключ собаки зберігається у стовпці Dogs.Id тощо. Це означає, що для цього зв'язку не можна створити обмеження FK.

На практиці це не є проблемою, якщо програма не намагається вставити недійсні дані. Наприклад, якщо всі дані вставляються EF Core та використовують навігацію для зв'язування сутностей, то гарантовано, що стовпець FK завжди міститиме дійсні значення PK.

## Підсумок та рекомендації

Підсумовуючи, TPH зазвичай підходить для більшості застосувань і є гарним варіантом за замовчуванням для широкого спектру сценаріїв, тому не додавайте складності TPC, якщо вам це не потрібно. Зокрема, якщо ваш код переважно запитуватиме сутності багатьох типів, наприклад, писатиме запити до базового типу, тоді схиляйтеся до TPH, а не до TPC.

З огляду на це, TPC також є гарною стратегією відображення, яку можна використовувати, коли ваш код переважно запитуватиме сутності однолистового типу, а ваші тести показують покращення порівняно з TPH.

Використовуйте TPT лише тоді, коли це обмежено зовнішніми факторами.

