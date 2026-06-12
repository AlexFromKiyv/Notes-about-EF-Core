# Зовнішні та головні ключі у зв'язках

Усі зв'язки «один до одного» та «один до багатьох» визначаються зовнішнім ключем на залежному кінці, який посилається на первинний або альтернативний ключ на головному кінці. Для зручності цей первинний або альтернативний ключ називається «principal key» для зв'язку. Зв'язки «багато до багатьох» складаються з двох зв'язків «один до багатьох», кожен з яких сам по собі визначається зовнішнім ключем, що посилається на головний ключ.

## Зовнішні ключі

Властивість або властивості, що складають зовнішній ключ, часто виявляються за домовленістю. Властивості також можна налаштувати явно за допомогою атрибутів зіставлення або за допомогою HasForeignKey в API побудови моделі. HasForeignKey можна використовувати з лямбда-виразом. Наприклад, для зовнішнього ключа, що складається з однієї властивості:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(b => b.Posts)
        .WithOne(p => p.Blog)
        .HasForeignKey(p => p.ContainingBlogId);
}
```
Або, для складеного зовнішнього ключа, що складається з кількох властивостей:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .HasForeignKey(e => new { e.ContainingBlogId1, e.ContainingBlogId2 });
}
```
Використання лямбда-виразів в API побудови моделей гарантує, що властивість доступна для аналізу коду та рефакторингу, а також надає тип властивості API для використання в подальших ланцюжкових методах.

HasForeignKey також може передаватися як назва властивості зовнішнього ключа у вигляді рядка.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .HasForeignKey("ContainingBlogId");
}
```
Використання рядка корисне, коли:

* Властивость або властивості є приватними.
* Властивість або властивості не існують для типу сутності та мають бути створені як тіньові властивості.
* Назва властивості обчислюється або створюється на основі певних вхідних даних, що надходять до процесу побудови моделі.

## Стовпці зовнішнього ключа, що не допускають значення null

Здатність властивості зовнішнього ключа до значення null визначає, чи є зв'язок необов'язковим чи обов'язковим. Однак властивість зовнішнього ключа, що може мати значення null, може бути використана для обов'язкового зв'язку за допомогою атрибута [Required] або викликом IsRequired в API побудови моделі.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .HasForeignKey(e => e.BlogId)
        .IsRequired();
}
```
Або, якщо зовнішній ключ виявлено за домовленістю, тоді IsRequired можна використовувати без виклику HasForeignKey:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .IsRequired();
}
```
Кінцевим результатом цього є те, що стовпець зовнішнього ключа в базі даних стає NOT NULL, навіть якщо властивість зовнішнього ключа є nullable. Те саме можна досягти, явно налаштувавши властивість зовнішнього ключа відповідно до вимог.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .Property(e => e.BlogId)
        .IsRequired();
}
```

## Тіньові зовнішні ключі

Властивості зовнішнього ключа можна створювати як тіньові властивості. Тіньова властивість існує в моделі EF, але не існує в типі .NET. EF відстежує значення та стан властивості внутрішньо. Тіньові зовнішні ключі зазвичай використовуються, коли є бажання приховати реляційну концепцію зовнішнього ключа від моделі домену, що використовується кодом програми/бізнес-логікою. Цей код програми потім маніпулює зв'язком повністю через навігацію.

Якщо сутності будуть серіалізовані, наприклад, для надсилання по проводу, то значення зовнішнього ключа можуть бути корисним способом зберегти інформацію про зв'язки недоторканою, коли сутності не мають об'єктної/графової форми. Тому часто прагматично зберігати властивості зовнішнього ключа в типі .NET для цієї мети. Властивості зовнішнього ключа можуть бути приватними, що часто є хорошим компромісом, щоб уникнути розкриття зовнішнього ключа, дозволяючи його значенню передаватись разом із сутністю.

Властивості тіньового зовнішнього ключа часто створюються за домовленістю. Тіньовий зовнішній ключ також буде створено, якщо аргумент HasForeignKey не відповідає жодній властивості .NET.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .HasForeignKey("MyBlogId");
}
```
За домовленістю, тіньовий зовнішній ключ отримує свій тип від головного ключа у зв'язку. Цей тип робиться таким, що допускає значення null, якщо зв'язок не виявлено або не налаштовано належним чином. 

Властивість тіньового зовнішнього ключа також можна створити явно, що корисно для налаштування аспектів властивості. Наприклад, щоб зробити властивість непридатною для значення null:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .Property<string>("MyBlogId")
        .IsRequired();

    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .HasForeignKey("MyBlogId");
}
```
За домовленістю, властивості зовнішнього ключа успадковують такі аспекти, як максимальна довжина та підтримка Unicode, від головного ключа у зв'язку. Тому рідко виникає потреба явно налаштовувати аспекти для властивості зовнішнього ключа.

Створення тіньової властивості, якщо задане ім'я не відповідає жодній властивості типу сутності, можна вимкнути за допомогою ConfigureWarnings.

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.ConfigureWarnings(b => b.Throw(CoreEventId.ShadowPropertyCreated));
```

## Назви обмежень зовнішнього ключа

За домовленістю обмеження зовнішнього ключа називаються FK_\<dependent type name\>_\<principal type name\>_\<foreign key property name\>. Для складених зовнішніх ключів \<foreign key property name\> стає списком імен властивостей зовнішнього ключа, розділених символами підкреслення.

Це можна змінити в API побудови моделі за допомогою HasConstraintName.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .HasForeignKey(e => e.BlogId)
        .HasConstraintName("My_BlogId_Constraint");
}
```

## Виключення обмежень зовнішнього ключа з міграцій

Іноді корисно мати представлений зв'язок зовнішнього ключа в моделі EF, але без створення відповідного обмеження зовнішнього ключа в базі даних. Це може статися зі застарілими базами даних, де обмеження не існують, або у сценаріях синхронізації даних, де порядок вставки пов'язаних сутностей може тимчасово порушувати обмеження цілісності посилань. У цих випадках використовуйте ExcludeForeignKeyFromMigrations, щоб запобігти генерації обмеження зовнішнього ключа EF в міграціях (та EnsureCreated):

```cs
modelBuilder.Entity<Blog>()
    .HasMany(e => e.Posts)
    .WithOne(e => e.Blog)
    .HasForeignKey(e => e.BlogId)
    .ExcludeForeignKeyFromMigrations();
```
За такої конфігурації EF не створюватиме обмеження зовнішнього ключа в базі даних, але зв'язок все ще відстежуватиметься в моделі EF і може використовуватися як зазвичай для завантаження пов'язаних даних, відстеження змін тощо. EF все одно створюватиме індекс бази даних для стовпця зовнішнього ключа, оскільки індекси корисні для запитів незалежно від того, чи існує обмеження.

Щоб застосувати це до всіх зовнішніх ключів у моделі (наприклад, щоб глобально вимкнути всі обмеження зовнішніх ключів), ви можете перебрати всі зовнішні ключі в OnModelCreating:

```cs
foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
{
    foreignKey.SetIsExcludedFromMigrations(true);
}
```

## Індекси для зовнішніх ключів

За домовленістю, EF створює індекс бази даних для властивості або властивостей зовнішнього ключа.

## Ключи Principal (основні)

За домовленістю, зовнішні ключі обмежені первинним ключем на головному кінці зв'язку. Однак, замість цього можна використовувати альтернативний ключ. Це досягається за допомогою HasPrincipalKey в API побудови моделі.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .HasPrincipalKey(e => e.AlternateId);
}
```
Або для складеного зовнішнього ключа з кількома властивостями:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .HasPrincipalKey(e => new { e.AlternateId1, e.AlternateId2 });
}
```

HasPrincipalKey також можна передати назву властивості альтернативного ключа у вигляді рядка.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Posts)
        .WithOne(e => e.Blog)
        .HasPrincipalKey("AlternateId");
}
```
Немає потреби викликати HasAlternateKey для визначення альтернативного ключа для головної сутності; це робиться автоматично, коли HasPrincipalKey використовується з властивостями, які не є властивостями первинного ключа. Однак, HasAlternateKey можна використовувати для подальшого налаштування альтернативного ключа, наприклад, для встановлення назви його обмеження бази даних.

## Зв'язки з безключовими сутностями

Кожен зв'язок повинен мати зовнішній ключ, який посилається на головний (первинний або альтернативний) ключ. Це означає, що безключовий тип сутності не може виступати в якості головного ключа зв'язку, оскільки немає головного ключа, на який могли б посилатися зовнішні ключі. 

Однак, безключові типи сутностей все ще можуть мати визначені зовнішні ключі, і тому можуть виступати в ролі залежного кінця зв'язку. Наприклад, розглянемо ці типи, де Tag не має ключа:

```cs
public class Tag
{
    public string Text { get; set; } = null!;
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
}

public class Post
{
    public int Id { get; set; }
}
```
Тег можна налаштувати на залежному кінці зв'язку:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Tag>()
        .HasNoKey();

    modelBuilder.Entity<Post>()
        .HasMany<Tag>()
        .WithOne(e => e.Post);
}
```

## Зовнішні ключі у зв'язках «many-to-many»

У зв'язках «many-to-many» зовнішні ключі визначаються для типу сутності об'єднання та зіставляються з обмеженнями зовнішніх ключів у таблиці об'єднань. Все описане вище також можна застосувати до цих зовнішніх ключів об'єднаних сутностей. Наприклад, встановлення імен обмежень бази даних:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity(
            l => l.HasOne(typeof(Tag)).WithMany().HasConstraintName("TagForeignKey_Constraint"),
            r => r.HasOne(typeof(Post)).WithMany().HasConstraintName("PostForeignKey_Constraint"));
}
```
