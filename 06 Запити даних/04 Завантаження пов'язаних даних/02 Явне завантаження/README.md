# Явне завантаження

Ви можете явно завантажити властивість навігації через API DbContext.Entry(...).

```cs
    var blog = await context.Blogs
        .SingleAsync(b => b.BlogId == 1);

    await context.Entry(blog)
        .Collection(b => b.Posts)
        .LoadAsync();

    await context.Entry(blog)
        .Reference(b => b.Owner)
        .LoadAsync();
    
    foreach(var post in blog.Posts)
    {
        Console.WriteLine($"Post: {post.Title}");
    }

    Console.WriteLine($"Owner: {blog.Owner.Name}");
```
Ви також можете явно завантажити властивість навігації, виконавши окремий запит, який повертає пов'язані сутності. Якщо відстеження змін увімкнено, то коли запит матеріалізує сутність, EF Core автоматично встановить властивості навігації щойно завантаженої сутності так, щоб вони посилалися на будь-які вже завантажені сутності, а властивості навігації вже завантажених сутностей – так, щоб вони посилалися на щойно завантажену сутність.

## Запити до пов'язаних сутностей

Ви також можете отримати запит LINQ, який представляє вміст властивості навігації. Це дозволяє застосовувати інші оператори до запиту. Наприклад, застосовувати агрегатний оператор до пов'язаних сутностей без завантаження їх у пам'ять.

```cs
    var blog = await context.Blogs
        .SingleAsync(b => b.BlogId == 1);

    var postCount = await context.Entry(blog)
        .Collection(b => b.Posts)
        .Query()
        .CountAsync();
```

Ви також можете фільтрувати, які пов'язані об'єкти завантажуються в пам'ять.

```cs
    var blog = await context.Blogs
        .SingleAsync(b => b.BlogId == 1);

    var goodPosts = await context.Entry(blog)
        .Collection(b => b.Posts)
        .Query()
        .Where(p => p.Rating > 3)
        .ToListAsync();
```