# Резервні поля (Backing Fields)

Резервні поля дозволяють EF читати та/або записувати дані в поле, а не у властивість. Це може бути корисним, коли інкапсуляція в класі використовується для обмеження використання та/або покращення семантики доступу до даних кодом програми, але значення слід зчитувати з бази даних та/або записувати в неї без використання цих обмежень/покращень.

## Базова конфігурація

За домовленістю, наступні поля будуть виявлені як резервні поля для заданої властивості (перелічені в порядку пріоритету). 

* \<camel-cased property name\>
* _\<camel-cased property name\>
* _\<property name\>
* m_\<camel-cased property name\>
* m_\<property name\>

У наступному прикладі властивість Url налаштовано на використання поля _url як резервного поля:

```cs
public class Blog
{
    private string _url;

    public int BlogId { get; set; }

    public string Url
    {
        get { return _url; }
        set { _url = value; }
    }
}
```
Зверніть увагу, що резервні поля виявляються лише для властивостей, які включені до моделі. 

Ви також можете налаштувати резервні поля за допомогою анотацій даних або Fluent API, наприклад, якщо назва поля не відповідає вищезазначеним умовам:

```cs
public class Blog
{
    private string _validatedUrl;

    public int BlogId { get; set; }

    [BackingField(nameof(_validatedUrl))]
    public string Url
    {
        get { return _validatedUrl; }
    }

    public void SetUrl(string url)
    {
        // put your validation code here

        _validatedUrl = url;
    }
}
```

## Доступ до полів та властивостей

За замовчуванням EF завжди читатиме та записуватиме в резервні поля (за умови, що воно було правильно налаштовано) і ніколи не використовуватиме властивість. Однак, EF також підтримує інші шаблони доступу. Наприклад, наступний приклад вказує EF записувати в резервне поле лише під час матеріалізації та використовувати властивість у всіх інших випадках:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Blog>()
        .Property(b => b.Url)
        .HasField("_validatedUrl")
        .UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction);
}
```

## Властивості лише для полів

Ви також можете створити концептуальну властивість у вашій моделі, яка не має відповідної властивості CLR у класі сутності, а натомість використовує поле для зберігання даних у сутності. Це відрізняється від тіньових властивостей (Shadow Properties), де дані зберігаються у відстежувачі змін, а не в типі CLR сутності.
Властивості лише для полів зазвичай використовуються, коли клас сутності використовує методи замість властивостей для отримання/встановлення значень, або у випадках, коли поля взагалі не повинні бути відображені в моделі предметної області (наприклад, первинні ключі).

Ви можете налаштувати властивість лише для полів, вказавши назву в API Property(...):

```cs
internal class MyContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>()
            .Property("_validatedUrl");
    }
}

public class Blog
{
    private string _validatedUrl;

    public int BlogId { get; set; }

    public string GetUrl()
    {
        return _validatedUrl;
    }

    public void SetUrl(string url)
    {
        using (var client = new HttpClient())
        {
            var response = client.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();
        }

        _validatedUrl = url;
    }
}
```
EF спробує знайти властивість CLR із заданим іменем або поле, якщо властивість не знайдено. Якщо ні властивість, ні поле не знайдено, замість цього буде встановлено тіньову властивість.

Вам може знадобитися звернутися до властивості лише для поля з LINQ-запитів, але такі поля зазвичай є приватними. Ви можете використовувати метод EF.Property(...) у LINQ-запиті для звернення до поля:

```cs
var blogs = db.blogs.OrderBy(b => EF.Property<string>(b, "_validatedUrl"));
```