# Теги запитів

## Підготовка прикладу

Встеновка пакету 

Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite

```cs
using NetTopologySuite.Geometries;

//...

public class Person
{
    public int Id { get; set; }
    public Point Location { get; set; }
}
```

```cs
using Draft.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Draft;

public class SpatialContext : DbContext
{
    public DbSet<Person> People { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(
            b =>
            {
                b.Property(e => e.Location).HasColumnType("geometry");

                b.HasData(
                    new Person { Id = 1, Location = new Point(0, 1) },
                    new Person { Id = 2, Location = new Point(2, 1) },
                    new Person { Id = 3, Location = new Point(4, 5) });
            });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(
            @"Server=(localdb)\mssqllocaldb;Database=MyDB;Trusted_Connection=True;ConnectRetryCount=0",
            b => b.UseNetTopologySuite());
    }
}
```
```cs
    await DoSpatialAsync();

static async Task DoSpatialAsync()
{
    using var context = new SpatialContext();
    await CleanDatabase(context);
    Console.WriteLine(context.Model.ToDebugString());
}
```

## Приклади

Теги запитів допомагають співвідносити LINQ-запити в коді зі згенерованими SQL-запитами, записаними в журналах. Ви анотуєте LINQ-запит за допомогою нового методу TagWith():

```cs
static async Task QieryWithTagWithAsync()
{
    using var context = new SpatialContext();

    var query = context.People.TagWith("All people");
    Console.WriteLine(query.ToQueryString());

    var people = await query.ToListAsync();
    foreach (var person in people)
    {
        Console.WriteLine($"Person ID: {person.Id}, Location: {person.Location}");
    }
}
```
```sql
-- All people

SELECT [p].[Id], [p].[Location]
FROM [People] AS [p]
Person ID: 1, Location: POINT (0 1)
Person ID: 2, Location: POINT (2 1)
Person ID: 3, Location: POINT (4 5)
```

Але як правило запити ускладнюються.

```cs
static async Task QieryWithTagWithAsync()
{
    using var context = new SpatialContext();

    var myLocation = new Point(1, 2);
    var query = (from f in context.People.TagWith("Querying nearest people")
                 orderby f.Location.Distance(myLocation) descending
                 select f).Take(5);

    Console.WriteLine(query.ToQueryString());

    var nearestPeople = await query.ToListAsync();

    foreach (var person in nearestPeople)
    {
        Console.WriteLine($"Person ID: {person.Id}, Location: {person.Location}");
    }
}
```
Цей запит LINQ перетворюється на наступний SQL-запит:

```sql
DECLARE @__p_1 int = 5;
DECLARE @__myLocation_0 geometry = 0x00000000010C000000000000F03F0000000000000040;

-- Querying nearest people

SELECT TOP(@__p_1) [p].[Id], [p].[Location]
FROM [People] AS [p]
ORDER BY [p].[Location].STDistance(@__myLocation_0) DESC
Person ID: 3, Location: POINT (4 5)
Person ID: 1, Location: POINT (0 1)
Person ID: 2, Location: POINT (2 1)
```
Можна викликати TagWith() багато разів для одного й того ж запиту. Теги запиту є кумулятивними. Наприклад, враховуючи такі методи:

```cs
static IQueryable<Person> GetNearestPeople(SpatialContext context, Point myLocation)
    => from f in context.People.TagWith("GetNearestPeople")
       orderby f.Location.Distance(myLocation) descending
       select f;

static IQueryable<T> Limit<T>(IQueryable<T> source, int limit) => source.TagWith("Limit").Take(limit);
```
Наступний запит:
```cs
    using var context = new SpatialContext();

    var myLocation = new Point(1, 2);

    var query = Limit(GetNearestPeople(context, myLocation), 25);

    Console.WriteLine(query.ToQueryString());

    var nearestPeople = await query.ToListAsync();
   

    foreach (var person in nearestPeople)
    {
        Console.WriteLine($"Person ID: {person.Id}, Location: {person.Location}");
    }

```
Перекладається як:

```sql
DECLARE @__p_1 int = 25;
DECLARE @__myLocation_0 geometry = 0x00000000010C000000000000F03F0000000000000040;

-- GetNearestPeople
-- Limit

SELECT TOP(@__p_1) [p].[Id], [p].[Location]
FROM [People] AS [p]
ORDER BY [p].[Location].STDistance(@__myLocation_0) DESC
Person ID: 3, Location: POINT (4 5)
Person ID: 1, Location: POINT (0 1)
Person ID: 2, Location: POINT (2 1)
```
Також можна використовувати багаторядкові рядки як теги запитів. 

```cs
    var query = Limit(GetNearestPeople(context, myLocation), 25)
        .TagWith(@"This is a multi-line 
string");
```

```
-- GetNearestPeople
-- Limit
-- This is a multi-line
-- string
```

## Позначення тегами з назвою файлу та номером рядка

Запити можна автоматично позначати тегами з назвою файлу та номером рядка, де запит LINQ визначено у вихідному коді. Це робиться за допомогою методу TagWithCallSite:

```cs
    var query = (from f in context.People.TagWithCallSite()
                 orderby f.Location.Distance(myLocation) descending
                 select f).Take(5);
```

Цей запит перетворюється на (фактичний номер рядка відображатиме, де запит визначено у вихідному файлі):

```sql
DECLARE @__p_1 int = 5;
DECLARE @__myLocation_0 geometry = 0x00000000010C000000000000F03F0000000000000040;

-- File: D:\Temp\Draft\Draft\Program.cs:50

SELECT TOP(@__p_1) [p].[Id], [p].[Location]
FROM [People] AS [p]
ORDER BY [p].[Location].STDistance(@__myLocation_0) DESC
Person ID: 3, Location: POINT (4 5)
Person ID: 1, Location: POINT (0 1)
Person ID: 2, Location: POINT (2 1)
```

## Відомі обмеження

Теги запитів не параметризуються: EF Core завжди обробляє теги запитів у запиті LINQ як рядкові літерали, що включені до згенерованого SQL. Скомпільовані запити, які приймають теги запитів як параметри, не дозволені.