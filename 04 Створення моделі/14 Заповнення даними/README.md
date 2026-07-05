# Заповнення даними (Data Seeding)

Data Seeding – це процес заповнення бази даних початковим набором даних.

* Конфігураційні параметри завантаження даних (UseSeeding).
* Спеціальна користувацька логіка ініціалізації
* Моделью налаштовані дані (HasData).
* Ручне налаштування міграції

## Параметри конфігурації методів UseSeeding та UseAsyncSeeding

Методи UseSeeding та UseAsyncSeeding забезпечують зручний спосіб заповнення бази даних початковими даними. Ці методи спрямовані на покращення використання логіки користувацької ініціалізації (пояснення нижче). Вони забезпечують одне чітке місце, де можна розмістити весь код заповнення даних. Більше того, код усередині методів UseSeeding та UseAsyncSeeding захищений механізмом блокування міграції для запобігання проблемам паралельності.

Методи заповнення викликаються як частина операції EnsureCreated, команди Migrate та dotnet ef database update, навіть якщо немає змін моделі та не було застосовано жодних міграцій.

Використання UseSeeding та UseAsyncSeeding – це рекомендований спосіб заповнення бази даних початковими даними під час роботи з EF Core.

Ці методи можна налаштувати на кроці налаштування параметрів. Ось приклад:


```cs
public class Language
{
    public int Id { get; set; }
    [StringLength(5)]
    public string Code { get; set; } = null!;
    [StringLength(32)]
    public string Name { get; set; } = null!;
    public int? Sort { get; set; }
}
```


```cs
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSeeding((context, _) =>
        {
            // Check if the Language table already has data
            if (!context.Set<Language>().Any())
            {
                context.Set<Language>().AddRange(
                    new Language { Name = "English", Code = "en", Sort = 1 },
                    new Language { Name = "Українська", Code = "ua", Sort = 2 },
                    new Language { Name = "Deutsch", Code = "de", Sort = 3 }
                );
                context.SaveChanges();
            }
        }).UseAsyncSeeding(async (context, _, cancellationToken) =>
        {
            // Асинхронна перевірка наявності даних
            if (!await context.Set<Language>().AnyAsync(cancellationToken))
            {
                await context.Set<Language>().AddRangeAsync(new[]
                {
                    new Language { Name = "English", Code = "en", Sort = 1 },
                    new Language { Name = "Українська", Code = "ua", Sort = 2 },
                    new Language { Name = "Deutsch", Code = "de", Sort = 3 }
                    }, cancellationToken);

                // Асинхронне збереження змін
                await context.SaveChangesAsync(cancellationToken);
            }
        }); 
    }
```

UseSeeding викликається з методу EnsureCreated, а UseAsyncSeeding викликається з методу EnsureCreatedAsync. Під час використання цієї функції рекомендується реалізувати методи UseSeeding та UseAsyncSeeding, використовуючи подібну логіку, навіть якщо код, що використовує EF, є асинхронним. Інструменти EF Core наразі покладаються на синхронну версію методу та не зможуть правильно заповнити базу даних, якщо метод UseSeeding не реалізовано.

## Спеціальна користувацька логіка ініціалізації

Простий та потужний спосіб виконання заповнення даних – це використання SaveChangesAsync перед початком виконання основної логіки програми. Для цієї мети рекомендується використовувати UseSeeding та UseAsyncSeeding, проте іноді використання цих методів не є найкращим рішенням. Прикладом сценарію є ситуація, коли для заповнення потрібно використовувати два різні контексти в одній транзакції. Нижче наведено приклад коду, який виконує користувацьку ініціалізацію безпосередньо в застосунку:

```cs
using (var context = new DataSeedingContext())
{
    await context.Database.EnsureCreatedAsync();

    var testBlog = await context.Blogs.FirstOrDefaultAsync(b => b.Url == "http://test.com");
    if (testBlog == null)
    {
        context.Blogs.Add(new Blog { Url = "http://test.com" });
        await context.SaveChangesAsync();
    }
}
```
Код заповнення не повинен бути частиною звичайного виконання програми, оскільки це може спричинити проблеми з паралельністю під час роботи кількох екземплярів, а також вимагатиме від програми дозволу на зміну схеми бази даних.

Залежно від обмежень вашого розгортання, код ініціалізації може бути виконаний різними способами:

* Локальний запуск програми ініціалізації
* Розгортання програми ініціалізації разом з основною програмою, виклик процедури ініціалізації та вимкнення або видалення програми ініціалізації.

Зазвичай це можна автоматизувати за допомогою профілів публікації.

## Моделью налаштовані дані (HasData)

Дані також можна пов’язати з типом сутності як частину конфігурації моделі. Потім міграції EF Core можуть автоматично обчислювати, які операції вставки, оновлення або видалення потрібно застосувати під час оновлення бази даних до нової версії моделі. 

Міграції враховують зміни моделі лише під час визначення того, яку операцію слід виконати, щоб привести керовані дані до бажаного стану. Таким чином, будь-які зміни до даних, виконані поза міграціями, можуть бути втрачені або спричинити помилку.

Як приклад, це налаштує керовані дані для Country в OnModelCreating:

```cs
modelBuilder.Entity<Country>(b =>
{
    b.Property(x => x.Name).IsRequired();
    b.HasData(
        new Country { CountryId = 1, Name = "USA" },
        new Country { CountryId = 2, Name = "Canada" },
        new Country { CountryId = 3, Name = "Mexico" });
});
```
Щоб додати сутності, що мають зв'язок, потрібно вказати значення зовнішнього ключа:

```cs
modelBuilder.Entity<City>().HasData(
    new City { Id = 1, Name = "Seattle", LocatedInId = 1 },
    new City { Id = 2, Name = "Vancouver", LocatedInId = 2 },
    new City { Id = 3, Name = "Mexico City", LocatedInId = 3 },
    new City { Id = 4, Name = "Puebla", LocatedInId = 3 });
```
Під час керування даними для навігації «many-to-many» сутність об'єднання потрібно налаштувати явно. Якщо тип сутності має будь-які властивості в тіньовому стані (наприклад, сутність об'єднання LanguageCountry нижче), для надання значень можна використовувати анонімний клас:

```cs
modelBuilder.Entity<Language>(b =>
{
    b.HasData(
        new Language { Id = 1, Name = "English" },
        new Language { Id = 2, Name = "French" },
        new Language { Id = 3, Name = "Spanish" });

    b.HasMany(x => x.UsedIn)
        .WithMany(x => x.OfficialLanguages)
        .UsingEntity(
            "LanguageCountry",
            r => r.HasOne(typeof(Country)).WithMany().HasForeignKey("CountryId").HasPrincipalKey(nameof(Country.CountryId)),
            l => l.HasOne(typeof(Language)).WithMany().HasForeignKey("LanguageId").HasPrincipalKey(nameof(Language.Id)),
            je =>
            {
                je.HasKey("LanguageId", "CountryId");
                je.HasData(
                    new { LanguageId = 1, CountryId = 2 },
                    new { LanguageId = 2, CountryId = 2 },
                    new { LanguageId = 3, CountryId = 3 });
            });
});
```
Типи сутностей, що належать власнику, можна налаштувати аналогічним чином:

```cs
modelBuilder.Entity<Language>().OwnsOne(p => p.Details).HasData(
    new { LanguageId = 1, Phonetic = false, Tonal = false, PhonemesCount = 44 },
    new { LanguageId = 2, Phonetic = false, Tonal = false, PhonemesCount = 36 },
    new { LanguageId = 3, Phonetic = true, Tonal = false, PhonemesCount = 24 });
```
Після додавання даних до моделі слід використовувати міграції для застосування змін.

Або ж ви можете використовувати EnsureCreatedAsync для створення нової бази даних, що містить керовані дані, наприклад, для тестової бази даних або під час використання постачальника даних у пам'яті чи будь-якої нереляційної бази даних. Зверніть увагу, що якщо база даних вже існує, EnsureCreatedAsync не оновить ні схему, ні керовані дані в базі даних. Для реляційних баз даних не слід викликати EnsureCreatedAsync, якщо ви плануєте використовувати міграції.

Заповнення бази даних за допомогою методу HasData раніше називалося "data seeding". Таке найменування створює неправильні очікування, оскільки ця функція має низку обмежень і підходить лише для певних типів даних. Саме тому ми вирішили перейменувати його на «model managed data»(дані, керовані моделлю). Методи UseSeeding та UseAsyncSeeding слід використовувати для загального заповнення даних.

## Обмеження даних, керованих моделлю

Цей тип даних керується міграціями, і скрипт для оновлення даних, які вже є в базі даних, потрібно створювати без підключення до бази даних. Це накладає деякі обмеження:

* Значення первинного ключа потрібно вказати, навіть якщо воно зазвичай генерується базою даних. Воно використовуватиметься для виявлення змін даних між міграціями.

* Раніше вставлені дані будуть видалені, якщо первинний ключ буде будь-яким чином змінено.

Тому ця функція найбільш корисна для статичних даних, які не очікуються змін поза міграціями та не залежать від нічого іншого в базі даних, наприклад, поштових індексів.

Якщо ваш сценарій включає будь-що з переліченого нижче, рекомендується використовувати методи UseSeeding та UseAsyncSeeding, описані в першому розділі:

* Тимчасові дані для тестування
* Дані, що залежать від стану бази даних
* Дані великого розміру (дані початкового значення фіксуються в знімках міграції, а великі дані можуть швидко призвести до величезних файлів та зниження продуктивності).
* Дані, для яких база даних потребує генерації значень ключів, включаючи сутності, що використовують альтернативні ключі як ідентифікатори.
* Дані, що потребують користувацького перетворення (які не обробляються перетвореннями значень), такі як деяке хешування паролів.
* Дані, що потребують викликів зовнішнього API, такі як ролі ASP.NET Core Identity та створення користувачів.
* Дані, які не є фіксованими та детермінованими, такі як існування до DateTime.Now.

## Ручне налаштування міграції

Коли додається міграція, зміни в даних, зазначених за допомогою HasData, перетворюються на виклики InsertData(), UpdateData() та DeleteData(). Один зі способів обійти деякі обмеження HasData — це вручну додати ці виклики або користувацькі операції до міграції.

```cs
migrationBuilder.InsertData(
    table: "Countries",
    columns: new[] { "CountryId", "Name" },
    values: new object[,]
    {
        { 1, "USA" },
        { 2, "Canada" },
        { 3, "Mexico" }
    });

migrationBuilder.InsertData(
    table: "Languages",
    columns: new[] { "Id", "Name", "Details_PhonemesCount", "Details_Phonetic", "Details_Tonal" },
    values: new object[,]
    {
        { 1, "English", 44, false, false },
        { 2, "French", 36, false, false },
        { 3, "Spanish", 24, true, false }
    });

migrationBuilder.InsertData(
    table: "Cites",
    columns: new[] { "Id", "LocatedInId", "Name" },
    values: new object[,]
    {
        { 1, 1, "Seattle" },
        { 2, 2, "Vancouver" },
        { 3, 3, "Mexico City" },
        { 4, 3, "Puebla" }
    });

migrationBuilder.InsertData(
    table: "LanguageCountry",
    columns: new[] { "CountryId", "LanguageId" },
    values: new object[,]
    {
        { 2, 1 },
        { 2, 2 },
        { 3, 3 }
    });
```

