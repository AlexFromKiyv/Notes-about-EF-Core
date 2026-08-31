# Базове збереження

DbContext.SaveChanges() – це один із двох методів збереження змін у базі даних за допомогою EF. За допомогою цього методу ви виконуєте одну або кілька відстежуваних змін (додавання, оновлення, видалення), а потім застосовуєте ці зміни, викликаючи метод SaveChanges. Як альтернативу, ExecuteUpdate та ExecuteDelete можна використовувати без залучення відстежувача змін. Для порівняння цих двох методів дивіться сторінку «Швидкий огляд» про збереження даних.

## Додавання даних
Використовуйте метод DbSet\<TEntity\>.Add для додавання нових екземплярів ваших класів сутностей. Дані будуть вставлені в базу даних, коли ви викличете DbContext.SaveChanges():

```cs

    await AddBlog(context);

static async Task AddBlog(ApplicationDbContext context)
{
    var blog = new Blog { Url = "http://example.com" };
    context.Blogs.Add(blog);
    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}
```
```
1
```

    Порада

    Методи Add, Attach та Update працюють з повним графом сутностей, що їм передані, як описано в розділі «Пов’язані дані». Крім того, властивість EntityEntry.State можна використовувати для встановлення стану лише однієї сутності. Наприклад, context.Entry(blog).State = EntityState.Modified.

## Оновлення даних

EF автоматично виявляє зміни, внесені до існуючої сутності, яка відстежується контекстом. Це включає сутності, які ви завантажуєте/запитуєте з бази даних, а також сутності, які раніше були додані та збережені в базі даних. 

Просто змініть значення, призначені властивостям, а потім викличте SaveChanges:

```cs
static async Task UpdateBlog(ApplicationDbContext context)
{
    Blog blog = await context.Blogs.SingleAsync(b => b.Url == "http://example.com");
    blog.Url = "http://example.com/blog";
    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}
```
```
1
```

## Видалення даних

Використовуйте метод DbSet<TEntity>.Remove для видалення екземплярів ваших класів сутностей:

```cs
static async Task DeleteBlog(ApplicationDbContext context)
{
    Blog? blog = await context.Blogs.SingleOrDefaultAsync(b => b.Url == "http://example.com/blog");

    if(blog is Blog blogForDelete)
    {
        context.Blogs.Remove(blogForDelete);
        int count = await context.SaveChangesAsync();
        Console.WriteLine(count);
    }
    else
    {
        Console.WriteLine("There is no entity for delete.");
    }
}
```
Якщо сутність вже існує в базі даних, її буде видалено під час виконання функції SaveChanges. Якщо сутність ще не збережена в базі даних (тобто вона відстежується як додана), то вона буде видалена з контексту та більше не буде вставлятися під час виклику SaveChanges.

## Кілька операцій в одній функції SaveChanges

Ви можете об'єднати кілька операцій додавання/оновлення/видалення в один виклик функції SaveChanges:

```cs
static async Task MultipleOperations(ApplicationDbContext context)
{
    // seeding database
    context.Blogs.Add(new Blog { Url = "http://example.com/blog" });
    context.Blogs.Add(new Blog { Url = "http://example.com/another_blog" });
    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);

    context.ChangeTracker.Clear();

    // add
    context.Blogs.Add(new Blog { Url = "http://example.com/blog_one" });
    context.Blogs.Add(new Blog { Url = "http://example.com/blog_two" });

    // update
    Blog firstBlog = await context.Blogs.FirstAsync();
    firstBlog.Url = "";

    // remove
    var lastBlog = await context.Blogs.OrderBy( b => b.BlogId ).LastAsync();
    context.Blogs.Remove(lastBlog);

    count = await context.SaveChangesAsync();
    Console.WriteLine(count);
}
```

Примітка

Для більшості постачальників баз даних SaveChanges є транзакційним. Це означає, що всі операції або виконуються успішно, або не виконуються, і операції ніколи не залишаються частково застосованими.