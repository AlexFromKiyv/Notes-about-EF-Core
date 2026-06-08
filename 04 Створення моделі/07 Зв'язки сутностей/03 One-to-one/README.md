# Зв'язки «one-to-one»

Зв'язки "one-to-one" використовуються, коли одна сутність пов'язана щонайбільше з однією іншою сутністю. Наприклад, блог має один заголовок блогу (BlogHeader), і цей заголовок блогу належить одному блогу (BlogHeader).

## Обов'язковий зв'язок "one-to-one"

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public BlogHeader? Header { get; set; } // Reference navigation to dependent
}

// Dependent (child)
public class BlogHeader
{
    public int Id { get; set; }
    public int BlogId { get; set; } // Required foreign key property
    public Blog Blog { get; set; } = null!; // Required reference navigation to principal
}
```

```cs
    var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Console.WriteLine(context.Model.ToDebugString());
```

Зв'язок «one-to-one» складається з:

* Одна або декілька властивостей первинного або альтернативного ключа для головної сутності. Наприклад, Blog.Id.
* Одна або декілька властивостей зовнішнього ключа для залежної сутності. Наприклад, BlogHeader.BlogId.
* За бажанням, посилкова навігація на головній сутності, що посилається на залежну сутність. Наприклад, Blog.Header.
* За бажанням, посилкова навігація на залежній сутності, що посилається на головну сутність. Наприклад, BlogHeader.Blog.

Не завжди очевидно, яка сторона зв'язку "один до одного" має бути головною, а яка - залежною. Деякі міркування:

* Якщо таблиці бази даних для двох типів вже існують, то таблиця зі стовпцем(ами) зовнішнього ключа повинна відповідати залежному типу.
* Тип зазвичай є залежним типом, якщо він логічно не може існувати без іншого типу. Наприклад, немає сенсу мати заголовок для блогу, якого не існує, тому BlogHeader, природно, є залежним типом. 
* Якщо існують природні стосунки між батьками та дитиною, то дитина зазвичай є залежним типом.

Отже, для зв'язку в цьому прикладі:

* Властивість зовнішнього ключа BlogHeader.BlogId не може мати значення null. Це робить зв'язок «обов'язковим», оскільки кожен залежний об'єкт (BlogHeader) має бути пов'язаний з деяким принципалом (Blog), оскільки його властивість зовнішнього ключа має бути встановлена ​​на певне значення.
* Обидві сутності мають навігацію, що вказує на пов'язану сутність на іншому боці зв'язку.

Обов'язковий зв'язок гарантує, що кожна залежна сутність має бути пов'язана з певною головною сутністю. Однак головна сутність завжди може існувати без будь-якої залежної сутності. Тобто, обов'язковий зв'язок не означає, що завжди буде залежна сутність.

Цей зв'язок виявляється за домовленістю. Тобто:

* Blog виявляється як принципал у зв'язку, а BlogHeader виявляється як залежний.
* BlogHeader.BlogId виявлено як зовнішній ключ залежного об'єкта, що посилається на первинний ключ Blog.Id принципала. Зв'язок виявлено належним чином, оскільки BlogHeader.BlogId не може мати значення null.
* Blog.BlogHeader виявлено як посилкову навігацію.
* BlogHeader.Blog виявлено як посилкову навігацію.

Цей звязок можна визначити явно:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne(e => e.Header)
        .WithOne(e => e.Blog)
        .HasForeignKey<BlogHeader>(e => e.BlogId)
        .IsRequired();
}
```
У наведеному вище прикладі налаштування зв'язків починається з основного типу сутності (Blog). Як і з усіма зв'язками, це точно еквівалентно початку із залежного типу сутності (BlogHeader).

## Необов'язковий зв'язок "one-to-one"

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public BlogHeader? Header { get; set; } // Reference navigation to dependent
}

// Dependent (child)
public class BlogHeader
{
    public int Id { get; set; }
    public int? BlogId { get; set; } // Optional foreign key property
    public Blog? Blog { get; set; } // Optional reference navigation to principal
}
```
Це те саме, що й попередній приклад, за винятком того, що властивість зовнішнього ключа та навігація до принципала тепер можуть мати значення null. Це робить зв'язок "необов'язковим", оскільки залежний об'єкт (BlogHeader) не може бути пов'язаний з жодним принципалом (Blog), встановивши його властивість зовнішнього ключа та навігацію на null.

Під час використання типів посилань у C#, що допускають значення null, властивість навігації від залежного до принципального має бути null-допустимою, якщо властивість зовнішнього ключа також має бути null-допустимою.

Цей звязок можна визначити явно:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne(e => e.Header)
        .WithOne(e => e.Blog)
        .HasForeignKey<BlogHeader>(e => e.BlogId)
        .IsRequired(false);
}
```

## Обов'язковий зв'язок "one-to-one" зі зв'язком первинного ключа до первинного ключа

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public BlogHeader? Header { get; set; } // Reference navigation to dependent
}

// Dependent (child)
public class BlogHeader
{
    public int Id { get; set; }
    public Blog Blog { get; set; } = null!; // Required reference navigation to principal
}
```
На відміну від зв'язків «one-to-many», залежний кінець зв'язку « one-to-one» може використовувати свою властивість або властивості первинного ключа як властивість або властивості зовнішнього ключа. Це часто називають зв'язком PK-to-PK. Це можливо лише тоді, коли головний та залежний типи мають однакові типи первинних ключів, а результуючий зв'язок завжди є обов'язковим, оскільки первинний ключ залежного типу не може мати значення null.

Будь-який зв'язок "one-to-one", де зовнішній ключ не виявляється за домовленістю, має бути налаштований таким чином, щоб вказувати головний та залежний кінці зв'язку. Зазвичай це робиться за допомогою виклику HasForeignKey. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne(e => e.Header)
        .WithOne(e => e.Blog)
        .HasForeignKey<BlogHeader>();
}
```
Якщо у виклику HasForeignKey не вказано жодної властивості, а первинний ключ підходить, тоді він використовується як зовнішній ключ. У випадках, коли навігація, зовнішній ключ або обов'язковий/необов'язковий характер зв'язку не виявляються за домовленістю, ці речі можна налаштувати явно.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne(e => e.Header)
        .WithOne(e => e.Blog)
        .HasForeignKey<BlogHeader>(e => e.Id)
        .IsRequired();
}
```

## Обов'язковий зв'язок "one-to-one" з тіньовим зовнішнім ключем

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public BlogHeader? Header { get; set; } // Reference navigation to dependent
}

// Dependent (child)
public class BlogHeader
{
    public int Id { get; set; }
    public Blog Blog { get; set; } = null!; // Required reference navigation to principal
}
```
У деяких випадках вам може не знадобитися властивість зовнішнього ключа у вашій моделі, оскільки зовнішні ключі є деталлю того, як зв'язок представлений у базі даних, що не потрібно при використанні зв'язку виключно в об'єктно-орієнтованому режимі. Однак, якщо сутності будуть серіалізовані, наприклад, для надсилання по проводу, то значення зовнішнього ключа можуть бути корисним способом зберегти інформацію про зв'язок недоторканою, коли сутності не перебувають у об'єктній формі. Тому часто прагматично зберігати властивості зовнішнього ключа в типі .NET для цієї мети. Властивості зовнішнього ключа можуть бути приватними, що часто є гарним компромісом, щоб уникнути розкриття зовнішнього ключа, дозволяючи його значенню передаватись разом із об'єктом.

Продовжуючи попередній приклад, цей приклад видаляє властивість зовнішнього ключа з залежного типу сутності. Однак, замість використання первинного ключа, EF отримує інструкцію створити тіньову властивість зовнішнього ключа під назвою BlogId типу int.

Важливо зазначити, що в C# використовуються типи посилань, що можуть мати значення null, тому можливість використання null для навігації від залежного до принципального використовується для визначення того, чи є властивість зовнішнього ключа null-допустимою, і, отже, чи є зв'язок необов'язковим чи обов'язковим. Якщо типи посилань, що допускають значення null, не використовуються, то властивість зовнішнього ключа shadow за замовчуванням буде null-допустимою, що зробить зв'язок необов'язковим за замовчуванням. У цьому випадку використовуйте IsRequired, щоб примусово зробити властивість зовнішнього ключа shadow не null-допустимою та зробити зв'язок обов'язковим.

Цей зв'язок знову потребує певного налаштування, щоб вказати головний та залежний кінці:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne(e => e.Header)
        .WithOne(e => e.Blog)
        .HasForeignKey<BlogHeader>("BlogId");
}
```

У випадках, коли навігація, зовнішній ключ або обов'язковий/необов'язковий характер зв'язку не виявляються за домовленістю, ці речі можна налаштувати явно. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne(e => e.Header)
        .WithOne(e => e.Blog)
        .HasForeignKey<BlogHeader>("BlogId")
        .IsRequired();
}
```

## Необов'язковий зв'язок "one-to-one" з тіньовим зовнішнім ключем

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public BlogHeader? Header { get; set; } // Reference navigation to dependent
}

// Dependent (child)
public class BlogHeader
{
    public int Id { get; set; }
    public Blog? Blog { get; set; } // Optional reference navigation to principal
}
```

Як і в попередньому прикладі, властивість зовнішнього ключа була видалена з залежного типу сутності. Однак, на відміну від попереднього прикладу, цього разу властивість зовнішнього ключа створюється як null-допустима, оскільки використовуються типи посилань C#, що допускають значення null, а навігація по залежному типу сутності є null-допустимою. Це робить зв'язок необов'язковим.

Коли не використовуються типи посилань C#, що допускають значення null, властивість зовнішнього ключа за замовчуванням буде створена як така, що допускає значення null. Це означає, що зв'язки з автоматично створеними тіньовими властивостями за замовчуванням є необов'язковими.

Як і раніше, цей зв'язок потребує певного налаштування, щоб вказати головний та залежний кінці:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne(e => e.Header)
        .WithOne(e => e.Blog)
        .HasForeignKey<BlogHeader>("BlogId");
}
```
У випадках, коли навігація, зовнішній ключ або обов'язковий/необов'язковий характер зв'язку не виявляються за домовленістю, ці речі можна налаштувати явно. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne(e => e.Header)
        .WithOne(e => e.Blog)
        .HasForeignKey<BlogHeader>("BlogId")
        .IsRequired(false);
}
```

## "one-to-one" без навігації до головної

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public BlogHeader? Header { get; set; } // Reference navigation to dependent
}

// Dependent (child)
public class BlogHeader
{
    public int Id { get; set; }
    public int BlogId { get; set; } // Required foreign key property
}
```
У цьому прикладі властивість зовнішнього ключа було повторно введено, але навігацію по залежному елементу було видалено.

Зв'язок лише з однією навігацією — однією від залежного до принципала або однією від принципала до залежного, але не обома — називається однонаправленим зв'язком.

Цей зв'язок виявляється за домовленістю, оскільки зовнішній ключ виявляється, тим самим вказуючи на залежну сторону. 

Це можна вказати явно:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne(e => e.Header)
        .WithOne()
        .HasForeignKey<BlogHeader>(e => e.BlogId)
        .IsRequired();
}
```
Зверніть увагу, що виклик WithOne не має аргументів. Це спосіб повідомити EF, що немає навігації від BlogHeader до Blog.

## "one-to-one" без навігації до головної та з тіньовим зовнішнім ключем

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public BlogHeader? Header { get; set; } // Reference navigation to dependent
}

// Dependent (child)
public class BlogHeader
{
    public int Id { get; set; }
}
```
Цей приклад поєднує два попередні приклади, видаляючи як властивість зовнішнього ключа, так і навігацію на залежному об'єкті. 

Як і раніше, цей зв'язок потребує певного налаштування, щоб вказати головний та залежний кінці:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne(e => e.Header)
        .WithOne()
        .HasForeignKey<BlogHeader>("BlogId")
        .IsRequired();
}
```

## "one-to-one" без навігації до залежної

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
}

// Dependent (child)
public class BlogHeader
{
    public int Id { get; set; }
    public int BlogId { get; set; } // Required foreign key property
    public Blog Blog { get; set; } = null!; // Required reference navigation to principal
}
```
Навігацію по залежному елементу знову введено, тоді як навігацію по головному елементу натомість видалено. За домовленістю, EF розглядатиме це як зв'язок "один до багатьох". Щоб зробити його зв'язком "один до одного", потрібна деяка мінімальна конфігурація:

```cs
    modelBuilder.Entity<BlogHeader>()
        .HasOne(e => e.Blog)
        .WithOne();
```
Зверніть увагу ще раз, що WithOne() викликається без аргументів, що вказує на відсутність навігації в цьому напрямку.

У випадках, коли навігація, зовнішній ключ або обов'язковий/необов'язковий характер зв'язку не виявляються за домовленістю, ці речі можна налаштувати явно.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<BlogHeader>()
        .HasOne(e => e.Blog)
        .WithOne()
        .HasForeignKey<BlogHeader>(e => e.BlogId)
        .IsRequired();
}
```

## "one-to-one" без навігації

Інколи може бути корисним налаштувати зв'язок без навігації. Такий зв'язок можна змінити лише шляхом безпосередньої зміни значення зовнішнього ключа.

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
}

// Dependent (child)
public class BlogHeader
{
    public int Id { get; set; }
    public int BlogId { get; set; } // Required foreign key property
}
```
Цей зв'язок не виявляється за домовленістю, оскільки немає навігації, яка б вказувала на те, що ці два типи пов'язані.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne<BlogHeader>()
        .WithOne();
}
```

За такої конфігурації властивість BlogHeader.BlogId все ще визначається як зовнішній ключ за домовленістю, а зв'язок є "обов'язковим", оскільки властивість зовнішнього ключа не може мати значення null. Зв'язок можна зробити "необов'язковим", зробивши властивість зовнішнього ключа null-можливою.

Більш повна явна конфігурація цього зв'язку така:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne<BlogHeader>()
        .WithOne()
        .HasForeignKey<BlogHeader>(e => e.BlogId)
        .IsRequired();
}
```

## "one-to-one" з альтернативним ключем

У всіх наведених прикладах властивість зовнішнього ключа залежного об'єкта обмежена властивістю первинного ключа головного об'єкта. У всіх наведених досі прикладах властивість зовнішнього ключа залежного об'єкта обмежена властивістю первинного ключа принципала. Зовнішній ключ може бути обмежений іншою властивістю, яка потім стає альтернативним ключем для основного типу сутності.

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public int AlternateId { get; set; } // Alternate key as target of the BlogHeader.BlogId foreign key
    public BlogHeader? Header { get; set; } // Reference navigation to dependent
}

// Dependent (child)
public class BlogHeader
{
    public int Id { get; set; }
    public int BlogId { get; set; } // Required foreign key property
    public Blog Blog { get; set; } = null!; // Required reference navigation to principal
}
```
Цей зв'язок не виявляється за домовленістю, оскільки EF завжди, за домовленістю, створюватиме зв'язок з первинним ключем. Його можна налаштувати явно.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne(e => e.Header)
        .WithOne(e => e.Blog)
        .HasPrincipalKey<Blog>(e => e.AlternateId);
}
```
HasPrincipalKey можна поєднувати з іншими викликами для явного налаштування навігації, властивостей зовнішнього ключа та обов'язкового/необов'язкового характеру.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne(e => e.Header)
        .WithOne(e => e.Blog)
        .HasPrincipalKey<Blog>(e => e.AlternateId)
        .HasForeignKey<BlogHeader>(e => e.BlogId)
        .IsRequired();
}
```

## "one-to-one" зі складеним зовнішнім ключем

У всіх наведених досі прикладах властивість первинного або альтернативного ключа принципала складалася з однієї властивості. Первинні або альтернативні ключі також можуть бути сформовані з кількох властивостей – вони відомі як «композитні ключі». Коли принципал зв'язку має складений ключ, то зовнішній ключ залежного об'єкта також має бути складеним ключем з такою ж кількістю властивостей.

```cs
// Principal (parent)
public class Blog
{
    public int Id1 { get; set; } // Composite key part 1
    public int Id2 { get; set; } // Composite key part 2
    public BlogHeader? Header { get; set; } // Reference navigation to dependent
}

// Dependent (child)
public class BlogHeader
{
    public int Id { get; set; }
    public int BlogId1 { get; set; } // Required foreign key property part 1
    public int BlogId2 { get; set; } // Required foreign key property part 2
    public Blog Blog { get; set; } = null!; // Required reference navigation to principal
}
```
Цей зв'язок виявляється за домовленістю.Однак, його буде виявлено, лише якщо складений ключ було налаштовано явно, оскільки складені ключі не виявляються автоматично.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasKey(e => new { e.Id1, e.Id2 });
}
```

## Обов'язковий зв'язок "one-to-one" без каскадного видалення

```cs
// Principal (parent)
public class Blog
{
    public int Id { get; set; }
    public BlogHeader? Header { get; set; } // Reference navigation to dependent
}

// Dependent (child)
public class BlogHeader
{
    public int Id { get; set; }
    public int BlogId { get; set; } // Required foreign key property
    public Blog Blog { get; set; } = null!; // Required reference navigation to principal
}
```
За домовленістю, обов'язкові зв'язки налаштовані на каскадне видалення. Це пояснюється тим, що залежний елемент не може існувати в базі даних після видалення принципала. Базу даних можна налаштувати так, щоб вона генерувала помилку, яка зазвичай призводить до аварійного завершення роботи програми, замість автоматичного видалення залежних рядків, які більше не можуть існувати. Це вимагає певного налаштування:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasOne(e => e.Header)
        .WithOne(e => e.Blog)
        .OnDelete(DeleteBehavior.Restrict);
}
```

## Самопосилання «one-to-one»

У всіх попередніх прикладах головний тип сутності відрізнявся від залежного типу сутності. Це не обов'язково має бути так. Наприклад, у наведених нижче типах кожна Особа (Person) необов'язково пов'язана з іншою Особою (Person).

```cs
public class Person
{
    public int Id { get; set; }

    public int? HusbandId { get; set; } // Optional foreign key property
    public Person? Husband { get; set; } // Optional reference navigation to principal
    public Person? Wife { get; set; } // Reference navigation to dependent
}
```
Цей зв'язок виявляється за домовленістю. У випадках, коли навігація, зовнішній ключ або обов'язковий/необов'язковий характер зв'язку не виявляються за домовленістю, ці речі можна налаштувати явно.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Person>()
        .HasOne(e => e.Husband)
        .WithOne(e => e.Wife)
        .HasForeignKey<Person>(e => e.HusbandId)
        .IsRequired(false);
}
```
Для самопосилань «один до одного», оскільки типи головної та залежної сутностей однакові, вказівка ​​типу, який містить зовнішній ключ, не уточнює залежний кінець. У цьому випадку навігація, зазначена в HasOne, вказує від залежного до головного, а навігація, зазначена в WithOne, вказує від головного до залежного.