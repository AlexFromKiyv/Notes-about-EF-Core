# Welcome

EF Core це технологія повязування сховища даних з додатком. Вона може служити об'єктно-реляційним співставленням (object-relational mapper (O/RM))

1. Дозволяє розробникам .NET працювати з базою даних за допомогою об'єктів .NET.
2. Усуває необхідність у більшості коду для доступу до даних, який зазвичай потрібно писати.

EF Core підтримує багато баз даних.

## Модель

Доступ до даних здійснюється за допомогою моделі. Модель складається з класів сутностей та об'єкта контексту, який представляє сеанс роботи з базою даних. Об'єкт контексту дозволяє запитувати та зберігати дані.

EF підтримує такі підходи до розробки моделей:

1. Генерація моделі з існуючої БД.
2. Створеня моделі вручну шоб відповідала БД.

Після створення моделі використовуйте EF Migrations для створення бази даних на основі цієї моделі. 

Створіть проек коснольної програми Welcome і додайте пакети

```
Microsoft.EntityFrameworkCore -v 9.0.16
Microsoft.EntityFrameworkCore.Design -v 9.0.16
Microsoft.EntityFrameworkCore.SqlServer -v 9.0.16
```

Welcome\Types.cs
```cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Welcome;


public class ProductContext : DbContext
{
    public ProductContext(DbContextOptions options) : base(options)
    {
    }
    public DbSet<Maker> Makers { get; set; }
    public DbSet<Product> Products { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(
            @"Server=(localdb)\mssqllocaldb;Database=Products;Trusted_Connection=True;ConnectRetryCount=0");
    }

}

public class ProductContextFactory : IDesignTimeDbContextFactory<ProductContext>
{
    public ProductContext CreateDbContext(string[]? args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProductContext>();
        string connectionString = @"Server=(localdb)\mssqllocaldb;Database=Products;Trusted_Connection=True;ConnectRetryCount=0";
        optionsBuilder.UseSqlServer(connectionString);
        return new ProductContext(optionsBuilder.Options);
    }
}

public class Maker
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<Product> Products { get; set; } = new List<Product>();
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int MakerId { get; set; }
    public Maker Maker { get; set; }
}
```


## Отримайте інструменти Entity Framework Core

Ви можете встановити інструменти для виконання завдань, пов’язаних з EF Core, у вашому проєкті, таких як створення та застосування міграцій баз даних або створення моделі EF Core на основі існуючої бази даних.

```
dotnet tool install --global dotnet-ef
```
Щоб оновити інструменти, скористайтеся командою 

```
dotnet tool update.
```

Наступні команди створюють міграції і БД для проекту Welcome.
```
dotnet ef migrations add Initial
dotnet ef database update
```

## Створення і збереження даних

Дані створюються, видаляються та змінюються в базі даних за допомогою екземплярів ваших класів сутностей.

```cs
using Welcome;

static void CreateDataInDatabase()
{
    using var context = new ProductContextFactory().CreateDbContext(null);
    var maker = new Maker { Name = "ЗАЗ" };
    var product = new Product { Name = "ЗАЗ - 966", Maker = maker };

    context.Makers.Add(maker);
    context.Products.Add(product);
    context.SaveChanges();
}
CreateDataInDatabase();
```


## Запити

Екземпляри класів сутностей отримуються з бази даних за допомогою Language Integrated Query (LINQ).

```cs
static void ReadDataFromDatabase()
{
    using var context = new ProductContextFactory().CreateDbContext(null);
    var products = context.
        Products
        .Include(p => p.Maker)
        .Where(p => p.Maker.Name == "ЗАЗ")
        .OrderBy(static p => p.Name)
        .ToList();
    foreach (var product in products)
    {
        Console.WriteLine($"Product: {product.Name}, Maker: {product.Maker.Name}");
    }
}
ReadDataFromDatabase();
```
```
Product: ЗАЗ - 966, Maker: ЗАЗ
```

## Міркування щодо EF O/RM

Хоча EF Core добре абстрагує багато деталей програмування, існують деякі найкращі практики, що застосовуються до будь-якого O/RM, які допомагають уникнути поширених помилок у виробничих додатках:

Знання базового сервера бази даних на середньому або вищому рівні є важливим для архітектури, налагодження, профілювання та міграції даних у високопродуктивних робочих додатках. Наприклад, знання первинних та зовнішніх ключів, обмежень, індексів, нормалізації, операторів DML та DDL, типів даних, профілювання тощо.

Функціональне та інтеграційне тестування: важливо максимально точно відтворити робоче середовище, щоб:
Виявляти проблеми в додатку, які проявляються лише під час використання певної версії або випуску сервера бази даних.
Виявляти критичні зміни під час оновлення EF Core та інших залежностей. Наприклад, додавання або оновлення фреймворків, таких як ASP.NET Core, OData або AutoMapper. Ці залежності можуть неочікувано впливати на EF Core.

Тестування продуктивності та стрес-тестування з репрезентативними навантаженнями. Наївне використання деяких функцій погано масштабується. Наприклад, кілька колекцій включають інтенсивне використання лінивого завантаження, умовні запити до неіндексованих стовпців, масові оновлення та вставки зі значеннями, згенерованими сховищем, відсутність обробки паралельності, великі моделі, неадекватна політика кешу.

Перевірка безпеки: наприклад, обробка рядків підключення та інших секретів, дозволи бази даних для операцій, що не пов’язані з розгортанням, перевірка вхідних даних для необробленого SQL, шифрування для конфіденційних даних.

Переконайтеся, що ведення журналу та діагностика достатні та зручні для використання. Наприклад, відповідна конфігурація ведення журналу, теги запитів та Application Insights.

Відновлення після помилок. Підготуйте резервні варіанти для поширених сценаріїв збоїв, таких як відкат версій, резервні сервери, масштабування та балансування навантаження, зменшення DoS-атак та резервне копіювання даних.

Розгортання та міграція застосунків. Сплануйте, як міграції будуть застосовуватися під час розгортання; їх виконання на початку застосунку може призвести до проблем із паралельністю та потребувати вищих дозволів, ніж необхідно для нормальної роботи. Використовуйте проміжне розміщення для полегшення відновлення після фатальних помилок під час міграції.

Детальне вивчення та тестування згенерованих міграцій. Міграції слід ретельно протестувати перед застосуванням до робочих даних. Форму схеми та типи стовпців не можна легко змінити після того, як таблиці містять робочі дані. Наприклад, на SQL Server nvarchar(max) та decimal(18, 2) рідко є найкращими типами для стовпців, що відображаються на рядкові та десяткові властивості, але це значення за замовчуванням, які використовує EF, оскільки він не знає вашого конкретного сценарію.
  
