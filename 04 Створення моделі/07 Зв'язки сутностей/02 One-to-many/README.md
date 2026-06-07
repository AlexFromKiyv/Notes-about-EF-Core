# Зв'язки «one-to-many»

Зв'язки «one-to-many» використовуються, коли одна сутність пов'язана з будь-якою кількістю інших сутностей. Наприклад, блог може мати багато пов’язаних публікацій, але кожна публікація пов’язана лише з одним блогом.

## Обов'язковий зв'язок "one-to-many"

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public ICollection<Post> Posts { get; } = new List<Post>(); // Collection navigation containing dependents
}

// Dependent (child)
public class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; } // Required foreign key property
    public Blog Blog { get; set; } = null!; // Required reference navigation to principal
}
```
Зв'язок «one-to-many» складається з:

* Однією або кількох властивостей первинного або альтернативного ключа на головній сутності; це кінець зв'язку «one». Наприклад, Blog.Id.
* Одна або декілька властивостей зовнішнього ключа залежної сутності; це кінець зв'язку "many". Наприклад, Post.BlogId.
* За бажанням, навігація колекції на головній сутності, що посилається на залежні сутності. Наприклад, Blog.Posts.
* За бажанням, навігація посиланнями на залежній сутності, що посилається на головну сутність. Наприклад, Post.Blog.

Отже, для зв’язку в цьому прикладі:

* Властивість зовнішнього ключа Post.BlogId не може бути null. Це робить зв'язок «обов'язковим», оскільки кожен залежний об'єкт (Post) має бути пов'язаний з певним головним об'ектом (Блог), оскільки його властивість зовнішнього ключа має бути встановлена ​​на певне значення.

* Обидві сутності мають навігацію, що вказує на пов'язану сутність або сутності на іншому боці зв'язку.

Необхідний зв’язок гарантує, що кожна залежна сутність має бути пов’язана з деякою головною сутністю. Однак, головна сутність завжди може існувати без будь-яких залежних сутностей. Тобто, обов'язковий зв'язок не означає, що завжди буде принаймні одна залежна сутність. У моделі EF, а також у реляційній базі даних немає способу гарантувати, що існує Principal пов'язаний з певною кількістю залежних об'єктів. Якщо це необхідно, то це має бути реалізовано в логіці програми (бізнесу).

Зв'язок з двома навігаціями, однією від залежного до головного та інверсною від головного до залежних, називається двонаправленим зв'язком.

Цей зв'язок виявляється за домовленістю. Тобто:

* Blog виявляється як головна сутність у зв'язку, а Post виявляється як залежний.
* Post.BlogId виявляється як зовнішній ключ залежного об'єкта, що посилається на первинний ключ Blog.Id принципала.
* Зв'язок виявлено як обов'язковий, оскільки Post.BlogId не може мати значення null.
* Blog.Posts виявлено як навігацію колекції.
* Post.Blog виявлено як навігація у посиланні.

Під час використання типів посилань C#, що допускають значення null, навігація за посиланнями має бути null-допустимою, якщо властивість зовнішнього ключа також допускає значення null. Якщо властивість зовнішнього ключа не допускає значення null, то навігація за посиланнями може бути null-допустимою чи ні. У цьому випадку Post.BlogId не допускає значення null, а Post.Blog також не допускає значення null. Конструкція = null!; використовується для позначення цього як навмисного для компілятора C#, оскільки EF зазвичай встановлює екземпляр Blog, і він не може бути null для повністю завантаженого зв'язку.

У випадках, коли навігація, зовнішній ключ або обов'язковий/необов'язковий характер зв'язку не виявляються за домовленістю, ці речі можна налаштувати явно. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(b => b.Posts)
        .WithOne(p => p.Blog)
        .HasForeignKey(p => p.BlogId)
        .IsRequired();
}
```

У наведеному вище прикладі конфігурація зв’язків починається з HasMany для основного типу об’єкта (Blog), а потім – за допомогою WithOne. Як і у випадку з усіма зв'язками, це точно еквівалентно почати з залежного типу сутності (Post) та використовувати HasOne, а потім WithMany. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasOne(e => e.Blog)
        .WithMany(e => e.Posts)
        .HasForeignKey(e => e.BlogId)
        .IsRequired();
}
```
Жоден з цих варіантів не кращий за інший; обидва призводять до абсолютно однакової конфігурації.

Ніколи не потрібно налаштовувати зв'язок двічі, один раз починаючи з принципала, а потім знову починаючи з залежного. Також спроба налаштувати головну та залежну половини зв'язку окремо зазвичай не працює. Виберіть налаштування кожного зв'язку з одного або з іншого кінця, а потім напишіть код конфігурації лише один раз.

## Необов'язковий зв'язок "one-to-many"

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public ICollection<Post> Posts { get; } = new List<Post>(); // Collection navigation containing dependents
}

// Dependent (child)
public class Post
{
    public int Id { get; set; }
    public int? BlogId { get; set; } // Optional foreign key property
    public Blog? Blog { get; set; } // Optional reference navigation to principal
}
```
Це те саме, що й попередній приклад, за винятком того, що властивість зовнішнього ключа та навігація до принципала тепер можуть мати значення null. Це робить зв'язок "необов'язковим", оскільки залежний об'єкт (Post) може існувати без зв'язку з жодним принципалом (Blog).

Під час використання типів посилань C#, що допускають значення null, навігація за посиланнями має бути null-допустимою, якщо властивість зовнішнього ключа також має значення null. У цьому випадку Post.BlogId має значення null, тому Post.Blog також має бути null-допустимим.

Ці речі можна налаштувати явно. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .HasForeignKey(e => e.BlogId)
        .IsRequired(false);
}
```

## Обов'язковий зв'язок "one-to-many" з тіньовим зовнішнім ключем

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public ICollection<Post> Posts { get; } = new List<Post>(); // Collection navigation containing dependents
}

// Dependent (child)
public class Post
{
    public int Id { get; set; }
    public Blog Blog { get; set; } = null!; // Required reference navigation to principal
}
```
У деяких випадках вам може не знадобитися властивість зовнішнього ключа у вашій моделі, оскільки зовнішні ключі є деталлю того, як зв'язок представлений у базі даних, що не потрібно при використанні зв'язку виключно в об'єктно-орієнтованому режимі. Однак, якщо сутності будуть серіалізовані, наприклад, для надсилання по мережі, то значення зовнішнього ключа можуть бути корисним способом зберегти інформацію про зв'язок недоторканою, коли сутності не перебувають у об'єктній формі. Тому часто прагматично зберігати властивості зовнішнього ключа в типі .NET для цієї мети. Властивості зовнішнього ключа можуть бути приватними, що часто є гарним компромісом, щоб уникнути розкриття зовнішнього ключа, дозволяючи його значенню передаватись разом із об'єктом.

Продовжуючи попередні два приклади, цей приклад видаляє властивість зовнішнього ключа з залежного типу сутності. Таким чином, EF створює тіньову властивість зовнішнього ключа під назвою BlogId типу int.

Важливо зазначити, що в C# використовуються типи посилань, що допускають значення null, тому можливість використання навігації посилань використовується для визначення того, чи є властивість зовнішнього ключа допустимою до значення null, і, отже, чи є зв'язок необов'язковим чи обов'язковим. Якщо типи посилань, що допускають значення null, не використовуються, то властивість зовнішнього ключа shadow буде допустимою до значення null за замовчуванням, що робить зв'язок необов'язковим за замовчуванням. У цьому випадку використовуйте IsRequired, щоб примусово зробити властивість зовнішнього ключа shadow не допустимою до значення null і зробити зв'язок обов'язковим.

Як і раніше, цей зв'язок виявляється за домовленістю. У випадках, коли навігація, зовнішній ключ або обов'язковий/необов'язковий характер зв'язку не виявляються за домовленістю, ці речі можна налаштувати явно. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .HasForeignKey("BlogId")
        .IsRequired();
}
```

## Необов'язковий зв'язок "one-to-many" з тіньовим зовнішнім ключем

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public ICollection<Post> Posts { get; } = new List<Post>(); // Collection navigation containing dependents
}

// Dependent (child)
public class Post
{
    public int Id { get; set; }
    public Blog? Blog { get; set; } // Optional reference navigation to principal
}
```
Як і в попередньому прикладі, властивість зовнішнього ключа було видалено з залежного типу сутності. Таким чином, EF створює тіньову властивість зовнішнього ключа під назвою BlogId типу int?. На відміну від попереднього прикладу, цього разу властивість зовнішнього ключа створюється як null-допустима, оскільки використовуються типи посилань C#, що дозволяють значення null, а навігація по залежному типу сутності є null-допустимою. Це робить зв'язок необов'язковим.

Коли не використовуються типи посилань C#, що дозволяють використовувати значення null, властивість зовнішнього ключа також за замовчуванням буде створена як null-здатна. Це означає, що зв'язки з автоматично створеними властивостями shadow за замовчуванням є необов'язковими.

Ці речі можна налаштувати явно:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .HasForeignKey("BlogId")
        .IsRequired(false);
}
```

## "one-to-many" без навігації до головної

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public ICollection<Post> Posts { get; } = new List<Post>(); // Collection navigation containing dependents
}

// Dependent (child)
public class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; } // Required foreign key property
}
```
У цьому прикладі властивість зовнішнього ключа було повторно введено, але навігацію по залежному елементу було видалено.

Зв'язок з лише однією навігацією, однією від залежного до принципала або однією від принципала до залежного(их), але не обома одночасно, називається однонаправленим зв'язком.

Ці можна налаштувати явно:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne()
        .HasForeignKey(e => e.BlogId)
        .IsRequired();
}
```
Зверніть увагу, що виклик WithOne не має аргументів. Це спосіб повідомити EF, що немає навігації від публікації до блогу.

## "one-to-many" без навігації до головної та з тіньовим зовнішнім ключем

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public ICollection<Post> Posts { get; } = new List<Post>(); // Collection navigation containing dependents
}

// Dependent (child)
public class Post
{
    public int Id { get; set; }
}
```
Цей приклад поєднує два попередні приклади, видаляючи як властивість зовнішнього ключа, так і навігацію на залежному елементі. 

Цей зв'язок за домовленістю визначається як необов'язковий. Оскільки в коді немає нічого, що могло б вказати на його обов'язковість, для створення обов'язкового зв'язку потрібна мінімальна конфігурація за допомогою IsRequired. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne()
        .IsRequired();
}
```
Більш повну конфігурацію можна використовувати для явного налаштування навігації та імені зовнішнього ключа, за потреби з відповідним викликом IsRequired() або IsRequired(false). Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne()
        .HasForeignKey("BlogId")
        .IsRequired();
}
```
## "one-to-many" без навігації до залежних елементів

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
}

// Dependent (child)
public class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; } // Required foreign key property
    public Blog Blog { get; set; } = null!; // Required reference navigation to principal
}
```
У попередніх двох прикладах була навігація від принципала до залежних, але не була навігація від залежного до принципала. У наступних кількох прикладах навігація в залежному знову вводиться, тоді як навігація в принципалу натомість видаляється.

Ці можна налаштувати явно:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasOne(e => e.Blog)
        .WithMany()
        .HasForeignKey(e => e.BlogId)
        .IsRequired();
}
```
Зверніть увагу ще раз, що WithMany() викликається без аргументів, щоб вказати, що навігація в цьому напрямку відсутня.

## "one-to-many" без навігації

Іноді може бути корисним налаштувати зв'язок без навігації. Такий зв'язок можна маніпулювати лише шляхом безпосередньої зміни значення зовнішнього ключа.

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
}

// Dependent (child)
public class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; } // Required foreign key property
}
```
Цей зв'язок не виявляється за домовленістю, оскільки немає навігації, яка б вказувала на те, що ці два типи пов'язані. Його можна явно налаштувати в OnModelCreating. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany<Post>()
        .WithOne();
}
```
За такої конфігурації властивість Post.BlogId все ще визначається як зовнішній ключ за домовленістю, а зв'язок є обов'язковим, оскільки властивість зовнішнього ключа не може мати значення null. Зв'язок можна зробити "необов'язковим", зробивши властивість зовнішнього ключа null-можливою.

Більш повна явна конфігурація цього зв'язку така:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany<Post>()
        .WithOne()
        .HasForeignKey(e => e.BlogId)
        .IsRequired();
}
```

## "one-to-many" з альтернативним ключем

У всіх наведених прикладах властивість зовнішнього ключа залежного об'єкта обмежена властивістю первинного ключа головного об'єкта. Зовнішній ключ може бути обмежений іншою властивістю, яка потім стає альтернативним ключем для основного типу сутності. Наприклад:

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public int AlternateId { get; set; } // Alternate key as target of the Post.BlogId foreign key
    public ICollection<Post> Posts { get; } = new List<Post>(); // Collection navigation containing dependents
}

// Dependent (child)
public class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; } // Required foreign key property
    public Blog Blog { get; set; } = null!; // Required reference navigation to principal
}
```
Цей зв'язок не виявляється за домовленістю, оскільки EF завжди, за домовленістю, створюватиме зв'язок з первинним ключем. Це можна налаштувати явно в OnModelCreating за допомогою виклику HasPrincipalKey. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(b => b.Posts)
        .WithOne(p => p.Blog)
        .HasPrincipalKey(b => b.AlternateId);
}
```
HasPrincipalKey можна поєднувати з іншими викликами для явного налаштування навігації, властивостей зовнішнього ключа та обов'язкового/необов'язкового характеру. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .HasPrincipalKey(e => e.AlternateId)
        .HasForeignKey(e => e.BlogId)
        .IsRequired();
}
```

## "one-to-many" зі складеним зовнішнім ключем

У всіх наведених досі прикладах властивість первинного або альтернативного ключа принципала складалася з однієї властивості. Первинні або альтернативні ключі також можуть бути сформовані з кількох властивостей – вони відомі як «композитні ключі». Коли принципал зв'язку має складений ключ, то зовнішній ключ залежного об'єкта також має бути складеним ключем з такою ж кількістю властивостей. Наприклад:

```cs
// Principal (parent)
public class Blog
{
    public int Id1 { get; set; } // Composite key part 1
    public int Id2 { get; set; } // Composite key part 2
    public ICollection<Post> Posts { get; } = new List<Post>(); // Collection navigation containing dependents
}

// Dependent (child)
public class Post
{
    public int Id { get; set; }
    public int BlogId1 { get; set; } // Required foreign key property part 1
    public int BlogId2 { get; set; } // Required foreign key property part 2
    public Blog Blog { get; set; } = null!; // Required reference navigation to principal
}
```
Цей зв'язок виявляється за домовленістю. Однак, сам складений ключ потрібно налаштувати явно:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasKey(e => new { e.Id1, e.Id2 });
}
```
Значення складеного зовнішнього ключа вважається null, якщо будь-яке з його значень властивості є null. Складений зовнішній ключ з однією властивістю, що дорівнює null, та іншою, що не дорівнює null, не вважатиметься збігом для первинного або альтернативного ключа з однаковими значеннями. Обидва вважатимуться null.

Як HasForeignKey, так і HasPrincipalKey можна використовувати для явного визначення ключів з кількома властивостями. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>(
        nestedBuilder =>
        {
            nestedBuilder.HasKey(e => new { e.Id1, e.Id2 });

            nestedBuilder.HasMany(e => e.Posts)
                .WithOne(e => e.Blog)
                .HasPrincipalKey(e => new { e.Id1, e.Id2 })
                .HasForeignKey(e => new { e.BlogId1, e.BlogId2 })
                .IsRequired();
        });
}
```
У наведеному вище коді виклики HasKey та HasMany згруповані у вкладений builder. Вкладені builder усувають необхідність викликати Entity<>() кілька разів для одного й того ж типу сутності, але функціонально еквівалентні багаторазовому виклику Entity<>().

## Обов'язковий зв'язок "one-to-many" без каскадного видалення

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public ICollection<Post> Posts { get; } = new List<Post>(); // Collection navigation containing dependents
}

// Dependent (child)
public class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; } // Required foreign key property
    public Blog Blog { get; set; } = null!; // Required reference navigation to principal
}
```
За домовленістю, обов'язкові зв'язки налаштовані на каскадне видалення; це означає, що коли видаляється принципал, усі його залежні також видаляються, оскільки залежні не можуть існувати в базі даних без принципала. Можна налаштувати EF таким чином, щоб він генерував виняток замість автоматичного видалення залежних рядків, які більше не можуть існувати:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .OnDelete(DeleteBehavior.Restrict);
}
```

## Самопосилання «one-to-many»

У всіх попередніх прикладах головний тип сутності відрізнявся від залежного типу сутності. Це не обов'язково має бути так. Наприклад, у наведених нижче типах кожен Працівник пов'язаний з іншими Працівниками.

```cs
public class Employee
{
    public int Id { get; set; }

    public int? ManagerId { get; set; } // Optional foreign key property
    public Employee? Manager { get; set; } // Optional reference navigation to principal
    public ICollection<Employee> Reports { get; } = new List<Employee>(); // Collection navigation containing dependents
}
```
Цей зв'язок виявляється за домовленістю. У випадках, коли навігація, зовнішній ключ або обов'язковий/необов'язковий характер зв'язку не виявляються за домовленістю, ці речі можна налаштувати явно. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Employee>()
        .HasOne(e => e.Manager)
        .WithMany(e => e.Reports)
        .HasForeignKey(e => e.ManagerId)
        .IsRequired(false);
}
```

Ще одним прикладом може бути групи товарів:

```cs
public class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int? ParentId { get; set; }
    public Group? Parent { get; set; }

    [InverseProperty(nameof(Parent))]
    public ICollection<Group> Children { get; set; } = new List<Group>();
}
```

```cs
    var context = new ApplicationDbContextFactory().CreateDbContext(null);
    Console.WriteLine(context.Model.ToDebugString());

    Group parent = new Group() { Name = "Bicycles" };
    Group child = new Group() { Name = "Road bicycles", Parent = parent };
    context.Groups.Add(child);
    Console.WriteLine($"Saved:{context.SaveChanges()}"); 
```
