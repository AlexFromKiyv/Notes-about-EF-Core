# null у запитах


## Підготовка прикладу




## Семантика null у запитах

Бази даних SQL працюють на основі 3-значної логіки (true, false, null) під час порівнянь, на відміну від булевої логіки C#. Під час перетворення запитів LINQ на SQL, EF Core намагається компенсувати різницю, вводячи додаткові перевірки на значення null для деяких елементів запиту. Щоб проілюструвати це, визначимо таку сутність:

```cs
public class NullSemanticsEntity
{
    public int Id { get; set; }
    public int Int { get; set; }
    public int? NullableInt { get; set; }
    public string String1 { get; set; }
    public string String2 { get; set; }
}
```

```cs
public class NullSemanticsContext : DbContext
{
    public DbSet<NullSemanticsEntity> Entities { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var relationalNulls = false;
        if (relationalNulls)
        {
            new SqlServerDbContextOptionsBuilder(optionsBuilder).UseRelationalNulls();
        }

        optionsBuilder.UseSqlServer(
            @"Server=(localdb)\mssqllocaldb;Database=NullSemanticsSample;Trusted_Connection=True;MultipleActiveResultSets=true;ConnectRetryCount=0");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NullSemanticsEntity>().HasData(
            new NullSemanticsEntity
            {
                Id = 1,
                Int = 1,
                NullableInt = 1,
                String1 = "A",
                String2 = "A"
            },
            new NullSemanticsEntity
            {
                Id = 2,
                Int = 2,
                NullableInt = 2,
                String1 = "A",
                String2 = "B"
            },
            new NullSemanticsEntity
            {
                Id = 3,
                Int = 2,
                NullableInt = null,
                String1 = null,
                String2 = "A"
            },
            new NullSemanticsEntity
            {
                Id = 4,
                Int = 2,
                NullableInt = null,
                String1 = "B",
                String2 = null
            },
            new NullSemanticsEntity
            {
                Id = 5,
                Int = 1,
                NullableInt = 3,
                String1 = null,
                String2 = null
            });

    }
}
```

```cs

    await CreateDB();

static async Task CreateDB()
{
    using var context = new NullSemanticsContext();
    await CleanDatabase(context);
    Console.WriteLine(context.Model.ToDebugString());
}
```

Виконаємо запити. Перші два запити виконують прості порівняння. У першому запиті обидва стовпці не мають значення null, тому перевірки на null не потрібні. У другому запиті NullableInt може містити значення null, але Id не може містити значення null; порівняння значення null з не-null повертає результат null, який буде відфільтровано операцією WHERE. Тому додаткові умови також не потрібні.


```cs
static async Task SomeQueryies()
{
    using var context = new NullSemanticsContext();

    var query1 = context.Entities.Where(e => e.Id == e.Int);
    var query2 = context.Entities.Where(e => e.Id == e.NullableInt);
    Console.WriteLine(query1.ToQueryString());
    Console.WriteLine();
    Console.WriteLine(query2.ToQueryString());
    Console.WriteLine();
}
```
```sql
SELECT [e].[Id], [e].[Int], [e].[NullableInt], [e].[String1], [e].[String2]
FROM [Entities] AS [e]
WHERE [e].[Id] = [e].[Int]

SELECT [e].[Id], [e].[Int], [e].[NullableInt], [e].[String1], [e].[String2]
FROM [Entities] AS [e]
WHERE [e].[Id] = [e].[NullableInt]
```

Третій запит вводить перевірку на null. Коли NullableInt має значення null, порівняння Id <> NullableInt повертає значення null, яке буде відфільтровано операцією WHERE. Однак, з точки зору булевої логіки, цей випадок має бути повернутий як частина результату. Отже, EF Core додає необхідну перевірку, щоб гарантувати це.

```cs
static async Task SomeQueryies()
{
    using var context = new NullSemanticsContext();

    var query3 = context.Entities.Where(e => e.Id != e.NullableInt);
    Console.WriteLine(query3.ToQueryString());
    Console.WriteLine();

}
```
```sql
SELECT [e].[Id], [e].[Int], [e].[NullableInt], [e].[String1], [e].[String2]
FROM [Entities] AS [e]
WHERE [e].[Id] <> [e].[NullableInt] OR [e].[NullableInt] IS NULL
```

Запити чотири та п'ять показують закономірність, коли обидва стовпці можуть мати значення NULL. Варто зазначити, що операція <> створює складніший (і потенційно повільніший) запит, ніж операція ==.

```cs
static async Task SomeQueryies()
{
    using var context = new NullSemanticsContext();

    var query4 = context.Entities.Where(e => e.String1 == e.String2);
    var query5 = context.Entities.Where(e => e.String1 != e.String2);
    Console.WriteLine(query4.ToQueryString());
    Console.WriteLine();
    Console.WriteLine(query5.ToQueryString());
}
```
```sql
SELECT [e].[Id], [e].[Int], [e].[NullableInt], [e].[String1], [e].[String2]
FROM [Entities] AS [e]
WHERE [e].[String1] = [e].[String2] OR ([e].[String1] IS NULL AND [e].[String2] IS NULL)

SELECT [e].[Id], [e].[Int], [e].[NullableInt], [e].[String1], [e].[String2]
FROM [Entities] AS [e]
WHERE ([e].[String1] <> [e].[String2] OR [e].[String1] IS NULL OR [e].[String2] IS NULL) AND ([e].[String1] IS NOT NULL OR [e].[String2] IS NOT NULL)
```


## Обробка значень, що допускають значення null, у функціях

Багато функцій у SQL можуть повертати null-результат, лише якщо деякі з їхніх аргументів є null-значеннями. EF Core використовує це для створення ефективніших запитів. Запит нижче ілюструє оптимізацію:

```cs
    var query = context.Entities
        .Where(e => e.String1.Substring(0, e.String2.Length) == null);
```
Згенерований SQL-запит виглядає наступним чином (нам не потрібно обчислювати функцію SUBSTRING, оскільки вона буде null лише тоді, коли будь-який з її аргументів має значення null):
```sql
SELECT [e].[Id], [e].[Int], [e].[NullableInt], [e].[String1], [e].[String2]
FROM [Entities] AS [e]
WHERE [e].[String1] IS NULL OR [e].[String2] IS NULL
```
Оптимізацію також можна використовувати для функцій, визначених користувачем. Докладніше див. на сторінці зіставлення функцій, визначених користувачем.

## Написання продуктивних запитів

* Порівняння стовпців, що не допускають значення null, простіше та швидше, ніж порівняння стовпців, що допускають значення null. По можливості позначайте стовпці як такі, що не допускають значення null.
* Перевірка на рівність (==) простіше та швидше, ніж перевірка на нерівність (!=), оскільки запиту не потрібно розрізняти null та false результат. Використовуйте порівняння на рівність, коли це можливо. Однак, просте заперечення порівняння == фактично те саме, що й !=, тому це не призводить до покращення продуктивності. 
* У деяких випадках можна спростити складне порівняння, явно відфільтровуючи нульові значення зі стовпця, наприклад, коли нульові значення відсутні або ці значення не є релевантними в результаті. Розглянемо наступний приклад:

```cs
    var query1 = context.Entities.Where(e => e.String1 != e.String2 || e.String1.Length == e.String2.Length);
    var query2 = context.Entities.Where(
        e => e.String1 != null && e.String2 != null && (e.String1 != e.String2 || e.String1.Length == e.String2.Length));
    Console.WriteLine(query1.ToQueryString());
    Console.WriteLine();
    Console.WriteLine(query2.ToQueryString());
    Console.WriteLine();
```
Ці запити створюють наступний SQL-код:
```sql
SELECT [e].[Id], [e].[Int], [e].[NullableInt], [e].[String1], [e].[String2]
FROM [Entities] AS [e]
WHERE (([e].[String1] <> [e].[String2] OR [e].[String1] IS NULL OR [e].[String2] IS NULL) AND ([e].[String1] IS NOT NULL OR [e].[String2] IS NOT NULL)) OR CAST(LEN([e].[String1]) AS int) = CAST(LEN([e].[String2]) AS int) OR ([e].[String1] IS NULL AND [e].[String2] IS NULL)

SELECT [e].[Id], [e].[Int], [e].[NullableInt], [e].[String1], [e].[String2]
FROM [Entities] AS [e]
WHERE [e].[String1] IS NOT NULL AND [e].[String2] IS NOT NULL AND ([e].[String1] <> [e].[String2] OR CAST(LEN([e].[String1]) AS int) = CAST(LEN([e].[String2]) AS int))
```
У другому запиті нульові результати явно відфільтровуються зі стовпця String1. EF Core може безпечно обробляти стовпець String1 як такий, що не може мати значення NULL, під час порівняння, що призводить до спрощення запиту.

## Використання реляційної нульової семантики

Можна вимкнути компенсацію порівняння нульових значень та використовувати реляційну нульову семантику безпосередньо. Це можна зробити, викликавши метод UseRelationalNulls(true) у конструкторі опцій усередині методу OnConfiguring:

```cs
new SqlServerDbContextOptionsBuilder(optionsBuilder).UseRelationalNulls();
```
Попередження

Під час використання реляційної null-семантики ваші LINQ-запити більше не матимуть того ж значення, що й у C#, і можуть давати результати, відмінні від очікуваних. Будьте обережні, використовуючи цей режим.