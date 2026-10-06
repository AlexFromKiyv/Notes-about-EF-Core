# Доступ до відстежуваних сутностей

Існує чотири основні API для доступу до сутностей, що відстежуються об'єктом DbContext:

* DbContext.Entry повертає екземпляр EntityEntry\<TEntity\> для заданого екземпляра сутності.
* ChangeTracker.Entries повертає екземпляри EntityEntry\<TEntity\> для всіх відстежуваних сутностей або для всіх відстежуваних сутностей заданого типу.
* Методи DbContext.Find, DbContext.FindAsync, DbSet\<TEntity\>.Find та DbSet\<TEntity\>.FindAsync знаходять окрему сутність за первинним ключем, спочатку перевіряючи відстежувані сутності, а за потреби — виконуючи запит до бази даних.
* DbSet\<TEntity\>.Local повертає безпосередньо сутності (а не екземпляри EntityEntry) для об'єктів того типу, який представлено цим DbSet.

Кожен із них детальніше описано в наведених нижче розділах.

    Порада

    У цьому документі передбачається розуміння станів сутностей та основ відстеження змін в EF Core. Докладнішу інформацію з цих питань див. у розділі «Відстеження змін в EF Core».

# Використання екземплярів DbContext.Entry та EntityEntry

Для кожного відстежуваної сутності Entity Framework Core (EF Core) відстежує:

* Загальний стан сутності. Це одне з таких значень: Unchanged (без змін), Modified (змінено), Added (додано) або Deleted (видалено); докладнішу інформацію див. у розділі «Відстеження змін в EF Core».
* Зв’язки між відстежуваними сутностями. Наприклад, блог, до якого належить допис.
* «Поточні значення» властивостей.
* «Початкові значення» властивостей (за наявності такої інформації). Це значення властивостей, які існували на момент отримання сутності з бази даних.
* Значення яких властивостей було змінено після того, як було здійснено запит щодо них?
* Інша інформація про значення власністі, наприклад, чи є ця власність тимчасовою.

Передача екземпляра сутності до DbContext.Entry повертає об’єкт EntityEntry\<TEntity\>, який надає доступ до відповідної інформації про цю сутність. Наприклад:

```cs
//Model
public class Blog : IEntityWithKey
{
    public int Id { get; set; }
    public string Name { get; set; }

    public IList<Post> Posts { get; } = new List<Post>();
}

public class Post : IEntityWithKey
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int? BlogId { get; set; }
    public Blog Blog { get; set; }
}

public interface IEntityWithKey
{
    int Id { get; set; }
}

// Programm.cs 
static async Task DoAsync()
{
    // Clean and populate the database
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);
    //Console.WriteLine(context.Model.ToDebugString());
    await CleanDatabase(context);
    await PopulateDatabase(context);
    context.Dispose();

    await Using_DbContext_Entry_and_EntityEntry_instances();
}
await DoAsync();

static async Task Using_DbContext_Entry_and_EntityEntry_instances()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);
 
    Blog blog = await context.Blogs.SingleAsync(blog => blog.Id == 1);

    var entityEntry = context.Entry(blog);

    Console.WriteLine(entityEntry.Context);
    Console.WriteLine(entityEntry.State);
    Console.WriteLine(entityEntry.IsKeySet);
    Console.WriteLine(entityEntry.Metadata.DisplayName());
}

```
```
Draft.ApplicationDbContext
Unchanged
True
Blog
```

У наступних розділах показано, як використовувати EntityEntry для доступу до стану сутності та керування ним, а також для роботи зі станом властивостей і навігаційних властивостей сутності.

## Робота із сутністю

Найпоширенішим випадком використання EntityEntry\<TEntity\> є отримання доступу до поточного стану сутності (EntityState). Наприклад:

```cs
static async Task Work_with_the_entity_1()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);
    var blog = await context.Blogs.SingleAsync(e => e.Id == 1);

    var currentState = context.Entry(blog).State;
    if (currentState == EntityState.Unchanged)
    {
        context.Entry(blog).State = EntityState.Modified;
    }

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
```
Blog {Id: 1} Modified
    Id: 1 PK
    Name: '.NET Blog' Modified
  Posts: []
```

Метод Entry також можна використовувати для сутностей, які ще не відстежуються. Це не розпочинає відстеження сутності; її стан залишається Detached (від’єднаним). Однак отриманий об'єкт EntityEntry можна потім використати для зміни стану сутності — після цього сутність почне відстежуватися в цьому стані. Наприклад, наведений нижче код розпочне відстеження екземпляра Blog зі станом Added:

```cs
static async Task Work_with_the_entity_2() 
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Blog newBlog = new Blog() { Name = "New Blog" };

    Console.WriteLine(context.Entry(newBlog).State);

    context.Entry(newBlog).State = EntityState.Added;

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
```
Detached
Blog {Id: -2147482647} Added
    Id: -2147482647 PK Temporary
    Name: 'New Blog'
  Posts: []
```

    Порада

    На відміну від EF6, встановлення стану окремої сутності не призводить до відстеження всіх пов’язаних із нею сутностей. Через це встановлення стану в такий спосіб є операцією нижчого рівня порівняно з викликом методів Add, Attach або Update, які діють на весь граф сутностей.

У наведеній нижче таблиці узагальнено способи використання EntityEntry для роботи з усією сутністю:

|Член EntityEntry|Опис|
|----------------|----|
|EntityEntry.State|Отримує та встановлює стан сутності (EntityState).|
|EntityEntry.Entity|Отримує екземпляр сутності.|
|EntityEntry.Context|DbContext, який відстежує цю сутність.|
|EntityEntry.Metadata|Метадані IEntityType для типу сутності.|
|EntityEntry.IsKeySet|Чи встановлено значення ключа для сутності.|
|EntityEntry.Reload()|Перезаписує значення властивостей значеннями, зчитаними з бази даних.|
|EntityEntry.DetectChanges()|Примусово виявляє зміни лише для цієї сутності; див. «Виявлення змін і сповіщення»|

## Робота з окремою властивістю

Кілька перевантажень методу EntityEntry\<TEntity\>.Property надають доступ до інформації про окрему властивість сутності. Наприклад, за допомогою суворо типізованого API в стилі fluent:

```cs
PropertyEntry<Blog, string> propertyEntry = context.Entry(blog).Property(e => e.Name);
```
Натомість назву властивості можна передати як рядок. Наприклад:

```cs
PropertyEntry<Blog, string> propertyEntry = context.Entry(blog).Property<string>("Name");
```
Повернутий об'єкт PropertyEntry\<TEntity, TProperty\> можна потім використовувати для отримання доступу до інформації про властивість. Наприклад, його можна застосувати для отримання та встановлення поточного значення властивості цієї сутності:

```cs
    string currentValue = context.Entry(blog).Property(e => e.Name).CurrentValue;
    Console.WriteLine(currentValue);

    context.Entry(blog).Property(e => e.Name).CurrentValue = "1unicorn2";
```
```
.NET Blog
```

Обидва методи Property, використані вище, повертають екземпляр строго типізованого узагальненого класу PropertyEntry\<TEntity, TProperty\>. Використання цього узагальненого типу є кращим, оскільки воно дозволяє отримувати доступ до значень властивостей без упакування (boxing) типів значень. Однак, якщо тип сутності або властивості невідомий на етапі компіляції, натомість можна отримати нешаблонний PropertyEntry:

```cs
PropertyEntry propertyEntry = context.Entry(blog).Property("Name");
```
Це надає доступ до інформації про будь-яку властивість об'єкту незалежно від його типу — ціною пакування (boxing) типів значень.

```cs
object blog = await context.Blogs.SingleAsync(e => e.Id == 1);

object currentValue = context.Entry(blog).Property("Name").CurrentValue;
context.Entry(blog).Property("Name").CurrentValue = "1unicorn2";
```

Повний приклад:

```cs
static async Task Work_with_a_single_property() 
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var blog = await context.Blogs.SingleAsync(e => e.Id == 1);

    PropertyEntry<Blog, string> namePropertyEntry0 = context.Entry(blog).Property(e => e.Name);

    PropertyEntry<Blog, string> namePropertyEntry = context.Entry(blog).Property<string>("Name");

    string currentValue = context.Entry(blog).Property(e => e.Name).CurrentValue;
    Console.WriteLine(currentValue);

    context.Entry(blog).Property(e => e.Name).CurrentValue = "1unicorn2";


    object blog1 = await context.Blogs.SingleAsync(e => e.Id == 1);
    object currentValue1 = context.Entry(blog).Property("Name").CurrentValue;

    context.Entry(blog).Property("Name").CurrentValue = "1unicorn2";

    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
```
.NET Blog
Blog {Id: 1} Modified
    Id: 1 PK
    Name: '1unicorn2' Modified Originally '.NET Blog'
  Posts: []
```

У наведеній нижче таблиці узагальнено інформацію про властивості, яку надає PropertyEntry:

|Член PropertyEntry|Опис|
|------------------|----|
|PropertyEntry<TEntity,TProperty>.CurrentValue|Отримує та встановлює поточне значення властивості.|
|PropertyEntry<TEntity,TProperty>.OriginalValue|Отримує та встановлює початкове значення властивості, якщо воно доступне.|
|PropertyEntry<TEntity,TProperty>.EntityEntry|Зворотне посилання на EntityEntry<TEntity> для сутності.|
|PropertyEntry.Metadata|Метадані IProperty для властивості.|
|PropertyEntry.IsModified|Вказує, чи позначено цю властивість як змінену, і дозволяє змінювати цей стан.|
|PropertyEntry.IsTemporary|Вказує, чи позначено цю властивість як тимчасову, і дозволяє змінювати цей стан.|

Примітки:

* Початкове значення властивості — це значення, яке вона мала на момент отримання сутності з бази даних. Однак початкові значення недоступні, якщо сутність була від’єднана, а потім явно приєднана до іншого екземпляра DbContext (наприклад, за допомогою методів Attach або Update).
* Метод SaveChanges оновлює лише ті властивості, які позначено як змінені. Встановіть для властивості IsModified значення true, щоб змусити EF Core оновити значення відповідної властивості, або значення false, щоб запобігти її оновленню.
* Тимчасові значення зазвичай генеруються генераторами значень EF Core. Встановлення поточного значення властивості замінить тимчасове значення на вказане та позначить властивість як таку, що не є тимчасовою. Встановіть для параметра IsTemporary значення true, щоб зробити значення тимчасовим навіть після того, як воно було явно задане.

## Робота з окремою навігаційною властивістю

Кілька перевантажень методів EntityEntry\<TEntity\>.Reference, EntityEntry\<TEntity\>.Collection та EntityEntry.Navigation надають доступ до інформації про окрему навігаційну властивість. 

Доступ до навігації за посиланнями на окремий пов'язаний об'єкт здійснюється через методи Reference. Навігаційні посилання вказують на сторони one у зв’язках one-to-many та на обидві сторони у зв’язках one-to-one. Наприклад:

```cs
ReferenceEntry<Post, Blog> referenceEntry1 = context.Entry(post).Reference(e => e.Blog);
ReferenceEntry<Post, Blog> referenceEntry2 = context.Entry(post).Reference<Blog>("Blog");
ReferenceEntry referenceEntry3 = context.Entry(post).Reference("Blog");
```
Навігаційні властивості також можуть являти собою колекції пов’язаних сутностей, якщо вони використовуються для сторони many у зв’язках типу one-to-many та many-to-many. Методи Collection використовуються для доступу до навігації колекції. Наприклад:

```cs
CollectionEntry<Blog, Post> collectionEntry1 = context.Entry(blog).Collection(e => e.Posts);
CollectionEntry<Blog, Post> collectionEntry2 = context.Entry(blog).Collection<Post>("Posts");
CollectionEntry collectionEntry3 = context.Entry(blog).Collection("Posts");
```

Деякі операції є спільними для всіх навігацій. До них можна отримати доступ як для навігації за посиланнями, так і для навігації за колекціями, використовуючи метод EntityEntry.Navigation. Зауважте, що при одночасному доступі до всіх навігаційних властивостей доступним є лише неузагальнений (non-generic) варіант.

```cs
NavigationEntry navigationEntry = context.Entry(blog).Navigation("Posts");
```

```cs
static async Task Work_with_a_single_navigation() 
{
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var post = await context.Posts.Include(e => e.Blog).SingleAsync(e => e.Id == 1);

        ReferenceEntry<Post, Blog> referenceEntry1 = context.Entry(post).Reference(e => e.Blog);
        ReferenceEntry<Post, Blog> referenceEntry2 = context.Entry(post).Reference<Blog>("Blog");
        ReferenceEntry referenceEntry3 = context.Entry(post).Reference("Blog");

        Console.WriteLine(referenceEntry1.Metadata);
        Console.WriteLine(referenceEntry1.CurrentValue.Name);
        Console.WriteLine(referenceEntry1.EntityEntry);
        Console.WriteLine(referenceEntry1.IsLoaded);
        Console.WriteLine(referenceEntry1.IsModified);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var blog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Id == 1);

        CollectionEntry<Blog, Post> collectionEntry1 = context.Entry(blog).Collection(e => e.Posts);
        CollectionEntry<Blog, Post> collectionEntry2 = context.Entry(blog).Collection<Post>("Posts");
        CollectionEntry collectionEntry3 = context.Entry(blog).Collection("Posts");
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var blog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Id == 1);

        NavigationEntry navigationEntry = context.Entry(blog).Navigation("Posts");
    }
}
```
```
Navigation: Post.Blog (Blog) ToPrincipal Blog Inverse: Posts
.NET Blog
Post {Id: 1} Unchanged FK {BlogId: 1}
True
False
```
У наведеній нижче таблиці узагальнено способи використання ReferenceEntry<TEntity,TProperty>, CollectionEntry<TEntity,TRelatedEntity> та NavigationEntry:

|Член NavigationEntry|Опис|
|--------------------|----|
|MemberEntry.CurrentValue|Отримує та встановлює поточне значення навігації. У випадку навігації за колекцією це вся колекція цілком.|
|NavigationEntry.Metadata|Метадані INavigationBase для навігації.|
|NavigationEntry.IsLoaded|Отримує або встановлює значення, яке вказує, чи було пов’язану сутність або колекцію повністю завантажено з бази даних.|
|NavigationEntry.Load()|Завантажує пов’язану сутність або колекцію з бази даних; див. «Явне завантаження пов’язаних даних».|
|NavigationEntry.Query()|Запит, який EF Core використовуватиме для завантаження цієї навігаційної властивості як об’єкта IQueryable, що допускає подальшу модифікацію; див. розділ «Явне завантаження пов’язаних даних».|

## Робота з усіма властивостями сутності

EntityEntry.Properties повертає IEnumerable\<T\> об'єктів PropertyEntry для кожної властивості сутності. Це можна використовувати для виконання певної дії для кожної властивості сутності. Наприклад, щоб встановити будь-яку властивість типу DateTime у значення DateTime.Now:

```cs
foreach (var propertyEntry in context.Entry(blog).Properties)
{
    if (propertyEntry.Metadata.ClrType == typeof(DateTime))
    {
        propertyEntry.CurrentValue = DateTime.Now;
    }
}
```
Крім того, EntityEntry містить кілька методів для отримання та встановлення значень усіх властивостей одночасно. У цих методах використовується клас PropertyValues, що являє собою колекцію властивостей та їхніх значень. Значення властивостей (PropertyValues) можна отримати для поточних або початкових значень, а також для значень у тому вигляді, в якому вони наразі зберігаються в базі даних. Наприклад:

```cs
var currentValues = context.Entry(blog).CurrentValues;
var originalValues = context.Entry(blog).OriginalValues;
var databaseValues = await context.Entry(blog).GetDatabaseValuesAsync();
```

Самі по собі ці об'єкти PropertyValues не надто корисні. Однак їх можна комбінувати для виконання типових операцій, необхідних під час маніпулювання сутностями. Це корисно під час роботи з об'єктами передачі даних (DTO) та вирішення конфліктів оптимістичного паралельного доступу. У наступних розділах наведено кілька прикладів.

```cs
static async Task Work_with_all_properties_of_an_entity()
{
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var blog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Id == 1);
        foreach (var propertyEntry in context.Entry(blog).Properties)
        {
            Console.WriteLine($"Property: {propertyEntry.Metadata.Name}, CurrentValue: {propertyEntry.CurrentValue}");
        }
    }

    using(var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var blog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Id == 1);
        
        var currentValues = context.Entry(blog).CurrentValues;
        var originalValues = context.Entry(blog).OriginalValues;
        var databaseValues = await context.Entry(blog).GetDatabaseValuesAsync();

        Console.WriteLine(currentValues);
        Console.WriteLine(originalValues);
        Console.WriteLine(databaseValues);
    }
}
```
```
Property: Id, CurrentValue: 1
Property: Name, CurrentValue: .NET Blog
Microsoft.EntityFrameworkCore.ChangeTracking.Internal.CurrentPropertyValues
Microsoft.EntityFrameworkCore.ChangeTracking.Internal.OriginalPropertyValues
Microsoft.EntityFrameworkCore.ChangeTracking.Internal.ArrayPropertyValues
```



## Встановлення поточних або початкових значень із сутності чи DTO

Поточні або початкові значення сутності можна оновити шляхом копіювання значень з іншого об'єкта. Наприклад, розгляньмо об’єкт передачі даних (data transfer object DTO) BlogDto, що має ті самі властивості, що й тип сутності:

```cs
public class BlogDto
{
    public int Id { get; set; }
    public string Name { get; set; }
}

```
Це можна використовувати для встановлення поточних значень відстежуваного об'єкта за допомогою PropertyValues.SetValues:

```cs
static async Task Setting_current_or_original_values()
{
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var blog = await context.Blogs.SingleAsync(e => e.Id == 1);
        var blogDto = new BlogDto { Id = 1, Name = "1unicorn2" };
        context.Entry(blog).CurrentValues.SetValues(blogDto);

        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    }
}
```
```
Blog {Id: 1} Modified
    Id: 1 PK
    Name: '1unicorn2' Modified Originally '.NET Blog'
  Posts: []
```

Цей підхід іноді застосовується під час оновлення сутності значеннями, отриманими внаслідок виклику сервісу або від клієнта в багаторівневій програмі. Зауважте, що використовуваний об’єкт не обов’язково має належати до того самого типу, що й сутність, за умови, що він має властивості, назви яких збігаються з назвами властивостей сутності. У наведеному вище прикладі екземпляр DTO `BlogDto` використовується для встановлення поточних значень відстежуваної сутності Blog.

Зауважте, що властивості позначатимуться як змінені лише в тому разі, якщо встановлене значення відрізняється від поточного.

## Встановлення поточних або початкових значень зі словника

У попередньому прикладі значення встановлювалися з екземпляра сутності або DTO. Така сама поведінка доступна, коли значення властивостей зберігаються у словнику як пари «ім'я — значення».

```cs
static async Task Setting_current_or_original_values()
{

    //...

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var blog = await context.Blogs.SingleAsync(e => e.Id == 1);

        var blogDictionary = new Dictionary<string, object> { ["Id"] = 1, ["Name"] = "1unicorn2" };
        context.Entry(blog).CurrentValues.SetValues(blogDictionary);

        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    }
}
```
```
Blog {Id: 1} Modified
    Id: 1 PK
    Name: '1unicorn2' Modified Originally '.NET Blog'
  Posts: []
```
## Встановлення поточних або початкових значень із бази даних

Поточні або початкові значення сутності можна оновити найновішими значеннями з бази даних, викликавши метод GetDatabaseValues() або GetDatabaseValuesAsync і використавши отриманий об'єкт для встановлення поточних чи початкових значень (або й тих, і інших).

```cs
static async Task Setting_current_or_original_values()
{
    //...

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var blog = await context.Blogs.SingleAsync(e => e.Id == 1);

        var databaseValues = await context.Entry(blog).GetDatabaseValuesAsync();
        context.Entry(blog).CurrentValues.SetValues(databaseValues);
        context.Entry(blog).OriginalValues.SetValues(databaseValues);

        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    }
}
```
```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: []
```

## Створення клонованого об'єкта, що містить поточні, початкові або отримані з бази даних значення

Об’єкт PropertyValues, отриманий за допомогою CurrentValues, OriginalValues або GetDatabaseValues, можна використовувати для створення клона сутності за допомогою методу PropertyValues.ToObject().
Наприклад: поточні або початкові значення сутності можна оновити найновішими значеннями з бази даних, викликавши метод GetDatabaseValues() або GetDatabaseValuesAsync і використавши отриманий об’єкт для встановлення поточних чи початкових значень (або і тих, і інших).

```cs
static async Task Setting_current_or_original_values()
{
    //...

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var blog = await context.Blogs.SingleAsync(e => e.Id == 1);

        var clonedBlog = (await context.Entry(blog).GetDatabaseValuesAsync()).ToObject();

        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    }

}
```
```
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: []
```
Зауважте, що ToObject повертає новий екземпляр, який не відстежується контекстом DbContext. Повернений об’єкт також не має встановлених зв’язків з іншими сутностями. Клонований об’єкт може стати в пригоді для вирішення проблем, пов’язаних із одночасним оновленням бази даних, особливо під час прив’язування даних до об’єктів певного типу. Докладнішу інформацію див. у розділі «Оптимістичне керування паралельним доступом» (optimistic concurrency).

## Робота з усіма навігаційними властивостями сутності

Властивість EntityEntry.Navigations повертає IEnumerable\<T\> об'єктів NavigationEntry для кожної навігаційної властивості сутності. EntityEntry.References та EntityEntry.Collections виконують ту саму функцію, але обмежені навігацією по посиланнях або колекціях відповідно. Це можна використовувати для виконання дії для кожної навігації сутності. Наприклад, щоб примусово завантажити всі пов’язані сутності:

```cs
foreach (var navigationEntry in context.Entry(blog).Navigations)
{
    navigationEntry.Load();
}
```
Повний приклад

```cs
static async Task Work_with_all_navigations_of_an_entity()
{
    var context = new ApplicationDbContextFactory().CreateDbContext(null);
    var blog = await context.Blogs.SingleAsync(e => e.Id == 1);
    
    Console.WriteLine(blog.Posts.Count);
    
    foreach (var navigationEntry in context.Entry(blog).Navigations)
    {
        navigationEntry.Load();
    }

    Console.WriteLine(blog.Posts.Count);
}
```
```
0
2
```

## Робота з усіма членами сутності

Звичайні властивості та навігаційні властивості мають різний стан і поведінку. Тому зазвичай навігаційні та ненавігаційні властивості обробляють окремо, як показано в наведених вище розділах. Однак іноді може бути корисно виконати певну дію з будь-яким членом сутності, незалежно від того, чи є він звичайною властивістю, чи навігаційною властивістю. Для цієї мети передбачено EntityEntry.Member та EntityEntry.Members. Наприклад:

```cs
static async Task Work_with_all_members_of_an_entity()
{
    var context = new ApplicationDbContextFactory().CreateDbContext(null);
    var blog = await context.Blogs.Include(e => e.Posts).SingleAsync(e => e.Id == 1);
    
    foreach (var memberEntry in context.Entry(blog).Members)
    {
        Console.WriteLine($"{memberEntry.Metadata.Name}\t{memberEntry.Metadata.ClrType.ShortDisplayName()}\t{memberEntry.CurrentValue}");
    }
}
```
```
Id      int     1
Name    string  .NET Blog
Posts   IList<Post>     System.Collections.Generic.List`1[Draft.Models.Post]
```

    Порада

    Режим налагоджувального перегляду (debug view) засобу відстеження змін відображає інформацію такого вигляду. Режим налагоджувального перегляду для всього механізму відстеження змін формується на основі окремих властивостей EntityEntry.DebugView для кожної відстежуваної сутності.


# Find та FindAsync

Методи DbContext.Find, DbContext.FindAsync, DbSet\<TEntity\>.Find та DbSet\<TEntity\>.FindAsync призначені для ефективного пошуку окремої сутності за відомим первинним ключем. Метод Find спочатку перевіряє, чи відстежується вже сутність, і якщо так — одразу її повертає. Запит до бази даних виконується лише тоді, коли сутність не відстежується локально. Наприклад, розгляньмо цей код, який двічі викликає Find для однієї й тієї самої сутності:

```cs
static async Task Find_and_FindAsync()
{
    var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Console.WriteLine("First call to Find...");
    var blog1 = await context.Blogs.FindAsync(1);

    Console.WriteLine($"...found blog {blog1.Name}\n");

    Console.WriteLine("Second call to Find...");
    var blog2 = await context.Blogs.FindAsync(1);
    Console.WriteLine(blog1 == blog2);

    Console.WriteLine("...returned the same instance without executing a query.");
}
```
```
First call to Find...
info: 03.10.2026 11:42:18.668 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (197ms) [Parameters=[@__p_0='1'], CommandType='Text', CommandTimeout='30']
      SELECT TOP(1) [b].[Id], [b].[Name]
      FROM [Blogs] AS [b]
      WHERE [b].[Id] = @__p_0
...found blog .NET Blog

Second call to Find...
True
...returned the same instance without executing a query.
```
Зауважте, що перший виклик не знаходить сутність локально, а отже, виконує запит до бази даних. Натомість другий виклик повертає той самий екземпляр без звернення до бази даних, оскільки він уже відстежується.

Метод Find повертає null, якщо сутність із заданим ключем не відстежується локально й відсутня в базі даних.

## Складені ключі

Метод Find також можна використовувати зі складеними ключами. Наприклад, розгляньмо сутність OrderLine із складеним ключем, що складається з ідентифікатора замовлення та ідентифікатора товару:

```cs
public class OrderLine
{
    public int OrderId { get; set; }
    public int ProductId { get; set; }

    //...
}
```
Складений ключ необхідно налаштувати в методі DbContext.OnModelCreating, щоб визначити частини ключа та їхній порядок. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder
        .Entity<OrderLine>()
        .HasKey(e => new { e.OrderId, e.ProductId });
}
```
Зверніть увагу, що OrderId є першою частиною ключа, а ProductId — другою. Цього порядку слід дотримуватися під час передачі значень ключа методу Find. Наприклад:

```cs
int orderId = 1;
int productId = 2;

var orderline = await context.OrderLines.FindAsync(orderId, productId);
```

# Використання ChangeTracker.Entries для доступу до всіх відстежуваних сутностей

Досі ми зверталися лише до одного об'єкта EntityEntry за раз. Метод ChangeTracker.Entries() повертає EntityEntry для кожної сутності, що наразі відстежується контекстом DbContext.

```cs
static async Task Using_ChangeTracker_Entries_to_access_all_tracked_entities()
{
    var context = new ApplicationDbContextFactory().CreateDbContext(null);
    List<Blog> blogs = await context.Blogs.Include(e => e.Posts).ToListAsync();

    foreach (var entityEntry in context.ChangeTracker.Entries())
    {
        Console.WriteLine($"{entityEntry.Metadata.DisplayName()}\t{entityEntry.Property("Id").CurrentValue}");
    }
}
```
```
info: 04.10.2026 11:34:12.926 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command)
      Executed DbCommand (68ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT [b].[Id], [b].[Name], [p].[Id], [p].[BlogId], [p].[Content], [p].[Title]
      FROM [Blogs] AS [b]
      LEFT JOIN [Posts] AS [p] ON [b].[Id] = [p].[BlogId]
      ORDER BY [b].[Id]
Blog    1
Post    1
Post    2
```
Зверніть увагу, що повертаються записи як для блогів, так і для дописів. Натомість результати можна відфільтрувати за конкретним типом сутності, використовуючи узагальнену перевантажену версію методу ChangeTracker.Entries\<TEntity\>():

```cs
static async Task Using_ChangeTracker_Entries_to_access_all_tracked_entities()
{
    //...
    
    foreach (var entityEntry in context.ChangeTracker.Entries<Post>())
    {
        Console.WriteLine($"{entityEntry.Metadata.DisplayName()}\t{entityEntry.Property("Id").CurrentValue}");
    }
}
```
```
Post    1
Post    2
```
Крім того, використання узагальненої перевантаженої версії методу повертає узагальнені екземпляри EntityEntry\<TEntity\>. Саме це й забезпечує такий зручний (у стилі fluent API) доступ до властивості `Id` у цьому прикладі.

Узагальнений тип, що використовується для фільтрації, не обов’язково має бути типом зіставленої сутності; натомість можна використовувати незіставлений базовий тип або інтерфейс. Наприклад, якщо всі типи сутностей у моделі реалізують інтерфейс, що визначає їхню властивість ключа:

```cs
public interface IEntityWithKey
{
    int Id { get; set; }
}
```
Тоді цей інтерфейс можна використовувати для роботи з ключем будь-якої відстежуваної сутності з дотриманням суворої типізації. Наприклад:

```cs
    foreach (var entityEntry in context.ChangeTracker.Entries<IEntityWithKey>())
    {
        Console.WriteLine($"{entityEntry.Metadata.DisplayName()}\t{entityEntry.Property("Id").CurrentValue}");
    }
```
```
Blog    1
Post    1
Post    2
```

# Використання DbSet.Local для запитів до відстежуваних сутностей

Запити EF Core завжди виконуються на рівні бази даних і повертають лише ті сутності, що були в ній збережені. DbSet\<TEntity\>.Local надає механізм для отримання з DbContext локальних сутностей, що відстежуються.

Оскільки властивість DbSet.Local використовується для запитів до відстежуваних сутностей, зазвичай спочатку завантажують сутності в DbContext, а потім працюють із ними. Це особливо актуально для прив’язки даних, але може бути корисним і в інших ситуаціях. Наприклад, у наведеному нижче коді спочатку надсилається запит до бази даних для отримання всіх блогів і дописів. Метод розширення Load використовується для виконання цього запиту так, щоб отримані результати відстежувалися контекстом, але не поверталися безпосередньо до програми. (Використання ToList або аналогічних методів дає такий самий результат, але супроводжується додатковими витратами ресурсів на створення списку, що в цьому випадку не потрібно.) Далі в прикладі використовується властивість DbSet.Local для доступу до сутностей, що відстежуються локально:

```cs
static async Task Using_DbSet_Local_to_query_tracked_entities()
{
    var context = new ApplicationDbContextFactory().CreateDbContext(null);

    await context.Blogs.Include(e => e.Posts).LoadAsync();

    foreach (var blog in context.Blogs.Local)
    {
        Console.WriteLine($"Blog: {blog.Name}");
    }

    foreach (var post in context.Posts.Local)
    {
        Console.WriteLine($"Post: {post.Title}");
    }
}
```
```
Blog: .NET Blog
Post: Announcing the Release of EF Core 5.0
Post: Announcing F# 5
```
Зауважте, що на відміну від ChangeTracker.Entries(), DbSet.Local повертає безпосередньо екземпляри сутностей. Звісно, ​​для повернутої сутності завжди можна отримати об'єкт EntityEntry, викликавши DbContext.Entry.

## Локальне представлення

Властивість DbSet\<TEntity\>.Local повертає представлення локально відстежуваних сутностей, яке відображає поточний стан EntityState цих сутностей. Зокрема, це означає, що:

* Сутності зі станом «Added» (додані) також включаються. Зауважте, що у випадку зі звичайними запитами EF Core ситуація інша: оскільки додані сутності ще не існують у базі даних, запит до неї їх не повертає. 
* Deleted сутності виключаються. Зауважте, що у випадку зі звичайними запитами EF Core ситуація інша: видалені сутності все ще існують у базі даних, тому вони повертаються під час виконання запитів до неї.

Усе це означає, що DbSet.Local — це представлення даних, яке відображає поточний концептуальний стан графа сутностей, включаючи сутності зі статусом Added та виключаючи сутності зі статусом Deleted. Це відповідає стану бази даних, який очікується після виклику методу SaveChanges.

Зазвичай це ідеальне представлення для прив’язки даних, оскільки воно відображає дані в тому вигляді, в якому їх розуміє користувач, з урахуванням змін, внесених програмою. Наведений нижче код демонструє це, позначаючи один допис як Deleted, а потім додаючи новий допис і позначаючи його як Added:

```cs
static async Task Using_DbSet_Local_to_query_tracked_entities_2()
{
    var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var posts = await context.Posts.Include(e => e.Blog).ToListAsync();

    Console.WriteLine("Local view after loading posts:");

    foreach (var post in context.Posts.Local)
    {
        Console.WriteLine($"  Post: {post.Title}");
    }

    context.Add(//!!!
    new Post
    {
        Title = "What’s next for System.Text.Json?",
        Content = ".NET 5.0 was released recently and has come with many...",
        Blog = posts[0].Blog
    });

    context.Remove(posts[1]); //!!!

    Console.WriteLine("Local view after adding and deleting posts:");

    foreach (var post in context.Posts.Local)
    {
        Console.WriteLine($"  Post: {post.Title}");
    }
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
```
Local view after loading posts:
  Post: Announcing the Release of EF Core 5.0
  Post: Announcing F# 5
Local view after adding and deleting posts:
  Post: What's next for System.Text.Json?
  Post: Announcing the Release of EF Core 5.0
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: -2147482647}]
Post {Id: -2147482647} Added
    Id: -2147482647 PK Temporary
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
```
Зверніть увагу, що видалений допис зникає з локального представлення, а доданий — з’являється в ньому.

## Використання властивості Local для додавання та видалення сутностей

Властивість DbSet\<TEntity\>.Local повертає екземпляр LocalView\<TEntity\>. Це реалізація інтерфейсу ICollection<T>, яка генерує сповіщення та реагує на них у разі додавання сутностей до колекції чи їх видалення з неї. (Це той самий підхід, що й в ObservableCollection<T>, але реалізований як проєкція наявних записів відстеження змін EF Core, а не як незалежна колекція.) 

Сповіщення локального подання інтегровано з механізмом відстеження змін DbContext таким чином, що локальне подання залишається синхронізованим із DbContext. Зокрема:

* Додавання нової сутності до DbSet.Local призводить до того, що DbContext починає відстежувати її — зазвичай зі станом Added. (Якщо сутність уже має згенероване значення ключа, вона відстежується зі станом Unchanged.)
* Видалення сутності з DbSet.Local призводить до того, що вона позначається як Deleted.
* Сутність, яку починає відстежувати DbContext, автоматично з’являється в колекції DbSet.Local. Наприклад, виконання запиту на отримання додаткових сутностей автоматично призводить до оновлення локального представлення.
* Об'єкт, позначений як видалений, буде автоматично вилучено з локальної колекції.

Це означає, що локальне представлення можна використовувати для керування відстежуваними сутностями — достатньо просто додавати їх до колекції або видаляти з неї. Наприклад, змінімо код із попереднього прикладу, щоб додавати до локальної колекції дописи та видаляти їх із неї:

```cs
static async Task Using_DbSet_Local_to_query_tracked_entities_3() 
{
    var context = new ApplicationDbContextFactory().CreateDbContext(null);

    var posts = await context.Posts.Include(e => e.Blog).ToListAsync();

    Console.WriteLine("Local view after loading posts:");

    foreach (var post in context.Posts.Local)
    {
        Console.WriteLine($"  Post: {post.Title}");
    }

    context.Posts.Local.Remove(posts[1]); //!!!

    context.Posts.Local.Add( //!!!
        new Post
        {
            Title = "What’s next for System.Text.Json?",
            Content = ".NET 5.0 was released recently and has come with many...",
            Blog = posts[0].Blog
        });

    Console.WriteLine("Local view after adding and deleting posts:");

    foreach (var post in context.Posts.Local)
    {
        Console.WriteLine($"  Post: {post.Title}");
    }
    Console.WriteLine(context.ChangeTracker.DebugView.LongView);
}
```
```
Local view after loading posts:
  Post: Announcing the Release of EF Core 5.0
  Post: Announcing F# 5
Local view after adding and deleting posts:
  Post: What's next for System.Text.Json?
  Post: Announcing the Release of EF Core 5.0
Blog {Id: 1} Unchanged
    Id: 1 PK
    Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: -2147482647}]
Post {Id: -2147482647} Added
    Id: -2147482647 PK Temporary
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
```
Результат залишається таким самим, як і в попередньому прикладі, оскільки зміни, внесені до локального подання, синхронізуються з DbContext.

## Використання локального подання для прив’язки даних у Windows Forms або WPF

Властивість DbSet<TEntity>.Local слугує основою для прив’язування даних до сутностей EF Core. Однак і Windows Forms, і WPF найкраще працюють тоді, коли використовуються з тими конкретними типами колекцій із підтримкою сповіщень, на які вони розраховані. Локальне подання підтримує створення саме таких типів колекцій:

* LocalView\<TEntity\>.ToObservableCollection() повертає ObservableCollection<T> для прив’язки даних у WPF.
* LocalView\<TEntity\>.ToBindingList() повертає BindingList\<T\> для прив’язки даних у Windows Forms.

```cs
ObservableCollection<Post> observableCollection = context.Posts.Local.ToObservableCollection();
BindingList<Post> bindingList = context.Posts.Local.ToBindingList();
```

    Порада

    Локальне представлення (local view) для конкретного екземпляра DbSet створюється «ліниво» (за запитом) під час першого звернення до нього, після чого кешується. Створення самого об'єкта LocalView відбувається швидко й не потребує значного обсягу пам'яті. Однак цей метод викликає DetectChanges, що може працювати повільно за наявності великої кількості сутностей. Колекції, що створюються методами ToObservableCollection і ToBindingList, також формуються «ліниво» (з відкладеним виконанням) і згодом кешуються. Обидва ці методи створюють нові колекції, що може відбуватися повільно й потребувати значного обсягу пам’яті, коли йдеться про тисячі сутностей.