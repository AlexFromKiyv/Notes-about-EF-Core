# Зв'язки «many-to-many»

Зв'язки «many-to-many» використовуються, коли будь-яка кількість сутностей одного типу пов'язана з будь-якою кількістю сутностей того ж або іншого типу. Наприклад, публікація може мати багато пов'язаних тегів, і кожен тег, у свою чергу, може бути пов'язаний з будь-якою кількістю публікацій.


## Розуміння зв'язків «many-to-many»

Зв'язки «many-to-many» відрізняються від зв'язків «one-to-many» та «one-to-one» тим, що їх не можна представити простим способом, використовуючи лише зовнішній ключ. Натомість, для «з’єднання» двох сторін зв’язку потрібен додатковий тип сутності. Це відомо як «тип сутності об'єднання» та відповідає «таблиці об'єднання» в реляційній базі даних. Сутності цього типу сутності об'єднання містять пари значень зовнішнього ключа, де одне з кожної пари вказує на сутність на одному боці зв'язку, а інше — на сутність на іншому боці зв'язку. Кожна сутність об'єднання, а отже, і кожен рядок у таблиці об'єднань, таким чином, представляє один зв'язок між типами сутностей у зв'язку.

EF Core може приховувати тип сутності об'єднання та керувати ним за лаштунками. Це дозволяє використовувати навігацію зв'язку «many-to-many» природним чином, додаючи або видаляючи сутності з кожного боку за потреби. Однак корисно розуміти, що відбувається «за лаштунками», щоб їхня загальна поведінка, і зокрема зіставлення з реляційною базою даних, мала сенс. Однак корисно розуміти, що відбувається «за лаштунками», щоб їхня загальна поведінка, і зокрема зіставлення з реляційною базою даних, мала сенс. Почнемо з налаштування схеми реляційної бази даних для представлення зв'язку «many-to-many» між публікаціями та тегами:

```sql
CREATE TABLE "Posts" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Posts" PRIMARY KEY AUTOINCREMENT);

CREATE TABLE "Tags" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Tags" PRIMARY KEY AUTOINCREMENT);

CREATE TABLE "PostTag" (
    "PostsId" INTEGER NOT NULL,
    "TagsId" INTEGER NOT NULL,
    CONSTRAINT "PK_PostTag" PRIMARY KEY ("PostsId", "TagsId"),
    CONSTRAINT "FK_PostTag_Posts_PostsId" FOREIGN KEY ("PostsId") REFERENCES "Posts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_PostTag_Tags_TagsId" FOREIGN KEY ("TagsId") REFERENCES "Tags" ("Id") ON DELETE CASCADE);
```
У цій схемі PostTag є таблицею об'єднання. Вона містить два стовпці: PostsId, який є зовнішнім ключем до первинного ключа таблиці Posts, та TagsId, який є зовнішнім ключем до первинного ключа таблиці Tags. Таким чином, кожен рядок у цій таблиці представляє зв'язок між однією публікацією та одним тегом.

Спрощене зіставлення для цієї схеми в EF Core складається з трьох типів сутностей – по одному для кожної таблиці. Якщо кожен із цих типів сутностей представлений класом .NET, то ці класи можуть виглядати так:

```cs
public class Post
{
    public int Id { get; set; }
    public List<PostTag> PostTags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public List<PostTag> PostTags { get; } = [];
}

[]
public class PostTag
{
    public int PostsId { get; set; }
    public int TagsId { get; set; }
    public Post Post { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
```
Зверніть увагу, що в цьому зіставленні немає зв'язку "many-to-many", а є два зв'язки "one-to-many", по одному для кожного із зовнішніх ключів, визначених у таблиці об'єднання. Це не є нерозумним способом зіставлення цих таблиць, але він не відображає мети таблиці об'єднання, яка полягає у представленні одного зв'язку "many-to-many", а не двох зв'язків "one-to-many".

EF дозволяє створити більш природне зіставлення завдяки введенню двох навігацій колекцій: однієї на публікації, що містить пов'язані з нею теги, та інверсної на тезі, що містить пов'язані з нею публікації.

Ці нові навігації відомі як «пропуски навігації», оскільки вони пропускають об’єднання, забезпечуючи прямий доступ до іншої сторони зв’язку «many-to-many».

```cs
public class Post
{
    public int Id { get; set; }
    public List<PostTag> PostTags { get; } = [];
    public List<Tag> Tags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public List<PostTag> PostTags { get; } = [];
    public List<Post> Posts { get; } = [];
}

public class PostTag
{
    public int PostsId { get; set; }
    public int TagsId { get; set; }
    public Post Post { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
```

Як показано у наведеному вище прикладі, зв'язок «many-to-many» можна відобразити таким чином, тобто з класом .NET для сутності об'єднання, а також з навігацією для двох зв'язків «one-to-many» та пропущеною навігацією, що доступна для типів сутностей. Однак, EF може прозоро керувати об'єктом об'єднання, без визначеного для нього класу .NET та без навігації для двох зв'язків "one-to-many".

```cs
public class Post
{
    public int Id { get; set; }
    public List<Tag> Tags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public List<Post> Posts { get; } = [];
}
```

Дійсно, конвенції побудови моделей EF за замовчуванням зіставляють типи Post та Tag, показані тут, з трьома таблицями у схемі бази даних у верхній частині цього розділу. Це зіставлення, без явного використання типу з'єднання, зазвичай мається на увазі під терміном "many-to-many".

# Приклади

У наступних розділах наведено приклади зв'язків «many-to-many», зокрема конфігурацію, необхідну для досягнення кожного зіставлення.

## Базовий зв'язок «many-to-many»

У найпростішому випадку зв'язку «many-to-many» типи сутностей на кожному кінці зв'язку мають навігацію колекції.

```cs
public class Post
{
    public int Id { get; set; }
    public List<Tag> Tags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public List<Post> Posts { get; } = [];
}
```
Цей зв'язок відображається за домовленістю. 
Це можна визначити явно:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts);
}
```
Навіть з такою явною конфігурацією, багато аспектів зв'язку все ще налаштовуються за домовленістю. Більш повна явна конфігурація, знову ж таки, для навчальних цілей, така:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity(
            "PostTag",
            r => r.HasOne(typeof(Tag)).WithMany().HasForeignKey("TagsId").HasPrincipalKey(nameof(Tag.Id)),
            l => l.HasOne(typeof(Post)).WithMany().HasForeignKey("PostsId").HasPrincipalKey(nameof(Post.Id)),
            j => j.HasKey("PostsId", "TagsId"));
}
```
Будь ласка, не намагайтеся повністю налаштувати все, навіть якщо це не потрібно. Як видно вище, код швидко ускладнюється, і легко помилитися. І навіть у наведеному вище прикладі в моделі є багато речей, які все ще налаштовані за домовленістю. Нереалістично вважати, що все в EF-моделі завжди можна повністю налаштувати явно.

Незалежно від того, чи зв'язок побудовано за домовленістю, чи з використанням будь-якої з показаних явних конфігурацій, результуюча схема зіставлення (за допомогою SQLite) має такий вигляд:

```sql
CREATE TABLE "Posts" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Posts" PRIMARY KEY AUTOINCREMENT);

CREATE TABLE "Tags" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Tags" PRIMARY KEY AUTOINCREMENT);

CREATE TABLE "PostTag" (
    "PostsId" INTEGER NOT NULL,
    "TagsId" INTEGER NOT NULL,
    CONSTRAINT "PK_PostTag" PRIMARY KEY ("PostsId", "TagsId"),
    CONSTRAINT "FK_PostTag_Posts_PostsId" FOREIGN KEY ("PostsId") REFERENCES "Posts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_PostTag_Tags_TagsId" FOREIGN KEY ("TagsId") REFERENCES "Tags" ("Id") ON DELETE CASCADE);
```

## «many-to-many» з іменованою таблицею об'єднання

У попередньому прикладі таблиця об'єднання мала назву PostTag за домовленістю. Їй можна надати явне ім'я за допомогою UsingEntity.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity("PostsToTagsJoinTable");
}
```
Все інше щодо зіставлення залишається незмінним, змінюється лише назва таблиці об'єднання:

```sql
CREATE TABLE [dbo].[PostsToTagsJoinTable] (
    [PostsId] INT NOT NULL,
    [TagsId]  INT NOT NULL,
    CONSTRAINT [PK_PostsToTagsJoinTable] PRIMARY KEY CLUSTERED ([PostsId] ASC, [TagsId] ASC),
    CONSTRAINT [FK_PostsToTagsJoinTable_Posts_PostsId] FOREIGN KEY ([PostsId]) REFERENCES [dbo].[Posts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PostsToTagsJoinTable_Tags_TagsId] FOREIGN KEY ([TagsId]) REFERENCES [dbo].[Tags] ([Id]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [IX_PostsToTagsJoinTable_TagsId]
    ON [dbo].[PostsToTagsJoinTable]([TagsId] ASC);
```

## «many-to-many» з іменами зовнішніх ключів таблиці об'єднання

Продовжуючи попередній приклад, назви стовпців зовнішнього ключа в таблиці об'єднання також можна змінити. Існує два способи зробити це. Перший – явно вказати імена властивостей зовнішнього ключа для об’єкта з’єднання.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity(
            r => r.HasOne(typeof(Tag)).WithMany().HasForeignKey("TagForeignKey"),
            l => l.HasOne(typeof(Post)).WithMany().HasForeignKey("PostForeignKey"));
}
```
Другий спосіб — залишити властивості з їхніми загальноприйнятими назвами, але потім зіставити ці властивості з різними іменами стовпців.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity(
            j =>
            {
                j.Property("PostsId").HasColumnName("PostForeignKey");
                j.Property("TagsId").HasColumnName("TagForeignKey");
            });
}
```
У будь-якому випадку зіставлення залишається незмінним, змінюються лише назви стовпців зовнішнього ключа:

```sql
CREATE TABLE "PostTag" (
    "PostForeignKey" INTEGER NOT NULL,
    "TagForeignKey" INTEGER NOT NULL,
    CONSTRAINT "PK_PostTag" PRIMARY KEY ("PostForeignKey", "TagForeignKey"),
    CONSTRAINT "FK_PostTag_Posts_PostForeignKey" FOREIGN KEY ("PostForeignKey") REFERENCES "Posts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_PostTag_Tags_TagForeignKey" FOREIGN KEY ("TagForeignKey") REFERENCES "Tags" ("Id") ON DELETE CASCADE);
```

## «many-to-many» з класом для сутності об'єднання

Досі в прикладах таблиця об'єднання автоматично зіставлялася з типом сутності спільного типу. Це усуває необхідність створення окремого класу для типу сутності. Однак, мати такий клас може бути корисним, щоб на нього можна було легко посилатися, особливо коли до класу додаються навігації або корисне навантаження («Корисне навантаження» – це будь-які додаткові дані в таблиці з’єднань. Наприклад, позначка часу, коли створюється запис у таблиці з’єднань.), як показано в наступних прикладах нижче. Для цього спочатку створіть тип PostTag для сутності об'єднання на додаток до існуючих типів для Post та Tag:

```cs
public class Post
{
    public int Id { get; set; }
    public List<Tag> Tags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public List<Post> Posts { get; } = [];
}

public class PostTag
{
    public int PostId { get; set; }
    public int TagId { get; set; }
}
```
Клас може мати будь-яку назву, але поширеною є комбінування назв типів на будь-якому кінці зв'язку. Тепер метод UsingEntity можна використовувати для налаштування цього як типу сутності об'єднання для зв'язку.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity<PostTag>();
}
```
PostId та TagId автоматично вибираються як зовнішні ключі та налаштовуються як складений первинний ключ для типу сутності об'єднання. Властивості, які використовуються для зовнішніх ключів, можна явно налаштувати для випадків, коли вони не відповідають конвенції EF.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity<PostTag>(
            r => r.HasOne<Tag>().WithMany().HasForeignKey(e => e.TagId),
            l => l.HasOne<Post>().WithMany().HasForeignKey(e => e.PostId));
}
```
Схема зіставленої бази даних для таблиці об'єднання в цьому прикладі структурно еквівалентна попереднім прикладам, але з деякими іншими іменами стовпців:

```cs
CREATE TABLE "PostTag" (
    "PostId" INTEGER NOT NULL,
    "TagId" INTEGER NOT NULL,
    CONSTRAINT "PK_PostTag" PRIMARY KEY ("PostId", "TagId"),
    CONSTRAINT "FK_PostTag_Posts_PostId" FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_PostTag_Tags_TagId" FOREIGN KEY ("TagId") REFERENCES "Tags" ("Id") ON DELETE CASCADE);
```

## «many-to-many» з навігацією до об'єкта об'єднання

Продовжуючи попередній приклад, тепер, коли є клас, що представляє об'єкт об'єднання, стає легко додати навігації, що посилаються на цей клас.

```cs
public class Post
{
    public int Id { get; set; }
    public List<Tag> Tags { get; } = [];
    public List<PostTag> PostTags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public List<Post> Posts { get; } = [];
    public List<PostTag> PostTags { get; } = [];
}

public class PostTag
{
    public int PostId { get; set; }
    public int TagId { get; set; }
}
```
Як показано в цьому прикладі, навігації до типу сутності об'єднання можна використовувати на додаток до пропускних навігацій між двома кінцями зв'язку «many-to-many». Це означає, що пропускні навігації можна використовувати для взаємодії зі зв'язком "many-to-many" природним чином, тоді як навігації до типу сутності об'єднання можна використовувати, коли потрібен більший контроль над самими сутностями об'єднання. У певному сенсі, це зіставлення забезпечує найкраще з обох світів: просте зіставлення "many-to-many" та зіставлення, яке більш явно відповідає схемі бази даних. 

У виклику UsingEntity нічого не потрібно змінювати, оскільки навігація до об'єкта об'єднання здійснюється за домовленістю. Тому конфігурація для цього прикладу така ж, як і для попереднього прикладу:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity<PostTag>();
}
```
Навігацію можна налаштувати явно для випадків, коли її неможливо визначити за домовленістю.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity<PostTag>(
            r => r.HasOne<Tag>().WithMany(e => e.PostTags),
            l => l.HasOne<Post>().WithMany(e => e.PostTags));
}
```
Схема зіставленої бази даних не залежить від включення навігації до моделі.

## «many-to-many» з навігацією до та від об'єднувальної сутності

У попередньому прикладі до типу об'єднувальної сутності було додано навігацію з типів сутностей на будь-якому кінці зв'язку багато-до-багатьох. Навігацію також можна додавати в іншому напрямку або в обох напрямках.

```cs
public class Post
{
    public int Id { get; set; }
    public List<Tag> Tags { get; } = [];
    public List<PostTag> PostTags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public List<Post> Posts { get; } = [];
    public List<PostTag> PostTags { get; } = [];
}

public class PostTag
{
    public int PostId { get; set; }
    public int TagId { get; set; }
    public Post Post { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
```

У виклику UsingEntity нічого не потрібно змінювати, оскільки навігація до об'єкта об'єднання здійснюється за домовленістю. Тому конфігурація для цього прикладу така ж, як і для попереднього прикладу:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity<PostTag>();
}
```
## «many-to-many» з навігацією та зміненими зовнішніми ключами

Цей приклад такий самий, за винятком того, що також змінено властивості зовнішнього ключа, що використовуються.

```cs
public class Post
{
    public int Id { get; set; }
    public List<Tag> Tags { get; } = [];
    public List<PostTag> PostTags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public List<Post> Posts { get; } = [];
    public List<PostTag> PostTags { get; } = [];
}

public class PostTag
{
    public int PostForeignKey { get; set; }
    public int TagForeignKey { get; set; }
    public Post Post { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
```
Знову ж таки, для налаштування цього використовується метод UsingEntity:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity<PostTag>(
            r => r.HasOne<Tag>(e => e.Tag).WithMany(e => e.PostTags).HasForeignKey(e => e.TagForeignKey),
            l => l.HasOne<Post>(e => e.Post).WithMany(e => e.PostTags).HasForeignKey(e => e.PostForeignKey));
}
```

## Однонаправлений зв'язок «many-to-many»

Необов'язково включати навігацію з обох боків зв'язку «багато до багатьох».

```cs
public class Post
{
    public int Id { get; set; }
    public List<Tag> Tags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
}
```
EF потребує певного налаштування, щоб знати, що це має бути зв'язок «many-to-many», а не «one-to-many». Це робиться за допомогою HasMany та WithMany, але без передачі аргументів на стороні без навігації.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany();
}
```

## Таблиця зв'язку "many-to-many" та об'єднання з корисним навантаженням

У наведених прикладах таблиця об'єднання використовувалася лише для зберігання пар зовнішніх ключів, що представляють кожен зв'язок. Однак, його також можна використовувати для зберігання інформації про асоціацію, наприклад, часу її створення. У таких випадках найкраще визначити тип для об'єкта об'єднання та додати властивості "корисного навантаження асоціації" до цього типу. Також поширеною є створення навігацій до об'єкта з'єднання на додаток до "пропускних навігацій", що використовуються для зв'язку "many-to-many". Ці додаткові навігації дозволяють легко посилатися на об'єкт з'єднання з коду, тим самим полегшуючи читання та/або зміну даних корисного навантаження.

```cs
public class Post
{
    public int Id { get; set; }
    public List<Tag> Tags { get; } = [];
    public List<PostTag> PostTags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public List<Post> Posts { get; } = [];
    public List<PostTag> PostTags { get; } = [];
}

public class PostTag
{
    public int PostId { get; set; }
    public int TagId { get; set; }
    public DateTime CreatedOn { get; set; }
}
```
Також поширеним є використання згенерованих значень для властивостей корисного навантаження, наприклад, позначки часу бази даних, яка автоматично встановлюється під час вставки рядка асоціації. Це вимагає мінімального налаштування.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity<PostTag>(
            j => j.Property(e => e.CreatedOn).HasDefaultValueSql("GETUTCDATE()"));
}
```
Результат відображається у схемі типу сутності з автоматично встановленим часом під час вставки рядка:

```sql
CREATE TABLE "PostTag" (
    "PostId" INTEGER NOT NULL,
    "TagId" INTEGER NOT NULL,
    "CreatedOn" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_PostTag" PRIMARY KEY ("PostId", "TagId"),
    CONSTRAINT "FK_PostTag_Posts_PostId" FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_PostTag_Tags_TagId" FOREIGN KEY ("TagId") REFERENCES "Tags" ("Id") ON DELETE CASCADE);
```

## Налаштований тип сутності спільного типу як сутність об'єднання

У попередньому прикладі як тип сутності об'єднання використовувався тип PostTag. Цей тип є специфічним для зв'язку posts-tags. Однак, якщо у вас є кілька таблиць об'єднання однакової форми, то для всіх них можна використовувати один і той самий тип CLR. Наприклад, уявіть, що всі наші таблиці об'єднань мають стовпець CreatedOn. Ми можемо зіставити їх за допомогою класу JoinType, зіставленого як тип сутності спільного типу:

```cs
public class JoinType
{
    public int Id1 { get; set; }
    public int Id2 { get; set; }
    public DateTime CreatedOn { get; set; }
}
```
На цей тип потім можна посилатися як на тип сутності об'єднання кількома різними зв'язками "many-to-many".

```cs
public class Post
{
    public int Id { get; set; }
    public List<Tag> Tags { get; } = [];
    public List<JoinType> PostTags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public List<Post> Posts { get; } = [];
    public List<JoinType> PostTags { get; } = [];
}

public class Blog
{
    public int Id { get; set; }
    public List<Author> Authors { get; } = [];
    public List<JoinType> BlogAuthors { get; } = [];
}

public class Author
{
    public int Id { get; set; }
    public List<Blog> Blogs { get; } = [];
    public List<JoinType> BlogAuthors { get; } = [];
}
```
А ці зв'язки потім можна налаштувати відповідним чином, щоб зіставити тип з'єднання з різною таблицею для кожного зв'язку:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity<JoinType>(
            "PostTag",
            r => r.HasOne<Tag>().WithMany(e => e.PostTags).HasForeignKey(e => e.Id1),
            l => l.HasOne<Post>().WithMany(e => e.PostTags).HasForeignKey(e => e.Id2),
            j => j.Property(e => e.CreatedOn).HasDefaultValueSql("CURRENT_TIMESTAMP"));

    modelBuilder.Entity<Blog>()
        .HasMany(e => e.Authors)
        .WithMany(e => e.Blogs)
        .UsingEntity<JoinType>(
            "BlogAuthor",
            r => r.HasOne<Author>().WithMany(e => e.BlogAuthors).HasForeignKey(e => e.Id1),
            l => l.HasOne<Blog>().WithMany(e => e.BlogAuthors).HasForeignKey(e => e.Id2),
            j => j.Property(e => e.CreatedOn).HasDefaultValueSql("CURRENT_TIMESTAMP"));
}
```
В результаті у схемі бази даних з'являться такі таблиці:

```sql
CREATE TABLE "BlogAuthor" (
    "Id1" INTEGER NOT NULL,
    "Id2" INTEGER NOT NULL,
    "CreatedOn" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_BlogAuthor" PRIMARY KEY ("Id1", "Id2"),
    CONSTRAINT "FK_BlogAuthor_Authors_Id1" FOREIGN KEY ("Id1") REFERENCES "Authors" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_BlogAuthor_Blogs_Id2" FOREIGN KEY ("Id2") REFERENCES "Blogs" ("Id") ON DELETE CASCADE);


CREATE TABLE "PostTag" (
    "Id1" INTEGER NOT NULL,
    "Id2" INTEGER NOT NULL,
    "CreatedOn" TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_PostTag" PRIMARY KEY ("Id1", "Id2"),
    CONSTRAINT "FK_PostTag_Posts_Id2" FOREIGN KEY ("Id2") REFERENCES "Posts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_PostTag_Tags_Id1" FOREIGN KEY ("Id1") REFERENCES "Tags" ("Id") ON DELETE CASCADE);
```

## «many-to-many» з альтернативними ключами

Досі всі приклади показували, що зовнішні ключі в типі сутності об'єднання обмежені первинними ключами типів сутностей з обох боків зв'язку. Кожен зовнішній ключ, або обидва, можуть бути обмежені альтернативним ключем. Наприклад, розглянемо таку модель, де Tag та Post мають властивості альтернативного ключа:

```cs
public class Post
{
    public int Id { get; set; }
    public int AlternateKey { get; set; }
    public List<Tag> Tags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public int AlternateKey { get; set; }
    public List<Post> Posts { get; } = [];
}
```
Конфігурація для цієї моделі така:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity(
            r => r.HasOne(typeof(Tag)).WithMany().HasPrincipalKey(nameof(Tag.AlternateKey)),
            l => l.HasOne(typeof(Post)).WithMany().HasPrincipalKey(nameof(Post.AlternateKey)));
}
```
І результуюча схема бази даних, для ясності, включаючи також таблиці з альтернативними ключами:

```sql
CREATE TABLE "Posts" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Posts" PRIMARY KEY AUTOINCREMENT,
    "AlternateKey" INTEGER NOT NULL,
    CONSTRAINT "AK_Posts_AlternateKey" UNIQUE ("AlternateKey"));

CREATE TABLE "Tags" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Tags" PRIMARY KEY AUTOINCREMENT,
    "AlternateKey" INTEGER NOT NULL,
    CONSTRAINT "AK_Tags_AlternateKey" UNIQUE ("AlternateKey"));

CREATE TABLE "PostTag" (
    "PostsAlternateKey" INTEGER NOT NULL,
    "TagsAlternateKey" INTEGER NOT NULL,
    CONSTRAINT "PK_PostTag" PRIMARY KEY ("PostsAlternateKey", "TagsAlternateKey"),
    CONSTRAINT "FK_PostTag_Posts_PostsAlternateKey" FOREIGN KEY ("PostsAlternateKey") REFERENCES "Posts" ("AlternateKey") ON DELETE CASCADE,
    CONSTRAINT "FK_PostTag_Tags_TagsAlternateKey" FOREIGN KEY ("TagsAlternateKey") REFERENCES "Tags" ("AlternateKey") ON DELETE CASCADE);
```
Конфігурація для використання альтернативних ключів дещо відрізняється, якщо тип сутності об'єднання представлений типом .NET.

```cs
public class Post
{
    public int Id { get; set; }
    public int AlternateKey { get; set; }
    public List<Tag> Tags { get; } = [];
    public List<PostTag> PostTags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public int AlternateKey { get; set; }
    public List<Post> Posts { get; } = [];
    public List<PostTag> PostTags { get; } = [];
}

public class PostTag
{
    public int PostId { get; set; }
    public int TagId { get; set; }
    public Post Post { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
```
Тепер у конфігурації можна використовувати універсальний метод UsingEntity<>:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity<PostTag>(
            r => r.HasOne<Tag>(e => e.Tag).WithMany(e => e.PostTags).HasPrincipalKey(e => e.AlternateKey),
            l => l.HasOne<Post>(e => e.Post).WithMany(e => e.PostTags).HasPrincipalKey(e => e.AlternateKey));
}
```

##  «many-to-many» та таблиця об'єднання з окремим первинним ключем

Досі тип сутності об'єднання у всіх прикладах має первинний ключ, що складається з двох властивостей зовнішнього ключа. Це пояснюється тим, що кожна комбінація значень цих властивостей може зустрічатися не більше одного разу. Таким чином, ці властивості утворюють природний первинний ключ.

EF Core не підтримує дублікати сутностей у жодній навігації колекцій.

Якщо ви керуєте схемою бази даних, то немає причин для того, щоб таблиця об'єднання мала додатковий стовпець первинного ключа. Однак можливо, що існуюча таблиця об'єднання може мати визначений стовпець первинного ключа. EF все ще може зіставити його з певною конфігурацією. 

Можливо, найпростіше це зробити, створивши клас для представлення сутності об'єднання.

```cs
public class Post
{
    public int Id { get; set; }
    public List<Tag> Tags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public List<Post> Posts { get; } = [];
}

public class PostTag
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public int TagId { get; set; }
}
```
Ця властивість PostTag.Id тепер за домовленістю вибирається як первинний ключ, тому єдина необхідна конфігурація — це виклик UsingEntity для типу PostTag:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity<PostTag>();
}
```
А результуюча схема для таблиці об'єднань така:

```sql
CREATE TABLE [dbo].[PostTags] (
    [Id]     INT IDENTITY (1, 1) NOT NULL,
    [PostId] INT NOT NULL,
    [TagId]  INT NOT NULL,
    CONSTRAINT [PK_PostTags] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PostTags_Posts_PostId] FOREIGN KEY ([PostId]) REFERENCES [dbo].[Posts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PostTags_Tags_TagId] FOREIGN KEY ([TagId]) REFERENCES [dbo].[Tags] ([Id]) ON DELETE CASCADE
);
```
Первинний ключ також можна додати до об'єкта об'єднання без визначення для нього класу.

```cs
public class Post
{
    public int Id { get; set; }
    public List<Tag> Tags { get; } = [];
}

public class Tag
{
    public int Id { get; set; }
    public List<Post> Posts { get; } = [];
}
```
Ключ можна додати за допомогою цієї конфігурації:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity(
            j =>
            {
                j.IndexerProperty<int>("Id");
                j.HasKey("Id");
            });
}
```
Що призводить до створення таблиці об'єднань з окремим стовпцем первинного ключа:

```sql
CREATE TABLE [dbo].[PostTag] (
    [Id]      INT IDENTITY (1, 1) NOT NULL,
    [PostsId] INT NOT NULL,
    [TagsId]  INT NOT NULL,
    CONSTRAINT [PK_PostTag] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PostTag_Posts_PostsId] FOREIGN KEY ([PostsId]) REFERENCES [dbo].[Posts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PostTag_Tags_TagsId] FOREIGN KEY ([TagsId]) REFERENCES [dbo].[Tags] ([Id]) ON DELETE CASCADE
);
```

## «many-to-many» без каскадного видалення

У всіх наведених вище прикладах зовнішні ключі, створені між таблицею об'єднання та двома сторонами зв'язку «багато-до-багатьох», створюються з каскадним видаленням. Це дуже корисно, оскільки означає, що якщо сутність з будь-якого боку зв'язку видаляється, то рядки в таблиці об'єднань для цієї сутності автоматично видаляються. Або, іншими словами, коли сутність більше не існує, то її зв'язки з іншими сутностями також більше не існують.

Важко уявити, коли корисно змінити таку поведінку, але це можна зробити за бажанням.

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .HasMany(e => e.Tags)
        .WithMany(e => e.Posts)
        .UsingEntity(
            r => r.HasOne(typeof(Tag)).WithMany().OnDelete(DeleteBehavior.Restrict),
            l => l.HasOne(typeof(Post)).WithMany().OnDelete(DeleteBehavior.Restrict));
}
```

## Самопосилання у зв'язку «many-to-many»

Один і той самий тип сутності може використовуватися на обох кінцях зв'язку «many-to-many»; це називається зв'язком «самопосилання».

```cs
public class Person
{
    public int Id { get; set; }
    public List<Person> Parents { get; } = [];
    public List<Person> Children { get; } = [];
}
```
Це відображається в таблицю об'єднань під назвою PersonPerson, де обидва зовнішні ключі вказують на таблицю People :

```sql
CREATE TABLE "PersonPerson" (
    "ChildrenId" INTEGER NOT NULL,
    "ParentsId" INTEGER NOT NULL,
    CONSTRAINT "PK_PersonPerson" PRIMARY KEY ("ChildrenId", "ParentsId"),
    CONSTRAINT "FK_PersonPerson_People_ChildrenId" FOREIGN KEY ("ChildrenId") REFERENCES "People" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_PersonPerson_People_ParentsId" FOREIGN KEY ("ParentsId") REFERENCES "People" ("Id") ON DELETE CASCADE);
```

## Симетричне самопосилання «many-to-many»

Іноді зв'язок «many-to-many» є природним чином симетричним. Тобто, якщо сутність A пов'язана з сутністю B, то сутність B також пов'язана з сутністю A. Це природним чином моделюється за допомогою однієї навігації. Наприклад, уявіть випадок, коли особа A дружить з особою B, а особа B дружить з особою A:

```cs
public class Person
{
    public int Id { get; set; }
    public List<Person> Friends { get; } = [];
}
```
На жаль, це непросто споставити. Одна навігація не може використовуватися для обох сторін зв’язку. Найкраще, що можна зробити, це відобразити це як односпрямований зв’язок «many-to-many».

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Person>()
        .HasMany(e => e.Friends)
        .WithMany();
}
```
Однак, щоб переконатися, що двоє людей є родичами, кожну людину потрібно буде вручну додати до колекції друзів іншої людини.

```cs
ginny.Friends.Add(hermione);
hermione.Friends.Add(ginny);
```

## Пряме використання таблиці об'єднання

У всіх наведених вище прикладах використовуються шаблони зіставлення «many-to-many» EF Core. Однак, також можливо зіставити таблицю об'єднання зі звичайним типом сутності та просто використовувати два зв'язки "one-to-many" для всіх операцій.

Наприклад, ці типи сутностей представляють зіставлення двох звичайних таблиць та об'єднаних таблиць без використання будь-яких зв'язків "many-to-many":

```cs
public class Post
{
    public int Id { get; set; }
    public List<PostTag> PostTags { get; } = new();
}

public class Tag
{
    public int Id { get; set; }
    public List<PostTag> PostTags { get; } = new();
}

public class PostTag
{
    public int PostId { get; set; }
    public int TagId { get; set; }
    public Post Post { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
```
Це не потребує спеціального зіставлення, оскільки це звичайні типи сутностей зі звичайними зв'язками «one-to-many».