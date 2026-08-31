# Збереження пов’язаних даних

Окрім ізольованих сутностей, ви також можете використовувати зв'язки, визначені у вашій моделі.

## Додавання графу нових сутностей

Якщо ви створюєте кілька нових пов’язаних сутностей, додавання однієї з них до контексту призведе до додавання інших.

У наступному прикладі блог та три пов’язані з ним публікації вставлені в базу даних. Публікації знайдено та додано, оскільки вони доступні через властивість навігації Blog.Posts.

```cs
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);
    await AddGrathObjectAsync(context);
```
```cs
static async Task AddGrathObjectAsync(ApplicationDbContext context)
{
    var blog = new Blog
    {
        Url = "http://blogs.msdn.com/dotnet",
        Posts = new List<Post>
        {
            new Post { Title = "Intro to C#", Content = "This is an introduction to C#." },
            new Post { Title = "Intro to VB.NET", Content = "This is an introduction to VB.NET." },
            new Post { Title = "Intro to F#", Content = "This is an introduction to F#." }
        }
    };
    context.Blogs.Add(blog);

    var countAdded = await context.SaveChangesAsync();
    Console.WriteLine(countAdded);
}  
```
```
4
```
    Порада

    Використовуйте властивість EntityEntry.State, щоб встановити стан лише однієї сутності. Наприклад, context.Entry(blog).State = EntityState.Modified.

## Додавання пов’язаної сутності

Якщо ви посилаєтеся на нову сутність з властивості навігації сутності, яка вже відстежується контекстом, сутність буде виявлена ​​та вставлена ​​в базу даних.

У наступному прикладі сутність post вставляється, оскільки вона додається до властивості Posts сутності blog, яка була отримана з бази даних.

```cs
    await AddingARelatedEntity(context);

static async Task AddingARelatedEntity(ApplicationDbContext context)
{
    Blog? blog = await context.Blogs.Include(b => b.Posts).FirstOrDefaultAsync();
    var post = new Post { Title = "Intro to Helth life", Content = "This is an introduction to Helth life." };

    blog?.Posts.Add(post);
    var count = await context.SaveChangesAsync();

    Console.WriteLine(count);
}
```
```
1
```

## Зміна зв'язків

Якщо змінити властивість навігації сутності, відповідні зміни будуть внесені до стовпця зовнішнього ключа в базі даних. У наступному прикладі сутність post оновлюється, щоб вона належала новій сутності blog, оскільки її властивість navigation Blog встановлено як вказівку на blog. Зверніть увагу, що blog також буде додано до бази даних, оскільки це нова сутність, на яку посилається властивість navigation сутності, яка вже відстежується контекстом (post).

```cs
    await ChangingRelationships(context);

static async Task ChangingRelationships(ApplicationDbContext context)
{
    var blog = new Blog { Url = "http://blogs.msdn.com/visualstudio" };
    var post = await context.Posts.FirstAsync();

    post.Blog = blog;

    var countAdded = await context.SaveChangesAsync();
    Console.WriteLine(countAdded);
}
```
```
2
```

## Видалення зв'язків

Ви можете видалити зв'язок, встановивши для навігації за посиланнями значення null або видаливши пов'язану сутність з навігації колекції.

Видалення зв'язку може мати побічні наслідки для залежної сутності, відповідно до каскадної поведінки видалення, налаштованої у зв'язку.

За замовчуванням для обов'язкових зв'язків налаштовано каскадне видалення, і дочірній/залежний об'єкт буде видалено з бази даних.Для необов'язкових зв'язків каскадне видалення не налаштовано за замовчуванням, але властивість зовнішнього ключа буде встановлено на null.

Див. розділ Обов’язкові та необов’язкові зв’язки, щоб дізнатися, як налаштувати обов’язковість зв’язків.

Див. Каскадне видалення для отримання додаткової інформації про те, як працюють каскадні режими видалення, як їх можна явно налаштувати та як їх вибирати за домовленістю. 

У наступному прикладі завантажується і видаляється з бази даних залежна сутність публікації.

```cs
static async Task RemoveARelatedEntity(ApplicationDbContext context)
{
    Blog? blog = await context.Blogs.Include(b => b.Posts).FirstOrDefaultAsync();
    Post? post = blog?.Posts.FirstOrDefault();

    if(post is Post postForDelete)
    {
        blog?.Posts.Remove(postForDelete);
    }  
    var count = await context.SaveChangesAsync();

    Console.WriteLine(count);
}
```

