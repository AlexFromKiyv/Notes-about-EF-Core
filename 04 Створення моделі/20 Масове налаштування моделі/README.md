# Масове налаштування моделі

Коли аспект потрібно налаштувати однаково для кількох типів сутностей, наступні методи дозволяють зменшити дублювання коду та консолідувати логіку.

## Масове налаштування в OnModelCreating

Кожен об'єкт modelBuilder, повернутий з ModelBuilder, надає властивість Model або Metadata, яка забезпечує низькорівневий доступ до об'єктів, що складають модель. Зокрема, існують методи, які дозволяють перебирати певні об'єкти в моделі та застосовувати до них загальну конфігурацію.

У наступному прикладі модель містить користувацький тип значення «Currency»:

```cs
public readonly struct Currency
{
    public Currency(decimal amount)
        => Amount = amount;

    public decimal Amount { get; }

    public override string ToString()
        => $"${Amount}";
}
```

Властивості цього типу не виявляються за замовчуванням, оскільки поточний постачальник EF не знає, як зіставити його з типом бази даних. Цей фрагмент OnModelCreating додає всі властивості типу Currency та налаштовує конвертер значень на підтримуваний тип — десяткове число:

```cs
foreach (var entityType in modelBuilder.Model.GetEntityTypes())
{
    foreach (var propertyInfo in entityType.ClrType.GetProperties())
    {
        if (propertyInfo.PropertyType == typeof(Currency))
        {
            entityType.AddProperty(propertyInfo)
                .SetValueConverter(typeof(CurrencyConverter));
        }
    }
}
```
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

## Недоліки API метаданих

* На відміну від Fluent API, кожну модифікацію моделі потрібно робити явно. Наприклад, якщо деякі властивості Currency були налаштовані як навігації за допомогою конвенції, то спочатку потрібно видалити навігацію, що посилається на властивість CLR, перш ніж додавати для неї властивість типу сутності.

* Конвенції виконуються після кожної зміни. Якщо ви видаляєте навігацію, виявлену конвенцією, то конвенція запускається знову і може додати її назад. Щоб запобігти цьому, вам потрібно або відкласти конвенції до моменту додавання властивості, викликавши DelayConventions() та пізніше утилізувавши повернутий об'єкт, або позначити властивість CLR як ігноровану за допомогою AddIgnored.

* Типи сутностей можуть бути додані після цієї ітерації, і конфігурація не буде до них застосована. Зазвичай цього можна запобігти, розмістивши цей код в кінці OnModelCreating, але якщо у вас є два взаємозалежні набори конфігурацій, може не бути порядку, який дозволить їх застосовувати послідовно.

## Доконвенційна конфігурація

EF Core дозволяє вказати конфігурацію зіставлення один раз для заданого типу CLR; ця конфігурація потім застосовується до всіх властивостей цього типу в моделі, коли вони виявляються. Це називається «конфігурацією моделі до початку дії конвенцій», оскільки вона налаштовує аспекти моделі до того, як дозволено виконувати конвенції побудови моделі. Така конфігурація застосовується шляхом перевизначення ConfigureConventions для типу, похідного від DbContext.

У цьому прикладі показано, як налаштувати всі властивості типу Currency, щоб вони мали конвертер значень:

```cs
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder
        .Properties<Currency>()
        .HaveConversion<CurrencyConverter>();
}
```
А цей приклад показує, як налаштувати деякі аспекти для всіх властивостей типу string:

```cs
configurationBuilder
    .Properties<string>()
    .AreUnicode(false)
    .HaveMaxLength(1024);
```
Тип, вказаний у виклику ConfigureConventions, може бути базовим типом, інтерфейсом або універсальним визначенням типу. Усі відповідні конфігурації будуть застосовані в порядку, починаючи з найменш специфічної:

* Інтерфейс
* Базовий тип
* Визначення універсального типу
* Тип значення, що не допускає значення null
* Точний тип

Доконвенційна конфігурація еквівалентна явному налаштуванню, яке застосовується одразу після додавання відповідного об'єкта до моделі. Вона замінить усі конвенції та анотації даних. Наприклад, за допомогою вищезгаданої конфігурації всі властивості зовнішнього ключа рядка будуть створені як не-юнікодові з максимальною довжиною 1024, навіть якщо це не відповідає головному ключу.

### Ігнорування типів

Доконвенційна конфігурація також дозволяє ігнорувати тип та запобігати його виявленню конвенціями як типу сутності або як властивості типу сутності:

```cs
configurationBuilder
    .IgnoreAny(typeof(IList<>));
```

### Зіставлення типів за замовчуванням

Зазвичай, EF може перетворювати запити з константами типу, який не підтримується постачальником, якщо ви вказали конвертер значень для властивості цього типу. Однак у запитах, які не містять властивостей цього типу, EF не може знайти правильний конвертер значень. У цьому випадку можна викликати DefaultTypeMapping, щоб додати або перевизначити зіставлення типу постачальника:

```cs
configurationBuilder
    .DefaultTypeMapping<Currency>()
    .HasConversion<CurrencyConverter>();
```

### Обмеження конфігурації до застосування конвенції

* Багато аспектів неможливо налаштувати за допомогою цього підходу.
* Наразі конфігурація визначається лише типом CLR. 
* Ця конфігурація виконується перед створенням моделі. Якщо під час її застосування виникають конфлікти, трасування стека винятків не міститиме методу ConfigureConventions, тому може бути важче знайти причину.

## Домовлкності (Conventions)

Конвенції побудови моделей EF Core – це класи, що містять логіку, що запускається на основі змін, внесених до моделі під час її побудови. Це підтримує актуальність моделі під час явного налаштування, застосування атрибутів відображення та виконання інших домовленостей. Щоб брати участь у цьому, кожна конвенція реалізує один або декілька інтерфейсів, які визначають, коли буде запущено відповідний метод. Наприклад, конвенція, яка реалізує IEntityTypeAddedConvention, спрацьовуватиме щоразу, коли до моделі додається новий тип сутності. Аналогічно, конвенція, яка реалізує як IForeignKeyAddedConvention, так і IKeyAddedConvention, спрацьовуватиме щоразу, коли до моделі додається ключ або зовнішній ключ.

Правила побудови моделей є потужним способом керування конфігурацією моделі, але можуть бути складними та важкодоступними. У багатьох випадках для легкого визначення загальної конфігурації властивостей і типів можна використовувати конфігурацію моделі до узгодження.

## Додавання нової домовленості

### Приклад: Обмеження довжини властивостей дискримінатора

Стратегія успадкування таблиць на ієрархію вимагає стовпця дискримінатора, щоб вказати, який тип представлений у будь-якому заданому рядку. За замовчуванням EF використовує необмежений рядковий стовпець для дискримінатора, що гарантує його роботу для будь-якої довжини дискримінатора. Однак, обмеження максимальної довжини рядків дискримінатора може забезпечити ефективніше зберігання та виконання запитів. Давайте створимо нову угоду, яка це зробить.

Визначення того, які інтерфейси реалізувати, може бути складним, оскільки конфігурація, внесена до моделі в один момент, може бути змінена або видалена пізніше. Наприклад, ключ може бути створений за домовленістю, але пізніше замінений, коли явно налаштовано інший ключ.

Давайте зробимо це трохи конкретніше, зробивши першу спробу реалізувати угоду про довжину дискримінатора:

```cs
public class DiscriminatorLengthConvention1 : IEntityTypeBaseTypeChangedConvention
{
    public void ProcessEntityTypeBaseTypeChanged(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionEntityType? newBaseType,
        IConventionEntityType? oldBaseType,
        IConventionContext<IConventionEntityType> context)
    {
        var discriminatorProperty = entityTypeBuilder.Metadata.FindDiscriminatorProperty();
        if (discriminatorProperty != null
            && discriminatorProperty.ClrType == typeof(string))
        {
            discriminatorProperty.Builder.HasMaxLength(24);
        }
    }
}
```
Ця конвенція реалізує IEntityTypeBaseTypeChangedConvention, що означає, що вона спрацьовуватиме щоразу, коли змінюється ієрархія відображеного успадкування для типу сутності. Потім конвенція знаходить і налаштовує властивість дискримінатора рядків для ієрархії.

Ця конвенція потім використовується шляхом виклику Add у ConfigureConventions:

```cs
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.Conventions.Add(_ =>  new DiscriminatorLengthConvention1());
}
```
Замість безпосереднього додавання екземпляра конвенції, метод Add приймає фабрику для створення екземплярів конвенції. Це дозволяє конвенції використовувати залежності від внутрішнього постачальника послуг EF Core. Оскільки ця конвенція не має залежностей, параметр постачальника послуг називається _, що вказує на те, що він ніколи не використовується.

Побудова моделі та аналіз типу сутності Post показує, що це спрацювало — властивість дискримінатора тепер налаштована на максимальну довжину 24:

```
 Discriminator (no field, string) Shadow Required AfterSave:Throw MaxLength(24)
```
Але що станеться, якщо ми тепер явно налаштуємо іншу властивість дискримінатора? Наприклад:

```cs
modelBuilder.Entity<Post>()
    .HasDiscriminator<string>("PostTypeDiscriminator")
    .HasValue<Post>("Post")
    .HasValue<FeaturedPost>("Featured");
```
Дивлячись на налагоджувальне подання моделі, ми виявляємо, що довжина дискримінатора більше не налаштована.

```
PostTypeDiscriminator (no field, string) Shadow Required AfterSave:Throw
```
Це тому, що властивість дискримінатора, яку ми налаштували в нашій конвенції, пізніше була видалена, коли було додано користувацький дискримінатор. Ми могли б спробувати виправити це, реалізувавши інший інтерфейс у нашій конвенції для реагування на зміни дискримінатора, але визначити, який інтерфейс реалізувати, непросто.

На щастя, існує простіший підхід. Часто неважливо, як виглядає модель під час її побудови, головне, щоб кінцева модель була правильною. Крім того, конфігурація, яку ми хочемо застосувати, часто не потребує реагування інших домовленостей. Отже, наша конвенція може реалізувати IModelFinalizingConvention. Конвенції фіналізації моделі виконуються після завершення всіх інших етапів побудови моделі, і тому мають доступ до майже кінцевого стану моделі. Це суперечить інтерактивним конвенціям, які реагують на кожну зміну моделі та гарантують, що модель є актуальною в будь-який момент виконання методу OnModelCreating. Конвенція про фіналізацію моделі зазвичай передбачає ітерацію по всій моделі, налаштовуючи елементи моделі по ходу процесу. Отже, в цьому випадку ми знайдемо кожен дискримінатор у моделі та налаштуємо його:

```cs
public class DiscriminatorLengthConvention2 : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes()
                     .Where(entityType => entityType.BaseType == null))
        {
            var discriminatorProperty = entityType.FindDiscriminatorProperty();
            if (discriminatorProperty != null
                && discriminatorProperty.ClrType == typeof(string))
            {
                discriminatorProperty.Builder.HasMaxLength(24);
            }
        }
    }
}
```

Після побудови моделі з використанням цієї нової домовленості ми виявляємо, що довжина дискримінатора тепер налаштована правильно, навіть попри те, що її було налаштовано:

```
PostTypeDiscriminator (no field, string) Shadow Required AfterSave:Throw MaxLength(24)
```

Ми можемо піти ще далі та налаштувати максимальну довжину як довжину найдовшого значення дискримінатора:

```cs
public class DiscriminatorLengthConvention3 : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes()
                     .Where(entityType => entityType.BaseType == null))
        {
            var discriminatorProperty = entityType.FindDiscriminatorProperty();
            if (discriminatorProperty != null
                && discriminatorProperty.ClrType == typeof(string))
            {
                var maxDiscriminatorValueLength =
                    entityType.GetDerivedTypesInclusive().Select(e => ((string)e.GetDiscriminatorValue()!).Length).Max();

                discriminatorProperty.Builder.HasMaxLength(maxDiscriminatorValueLength);
            }
        }
    }
}
```
Тепер максимальна довжина стовпця дискримінатора становить 8, що дорівнює довжині "Featured" – найдовшого значення дискримінатора, що використовується.

### Приклад: Довжина за замовчуванням для всіх властивостей рядка

Розглянемо ще один приклад, де можна використовувати угоду про завершення – встановлення максимальної довжини за замовчуванням для будь-якої властивості рядка. Ця угода виглядає досить схоже на попередній приклад:

```cs
public class MaxStringLengthConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var property in modelBuilder.Metadata.GetEntityTypes()
                     .SelectMany(
                         entityType => entityType.GetDeclaredProperties()
                             .Where(
                                 property => property.ClrType == typeof(string))))
        {
            property.Builder.HasMaxLength(512);
        }
    }
}
```
Ця домовленість досить проста. Вона знаходить кожну властивість рядка в моделі та встановлює її максимальну довжину на 512. Дивлячись у вікні налагодження на властивості для Post, ми бачимо, що всі властивості рядка тепер мають максимальну довжину 512.

```
EntityType: Post
  Properties:
    Id (int) Required PK AfterSave:Throw ValueGenerated.OnAdd
    AuthorId (no field, int?) Shadow FK Index
    BlogId (no field, int) Shadow Required FK Index
    Content (string) Required MaxLength(512)
    Discriminator (no field, string) Shadow Required AfterSave:Throw MaxLength(512)
    PublishedOn (DateTime) Required
    Title (string) Required MaxLength(512)
```
Те саме можна досягти за допомогою попередньої конфігурації, але використання конвенції дозволяє додатково фільтрувати відповідні властивості та замінювати конфігурацію анотаціями даних.

Нарешті, перш ніж ми залишимо цей приклад, що станеться, якщо ми використаємо обидва типи, MaxStringLengthConvention та DiscriminatorLengthConvention3, одночасно? Відповідь полягає в тому, що це залежить від порядку їх додавання, оскільки конвенції фіналізації моделі виконуються в порядку їх додавання. Отже, якщо MaxStringLengthConvention додається останнім, то він виконуватиметься останнім і встановить максимальну довжину властивості дискримінатора на 512. Тому в цьому випадку краще додати DiscriminatorLengthConvention3 останнім, щоб він міг перевизначити максимальну довжину за замовчуванням лише для властивостей дискримінатора, залишаючи всі інші властивості рядків як 512.

## Заміна існуючої конвенції

Іноді, замість того, щоб повністю видалити існуючу конвенцію, ми хочемо замінити її конвенцією, яка робить по суті те саме, але зі зміненою поведінкою. Це корисно, оскільки існуюча конвенція вже реалізує інтерфейси, необхідні для відповідного запуску.

### Приклад: Зіставлення властивостей Opt-in

EF Core зіставляє всі публічні властивості для читання та запису за домовленістю. Це може не підходити для способу визначення ваших типів сутностей. Щоб змінити це, ми можемо замінити PropertyDiscoveryConvention нашою власною реалізацією, яка не відображає жодної властивості, якщо вона явно не відображається в OnModelCreating або не позначена новим атрибутом під назвою Persist:

```cs
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class PersistAttribute : Attribute
{
}
```
Ось нова конвенція:

```cs
public class AttributeBasedPropertyDiscoveryConvention : PropertyDiscoveryConvention
{
    public AttributeBasedPropertyDiscoveryConvention(ProviderConventionSetBuilderDependencies dependencies)
        : base(dependencies)
    {
    }

    public override void ProcessEntityTypeAdded(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionContext<IConventionEntityTypeBuilder> context)
        => Process(entityTypeBuilder);

    public override void ProcessEntityTypeBaseTypeChanged(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionEntityType? newBaseType,
        IConventionEntityType? oldBaseType,
        IConventionContext<IConventionEntityType> context)
    {
        if ((newBaseType == null
             || oldBaseType != null)
            && entityTypeBuilder.Metadata.BaseType == newBaseType)
        {
            Process(entityTypeBuilder);
        }
    }

    private void Process(IConventionEntityTypeBuilder entityTypeBuilder)
    {
        foreach (var memberInfo in GetRuntimeMembers())
        {
            if (Attribute.IsDefined(memberInfo, typeof(PersistAttribute), inherit: true))
            {
                entityTypeBuilder.Property(memberInfo);
            }
            else if (memberInfo is PropertyInfo propertyInfo
                     && Dependencies.TypeMappingSource.FindMapping(propertyInfo) != null)
            {
                entityTypeBuilder.Ignore(propertyInfo.Name);
            }
        }

        IEnumerable<MemberInfo> GetRuntimeMembers()
        {
            var clrType = entityTypeBuilder.Metadata.ClrType;

            foreach (var property in clrType.GetRuntimeProperties()
                         .Where(p => p.GetMethod != null && !p.GetMethod.IsStatic))
            {
                yield return property;
            }

            foreach (var property in clrType.GetRuntimeFields())
            {
                yield return property;
            }
        }
    }
}
```
Під час заміни вбудованої конвенції, нова реалізація конвенції повинна успадковуватися від існуючого класу конвенції. Зверніть увагу, що деякі конвенції мають реляційні або специфічні для постачальника реалізації, і в цьому випадку нова реалізація конвенції повинна успадковуватися від найспецифічнішого існуючого класу конвенції для використовуваного постачальника бази даних.

Потім конвенцію реєструють за допомогою методу Replace у ConfigureConventions:

```cs
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.Conventions.Replace<PropertyDiscoveryConvention>(
        serviceProvider => new AttributeBasedPropertyDiscoveryConvention(
            serviceProvider.GetRequiredService<ProviderConventionSetBuilderDependencies>()));
}
```
Це випадок, коли існуюча конвенція має залежності, представлені об’єктом залежностей ProviderConventionSetBuilderDependencies. Вони отримуються від внутрішнього постачальника послуг за допомогою GetRequiredService і передаються конструктору домовленостей.

Зверніть увагу, що ця домовленість дозволяє відображати поля (на додаток до властивостей), якщо вони позначені як [Persist]. Це означає, що ми можемо використовувати приватні поля як приховані ключі в моделі.

Наприклад, розглянемо такі типи сутностей:

```cs
public class LaundryBasket
{
    [Persist]
    [Key]
    private readonly int _id;

    [Persist]
    public int TenantId { get; init; }

    public bool IsClean { get; set; }

    public List<Garment> Garments { get; } = new();
}

public class Garment
{
    public Garment(string name, string color)
    {
        Name = name;
        Color = color;
    }

    [Persist]
    [Key]
    private readonly int _id;

    [Persist]
    public int TenantId { get; init; }

    [Persist]
    public string Name { get; }

    [Persist]
    public string Color { get; }

    public bool IsClean { get; set; }

    public LaundryBasket? Basket { get; set; }
}
```
Модель, побудована з цих типів сутностей, має вигляд:

```
Model:
  EntityType: Garment
    Properties:
      _id (_id, int) Required PK AfterSave:Throw ValueGenerated.OnAdd
      Basket_id (no field, int?) Shadow FK Index
      Color (string) Required
      Name (string) Required
      TenantId (int) Required
    Navigations:
      Basket (LaundryBasket) ToPrincipal LaundryBasket Inverse: Garments
    Keys:
      _id PK
    Foreign keys:
      Garment {'Basket_id'} -> LaundryBasket {'_id'} ToDependent: Garments ToPrincipal: Basket ClientSetNull
    Indexes:
      Basket_id
  EntityType: LaundryBasket
    Properties:
      _id (_id, int) Required PK AfterSave:Throw ValueGenerated.OnAdd
      TenantId (int) Required
    Navigations:
      Garments (List<Garment>) Collection ToDependent Garment Inverse: Basket
    Keys:
      _id PK
```
Зазвичай IsClean було б зіставлено, але оскільки воно не позначено [Persist], тепер воно розглядається як не зіставлена ​​властивість.

## Коли використовувати кожен підхід для масового налаштування

Використовуйте API метаданих, коли:

* Конфігурацію потрібно застосувати у певний час і не реагувати на пізніші зміни в моделі.
* Швидкість побудови моделі дуже важлива. API метаданих має менше перевірок безпеки і тому може бути трохи швидшим, ніж інші підходи, проте використання скомпільованої моделі забезпечить ще кращий час запуску.

Використовуйте конфігурацію моделі перед конвенцією, коли:

* Умова застосовності проста, оскільки залежить лише від типу.
* Конфігурацію потрібно застосовувати в будь-який момент, коли властивість заданого типу додається до моделі, і вона замінює анотації даних та домовленості.

Використовуйте конвенції завершення, коли:

* Умова застосовності є складною.
* Конфігурація не повинна замінювати те, що визначено в анотаціях даних.

Використовуйте інтерактивні конвенції, коли:

* Кілька конвенцій залежать одна від одної. Фіналізуючі конвенції виконуються в порядку їх додавання і тому не можуть реагувати на зміни, внесені пізнішими фіналізуючими конвенціями.
* Логіка є спільною для кількох контекстів. Інтерактивні конвенції безпечніші за інші підходи.