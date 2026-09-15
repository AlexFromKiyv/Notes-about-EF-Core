# Відключені сутності

Екземпляр DbContext автоматично відстежуватиме об’єкти, що повертаються з бази даних. Зміни, внесені до цих об’єктів, будуть виявлені під час виклику SaveChanges, і база даних буде оновлена ​​за потреби. Див. розділ «Основне збереження та пов’язані дані» для отримання детальної інформації.

Однак, іноді сутності запитуються за допомогою одного екземпляра контексту, а потім зберігаються за допомогою іншого екземпляра. Це часто трапляється в "відокремлених" сценаріях, таких як веб-застосунок, де сутності запитуються, надсилаються клієнту, змінюються, надсилаються назад на сервер у запиті, а потім зберігаються. У цьому випадку другий екземпляр контексту повинен знати, чи є сутності новими (їх слід вставити), чи існуючими (їх слід оновити).

    Порада

    EF Core може відстежувати лише один екземпляр будь-якої сутності з заданим значенням первинного ключа.
    Найкращий спосіб уникнути цієї проблеми — використовувати короткочасний контекст для кожної одиниці роботи, щоб контекст спочатку був порожнім, до нього були приєднані сутності, зберігав ці сутності, а потім контекст видалявся та відкидався.

Модель для прикладу.

```cs
public abstract class EntityBase
{
    [NotMapped]
    public bool IsNew { get; set; }

    [NotMapped]
    public bool IsDeleted { get; set; }

    [NotMapped]
    public bool IsChanged { get; set; }
}

public class Blog : EntityBase
{
    public int BlogId { get; set; }
    public string Url { get; set; } = string.Empty;
    public List<Post> Posts { get; set; }

    public override string? ToString()
    {
        return $"{BlogId}\t{Url}";
    }
}

public class Post :EntityBase
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}
```


# Ідентифікація нових сутностей

### Клієнт ідентифікує нові сутності

Найпростіший випадок, який потрібно розглянути, це коли клієнт повідомляє сервер, чи є об'єкт новим, чи існуючим. Наприклад, часто запит на вставку нової сутності відрізняється від запиту на оновлення існуючої сутності.

Решта цього розділу охоплює випадки, коли необхідно якимось іншим способом визначити, чи слід вставляти, чи оновлювати.

## З автоматично згенерованими ключами

Значення автоматично згенерованого ключа часто можна використовувати для визначення того, чи потрібно вставляти або оновлювати сутність. Якщо ключ не встановлено (тобто він все ще має значення CLR за замовчуванням null, 0 тощо), то сутність має бути новою та потребує вставки. З іншого боку, якщо значення ключа було встановлено, то воно, мабуть, вже було збережено раніше і тепер потребує оновлення. Іншими словами, якщо ключ має значення, то сутність була запитана, надіслана клієнту та тепер повернулася для оновлення. 

Легко перевірити наявність невстановленого ключа, коли тип сутності відомий:

```cs
static bool IsItNew(Blog blog) => blog.BlogId == 0;
```
Використаємо для додавання.

```cs
static async Task CheckIsItNew1(ApplicationDbContext context)
{
    Blog blog = new Blog { Url = "http://sample.com" };

    // Key is not set for a new entity
    Console.WriteLine($"  Blog entity is {(IsItNew(blog) ? "new" : "existing")}.");

    context.Add(blog);
    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);

    // Key is now set
    Console.WriteLine($"  Blog entity is {(IsItNew(blog) ? "new" : "existing")}.");

    static bool IsItNew(Blog blog) => blog.BlogId == 0;
}
```
```
  Blog entity is new.
1
  Blog entity is existing.
```

Однак, EF також має вбудований спосіб зробити це для будь-якого типу сутності та типу ключа:

```cs
static bool IsItNew(DbContext context, object entity)
    => !context.Entry(entity).IsKeySet;
```
```cs
static async Task CheckIsItNew2(ApplicationDbContext context)
{
    Blog blog = new Blog { Url = "http://sample.com" };

    context.Add(blog);
    // Key is not set for a new entity
    Console.WriteLine($"  Blog entity is {(IsItNew(context, blog) ? "new" : "existing")}.");
    Console.WriteLine(blog.BlogId);

    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);

    // Key is now set
    Console.WriteLine($"  Blog entity is {(IsItNew(context, blog) ? "new" : "existing")}.");

    static bool IsItNew(DbContext context, object entity)
    => !context.Entry(entity).IsKeySet;
}
```
```
  Blog entity is new.
0
1
  Blog entity is existing.
```


    Порада

    Ключі встановлюються, щойно контекст відстежує сутності, навіть якщо сутність перебуває у стані Added. Це допомагає під час перегляду графу сутностей та вирішення питань щодо дій з кожною з них, наприклад, під час використання TrackGraph API. Ключове значення слід використовувати лише так, як показано тут, перед будь-яким викликом для відстеження сутності.


## З іншими ключами

Потрібен якийсь інший механізм для ідентифікації нових сутностей, коли значення ключів не генеруються автоматично. Для ідентифікації нових сутностей, коли ключові значення не генеруються автоматично, потрібен інший механізм. Існує два загальних підходи до цього:

* Запит до сутності
* Передати прапорець від клієнта

Щоб запросити сутність, просто скористайтеся методом Find:

```cs
public static async Task<bool> IsItNew(BloggingContext context, Blog blog)
    => (await context.Blogs.FindAsync(blog.BlogId)) == null;
```

Показати повний код для передачі прапорця від клієнта виходить за рамки цього документа. У веб-застосунку це зазвичай означає створення різних запитів для різних дій або передачу певного стану в запиті з подальшим його вилученням у контролері.



# Збереження окремих об'єктів

Якщо відомо, чи потрібна вставка або оновлення, тоді можна відповідно використовувати або Add, або Update:


```cs
static async Task<int> Insert(DbContext context, object entity)
{
    context.Add(entity);
    return await context.SaveChangesAsync();
}
static async Task<int> Update(DbContext context, object entity)
{
    context.Update(entity);
    return await context.SaveChangesAsync();
}
static async Task UsingInsertAndUpdateSingleEntity() 
{
    var blog = new Blog { Url = "http://sample.com" };


    // Inserting
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        Console.WriteLine($"  Inserting with URL {blog.Url}");
        int count = await Insert(context, blog);
        Console.WriteLine(count);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        Console.WriteLine($"  Found with URL {(await context.Blogs.SingleAsync(b => b.BlogId == blog.BlogId)).Url}");
    }

    //Updating
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        blog.Url = "https://sample.com";
        Console.WriteLine($"  Updating with URL {blog.Url}");
        int count = await Update(context, blog);
        Console.WriteLine(count);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        Console.WriteLine($"  Found with URL {(await context.Blogs.SingleAsync(b => b.BlogId == blog.BlogId)).Url}");
    }
}
```

```
  Inserting with URL http://sample.com
1
  Found with URL http://sample.com
  Updating with URL https://sample.com
1
  Found with URL https://sample.com
```

Однак, якщо сутність використовує автоматично згенеровані значення ключів, то метод Update можна використовувати в обох випадках:

```cs
static async Task<int> InsertOrUpdate(DbContext context, object entity)
{
    context.Update(entity);
    return await context.SaveChangesAsync();
}

static async Task UsingInsertOrUpdateSingleEntity()
{
    var blog = new Blog { Url = "http://sample.com" };

    // Inserting
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        Console.WriteLine($"  Inserting with URL {blog.Url}");
        int count = await InsertOrUpdate(context, blog);
        Console.WriteLine(count);
    }

    //Updating
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        blog.Url = "https://sample.com";
        Console.WriteLine($"  Updating with URL {blog.Url}");
        int count = await InsertOrUpdate(context, blog);
        Console.WriteLine(count);
    }
}

```
```
  Inserting with URL http://sample.com
1
  Updating with URL https://sample.com
1
```

Метод Update зазвичай позначає сутність для оновлення, а не для вставки. Однак, якщо сутність має автоматично згенерований ключ, і значення ключа не було встановлено, тоді сутність автоматично позначається для вставки. 

Якщо сутність не використовує автоматично згенеровані ключі, то програма повинна вирішити, чи слід вставляти сутність, чи оновлювати її: Наприклад:

```cs
static async Task<int> InsertOrUpdateNoAutoGenerationKey(ApplicationDbContext context, Blog blog)
{
    var existingBlog = await context.Blogs.FindAsync(blog.BlogId);
    if (existingBlog == null)
    {
        context.Add(blog);
    }
    else
    {
        context.Entry(existingBlog).CurrentValues.SetValues(blog);
    }
    return await context.SaveChangesAsync();
}

static async Task UsingInsertOrUpdateNoAutoGenerationKeySingleEntity()
{
    var blog = new Blog { Url = "http://sample.com" };

    // Inserting
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        Console.WriteLine($"  Inserting with URL {blog.Url}");
        int count = await InsertOrUpdateNoAutoGenerationKey(context, blog);
        Console.WriteLine(count);
    }

    //Updating
    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        blog.Url = "https://sample.com";
        Console.WriteLine($"  Updating with URL {blog.Url}");
        int count = await InsertOrUpdateNoAutoGenerationKey(context, blog);
        Console.WriteLine(count);
    }
}
```
```
  Inserting with URL http://sample.com
1
  Updating with URL https://sample.com
1
```
Кроки тут такі:

* Якщо Find повертає значення null, то база даних ще не містить блогу з цим ідентифікатором, тому ми викликаємо Add, щоб позначити його для вставки.
* Якщо Find повертає сутність, то вона існує в базі даних, і контекст тепер відстежує існуючу сутність.
    * Потім ми використовуємо SetValues, щоб встановити значення для всіх властивостей цієї сутності рівними тим, що надійшли від клієнта.
    * Виклик SetValues ​​позначить сутність для оновлення за потреби.


    Порада

    Функція SetValues ​​позначатиме як змінені лише ті властивості, значення яких відрізняються від значень у відстежуваній сутності. Це означає, що під час надсилання оновлення будуть оновлені лише ті стовпці, які фактично змінилися. (А якщо нічого не змінилося, то оновлення взагалі не буде надіслано.)

## Робота з графами

### Розв'язання ідентичності

Як зазначалося вище, EF Core може відстежувати лише один екземпляр будь-якої сутності із заданим значенням первинного ключа. Під час роботи з графами граф в ідеалі має бути створений таким чином, щоб цей інваріант зберігався, а контекст мав використовуватися лише для однієї одиниці роботи. Якщо граф містить дублікати, то перед надсиланням до EF його необхідно обробити, щоб об'єднати кілька екземплярів в один. Це може бути нетривіально, коли екземпляри мають конфліктні значення та зв'язки, тому консолідацію дублікатів слід виконувати якомога швидше у вашому конвеєрі застосунку, щоб уникнути вирішення конфліктів.

### Усі нові/всі існуючі об’єкти

Прикладом роботи з графіками є вставка або оновлення блогу разом з колекцією пов'язаних з ним публікацій. Якщо всі сутності в графі потрібно вставити або всі потрібно оновити, то процес такий самий, як описано вище для окремих сутностей. Наприклад, графік створених блогів та публікацій можна вставити таким чином:

```cs
    await UsingInsertGraph(context);

static async Task<int> InsertGraph(DbContext context, object rootEntity)
{
    context.Add(rootEntity);
    return await context.SaveChangesAsync();
}

static async Task UsingInsertGraph(ApplicationDbContext context)
{
    Blog blog = CreateBlogAndPosts();
    Console.WriteLine($"  Inserting with URL {blog.Url} and {blog.Posts[0].Title}, {blog.Posts[1].Title}");
    int count = await InsertGraph(context, blog);
    Console.WriteLine(count);
}

static Blog CreateBlogAndPosts()
{
    Blog blog = new Blog
    {
        Url = "http://sample.com",
        Posts = new List<Post> 
        { 
            new Post { Title = "Post 1" }, 
            new Post { Title = "Post 2" }, 
        }
    };
    return blog;
}
```
```
  Inserting with URL http://sample.com and Post 1, Post 2
3
```
Виклик функції Add позначить блог та всі публікації для вставки.

Аналогічно, якщо потрібно оновити всі сутності в графі, тоді можна використовувати Update:

```cs
static async Task<int> UpdateGraph(DbContext context, object rootEntity)
{
    context.Update(rootEntity);
    return await context.SaveChangesAsync();
}

static async Task UsingUpdateGraph(ApplicationDbContext context)
{
    Blog? blog = await context.Blogs
        .Include(b => b.Posts)
        .SingleAsync(b => b.Url == "http://sample.com");

    blog.Url = "https://sample.com";
    blog.Posts[0].Title = "Post A";
    blog.Posts[1].Title = "Post B";

    Console.WriteLine($"  Updating with URL {blog.Url}");
    int count = await UpdateGraph(context, blog);
    Console.WriteLine(count);
}
```
```
  Updating with URL https://sample.com
3
```
Блог та всі його публікації будуть позначені для оновлення.

### Поєднання нових та існуючих сутностей

З автоматично згенерованими ключами, Update знову можна використовувати як для вставки, так і для оновлення, навіть якщо граф містить поєднання сутностей, які потребують вставки, і тих, які потребують оновлення:

```cs
static async Task<int> InsertOrUpdateGraph(DbContext context, object rootEntity)
{
    context.Update(rootEntity);
    return await context.SaveChangesAsync();
}

static async Task UsingInsertOrUpdateGraph()
{
    Blog blog = CreateBlogAndPosts();

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        Console.WriteLine($"  Inserting with URL {blog.Url} and {blog.Posts[0].Title}, {blog.Posts[1].Title}");
        int count = await InsertOrUpdateGraph(context,blog);
        Console.WriteLine(count);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var read = await context.Blogs.Include(b => b.Posts).SingleAsync(b => b.BlogId == blog.BlogId);
        Console.WriteLine($"  Found with URL {read.Url} and {read.Posts[0].Title}, {read.Posts[1].Title}");
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        blog.Url = "https://sample.com";
        blog.Posts[0].Title = "Post A";
        blog.Posts[1].Title = "Post B";
        blog.Posts.Add(new Post { Title = "New Post" });

        Console.WriteLine($"  Updating with URL {blog.Url}");
        int count = await InsertOrUpdateGraph(context, blog);
        Console.WriteLine(count);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var read = await context.Blogs.Include(b => b.Posts).SingleAsync(b => b.BlogId == blog.BlogId);
        Console.WriteLine($"  Found with URL {read.Url} and {read.Posts[0].Title}, {read.Posts[1].Title}, {read.Posts[2].Title} ");
    }
}
```
```
  Inserting with URL http://sample.com and Post 1, Post 2
3
  Found with URL http://sample.com and Post 1, Post 2
  Updating with URL https://sample.com
4
  Found with URL https://sample.com and Post A, Post B, New Post
```
Оновлення позначить будь-яку сутність на графі, блозі чи публікації для вставки, якщо для неї не встановлено ключове значення, тоді як усі інші сутності позначені для оновлення.

Як і раніше, коли не використовуються автоматично згенеровані ключі, можна використовувати запит та деяку обробку:

```cs
static async Task<int> InsertOrUpdateGraphNoAutoGenerationKeys(ApplicationDbContext context, Blog blog)
{
    var existingBlog = await context.Blogs
        .Include(b => b.Posts)
        .FirstOrDefaultAsync(b => b.BlogId == blog.BlogId);

    if (existingBlog == null)
    {
        context.Add(blog);
    }
    else
    {
        context.Entry(existingBlog).CurrentValues.SetValues(blog);
        foreach (var post in blog.Posts)
        {
            var existingPost = existingBlog.Posts
                .FirstOrDefault(p => p.PostId == post.PostId);

            if (existingPost == null)
            {
                existingBlog.Posts.Add(post);
            }
            else
            {
                context.Entry(existingPost).CurrentValues.SetValues(post);
            }
        }
    }

    return await context.SaveChangesAsync();
}

static async Task UsingInsertOrUpdateGraphNoAutoGenerationKeys()
{
    Blog blog = CreateBlogAndPosts();

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        Console.WriteLine($"  Inserting with URL {blog.Url} and {blog.Posts[0].Title}, {blog.Posts[1].Title}");
        int count = await InsertOrUpdateGraphNoAutoGenerationKeys(context, blog);
        Console.WriteLine(count);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var read = await context.Blogs.Include(b => b.Posts).SingleAsync(b => b.BlogId == blog.BlogId);
        Console.WriteLine($"  Found with URL {read.Url} and {read.Posts[0].Title}, {read.Posts[1].Title}");
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        blog.Url = "https://sample.com";
        blog.Posts[0].Title = "Post A";
        blog.Posts[1].Title = "Post B";
        blog.Posts.Add(new Post { Title = "New Post" });

        Console.WriteLine($"  Updating with URL {blog.Url}");
        int count = await InsertOrUpdateGraphNoAutoGenerationKeys(context, blog);
        Console.WriteLine(count);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var read = await context.Blogs.Include(b => b.Posts).SingleAsync(b => b.BlogId == blog.BlogId);
        Console.WriteLine($"  Found with URL {read.Url} and {read.Posts[0].Title}, {read.Posts[1].Title}, {read.Posts[2].Title} ");
    }
}
```
```
  Inserting with URL http://sample.com and Post 1, Post 2
3
  Found with URL http://sample.com and Post 1, Post 2
  Updating with URL https://sample.com
4
  Found with URL https://sample.com and Post A, Post B, New Post
```

### Обробка видалення

Операція видалення може бути складною, оскільки часто відсутність сутності означає, що її слід видалити. Один зі способів вирішення цієї проблеми — використання «м’якого видалення» (soft deletes), коли сутність позначається як видалена, а не видаляється фактично. Операції видалення фактично стають ідентичними операціям оновлення. «М’яке» видалення можна реалізувати за допомогою фільтрів запитів.

Для фактичного видалення часто застосовують підхід, що розширює шаблон запиту для виконання операції, яка, по суті, є порівнянням графів (graph diff).

```cs
static async Task<int> InsertUpdateOrDeleteGraph(ApplicationDbContext context, Blog blog)
{
    Blog? existingBlog = await context.Blogs
        .Include(b => b.Posts)
        .FirstOrDefaultAsync(b => b.BlogId == blog.BlogId);

    if (existingBlog == null)
    {
        context.Add(blog);
    }
    else
    {
        context.Entry(existingBlog).CurrentValues.SetValues(blog);
        foreach (var post in blog.Posts)
        {
            var existingPost = existingBlog.Posts
                .FirstOrDefault(p => p.PostId == post.PostId);

            if (existingPost == null)
            {
                existingBlog.Posts.Add(post);
            }
            else
            {
                context.Entry(existingPost).CurrentValues.SetValues(post);
            }
        }

        foreach (var post in existingBlog.Posts)
        {
            if (!blog.Posts.Any(p => p.PostId == post.PostId))
            {
                context.Remove(post);
            }
        }
    }
   return await context.SaveChangesAsync();
}

static async Task UsingInsertUpdateOrDeleteGraph()
{
    Console.WriteLine("Save graph with deletes and any kind of key:");
    Blog blog = CreateBlogAndPosts();

    using(var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        Console.WriteLine($"  Inserting with URL {blog.Url} and {blog.Posts[0].Title}, {blog.Posts[1].Title}");
        int count = await InsertUpdateOrDeleteGraph(context, blog);
        Console.WriteLine(count);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var read = await context.Blogs.Include(b => b.Posts).SingleAsync(b => b.BlogId == blog.BlogId);
        Console.WriteLine($"  Found with URL {read.Url} and {read.Posts[0].Title}, {read.Posts[1].Title}");
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        blog.Url = "https://sample.com";
        blog.Posts[0].Title = "Post A";
        blog.Posts.Remove(blog.Posts[1]);
        blog.Posts.Add(new Post { Title = "New Post" });

        Console.WriteLine($"  Updating with URL {blog.Url}");
        int count = await InsertUpdateOrDeleteGraph(context, blog);
        Console.WriteLine(count);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var read = await context.Blogs.Include(b => b.Posts).SingleAsync(b => b.BlogId == blog.BlogId);
        Console.WriteLine($"  Found with URL {read.Url} and {read.Posts[0].Title}, {read.Posts[1].Title}");
    }
}
```
```
Save graph with deletes and any kind of key:
  Inserting with URL http://sample.com and Post 1, Post 2
3
  Found with URL http://sample.com and Post 1, Post 2
  Updating with URL https://sample.com
4
  Found with URL https://sample.com and Post A, New Post
```

## TrackGraph

Внутрішньо методи Add, Attach та Update використовують механізм обходу графа, визначаючи для кожної сутності, чи слід позначити її як Added (для вставки), Modified (для оновлення), Unchanged (без змін) або Deleted (для видалення). Цей механізм доступний через API TrackGraph. Наприклад, припустімо, що коли клієнт надсилає назад граф сутностей, він встановлює для кожної сутності певний прапорець, який вказує, як саме її слід обробити. Після цього для обробки цього прапорця можна використовувати TrackGraph:

```cs
static async Task<int> SaveAnnotatedGraph(DbContext context, object rootEntity)
{
    context.ChangeTracker.TrackGraph(
        rootEntity,
        n =>
        {
            var entity = (EntityBase)n.Entry.Entity;
            n.Entry.State = entity.IsNew
                ? EntityState.Added
                : entity.IsChanged
                    ? EntityState.Modified
                    : entity.IsDeleted
                        ? EntityState.Deleted
                        : EntityState.Unchanged;
        });

    return await context.SaveChangesAsync();
}

static async Task UsingSaveAnnotatedGraph()
{
    Blog blog = CreateBlogAndPosts();
    blog.IsNew = true;
    blog.Posts[0].IsNew = true;
    blog.Posts[1].IsNew = true;

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        Console.WriteLine($"  Inserting with URL {blog.Url} and {blog.Posts[0].Title}, {blog.Posts[1].Title}");
        int count = await SaveAnnotatedGraph(context, blog);
        Console.WriteLine(count);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var read = await context.Blogs.Include(b => b.Posts).SingleAsync(b => b.BlogId == blog.BlogId);
        Console.WriteLine($"  Found with URL {read.Url} and {read.Posts[0].Title}, {read.Posts[1].Title}");
    }

    blog.IsNew = false;
    blog.Posts[0].IsNew = false;
    blog.Posts[1].IsNew = false;


    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        blog.Url = "https://sample.com";
        blog.IsChanged = true;
        blog.Posts[0].Title = "Post A";
        blog.Posts[0].IsDeleted = true;
        blog.Posts[1].Title = "Post B";
        blog.Posts.Add(new Post { Title = "New Post", IsNew = true });

        Console.WriteLine($"  Updating with URL {blog.Url}");
        int count = await SaveAnnotatedGraph(context, blog);
        Console.WriteLine(count);
    }

    using (var context = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        var read = await context.Blogs.Include(b => b.Posts).SingleAsync(b => b.BlogId == blog.BlogId);
        Console.WriteLine($"  Found with URL {read.Url} and {read.Posts[0].Title}, {read.Posts[1].Title}");
    }
}

```

```
  Inserting with URL http://sample.com and Post 1, Post 2
3
  Found with URL http://sample.com and Post 1, Post 2
  Updating with URL https://sample.com
3
  Found with URL https://sample.com and Post 2, New Post
```

Прапорці показано як частину сутності лише для спрощення прикладу. Зазвичай вони є частиною DTO або іншого стану, що передається в запиті.
