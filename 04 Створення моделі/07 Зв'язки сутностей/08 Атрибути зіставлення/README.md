# Атрибути зіставлення (також відомі як анотації даних) для зв'язків

Атрибути зіставлення використовуються для зміни або перевизначення конфігурації, виявленої за допомогою правил побудови моделі за домовленістю. Конфігурацію, виконану за допомогою атрибутів зіставлення, можна перевизначити API побудови моделі, що використовується в OnModelCreating.

Тут охоплєні атрибути зіставлення лише в контексті конфігурації зв'язків. 

## З відки отримуються атрибути зіставлення

Багато атрибутів зіставлення походять із просторів імен System.ComponentModel.DataAnnotations та System.ComponentModel.DataAnnotations.Schema. Атрибути в цих просторах імен включені до базової платформи у всіх підтримуваних версіях .NET, тому не потребують встановлення жодних додаткових пакетів NuGet. Ці атрибути зіставлення зазвичай називаються «анотаціями даних» і використовуються різноманітними фреймворками, включаючи EF Core,  ASP.NET Core MVC тощо. Вони також використовуються для валідації.

Використання анотацій даних у багатьох технологіях, як для зіставлення, так і для перевірки, призвело до відмінностей у семантиці між технологіями. Усі нові атрибути зіставлення, розроблені для EF Core, тепер є специфічними для EF Core, що зберігає їхню семантику та використання простими та зрозумілими. Ці атрибути містяться в пакеті Microsoft.EntityFrameworkCore.Abstractions NuGet. Цей пакет включається як залежність щоразу, коли використовується основний пакет Microsoft.EntityFrameworkCore або один із пов'язаних пакетів постачальників баз даних. Однак пакет Abstractions — це легкий пакет, на який можна безпосередньо посилатися з коду програми, не залучаючи весь EF Core та його залежності.

## RequiredAttribute

RequiredAttribute застосовується до властивості, щоб вказати, що властивість не може бути null. У контексті зв'язків, [Required] зазвичай використовується для властивості зовнішнього ключа. Це робить зовнішній ключ непридатним для використання з значенням null, тим самим роблячи зв'язок обов'язковим. Наприклад, для наведених нижче типів властивість Post.BlogId стає ненульовою, і зв'язок стає обов'язковим.

```cs
public class Blog
{
    public string Id { get; set; }
    public List<Post> Posts { get; } = new();
}

public class Post
{
    public int Id { get; set; }

    [Required]
    public string BlogId { get; set; }

    public Blog Blog { get; init; }
}
```

Під час використання типів посилань C#, що допускають значення null, властивість BlogId у цьому прикладі вже не допускає значення null, а це означає, що атрибут [Required] не матиме жодного ефекту.

Розміщення [Required] до залежної навігації має той самий ефект. Тобто, зовнішній ключ стає ненульовим, і таким чином зв'язок обов'язковим.

```cs
public class Blog
{
    public string Id { get; set; }
    public List<Post> Posts { get; } = new();
}

public class Post
{
    public int Id { get; set; }

    public string BlogId { get; set; }

    [Required]
    public Blog Blog { get; init; }
}
```
Якщо [Required] знайдено в залежній навігації, а властивість зовнішнього ключа перебуває в стані shadow, тоді властивість shadow стає ненульовою, що робить зв'язок обов'язковим.

```cs
public class Blog
{
    public string Id { get; set; }
    public List<Post> Posts { get; } = new();
}

public class Post
{
    public int Id { get; set; }

    [Required]
    public Blog Blog { get; init; }
}
```
Використання [Required] на стороні головної навігації зв'язку не має жодного ефекту.

## Атрибут ForeignKeyAttribute
ForeignKeyAttribute використовується для зв'язку властивості зовнішнього ключа з її навігаціями. [ForeignKey] можна розмістити на властивості зовнішнього ключа разом з назвою залежної навігації.

```cs
public class Blog
{
    public string Id { get; set; }
    public List<Post> Posts { get; } = new();
}

public class Post
{
    public int Id { get; set; }

    [ForeignKey(nameof(Blog))]
    public string BlogKey { get; set; }

    public Blog Blog { get; init; }
}
```
Або ж [ForeignKey] можна розмістити як у залежній, так і в головній навігації з назвою властивості, яка використовуватиметься як зовнішній ключ.

```cs
public class Blog
{
    public string Id { get; set; }
    public List<Post> Posts { get; } = new();
}

public class Post
{
    public int Id { get; set; }

    public string BlogKey { get; set; }

    [ForeignKey(nameof(BlogKey))]
    public Blog Blog { get; init; }
}
```
Коли [ForeignKey] розміщується в навігації, а надане ім'я не збігається з жодним ім'ям властивості, тоді буде створено тіньову властивість з цим ім'ям, яка діятиме як зовнішній ключ.

```cs
public class Blog
{
    public string Id { get; set; }
    public List<Post> Posts { get; } = new();
}

public class Post
{
    public int Id { get; set; }

    [ForeignKey("BlogKey")]
    public Blog Blog { get; init; }
}
```

## InversePropertyAttribute

InversePropertyAttribute використовується для зв'язку навігації з її інверсією. Наприклад, у наступних типах сутностей існують два зв'язки між блогом та публікацією. Без будь-якої конфігурації, конвенції EF не можуть визначити, які навігації між двома типами слід парувати. Додавання [InverseProperty] до однієї з парних навігацій вирішує цю неоднозначність і дозволяє EF побудувати модель.

```cs
public class Blog
{
    public int Id { get; set; }

    [InverseProperty("Blog")]
    public List<Post> Posts { get; } = new();

    public int FeaturedPostId { get; set; }
    public Post FeaturedPost { get; set; }
}

public class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; }

    public Blog Blog { get; init; }
}
```
[InverseProperty] потрібен лише тоді, коли між однаковими типами існує більше одного зв'язку. З одним зв'язком дві навігації автоматично об'єднуються в пару.

## DeleteBehaviorAttribute

За домовленістю, EF використовує ClientSetNull DeleteBehavior для необов'язкових зв'язків та Cascade для обов'язкових. Це можна змінити, розмістивши DeleteBehaviorAttribute в одній з навігацій зв'язку.

```cs
public class Blog
{
    public int Id { get; set; }
    public List<Post> Posts { get; } = new();
}

public class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Blog Blog { get; init; }
}
```