# Розширене зіставлення таблиць

EF Core пропонує значну гнучкість, коли йдеться про зіставлення типів сутностей з таблицями в базі даних. Це стає ще кориснішим, коли вам потрібно використовувати базу даних, яка не була створена за допомогою EF. Наведені нижче методи описані у вигляді таблиць, але такого ж результату можна досягти і при зіставленні з представленнями.

## Розділення таблиці

EF Core дозволяє зіставити дві або більше сутностей в один рядок. Це називається розділенням таблиці або спільним використанням таблиці.

### Конфігурація

Щоб використовувати розділення таблиці, типи сутностей повинні бути зіставлені з однією таблицею, первинні ключі повинні бути зіставлені з одними й тими ж стовпцями, а також налаштовано принаймні один зв'язок між первинним ключем одного типу сутності та іншим в одній таблиці.

Поширений сценарій розділення таблиці — використання лише підмножини стовпців таблиці для підвищення продуктивності або інкапсуляції.

У цьому прикладі Order представляє підмножину DetailedOrder.

```cs
public class Order
{
    public int Id { get; set; }
    public OrderStatus? Status { get; set; }
    public DetailedOrder DetailedOrder { get; set; }
}
```
```cs
public class DetailedOrder
{
    public int Id { get; set; }
    public OrderStatus? Status { get; set; }
    public string BillingAddress { get; set; }
    public string ShippingAddress { get; set; }
    public byte[] Version { get; set; }
}
```
Окрім необхідної конфігурації, ми викликаємо Property(o => o.Status).HasColumnName("Status"), щоб зіставити DetailedOrder.Status з тим самим стовпцем, що й Order.Status.

```cs
modelBuilder.Entity<DetailedOrder>(
    dob =>
    {
        dob.ToTable("Orders");
        dob.Property(o => o.Status).HasColumnName("Status");
    });

modelBuilder.Entity<Order>(
    ob =>
    {
        ob.ToTable("Orders");
        ob.Property(o => o.Status).HasColumnName("Status");
        ob.HasOne(o => o.DetailedOrder).WithOne()
            .HasForeignKey<DetailedOrder>(o => o.Id);
        ob.Navigation(o => o.DetailedOrder).IsRequired();
    });
```

### Використання

Збереження та запит сутностей за допомогою розбиття таблиці виконується так само, як і інші сутності: 

```cs
using (var context = new TableSplittingContext())
{
    await context.Database.EnsureDeletedAsync();
    await context.Database.EnsureCreatedAsync();

    context.Add(
        new Order
        {
            Status = OrderStatus.Pending,
            DetailedOrder = new DetailedOrder
            {
                Status = OrderStatus.Pending,
                ShippingAddress = "221 B Baker St, London",
                BillingAddress = "11 Wall Street, New York"
            }
        });

    await context.SaveChangesAsync();
}

using (var context = new TableSplittingContext())
{
    var pendingCount = await context.Orders.CountAsync(o => o.Status == OrderStatus.Pending);
    Console.WriteLine($"Current number of pending orders: {pendingCount}");
}

using (var context = new TableSplittingContext())
{
    var order = await context.DetailedOrders.FirstAsync(o => o.Status == OrderStatus.Pending);
    Console.WriteLine($"First pending order will ship to: {order.ShippingAddress}");
}
```
### Необов'язкова залежна сутність

Якщо всі стовпці, що використовуються залежною сутністю, мають значення NULL у базі даних, то під час запиту для неї не буде створено жодного екземпляра. Це дозволяє моделювати необов'язкову залежну сутність, де властивість зв'язку принципала буде null. Зверніть увагу, що це також станеться, якщо всі властивості залежного елемента є необов'язковими та мають значення null, що може бути неочікуваним.

Однак, додаткова перевірка може вплинути на продуктивність запиту. Крім того, якщо залежний тип сутності має власні залежні елементи, то визначення того, чи слід створювати екземпляр, стає нетривіальним. Щоб уникнути цих проблем, залежний тип сутності можна позначити як обов'язковий.

### Токени паралельності

Якщо будь-який із типів сутностей, що спільно використовують таблицю, має токен паралельності, то він також має бути включений до всіх інших типів сутностей. Це необхідно для того, щоб уникнути застарілого значення токена паралельності, коли оновлюється лише одна з сутностей, зіставлених з однією таблицею. Щоб уникнути розкриття токена паралельності коду-споживачу, можливо створити його як тіньову властивість:

```cs
modelBuilder.Entity<Order>()
    .Property<byte[]>("Version").IsRowVersion().HasColumnName("Version");

modelBuilder.Entity<DetailedOrder>()
    .Property(o => o.Version).IsRowVersion().HasColumnName("Version");
```

### Успадкування

Перш ніж продовжувати читати цей розділ, рекомендується ознайомитися зі спеціальною сторінкою про успадкування. 

Залежні типи, що використовують розділення таблиць, можуть мати ієрархію успадкування, але є деякі обмеження:

* Залежний тип сутності не може використовувати зіставлення TPC, оскільки похідні типи не зможуть зіставлятися з тією ж таблицею.
* Залежний тип сутності може використовувати зіставлення TPT, але лише кореневий тип сутності може використовувати розділення таблиць.
* Якщо головний тип сутності використовує TPC, то розділення таблиць можуть використовувати лише ті типи сутностей, які не мають нащадків. В іншому випадку залежні стовпці потрібно було б дублювати в таблицях, що відповідають похідним типам, що ускладнювало б усі взаємодії.

## Розділення сутностей

EF Core дозволяє зіставляти сутність з рядками у двох або більше таблицях. Це називається розділенням сутностей.

### Конфігурація

Наприклад, розглянемо базу даних із трьома таблицями, що містять дані про клієнтів:

* Таблиця Customers для інформації про клієнтів
* Таблиця PhoneNumbers для номера телефону клієнта
* Таблиця Addresses для адреси клієнта

Ось визначення для цих таблиць у SQL Server:

```sql
CREATE TABLE [Customers] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Customers] PRIMARY KEY ([Id])
);

CREATE TABLE [PhoneNumbers] (
    [CustomerId] int NOT NULL,
    [PhoneNumber] nvarchar(max) NULL,
    CONSTRAINT [PK_PhoneNumbers] PRIMARY KEY ([CustomerId]),
    CONSTRAINT [FK_PhoneNumbers_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Addresses] (
    [CustomerId] int NOT NULL,
    [Street] nvarchar(max) NOT NULL,
    [City] nvarchar(max) NOT NULL,
    [PostCode] nvarchar(max) NULL,
    [Country] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Addresses] PRIMARY KEY ([CustomerId]),
    CONSTRAINT [FK_Addresses_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE CASCADE
);
```
Кожна з цих таблиць зазвичай відображається на власний тип сутності зі зв'язками між типами. Однак, якщо всі три таблиці завжди використовуються разом, тоді може бути зручніше зіставити їх усі з одним типом сутності.

```cs
public class Customer
{
    public Customer(string name, string street, string city, string? postCode, string country)
    {
        Name = name;
        Street = street;
        City = city;
        PostCode = postCode;
        Country = country;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public string? PhoneNumber { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string? PostCode { get; set; }
    public string Country { get; set; }
}
```
Це досягається шляхом виклику SplitToTable для кожного розділення типу сутності. Наприклад, наступний код розділяє тип сутності Customer на таблиці Customers, PhoneNumbers та Addresses, показані вище:

```cs
modelBuilder.Entity<Customer>(
    entityBuilder =>
    {
        entityBuilder
            .ToTable("Customers")
            .SplitToTable(
                "PhoneNumbers",
                tableBuilder =>
                {
                    tableBuilder.Property(customer => customer.Id).HasColumnName("CustomerId");
                    tableBuilder.Property(customer => customer.PhoneNumber);
                })
            .SplitToTable(
                "Addresses",
                tableBuilder =>
                {
                    tableBuilder.Property(customer => customer.Id).HasColumnName("CustomerId");
                    tableBuilder.Property(customer => customer.Street);
                    tableBuilder.Property(customer => customer.City);
                    tableBuilder.Property(customer => customer.PostCode);
                    tableBuilder.Property(customer => customer.Country);
                });
    });

```
Зверніть також увагу, що за потреби для кожної таблиці можна вказати різні назви стовпців. Щоб налаштувати назву стовпця для головної таблиці, див. далі.

### Налаштування зовнішнього ключа зв'язування

FK, що зв'язує зіставлені таблиці, орієнтований на ті ж властивості, для яких він оголошений. Зазвичай його не створюють у базі даних, оскільки це буде надлишково. Але є виняток, коли тип сутності зіставлено з кількома таблицями. Щоб змінити його аспекти, можна скористатися Fluent API конфігурації зв'язків:

```cs
modelBuilder.Entity<Customer>()
    .HasOne<Customer>()
    .WithOne()
    .HasForeignKey<Customer>(a => a.Id)
    .OnDelete(DeleteBehavior.Restrict);
```

## Обмеження

* Розділення сутностей не можна використовувати для типів сутностей в ієрархіях.
* Для будь-якого рядка в головній таблиці має бути рядок у кожній з розділених таблиць (фрагменти не є необов'язковими).

## Конфігурація аспектів для окремих таблиць

Деякі шаблони зіставлення призводять до того, що одна й та сама властивість CLR зіставляється зі стовпцем у кожній з кількох різних таблиць. EF дозволяє цим стовпцям мати різні назви. Наприклад, розглянемо просту ієрархію успадкування:

```cs
public abstract class Animal
{
    public int Id { get; set; }
    public string Breed { get; set; } = null!;
}

public class Cat : Animal
{
    public string? EducationalLevel { get; set; }
}

public class Dog : Animal
{
    public string? FavoriteToy { get; set; }
}
```
За допомогою стратегії зіставлення успадкування TPT ці типи будуть зіставлені з трьома таблицями. Однак стовпець первинного ключа в кожній таблиці може мати різну назву.

```sql
CREATE TABLE [Animals] (
    [Id] int NOT NULL IDENTITY,
    [Breed] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Animals] PRIMARY KEY ([Id])
);

CREATE TABLE [Cats] (
    [CatId] int NOT NULL,
    [EducationalLevel] nvarchar(max) NULL,
    CONSTRAINT [PK_Cats] PRIMARY KEY ([CatId]),
    CONSTRAINT [FK_Cats_Animals_CatId] FOREIGN KEY ([CatId]) REFERENCES [Animals] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Dogs] (
    [DogId] int NOT NULL,
    [FavoriteToy] nvarchar(max) NULL,
    CONSTRAINT [PK_Dogs] PRIMARY KEY ([DogId]),
    CONSTRAINT [FK_Dogs_Animals_DogId] FOREIGN KEY ([DogId]) REFERENCES [Animals] ([Id]) ON DELETE CASCADE
);
```
EF дозволяє налаштувати це зіставлення за допомогою конструктора вкладених таблиць:

```cs
modelBuilder.Entity<Animal>().ToTable("Animals");

modelBuilder.Entity<Cat>()
    .ToTable(
        "Cats",
        tableBuilder => tableBuilder.Property(cat => cat.Id).HasColumnName("CatId"));

modelBuilder.Entity<Dog>()
    .ToTable(
        "Dogs",
        tableBuilder => tableBuilder.Property(dog => dog.Id).HasColumnName("DogId"));
```
Завдяки зіставленню успадкування TPC властивість Breed також можна зіставити з різними іменами стовпців у різних таблицях.

```sql
CREATE TABLE [Cats] (
    [CatId] int NOT NULL DEFAULT (NEXT VALUE FOR [AnimalSequence]),
    [CatBreed] nvarchar(max) NOT NULL,
    [EducationalLevel] nvarchar(max) NULL,
    CONSTRAINT [PK_Cats] PRIMARY KEY ([CatId])
);

CREATE TABLE [Dogs] (
    [DogId] int NOT NULL DEFAULT (NEXT VALUE FOR [AnimalSequence]),
    [DogBreed] nvarchar(max) NOT NULL,
    [FavoriteToy] nvarchar(max) NULL,
    CONSTRAINT [PK_Dogs] PRIMARY KEY ([DogId])
);
```
EF підтримує це зіставлення таблиць:

```cs
modelBuilder.Entity<Animal>().UseTpcMappingStrategy();

modelBuilder.Entity<Cat>()
    .ToTable(
        "Cats",
        builder =>
        {
            builder.Property(cat => cat.Id).HasColumnName("CatId");
            builder.Property(cat => cat.Breed).HasColumnName("CatBreed");
        });

modelBuilder.Entity<Dog>()
    .ToTable(
        "Dogs",
        builder =>
        {
            builder.Property(dog => dog.Id).HasColumnName("DogId");
            builder.Property(dog => dog.Breed).HasColumnName("DogBreed");
        });
```
