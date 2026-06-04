# Створення та налаштування моделі

EF Core використовує метадані під назвою "Model" для опису того, як типи сутностей програми зіставляються з базовою базою даних. Ця модель побудована з використанням набору домовленостей – евристик, які шукають спільні закономірності. Тако ж модель можна налаштувати за допомогою атрибутів відображення (відомих як анотації даних) та/або викликів методів ModelBuilder (відомих як fluent API) в OnModelCreating, обидва з яких замінять конфігурацію, виконану за допомогою домовленостей.

Більшість конфігурацій можна застосувати до моделі, орієнтованої на будь-яке сховище даних. Постачальники також можуть увімкнути конфігурацію, специфічну для певного сховища даних, а також ігнорувати конфігурацію, яка не підтримується або не застосовується.

## Використання Fluent API для налаштування моделі

Ви можете перевизначити метод OnModelCreating у вашому похідному контексті та використовувати Fluent API для налаштування вашої моделі. Це найпотужніший метод налаштування, який дозволяє вказати конфігурацію без зміни класів сутностей. Конфігурація Fluent API має найвищий пріоритет і замінює конвенції та анотації даних. Конфігурація застосовується в порядку виклику методів, і якщо є якісь конфлікти, останній виклик замінить попередньо задану конфігурацію.

```cs
using Microsoft.EntityFrameworkCore;

namespace EFModeling.EntityProperties.FluentAPI.Required;

internal class MyContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }

    #region Required
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>()
            .Property(b => b.Url)
            .IsRequired();
    }
    #endregion
}

public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
}
```

## Групування конфігурації

Щоб зменшити розмір методу OnModelCreating, всю конфігурацію для типу сутності можна витягти в окремий клас, що реалізує IEntityTypeConfiguration<TEntity>.

```cs
public class BlogEntityTypeConfiguration : IEntityTypeConfiguration<Blog>
{
    public void Configure(EntityTypeBuilder<Blog> builder)
    {
        builder
            .Property(b => b.Url)
            .IsRequired();
    }
}
```
```cs
new BlogEntityTypeConfiguration().Configure(modelBuilder.Entity<Blog>());
```

## Використання EntityTypeConfigurationAttribute для типів сутностей

Замість явного виклику Configure, EntityTypeConfigurationAttribute можна розмістити для типу сутності, щоб EF Core міг знайти та використовувати відповідну конфігурацію.

```cs
[EntityTypeConfiguration(typeof(BookConfiguration))]
public class Book
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Isbn { get; set; }
}
```
Цей атрибут означає, що EF Core використовуватиме вказану реалізацію IEntityTypeConfiguration щоразу, коли тип сутності Book включено до моделі. Тип сутності включається в модель за допомогою одного зі звичайних механізмів. Наприклад, шляхом створення властивості DbSet\<TEntity\> для типу сутності:

```cs
public class BooksContext : DbContext
{
    public DbSet<Book> Books { get; set; }

    //...
}
```
Або зареєструвавши його в OnModelCreating:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Book>();
}
```

## Використання анотацій даних для налаштування моделі

Ви також можете застосовувати певні атрибути (відомі як анотації даних) до своїх класів та властивостей. Анотації даних замінять домовленості, але будуть замінені конфігурацією Fluent API.

```cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EFModeling.EntityProperties.DataAnnotations.Annotations;

internal class MyContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
}

[Table("Blogs")]
public class Blog
{
    public int BlogId { get; set; }

    [Required]
    public string Url { get; set; }
}
```

## Вбудовані конвенції

EF Core містить багато конвенцій побудови моделей, які ввімкнено за замовчуванням. Ви можете знайти їх усі у списку класів, що реалізують інтерфейс IConvention. Однак цей список не включає конвенції, запроваджені сторонніми постачальниками баз даних та плагінами.

Програми можуть видаляти або замінювати будь-яку з цих конвенцій, а також додавати нові власні конвенції, які застосовують конфігурацію для шаблонів, що не розпізнаються EF "з коробки".

## Видалення існуючої домовленості

Іноді одна з вбудованих домовленостей може не підходити для вашої програми, і в такому разі її можна видалити.

## Приклад: Не створюйте індекси для стовпців зовнішнього ключа

Зазвичай має сенс створювати індекси для стовпців зовнішнього ключа (FK), і тому для цього існує вбудована конвенція: ForeignKeyIndexConvention. Дивлячись на debug view моделі для типу сутності Post зі зв'язками з Blog та Author, ми бачимо, що створено два індекси – один для FK BlogId, а інший – для FK AuthorId.

```txt
EntityType: Post
    Properties:
      Id (int) Required PK AfterSave:Throw ValueGenerated.OnAdd
      AuthorId (no field, int?) Shadow FK Index
      BlogId (no field, int) Shadow Required FK Index
    Navigations:
      Author (Author) ToPrincipal Author Inverse: Posts
      Blog (Blog) ToPrincipal Blog Inverse: Posts
    Keys:
      Id PK
    Foreign keys:
      Post {'AuthorId'} -> Author {'Id'} ToDependent: Posts ToPrincipal: Author ClientSetNull
      Post {'BlogId'} -> Blog {'Id'} ToDependent: Posts ToPrincipal: Blog Cascade
    Indexes:
      AuthorId
      BlogId
```

Однак, індекси мають додаткові витрати, і не завжди доцільно створювати їх для всіх стовпців FK. Для досягнення цього можна видалити конвенцію ForeignKeyIndex під час побудови моделі:

```cs
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
}
```
Дивлячись на налагоджувальне подання моделі для Post зараз, ми бачимо, що індекси на FK не створені:

```
EntityType: Post
    Properties:
      Id (int) Required PK AfterSave:Throw ValueGenerated.OnAdd
      AuthorId (no field, int?) Shadow FK
      BlogId (no field, int) Shadow Required FK
    Navigations:
      Author (Author) ToPrincipal Author Inverse: Posts
      Blog (Blog) ToPrincipal Blog Inverse: Posts
    Keys:
      Id PK
    Foreign keys:
      Post {'AuthorId'} -> Author {'Id'} ToDependent: Posts ToPrincipal: Author ClientSetNull
      Post {'BlogId'} -> Blog {'Id'} ToDependent: Posts ToPrincipal: Blog Cascade
```
За потреби, індекси все ще можна явно створювати для стовпців зовнішнього ключа, або за допомогою IndexAttribute, або за допомогою конфігурації в OnModelCreating.

## Перегляд моделі даних 

В Debug можна створити точку останова і передивитись склд зміної context.Model > Debug View > View.

До нього також можна отримати доступ безпосередньо з коду, наприклад, для надсилання debug view в консоль:

```cs
Console.WriteLine(context.Model.ToDebugString());
```
Представлення налагодження має коротку та довгу форми. Довга форма також містить усі анотації, які можуть бути корисними, якщо вам потрібно переглянути реляційні або специфічні для постачальника метадані. До довгої форми також можна отримати доступ з коду:

```cs
Console.WriteLine(context.Model.ToDebugString(MetadataDebugStringOptions.LongDefault));
```
Можна отримати конфігурування окремої сутності

```cs
var context = new ApplicationDbContextFactory().CreateDbContext(null);

Console.WriteLine(context.Model.FindEntityType(typeof(Blog)).ToDebugString());
Console.WriteLine();
Console.WriteLine(context.Model.FindEntityType(typeof(Blog)).ToDebugString(MetadataDebugStringOptions.LongDefault));

```
```
EntityType: Blog
  Properties:
    Id (int) Required PK AfterSave:Throw ValueGenerated.OnAdd
    Url (string) Required
  Navigations:
    Posts (List<Post>) Collection ToDependent Post Inverse: Blog
  Keys:
    Id PK

EntityType: Blog
  Properties:
    Id (int) Required PK AfterSave:Throw ValueGenerated.OnAdd
      Annotations:
        SqlServer:ValueGenerationStrategy: IdentityColumn
    Url (string) Required
      Annotations:
        SqlServer:ValueGenerationStrategy: None
  Navigations:
    Posts (List<Post>) Collection ToDependent Post Inverse: Blog
  Keys:
    Id PK
  Annotations:
    DiscriminatorProperty:
    Relational:FunctionName:
    Relational:Schema:
    Relational:SqlQuery:
    Relational:TableName: Blogs
    Relational:ViewName:
    Relational:ViewSchema:
```