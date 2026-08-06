# Оперативне завантаження

Ви можете використовувати метод Include, щоб указати пов’язані дані, які потрібно включити до результатів запиту. У наведеному нижче прикладі властивість Posts блогів, що повертаються в результатах, буде заповнена пов’язаними публікаціями.

```cs
    var blogs = await context.Blogs
        .Include(b => b.Posts)
        .ToListAsync();
```

Entity Framework Core автоматично виправляє властивості навігації для будь-яких інших сутностей, які раніше були завантажені в екземпляр контексту. Тож навіть якщо ви явно не включаєте дані для властивості навігації, властивість все одно може бути заповнена, якщо деякі або всі пов'язані сутності були завантажені раніше.

Ви можете включити пов'язані дані з кількох зв'язків в один запит.

```cs
    var blogs = await context.Blogs
            .Include(blog => blog.Posts)
            .Include(blog => blog.Owner)
            .ToListAsync();

    foreach (var blog in blogs)
    {
        Console.WriteLine($"Blog: {blog.Url}, Owner: {blog.Owner?.Name}, Posts: {blog.Posts.Count}");
    }
```

Увага

Оперативне завантаження навігації колекції в одному запиті може спричинити проблеми з продуктивністю. Для отримання додаткової інформації див. розділ Одинарні та розділені запити.

## Включення кількох рівнів

Ви можете деталізувати зв'язки, щоб включити кілька рівнів пов'язаних даних за допомогою методу ThenInclude. У наступному прикладі завантажуються всі блоги, пов’язані з ними публікації та автор кожної публікації.

```cs
    var blogs = await context.Blogs
        .Include(blog => blog.Posts)
        .ThenInclude(post => post.Author)
        .ToListAsync();
```
Ви можете об'єднати кілька викликів ThenInclude в ланцюжок, щоб продовжити включати подальші рівні пов'язаних даних.

```cs
    var blogs = await context.Blogs
        .Include(blog => blog.Posts)
        .ThenInclude(post => post.Author)
        .ThenInclude(author => author.Photo)
        .ToListAsync();
```
Ви можете об'єднати всі виклики, щоб включити пов'язані дані з кількох рівнів та кількох коренів в один запит.

```cs
    var blogs = await context.Blogs
        .Include(blog => blog.Posts)
        .ThenInclude(post => post.Author)
        .ThenInclude(author => author.Photo)
        .Include(blog => blog.Owner)
        .ThenInclude(owner => owner.Photo)
        .ToListAsync();
```
Можливо, вам знадобиться включити кілька пов'язаних сутностей для однієї з сутностей, що включаються. Наприклад, під час запиту блогів ви включаєте публікації, а потім хочете включити як автора, так і теги публікацій. Щоб включити обидва, потрібно вказати кожен шлях включення, починаючи з кореня. Наприклад, Blog -> Posts -> Author та Blog -> Posts -> Tags. Це не означає, що ви отримаєте надлишкові об'єднання; у більшості випадків EF об'єднає об'єднання під час генерації SQL.

```cs
    var blogs = await context.Blogs
        .Include(blog => blog.Posts)
        .ThenInclude(post => post.Author)
        .Include(blog => blog.Posts)
        .ThenInclude(post => post.Tags)
        .ToListAsync();
```
Ви також можете завантажити кілька навігацій за допомогою одного методу Include. Це можливо для навігаційних "ланцюжків", які є посиланнями, або коли вони закінчуються однією колекцією.

```cs
    var blogs = await context.Blogs
        .Include(blog => blog.Owner.AuthoredPosts)
        .ThenInclude(post => post.Blog.Owner.Photo)
        .ToListAsync();
```

## Фільтроване включення

Під час застосування методу Include для завантаження пов'язаних даних можна додати певні перелічувані операції до навігації колекції, що дозволяє фільтрувати та сортувати результати.

Підтримувані операції: Where, OrderBy, OrderByDescending, ThenBy, ThenByDescending, Skip та Take.

Такі операції слід застосовувати до навігації колекції в лямбда-виразі, що передається методу Include, як показано в прикладі нижче:

```cs
    var filteredBlogs = await context.Blogs
        .Include(
            blog => blog.Posts
                .Where(post => post.BlogId == 1)
                .OrderByDescending(post => post.Title)
                .Take(5))
        .ToListAsync();
```

Кожна включена навігація дозволяє лише один унікальний набір операцій фільтрації. У випадках, коли для заданої навігації колекції (blog.Posts у прикладах нижче) застосовується кілька операцій Include, операції фільтрації можна вказати лише для однієї з них:

```cs
    var blogs = await context.Blogs
        .Include(blog => blog.Posts.Where(post => post.BlogId == 1))
        .ThenInclude(post => post.Author)
        .Include(blog => blog.Posts)
        .ThenInclude(post => post.Tags.OrderBy(postTag => postTag.TagId).Skip(3))
        .ToListAsync();
```
Або ж ідентичні операції можна застосовувати для кожної навігації, яка включається кілька разів:

```cs
    var filteredBlogs = await context.Blogs
        .Include(blog => blog.Posts.Where(post => post.BlogId == 1))
        .ThenInclude(post => post.Author)
        .Include(blog => blog.Posts.Where(post => post.BlogId == 1))
        .ThenInclude(post => post.Tags.OrderBy(postTag => postTag.TagId).Skip(3))
        .ToListAsync();
```

Увага

У випадку запитів відстеження результати фільтрованого включення можуть бути неочікуваними через виправлення навігації. Усі відповідні сутності, які були запитувані раніше та збережені у Відстежувачі змін, будуть присутні в результатах запиту фільтрованого включення, навіть якщо вони не відповідають вимогам фільтра. У таких ситуаціях розгляньте можливість використання запитів NoTracking або повторного створення DbContext під час використання фільтрованого включення. Усі відповідні сутності, які раніше запитувалися та зберігалися у Відстежувачі змін, будуть присутні в результатах запиту «Відфільтроване включення», навіть якщо вони не відповідають вимогам фільтра. У таких ситуаціях, використовуючи «Відфільтроване включення», спробуйте використовувати запити NoTracking або повторно створіть DbContext.

```cs
var orders = await context.Orders.Where(o => o.Id > 1000).ToListAsync();

// customer entities will have references to all orders where Id > 1000, rather than > 5000
var filtered = await context.Customers.Include(c => c.Orders.Where(o => o.Id > 5000)).ToListAsync();
```

Примітка

У випадку запитів відстеження, навігація, до якої було застосовано фільтроване включення, вважається завантаженою. Це означає, що EF Core не намагатиметься перезавантажити свої значення за допомогою явного або відкладеного завантаження, навіть якщо деякі елементи можуть бути відсутніми.

## Включення для похідних типів

Ви можете включати пов'язані дані з навігації, визначеної лише для похідного типу, за допомогою Include та ThenInclude.

Враховуючи наступну модель:

```cs
public class SchoolContext : DbContext
{
    public DbSet<Person> People { get; set; }
    public DbSet<School> Schools { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<School>().HasMany(s => s.Students).WithOne(s => s.School);
    }
}

public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Student : Person
{
    public School School { get; set; }
}

public class School
{
    public int Id { get; set; }
    public string Name { get; set; }

    public List<Student> Students { get; set; }
}
```

Вміст навігації School для всіх людей, які є учнями, можна швидко завантажити за допомогою багатьох шаблонів:

Використання кастингу

```cs
context.People.Include(person => ((Student)person).School).ToList()
```

Використання оператора as

```cs
context.People.Include(person => (person as Student).School).ToList()
```
Використання перевантаження Include, яке приймає параметр типу string.

```cs
context.People.Include("School").ToList()
```

## Конфігурація моделі для автоматичного включення навігації

Ви можете налаштувати навігацію в моделі так, щоб вона включалася щоразу, коли сутність завантажується з бази даних, використовуючи метод AutoInclude. Це має той самий ефект, що й визначення параметра Include з навігацією в кожному запиті, де тип сутності повертається в результатах. У наступному прикладі показано, як налаштувати автоматичне включення навігації.

```cs
modelBuilder.Entity<Theme>().Navigation(e => e.ColorScheme).AutoInclude();
```
Після вищезазначеної конфігурації, виконання запиту, як показано нижче, завантажить навігацію ColorScheme для всіх тем у результатах.

```cs
    var themes = await context.Themes.ToListAsync();
```

Ця конфігурація застосовується до кожної сутності, що повертається в результаті, незалежно від того, як вона відображалася в результатах. Це означає, що якщо сутність знаходиться в результаті використання навігації, використання Include поверх іншого типу сутності або конфігурації автоматичного включення, будуть завантажені всі автоматично включені для неї навігації. Це саме правило поширюється на навігації, налаштовані як автоматично включені для похідного типу сутності.

Якщо для певного запиту ви не хочете завантажувати пов'язані дані через навігацію, яка налаштована на рівні моделі для автоматичного включення, ви можете використовувати метод IgnoreAutoIncludes у своєму запиті. Використання цього методу зупинить завантаження всіх навігацій, налаштованих користувачем як автоматично включені. Виконання запиту, подібного до наведеного нижче, поверне всі теми з бази даних, але не завантажить ColorScheme, навіть якщо вона налаштована як автоматично включена навігація.

```cs
    var themes = await context.Themes.IgnoreAutoIncludes().ToListAsync();
```

Навігація до owned типів також налаштована як автоматично включена за домовленістю, і використання IgnoreAutoIncludes API не зупиняє її включення. Вона все одно буде включена до результатів запиту.