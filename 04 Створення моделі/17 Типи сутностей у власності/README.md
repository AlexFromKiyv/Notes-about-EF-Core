# Типи сутностей що у власності

EF Core дозволяє моделювати типи сутностей, які можуть відображатися лише у властивостях навігації інших типів сутностей. Вони називаються типами сутностей, що у власності. Сутність, що містить тип сутності, що належать, є його власником. Cутності у власності по суті є частиною власника та не можуть існувати без нього, вони концептуально подібні до агрегатів. Це означає, що об'єкт власності за визначенням знаходиться на залежній стороні відносин з власником. 

## Налаштування типів як у власності

У більшості постачальників типи сутностей ніколи не налаштовуються як власні за домовленістю – ви повинні явно використовувати метод OwnsOne в OnModelCreating або анотувати тип за допомогою OwnedAttribute, щоб налаштувати тип як власність. Постачальник Azure Cosmos DB є винятком. Оскільки Azure Cosmos DB є базою даних документів, постачальник налаштовує всі пов’язані типи сутностей як у власності за замовчуванням.

У цьому прикладі StreetAddress – це тип без властивості ідентичності. Він використовується як властивість типу Order для визначення адреси доставки певного замовлення.

```cs
[Owned]
public class StreetAddress
{
    public string Street { get; set; }
    public string City { get; set; }
}
```
```cs
public class Order
{
    public int Id { get; set; }
    public StreetAddress ShippingAddress { get; set; }
}
```
Також можна використовувати метод OwnsOne в OnModelCreating, щоб вказати, що властивість ShippingAddress є власністю типу сутності Order, та налаштувати додаткові аспекти за потреби.

```cs
modelBuilder.Entity<Order>().OwnsOne(p => p.ShippingAddress);
```
Якщо властивість ShippingAddress є приватною у типі Order, можна використовувати рядкову версію методу OwnsOne:

```cs
modelBuilder.Entity<Order>().OwnsOne(typeof(StreetAddress), "ShippingAddress");
```
Наведена вище модель відображається на наступну схему бази даних:

```sql
CREATE TABLE [dbo].[Orders] (
    [Id]                     INT            IDENTITY (1, 1) NOT NULL,
    [ShippingAddress_Street] NVARCHAR (MAX) NOT NULL,
    [ShippingAddress_City]   NVARCHAR (MAX) NOT NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED ([Id] ASC)
);
```

## Неявні ключі

Типи що у власності, налаштовані за допомогою OwnsOne або виявлені через навігацію за посиланнями, завжди мають зв'язок "один до одного" з власником, тому їм не потрібні власні значення ключів, оскільки значення зовнішніх ключів унікальні. У попередньому прикладі тип StreetAddress не потребує визначення властивості ключа.

```cs
    Console.WriteLine(context.Model.ToDebugString());
```
```
Model:
  EntityType: Order
    Properties:
      Id (int) Required PK AfterSave:Throw ValueGenerated.OnAdd
    Navigations:
      ShippingAddress (StreetAddress) ToDependent StreetAddress
    Keys:
      Id PK
  EntityType: StreetAddress Owned
    Properties:
      OrderId (no field, int) Shadow Required PK FK AfterSave:Throw
      City (string) Required
      Street (string) Required
    Keys:
      OrderId PK
    Foreign keys:
      StreetAddress {'OrderId'} -> Order {'Id'} Unique Required RequiredDependent Ownership Cascade ToDependent: ShippingAddress
```
Щоб зрозуміти, як EF Core відстежує ці об'єкти, корисно знати, що первинний ключ створюється як тіньова властивість для Owned типу. Значення ключа екземпляра Owned типу буде таким самим, як значення ключа екземпляра власника.

## Колекції owned типів

Щоб налаштувати колекцію owned типів, використовуйте OwnsMany в OnModelCreating.

Owned типи потребують первинного ключа. Якщо для типу .NET немає відповідних властивостей-кандидатів, EF Core може спробувати створити одну. Однак, коли власні типи визначаються через колекцію, недостатньо просто створити властивість shadow, яка діятиме як зовнішній ключ для власника та первинний ключ власного екземпляра, як ми робимо для OwnsOne: для кожного власника може бути кілька екземплярів owned типів, і тому ключа власника недостатньо, щоб забезпечити унікальну ідентифікацію для кожного owned екземпляра.

Два найпростіші рішення цієї проблеми:

* Визначення сурогатного первинного ключа для нової властивості, незалежної від зовнішнього ключа, який вказує на власника. Значення, що містяться в ньому, повинні бути унікальними для всіх власників (наприклад, якщо Батьківський об'єкт {1} має Дочірній об'єкт {1}, то Батьківський об'єкт {2} не може мати Дочірній об'єкт {1}), тому значення не має жодного внутрішнього значення. Оскільки зовнішній ключ не є частиною первинного ключа, його значення можна змінювати, тому ви можете переміщувати дочірній об'єкт від одного батьківського об'єкта до іншого, проте це зазвичай суперечить агрегованій семантиці.
* Використання зовнішнього ключа та додаткової властивості як складеного ключа. Значення додаткової властивості тепер має бути унікальним лише для заданого батьківського об'єкта (тому, якщо Батьківський об'єкт {1} має Дочірній об'єкт {1,1}, то Батьківський об'єкт {2} все ще може мати Дочірній об'єкт {2,1}). Зробивши зовнішній ключ частиною первинного ключа, зв'язок між власником та власною сутністю стає незмінним та краще відображає агрегатну семантику. Це те, що EF Core робить за замовчуванням.

У цьому прикладі ми використовуватимемо клас Distributor.

```cs
public class Distributor
{
    public int Id { get; set; }
    public ICollection<StreetAddress> ShippingCenters { get; set; }
}
```
За замовчуванням первинний ключ, що використовується для owned типу, на який посилається властивість навігації ShippingCenters, буде ("DistributorId", "Id"), де "DistributorId" – це FK, а "Id" – унікальне цілочисельне значення.

```sql
CREATE TABLE [dbo].[Distributors] (
    [Id] INT IDENTITY (1, 1) NOT NULL,
    CONSTRAINT [PK_Distributors] PRIMARY KEY CLUSTERED ([Id] ASC)
);
CREATE TABLE [dbo].[StreetAddress] (
    [DistributorId] INT            NOT NULL,
    [Id]            INT            IDENTITY (1, 1) NOT NULL,
    [Street]        NVARCHAR (MAX) NOT NULL,
    [City]          NVARCHAR (MAX) NOT NULL,
    CONSTRAINT [PK_StreetAddress] PRIMARY KEY CLUSTERED ([DistributorId] ASC, [Id] ASC),
    CONSTRAINT [FK_StreetAddress_Distributors_DistributorId] FOREIGN KEY ([DistributorId]) REFERENCES [dbo].[Distributors] ([Id]) ON DELETE CASCADE
);
```

Щоб налаштувати іншу назву первинного ключа, викличте HasKey.

```cs
modelBuilder.Entity<Distributor>().OwnsMany(
    p => p.ShippingCenters, a =>
    {
        a.WithOwner().HasForeignKey("OwnerId");
        a.Property<int>("Id");
        a.HasKey("Id");
    });
```

## Зіставлення owned типів з розділенням таблиці

Під час використання реляційних баз даних, за замовчуванням посилальні owned типи зіставляються з тією ж таблицею, що й власник. Це вимагає розділення таблиці на дві частини: деякі стовпці використовуватимуться для зберігання даних власника, а деякі стовпці – для зберігання даних owned об’єкта. Це поширена функція, відома як розділення таблиці.

За замовчуванням EF Core іменуватиме стовпці бази даних для властивостей типу власної сутності за шаблоном Navigation_OwnedEntityProperty. Таким чином, властивості StreetAddress відображатимуться в таблиці «Order» з іменами «ShippingAddress_Street» та «ShippingAddress_City».

Ви можете скористатися методом HasColumnName для перейменування цих стовпців.

```cs
modelBuilder.Entity<Order>().OwnsOne(
    o => o.ShippingAddress,
    sa =>
    {
        sa.Property(p => p.Street).HasColumnName("ShipsToStreet");
        sa.Property(p => p.City).HasColumnName("ShipsToCity");
    });
```
Більшість звичайних методів конфігурації типів сутностей, таких як Ignore, можна викликати таким самим чином.

## Спільне використання одного й того ж типу .NET серед кількох owned типів

Тип owned сутності може бути того ж типу .NET, що й інший тип owned сутності, тому типу .NET може бути недостатньо для ідентифікації owned типу.

У таких випадках властивість, що вказує від власника до власної сутності, стає визначальною навігацією типу власної сутності. З точки зору EF Core, визначення навігації є частиною ідентичності типу поряд із типом .NET.
Наприклад, у наступному класі ShippingAddress та BillingAddress належать до одного й того ж типу .NET, StreetAddress.

```cs
public class OrderDetails
{
    public DetailedOrder Order { get; set; }
    public StreetAddress BillingAddress { get; set; }
    public StreetAddress ShippingAddress { get; set; }
}
```
Щоб зрозуміти, як EF Core розрізнятиме відстежувані екземпляри цих об'єктів, може бути корисним подумати, що визначальна навігація стала частиною ключа екземпляра поряд зі значенням ключа власника та типом .NET типу, що належить.

## Вкладені owned типи

У цьому прикладі OrderDetails володіє типами BillingAddress та ShippingAddress, які обидва є типами StreetAddress.

```cs
public class DetailedOrder
{
    public int Id { get; set; }
    public OrderDetails OrderDetails { get; set; }
    public OrderStatus Status { get; set; }
}
```
Кожна навігація до власного типу визначає окремий тип сутності з повністю незалежною конфігурацією. Окрім вкладених owned типів, власний тип може посилатися на звичайну сутність, яка може бути як власником, так і іншою сутністю, за умови, що власна сутність знаходиться на залежній стороні.

### Налаштування owned типів

Можна об'єднати метод OwnsOne у вільному виклику для налаштування цієї моделі:

```cs
modelBuilder.Entity<DetailedOrder>().OwnsOne(
    p => p.OrderDetails, od =>
    {
        od.WithOwner(d => d.Order);
        od.Navigation(d => d.Order).UsePropertyAccessMode(PropertyAccessMode.Property);
        od.OwnsOne(c => c.BillingAddress);
        od.OwnsOne(c => c.ShippingAddress);
    });
```

Зверніть увагу на виклик WithOwner, який використовується для визначення властивості навігації, що вказує на власника. Щоб визначити навігацію до типу сутності власника, який не є частиною відносини власності, WithOwner() слід викликати без будь-яких аргументів.

Також можна досягти цього результату, використовуючи OwnedAttribute як для OrderDetails, так і для StreetAddress. 

Крім того, зверніть увагу на виклик Navigation. Властивості навігації для власних типів можна додатково налаштувати так само, як і для властивостей навігації, що не належать.

### Зберігання owned типів в окремих таблицях

Також owned типи можна зберігати в окремій таблиці, відокремленій від власника. Щоб змінити домовленість, яка зіставляє owned тип з тією ж таблицею, що й власник, можна просто викликати ToTable та вказати інше ім'я таблиці. У наступному прикладі OrderDetails та його дві адреси зіставляться з окремою таблицею з DetailedOrder:

```cs
modelBuilder.Entity<DetailedOrder>().OwnsOne(p => p.OrderDetails, od => { od.ToTable("OrderDetails"); });
```
Також можна використовувати TableAttribute для досягнення цієї мети, але зауважте, що це не вдасться, якщо є кілька переходів до власного типу, оскільки в такому випадку кілька типів сутностей будуть зіставлені з однією таблицею.

## Запит до owned типів

Під час запиту до власника owned типи будуть включені за замовчуванням. Немає потреби використовувати метод Include, навіть якщо власні типи зберігаються в окремій таблиці. На основі описаної раніше моделі, наступний запит отримає з бази даних значення Order, OrderDetails та дві owned адреси StreetAddress:

```cs
var order = await context.DetailedOrders.FirstAsync(o => o.Status == OrderStatus.Pending);
Console.WriteLine($"First pending order will ship to: {order.OrderDetails.ShippingAddress.City}");
```

## Обмеження

Деякі з цих обмежень є фундаментальними для роботи owned типів сутностей, але деякі інші ми можемо усунути в майбутніх випусках: 

* Обмеження за проектуванням
    * Не можна створити DbSet\<T\> для owned типу.
    * Ви не можете викликати Entity\<T\>() з owned типом у ModelBuilder.
    * Екземпляри owned типів сутностей не можуть спільно використовуватися кількома власниками (це добре відомий сценарій для об'єктів значень, які неможливо реалізувати за допомогою власних типів сутностей).

* Поточні недоліки
    * Типи сутностей, що належать власникам, не можуть мати ієрархій успадкування




