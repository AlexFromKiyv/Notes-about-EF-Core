# Перетворення значень

Конвертори значень дозволяють перетворювати значення властивостей під час читання з бази даних або запису в неї. Це перетворення може здійснюватися з одного значення в інше того ж типу (наприклад, шифрування рядків) або зі значення одного типу в значення іншого типу (наприклад, перетворення значень перерахунку в рядки та з рядків у базі даних).

## Огляд

Конвертори значень визначаються з точки зору ModelClrType та ProviderClrType. Тип моделі – це тип .NET властивості в типі сутності. Тип постачальника – це тип .NET, який розуміє постачальник бази даних. Наприклад, щоб зберегти перелічення як рядки в базі даних, тип моделі — це тип переліку, а тип постачальника — рядок. Ці два типи можуть бути однаковими.

Перетворення визначаються за допомогою двох дерев виразів Func: одне з ModelClrType до ProviderClrType, а інше з ProviderClrType до ModelClrType. Дерева виразів використовуються для того, щоб їх можна було скомпілювати в делегат доступу до бази даних для ефективного перетворення. Дерево виразів може містити простий виклик методу перетворення для складних перетворень.

Властивість, налаштована для перетворення значень, також може потребувати вказівки ValueComparer\<T\>.

## Налаштування конвертера значень

Перетворення значень налаштовуються в DbContext.OnModelCreating. Наприклад, розглянемо перелік та тип сутності, визначені як:

```cs
public class Rider
{
    public int Id { get; set; }
    public EquineBeast Mount { get; set; }
}

public enum EquineBeast
{
    Donkey,
    Mule,
    Horse,
    Unicorn
}
```

Перетворення можна налаштувати в OnModelCreating для зберігання значень перерахувань у вигляді рядків, таких як "Donkey", "Mule" тощо в базі даних; вам просто потрібно надати одну функцію, яка перетворює з ModelClrType на ProviderClrType, та іншу для протилежного перетворення:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder
        .Entity<Rider>()
        .Property(e => e.Mount)
        .HasConversion(
            v => v.ToString(),
            v => (EquineBeast)Enum.Parse(typeof(EquineBeast), v));
}
```
Значення null ніколи не буде передано до конвертера значень. Значення null у стовпці бази даних завжди є значенням null в екземплярі сутності, і навпаки. Це спрощує реалізацію перетворень і дозволяє їх розподіляти між властивостями, що допускають та не допускають значення null.

## Масове налаштування конвертера значень

Звичайно, один і той самий конвертер значень налаштовується для кожної властивості, яка використовує відповідний тип CLR. Замість того, щоб робити це вручну для кожної властивості, ви можете скористатися конфігурацією моделі перед узгодженням, щоб зробити це один раз для всієї моделі. Для цього визначте свій конвертер значень як клас:
```cs
public class CurrencyConverter : ValueConverter<Currency, decimal>
{
    public CurrencyConverter()
        : base(
            v => v.Amount,
            v => new Currency(v))
    {
    }
}
```
Потім перевизначте ConfigureConventions у вашому типі контексту та налаштуйте конвертер наступним чином:

```cs
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder
        .Properties<Currency>()
        .HaveConversion<CurrencyConverter>();
}
```
## Попередньо визначені перетворення

EF Core містить багато попередньо визначених перетворень, які дозволяють уникнути необхідності писати функції перетворення вручну. Натомість EF Core вибере перетворення для використання на основі типу властивості в моделі та запитуваного типу постачальника бази даних. Наприклад, як приклад вище використовуються перетворення перерахувань у рядки, але EF Core фактично зробить це автоматично, коли тип постачальника налаштовано як рядок з використанням універсального типу HasConversion:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder
        .Entity<Rider>()
        .Property(e => e.Mount)
        .HasConversion<string>();
}
```
Те саме можна досягти, явно вказавши тип стовпця бази даних. Наприклад, якщо тип сутності визначено так:

```cs
public class Rider2
{
    public int Id { get; set; }

    [Column(TypeName = "nvarchar(24)")]
    public EquineBeast Mount { get; set; }
}
```
Тоді значення перерахувань будуть збережені як рядки в базі даних без будь-якого подальшого налаштування в OnModelCreating.

## Клас ValueConverter

Виклик HasConversion, як показано вище, створить екземпляр ValueConverter\<TModel,TProvider\> та встановить його на властивість. Натомість ValueConverter можна створити явно. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var converter = new ValueConverter<EquineBeast, string>(
        v => v.ToString(),
        v => (EquineBeast)Enum.Parse(typeof(EquineBeast), v));

    modelBuilder
        .Entity<Rider>()
        .Property(e => e.Mount)
        .HasConversion(converter);
}
```
Це може бути корисним, коли кілька властивостей використовують однакове перетворення.

## Вбудовані конвертери

Як згадувалося вище, EF Core постачається з набором попередньо визначених класів ValueConverter\<TModel,TProvider\>, що знаходяться в просторі імен Microsoft.EntityFrameworkCore.Storage.ValueConversion. У багатьох випадках EF вибиратиме відповідний вбудований конвертер на основі типу властивості в моделі та типу, запитуваного в базі даних, як показано вище для перерахувань. Наприклад, використання .HasConversion<int>() для властивості bool призведе до того, що EF Core перетворить значення bool на числові значення нуль та одиниця:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder
        .Entity<User>()
        .Property(e => e.IsActive)
        .HasConversion<int>();
}
```
Це функціонально те саме, що й створення екземпляра вбудованого BoolToZeroOneConverter<TProvider> та його явне встановлення:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var converter = new BoolToZeroOneConverter<int>();

    modelBuilder
        .Entity<User>()
        .Property(e => e.IsActive)
        .HasConversion(converter);
}
```
В докуменьації є тамлиця вбудованих претворювачів. Зверніть увагу, що ці перетворення передбачають, що формат значення відповідає перетворенню. Наприклад, перетворення рядків на числа завершиться невдачею, якщо рядкові значення не можна розібрати як числа.

Зверніть увагу, що всі вбудовані конвертери не враховують стан, тому один екземпляр може безпечно використовуватися кількома властивостями.

## Аспекти стовпців та підказки щодо зіставлення

Деякі типи баз даних мають аспекти, які змінюють спосіб зберігання даних. До них належать:

* Точність та масштаб для десяткових чисел та стовпців дати/часу
* Розмір/довжина для двійкових та рядкових стовпців
* Юнікод для рядкових стовпців

Ці аспекти можна налаштувати звичайним чином для властивості, яка використовує конвертер значень, і вони застосовуватимуться до конвертованого типу бази даних. Наприклад, під час перетворення переліку на рядки ми можемо вказати, що стовпець бази даних має бути не в Unicode та зберігати до 20 символів:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder
        .Entity<Rider>()
        .Property(e => e.Mount)
        .HasConversion<string>()
        .HasMaxLength(20)
        .IsUnicode(false);
}
```
Або, під час явного створення конвертера:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var converter = new ValueConverter<EquineBeast, string>(
        v => v.ToString(),
        v => (EquineBeast)Enum.Parse(typeof(EquineBeast), v));

    modelBuilder
        .Entity<Rider>()
        .Property(e => e.Mount)
        .HasConversion(converter)
        .HasMaxLength(20)
        .IsUnicode(false);
}
```

Це призводить до появи стовпця varchar(20) під час використання міграцій EF Core з SQL Server:

```sql
CREATE TABLE [Rider] (
    [Id] int NOT NULL IDENTITY,
    [Mount] varchar(20) NOT NULL,
    CONSTRAINT [PK_Rider] PRIMARY KEY ([Id]));
```
Однак, якщо за замовчуванням усі стовпці EquineBeast мають бути varchar(20), тоді цю інформацію можна передати конвертеру значень як ConverterMappingHints. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var converter = new ValueConverter<EquineBeast, string>(
        v => v.ToString(),
        v => (EquineBeast)Enum.Parse(typeof(EquineBeast), v),
        new ConverterMappingHints(size: 20, unicode: false));

    modelBuilder
        .Entity<Rider>()
        .Property(e => e.Mount)
        .HasConversion(converter);
}
```
Тепер щоразу, коли використовується цей конвертер, стовпець бази даних буде не в Юнікоді з максимальною довжиною 20. Однак це лише підказки, оскільки вони перевизначаються будь-якими аспектами, явно встановленими для властивості відображення.

# Приклади

## Прості об'єкти-значення

У цьому прикладі використовується простий тип для обгортання примітивного типу. Це може бути корисним, коли ви хочете, щоб тип у вашій моделі був більш специфічним (і, отже, більш типобезпечним), ніж примітивний тип. У цьому прикладі цим типом є Dollars, який обгортає десятковий примітив:

```cs
public readonly struct Dollars
{
    public Dollars(decimal amount)
        => Amount = amount;

    public decimal Amount { get; }

    public override string ToString()
        => $"${Amount}";
}
```
Це можна використовувати в типі сутності:

```cs
public class Order
{
    public int Id { get; set; }

    public Dollars Price { get; set; }
}
```
І перетворюється на базовий decimal при зберіганні в базі даних:

```cs
modelBuilder.Entity<Order>()
    .Property(e => e.Price)
    .HasConversion(
        v => v.Amount,
        v => new Dollars(v));
```
Цей об'єкт значення реалізовано як структура лише для читання. Це означає, що EF Core може без проблем робити знімки та порівнювати значення.

## Складені об'єкти значення

У попередньому прикладі тип об'єкта значення містив лише одну властивість. Для об'єкта типу значення частіше використовується кілька властивостей, які разом утворюють концепцію домену. Наприклад, загальний тип Money, який містить і суму, і валюту:

```cs
public readonly struct Money
{
    [JsonConstructor]
    public Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public override string ToString()
        => (Currency == Currency.UsDollars ? "$" : "£") + Amount;

    public decimal Amount { get; }
    public Currency Currency { get; }
}

public enum Currency
{
    UsDollars,
    PoundsSterling
}
```
Цей об'єкт значення можна використовувати в типі сутності, як і раніше:

```cs
public class Order
{
    public int Id { get; set; }

    public Money Price { get; set; }
}
```
Конвертери значень наразі можуть конвертувати значення лише в один стовпець бази даних та з нього. Це обмеження означає, що всі значення властивостей об'єкта мають бути закодовані в одне значення стовпця. Зазвичай це вирішується шляхом серіалізації об'єкта під час його додавання до бази даних, а потім його повторної десеріалізації під час виведення з бази даних. Наприклад, за допомогою System.Text.Json:

```cs
        modelBuilder.Entity<Order>()
           .Property(e => e.Price)
           .HasConversion(
               v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
               v => JsonSerializer.Deserialize<Money>(v, (JsonSerializerOptions)null));

```
Як і в попередньому прикладі, цей об'єкт значення реалізовано як структура лише для читання. Це означає, що EF Core може без проблем робити знімки та порівнювати значення.

## Колекції примітивів

Сериалізація також може бути використана для зберігання колекції значень примітивів.

```cs
public class Post
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Contents { get; set; }

    public ICollection<string> Tags { get; set; }
}
```
Повторне використання System.Text.Json:

```cs
modelBuilder.Entity<Post>()
    .Property(e => e.Tags)
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null),
        new ValueComparer<ICollection<string>>(
            (c1, c2) => c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => (ICollection<string>)c.ToList()));
```
ICollection<string> представляє змінний тип посилання. Це означає, що потрібен ValueComparer<T>, щоб EF Core міг правильно відстежувати та виявляти зміни.

## Колекції об'єктів-значень

Поєднуючи два попередні приклади, ми можемо створити колекцію об'єктів-значень. Наприклад, розглянемо тип AnnualFinance, який моделює фінанси блогу за один рік:

```cs
public readonly struct AnnualFinance
{
    [JsonConstructor]
    public AnnualFinance(int year, Money income, Money expenses)
    {
        Year = year;
        Income = income;
        Expenses = expenses;
    }

    public int Year { get; }
    public Money Income { get; }
    public Money Expenses { get; }
    public Money Revenue => new Money(Income.Amount - Expenses.Amount, Income.Currency);
}
```

Цей тип складається з кількох типів Money, які ми створили раніше

Потім ми можемо додати колекцію AnnualFinance до нашого типу сутності:

```cs
public class Blog
{
    public int Id { get; set; }
    public string Name { get; set; }

    public IList<AnnualFinance> Finances { get; set; }
}
```
І знову використовуйте серіалізацію для зберігання цього:

```cs
modelBuilder.Entity<Blog>()
    .Property(e => e.Finances)
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
        v => JsonSerializer.Deserialize<List<AnnualFinance>>(v, (JsonSerializerOptions)null),
        new ValueComparer<IList<AnnualFinance>>(
            (c1, c2) => c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => (IList<AnnualFinance>)c.ToList()));
```

## Об'єкти-значення як ключі

Іноді примітивні властивості ключів можуть бути обгорнуті в об'єкти-значення, щоб додати додатковий рівень безпеки типів під час призначення значень. Наприклад, ми могли б реалізувати ключовий тип для блогів та ключовий тип для публікацій:

```cs
public readonly struct BlogKey
{
    public BlogKey(int id) => Id = id;
    public int Id { get; }
}

public readonly struct PostKey
{
    public PostKey(int id) => Id = id;
    public int Id { get; }
}
```

Потім їх можна використовувати в моделі домену:

```cs
public class Blog
{
    public BlogKey Id { get; set; }
    public string Name { get; set; }

    public ICollection<Post> Posts { get; set; }
}

public class Post
{
    public PostKey Id { get; set; }

    public string Title { get; set; }
    public string Content { get; set; }

    public BlogKey? BlogId { get; set; }
    public Blog Blog { get; set; }
}
```
Зверніть увагу, що Blog.Id не може випадково бути призначений PostKey, а Post.Id не може випадково бути призначений BlogKey. Аналогічно, властивість зовнішнього ключа Post.BlogId повинна бути призначена BlogKey.

Показ цього шаблону не означає, що ми його рекомендуємо. Ретельно подумайте, чи цей рівень абстракції допомагає вам у розробці, чи навпаки. Також розгляньте можливість використання навігації та згенерованих ключів замість безпосередньої роботи зі значеннями ключів.

Ці ключові властивості потім можна зіставити за допомогою конвертерів значень

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var blogKeyConverter = new ValueConverter<BlogKey, int>(
        v => v.Id,
        v => new BlogKey(v));

    modelBuilder.Entity<Blog>().Property(e => e.Id).HasConversion(blogKeyConverter);

    modelBuilder.Entity<Post>(
        b =>
        {
            b.Property(e => e.Id).HasConversion(v => v.Id, v => new PostKey(v));
            b.Property(e => e.BlogId).HasConversion(blogKeyConverter);
        });
}
```

## Використовуйте ulong для timestamp/rowversion

SQL Server підтримує автоматичний оптимістичний паралельний запис, використовуючи 8-байтові двійкові стовпці rowversion/timestamp часу. Вони завжди зчитуються з бази даних та записуються в неї за допомогою 8-байтового масиву. Однак, масиви байтів є змінним типом посилань, що робить їх дещо складними для роботи. Конвертери значень дозволяють зіставити rowversion з властивістю ulong, що набагато доречніше та простіше у використанні, ніж масив байтів. Наприклад, розглянемо сутність Blog з токеном паралелізму ulong:

```cs
public class Blog
{
    public int Id { get; set; }
    public string Name { get; set; }
    public ulong Version { get; set; }
}
```
Це можна зіставити зі стовпцем rowversion SQL-сервера за допомогою конвертера значень:

```cs
modelBuilder.Entity<Blog>()
    .Property(e => e.Version)
    .IsRowVersion()
    .HasConversion<byte[]>();
```

## Вказування DateTime.Kind під час читання дат

SQL Server скидає прапорець DateTime.Kind під час зберігання DateTime як datetime або datetime2. Це означає, що значення DateTime, що повертаються з бази даних, завжди мають DateTimeKind з значенням Unspecified. Конвертери значень можна використовувати двома способами для вирішення цієї проблеми. По-перше, EF Core має конвертер значень, який створює 8-байтове непрозоре значення, що зберігає прапорець Kind. Наприклад:

```cs
modelBuilder.Entity<Post>()
    .Property(e => e.PostedOn)
    .HasConversion<long>();
```
Це дозволяє змішувати в базі даних значення DateTime з різними прапорцями Kind. Проблема такого підходу полягає в тому, що база даних більше не має розпізнаваних стовпців datetime або datetime2. Тож замість цього зазвичай завжди зберігають час UTC (або, рідше, завжди місцевий час), а потім або ігнорують прапорець Kind, або встановлюють його на відповідне значення за допомогою конвертера значень. Наприклад, наведений нижче конвертер гарантує, що значення DateTime, зчитане з бази даних, матиме DateTimeKind UTC:

```cs
modelBuilder.Entity<Post>()
    .Property(e => e.LastUpdated)
    .HasConversion(
        v => v,
        v => new DateTime(v.Ticks, DateTimeKind.Utc));
```
Якщо в екземплярах сутностей встановлюється поєднання локальних значень та значень UTC, то конвертер можна використовувати для відповідного перетворення перед вставкою. Наприклад:

```cs
modelBuilder.Entity<Post>()
    .Property(e => e.LastUpdated)
    .HasConversion(
        v => v.ToUniversalTime(),
        v => new DateTime(v.Ticks, DateTimeKind.Utc));
```
Ретельно розгляньте можливість уніфікації всього коду доступу до бази даних, щоб він постійно використовував час UTC, а місцевий час використовувався лише під час представлення даних користувачам.

## Використання ключів рядків без урахування регістру

Деякі бази даних, включаючи SQL Server, за замовчуванням виконують порівняння рядків без урахування регістру. З іншого боку, .NET за замовчуванням виконує порівняння рядків з урахуванням регістру. Це означає, що значення зовнішнього ключа, таке як "DotNet", відповідатиме значенню первинного ключа "dotnet" на SQL Server, але не відповідатиме йому в EF Core. Порівняльник значень для ключів можна використовувати, щоб примусово використовувати EF Core для порівняння рядків без урахування регістру, як у базі даних. Наприклад, розглянемо модель блогу/дописів з рядковими ключами:

```cs
public class Blog
{
    public string Id { get; set; }
    public string Name { get; set; }

    public ICollection<Post> Posts { get; set; }
}

public class Post
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public string BlogId { get; set; }
    public Blog Blog { get; set; }
}
```
Це не працюватиме належним чином, якщо деякі значення Post.BlogId матимуть різний регістр. Помилки, спричинені цим, залежатимуть від того, що робить програма, але зазвичай стосуються графів об'єктів, які неправильно виправлені, та/або оновлень, які не вдаються через неправильне значення FK. Для виправлення цього можна використовувати порівняльник значень:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var comparer = new ValueComparer<string>(
        (l, r) => string.Equals(l, r, StringComparison.OrdinalIgnoreCase),
        v => v.ToUpper().GetHashCode(),
        v => v);

    modelBuilder.Entity<Blog>()
        .Property(e => e.Id)
        .Metadata.SetValueComparer(comparer);

    modelBuilder.Entity<Post>(
        b =>
        {
            b.Property(e => e.Id).Metadata.SetValueComparer(comparer);
            b.Property(e => e.BlogId).Metadata.SetValueComparer(comparer);
        });
}
```
Порівняння рядків .NET та порівняння рядків бази даних можуть відрізнятися не лише чутливістю до регістру. Цей шаблон працює для простих ключів ASCII, але може не працювати для ключів із будь-якими символами, специфічними для культури.

## Обробка рядків бази даних фіксованої довжини

У попередньому прикладі не потрібен був конвертер значень. Однак, конвертер може бути корисним для рядків бази даних фіксованої довжини, таких як char(20) або nchar(20). Рядки фіксованої довжини доповнюються до повної довжини щоразу, коли значення вставляється в базу даних. Це означає, що ключове значення "dotnet" буде зчитано з бази даних як "dotnet..............", де . позначає пробіл. Тоді воно не буде правильно порівнюватися зі значеннями ключів, які не доповнені. Конвертер значень можна використовувати для обрізання відступів під час зчитування значень ключів. Це можна поєднати з порівняльником значень з попереднього прикладу для правильного порівняння ASCII-ключів фіксованої довжини без урахування регістру. Наприклад:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var converter = new ValueConverter<string, string>(
        v => v,
        v => v.Trim());

    var comparer = new ValueComparer<string>(
        (l, r) => string.Equals(l, r, StringComparison.OrdinalIgnoreCase),
        v => v.ToUpper().GetHashCode(),
        v => v);

    modelBuilder.Entity<Blog>()
        .Property(e => e.Id)
        .HasColumnType("char(20)")
        .HasConversion(converter, comparer);

    modelBuilder.Entity<Post>(
        b =>
        {
            b.Property(e => e.Id).HasColumnType("char(20)").HasConversion(converter, comparer);
            b.Property(e => e.BlogId).HasColumnType("char(20)").HasConversion(converter, comparer);
        });
}
```

## Шифрування значень властивостей

Конвертери значень можна використовувати для шифрування значень властивостей перед їх надсиланням до бази даних, а потім для їх розшифрування під час виведення. Наприклад, використання зворотного порядку символів рядків як заміну справжнього алгоритму шифрування:

```cs
modelBuilder.Entity<User>().Property(e => e.Password).HasConversion(
    v => new string(v.Reverse().ToArray()),
    v => new string(v.Reverse().ToArray()));
```

## Обмеження

Існує кілька відомих поточних обмежень системи конвертації значень:
* Як зазначалося вище, значення null не можна конвертувати.
* Неможливо робити запити до властивостей, перетворених на значення, наприклад, посилатися на члени типу .NET, перетвореного на значення, у ваших запитах LINQ.
* Наразі немає способу розподілити перетворення однієї властивості на кілька стовпців або навпаки.
* Генерація значень не підтримується для більшості ключів, що відображаються через конвертери значень.
* Перетворення значень не можуть посилатися на поточний екземпляр DbContext.
* Параметри, що використовують типи, перетворені на значення, наразі не можна використовувати в необроблених SQL API.

У майбутніх релізах розглядається питання про скасування цих обмежень.
