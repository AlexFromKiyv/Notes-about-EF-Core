# Відкладене завантаження

Найпростіший спосіб використовувати відкладене завантаження – це встановити пакет Microsoft.EntityFrameworkCore.Proxies та ввімкнути його за допомогою виклику UseLazyLoadingProxies. Наприклад:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder
        .UseLazyLoadingProxies()
        .UseSqlServer(myConnectionString);
```

Або під час використання AddDbContext:

```cs
.AddDbContext<BloggingContext>(
    b => b.UseLazyLoadingProxies()
          .UseSqlServer(myConnectionString));
```

EF Core потім увімкне відкладене завантаження для будь-якої властивості навігації, яку можна перевизначити, тобто вона має бути віртуальною та належати до класу, від якого можна успадковувати. Наприклад, у наступних сутностях властивості навігації Post.Blog та Blog.Posts будуть завантажені відкладено.

```cs
public class Blog
{
    public int Id { get; set; }
    public string Name { get; set; }

    public virtual ICollection<Post> Posts { get; set; }
}

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public virtual Blog Blog { get; set; }
}
```
Попередження

Ліниве завантаження може призвести до непотрібних додаткових циклів обробки даних (так звана проблема N+1), і слід бути обережними, щоб уникнути цього.

## Відкладене завантаження без проксі-серверів

Відкладене завантаження без проксі-серверів працює шляхом введення сервісу ILazyLoader в сутність, як описано в розділі Конструктори типів сутностей.

```cs
public class Blog
{
    private ICollection<Post> _posts;

    public Blog()
    {
    }

    private Blog(ILazyLoader lazyLoader)
    {
        LazyLoader = lazyLoader;
    }

    private ILazyLoader LazyLoader { get; set; }

    public int Id { get; set; }
    public string Name { get; set; }

    public ICollection<Post> Posts
    {
        get => LazyLoader.Load(this, ref _posts);
        set => _posts = value;
    }
}

public class Post
{
    private Blog _blog;

    public Post()
    {
    }

    private Post(ILazyLoader lazyLoader)
    {
        LazyLoader = lazyLoader;
    }

    private ILazyLoader LazyLoader { get; set; }

    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public Blog Blog
    {
        get => LazyLoader.Load(this, ref _blog);
        set => _blog = value;
    }
}
```
Цей метод не вимагає успадкування типів сутностей або віртуальності властивостей навігації, а також дозволяє екземплярам сутностей, створеним за допомогою new, завантажуватися методом lazy-load після приєднання до контексту. Однак він вимагає посилання на службу ILazyLoader, яка визначена в пакеті Microsoft.EntityFrameworkCore.Abstractions. Цей пакет містить мінімальний набір типів, тому залежність від нього має незначний вплив. Однак, щоб повністю уникнути залежності від будь-яких пакетів EF Core у типах сутностей, можна впровадити метод ILazyLoader.Load як делегат. Наприклад:

```cs
public class Blog
{
    private ICollection<Post> _posts;

    public Blog()
    {
    }

    private Blog(Action<object, string> lazyLoader)
    {
        LazyLoader = lazyLoader;
    }

    private Action<object, string> LazyLoader { get; set; }

    public int Id { get; set; }
    public string Name { get; set; }

    public ICollection<Post> Posts
    {
        get => LazyLoader.Load(this, ref _posts);
        set => _posts = value;
    }
}

public class Post
{
    private Blog _blog;

    public Post()
    {
    }

    private Post(Action<object, string> lazyLoader)
    {
        LazyLoader = lazyLoader;
    }

    private Action<object, string> LazyLoader { get; set; }

    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public Blog Blog
    {
        get => LazyLoader.Load(this, ref _blog);
        set => _blog = value;
    }
}
```
У наведеному вище коді використовується метод розширення Load, щоб зробити використання делегата трохи зрозумілішим:

```cs
public static class PocoLoadingExtensions
{
    public static TRelated Load<TRelated>(
        this Action<object, string> loader,
        object entity,
        ref TRelated navigationField,
        [CallerMemberName] string navigationName = null)
        where TRelated : class
    {
        loader?.Invoke(entity, navigationName);

        return navigationField;
    }
}
```
Примітка

Параметр конструктора для делегата лінивого завантаження повинен називатися "lazyLoader". Конфігурація з використанням іншої назви планується для майбутнього випуску.