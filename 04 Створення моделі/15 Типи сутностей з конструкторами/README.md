# Типи сутностей з конструкторами

Можна визначити конструктор з параметрами та налаштувати EF Core так, щоб він викликав цей конструктор під час створення екземпляра сутності. Параметри конструктора можуть бути прив'язані до відображених властивостей або до різних видів сервісів для полегшення такої поведінки, як відкладене завантаження.

Наразі все зв'язування конструкторів здійснюється за домовленістю.

## Прив’язка до зіставлених властивостей

Розглянемо типову модель Blog/Post:

```cs
public class Blog
{
    public int Id { get; set; }

    public string Name { get; set; }
    public string Author { get; set; }

    public ICollection<Post> Posts { get; } = new List<Post>();
}

public class Post
{
    public int Id { get; set; }

    public string Title { get; set; }
    public string Content { get; set; }
    public DateTime PostedOn { get; set; }

    public Blog Blog { get; set; }
}
```
Коли EF Core створює екземпляри цих типів, наприклад, для результатів запиту, він спочатку викликає конструктор без параметрів за замовчуванням, а потім встановлює кожну властивість на значення з бази даних. Однак, якщо EF Core знаходить параметризований конструктор з іменами та типами параметрів, що відповідають іменам та типам відображених властивостей, то він натомість викликає параметризований конструктор зі значеннями для цих властивостей і не встановлює кожну властивість явно.

```cs
public class Blog
{
    public Blog(int id, string name, string author)
    {
        Id = id;
        Name = name;
        Author = author;
    }

    public int Id { get; set; }

    public string Name { get; set; }
    public string Author { get; set; }

    public ICollection<Post> Posts { get; } = new List<Post>();
}

public class Post
{
    public Post(int id, string title, DateTime postedOn)
    {
        Id = id;
        Title = title;
        PostedOn = postedOn;
    }

    public int Id { get; set; }

    public string Title { get; set; }
    public string Content { get; set; }
    public DateTime PostedOn { get; set; }

    public Blog Blog { get; set; }
}
```

Деякі речі, які слід зазначити:

* Не всі властивості повинні мати параметри конструктора. Наприклад, властивість Post.Content не встановлюється жодним параметром конструктора, тому EF Core встановить її після виклику конструктора звичайним способом.
* Типи та назви параметрів повинні збігатися з типами та назвами властивостей, за винятком того, що властивості можуть бути записані у регістрі Pascal-cased, тоді як параметри – у регістрі  camel-cased.
* EF Core не може встановлювати властивості навігації (такі як Blog або Posts вище) за допомогою конструктора.
* Конструктор може бути публічним, приватним або мати будь-яку іншу доступність. Однак проксі-сервери з лінивим завантаженням вимагають, щоб конструктор був доступний з успадковуваного проксі-класу. Зазвичай це означає, що його потрібно зробити публічним або захищеним.

## Властивості лише для читання

Після того, як властивості встановлюються через конструктор, може бути доцільно зробити деякі з них доступними лише для читання. EF Core підтримує це, але є деякі речі, на які слід звернути увагу:

* Властивості без сетерів не відображаються за домовленістю. (Це призводить до відображення властивостей, які не повинні відображатися, наприклад, обчислюваних властивостей.)
* Використання автоматично згенерованих значень ключів вимагає властивості ключа з можливістю читання та запису, оскільки значення ключа має бути встановлено генератором ключів під час вставки нових сутностей.
Простий спосіб уникнути цього – використовувати приватні сеттери.

```cs
public class Blog
{
    public Blog(int id, string name, string author)
    {
        Id = id;
        Name = name;
        Author = author;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }
    public string Author { get; private set; }

    public ICollection<Post> Posts { get; } = new List<Post>();
}

public class Post
{
    public Post(int id, string title, DateTime postedOn)
    {
        Id = id;
        Title = title;
        PostedOn = postedOn;
    }

    public int Id { get; private set; }

    public string Title { get; private set; }
    public string Content { get; set; }
    public DateTime PostedOn { get; private set; }

    public Blog Blog { get; set; }
}
```
EF Core розглядає властивість із приватним методом встановлення як таку, що доступна для читання та запису, що означає, що всі властивості відображаються як і раніше, і ключ все ще може бути згенерований сховищем. Альтернативою використанню приватних методів встановлення є перетворення властивостей на дійсно доступні лише для читання та додавання більш явного зіставлення в OnModelCreating. Аналогічно, деякі властивості можна повністю видалити та замінити лише полями. Наприклад, розглянемо такі типи сутностей:

```cs
public class Blog
{
    private int _id;

    public Blog(string name, string author)
    {
        Name = name;
        Author = author;
    }

    public string Name { get; }
    public string Author { get; }

    public ICollection<Post> Posts { get; } = new List<Post>();
}

public class Post
{
    private int _id;

    public Post(string title, DateTime postedOn)
    {
        Title = title;
        PostedOn = postedOn;
    }

    public string Title { get; }
    public string Content { get; set; }
    public DateTime PostedOn { get; }

    public Blog Blog { get; set; }
}
```
Та ця конфігурація в OnModelCreating:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>(
        b =>
        {
            b.HasKey("_id");
            b.Property(e => e.Author);
            b.Property(e => e.Name);
        });

    modelBuilder.Entity<Post>(
        b =>
        {
            b.HasKey("_id");
            b.Property(e => e.Title);
            b.Property(e => e.PostedOn);
        });
}
```

Варто зазначити:

* Ключ «властивість» тепер є полем. Це поле не призначене лише для читання, тому можна використовувати ключі, згенеровані сховищем.
* Інші властивості доступні лише для читання та встановлюються лише в конструкторі.
* Якщо значення первинного ключа встановлюється лише EF або зчитується з бази даних, то немає потреби включати його в конструктор. Це залишає ключову "властивість" як просте поле та чітко показує, що її не слід явно встановлювати під час створення нових блогів або публікацій.

Цей код призведе до виведення попередження компілятора «169», яке вказує на те, що поле ніколи не використовується. Це можна ігнорувати, оскільки насправді EF Core використовує поле екстралінгвістичним чином.

## Впровадження сервісів

EF Core також може впроваджувати "сервіси" в конструктор типу сутності. Наприклад, можна впровадити наступне:

* DbContext – поточний екземпляр контексту, який також можна ввести як ваш похідний тип DbContext
* ILazyLoader – служба лінивого завантаження – див. документацію з лінивого завантаження для отримання додаткової інформації
* Action\<object, string\> – делегат із лінивим завантаженням – див. документацію щодо лінивого завантаження для отримання додаткової інформації
* IEntityType – метадані EF Core, пов'язані з цим типом сутності

Наразі можна впроваджувати лише сервіси, відомі EF Core.

Наприклад, введений DbContext можна використовувати для вибіркового доступу до бази даних, щоб отримати інформацію про пов'язані об'єкти без завантаження їх усіх. У наведеному нижче прикладі це використовується для отримання кількості публікацій у блозі без їх завантаження:

```cs
public class Blog
{
    public Blog()
    {
    }

    private Blog(BloggingContext context)
    {
        Context = context;
    }

    private BloggingContext Context { get; set; }

    public int Id { get; set; }
    public string Name { get; set; }
    public string Author { get; set; }

    public ICollection<Post> Posts { get; set; }

    public int PostsCount
        => Posts?.Count
           ?? Context?.Set<Post>().Count(p => Id == EF.Property<int?>(p, "BlogId"))
           ?? 0;
}

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public DateTime PostedOn { get; set; }

    public Blog Blog { get; set; }
}
```

Кілька речей, які слід зазначити щодо цього:

* Конструктор є приватним, оскільки його викликає лише EF Core, а для загального використання існує ще один публічний конструктор.
* Код, що використовує введений сервіс (тобто контекст), захищає його від значення null для обробки випадків, коли EF Core не створює екземпляр.
* Оскільки сервіс зберігається у властивості читання/запису, його значення буде скинуто, коли сутність буде приєднана до нового екземпляра контексту.

Впровадження DbContext таким чином часто вважається антишаблоном, оскільки воно безпосередньо пов'язує ваші типи сутностей з EF Core. Ретельно розгляньте всі варіанти, перш ніж використовувати впровадження сервісу таким чином.