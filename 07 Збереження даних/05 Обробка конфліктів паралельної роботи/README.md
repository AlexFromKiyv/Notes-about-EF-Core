# Обробка конфліктів паралельної роботи

У більшості сценаріїв бази даних використовуються одночасно кількома екземплярами програм, кожен з яких виконує зміни до даних незалежно один від одного. Коли ті самі дані змінюються одночасно, можуть виникати невідповідності та пошкодження даних, наприклад, коли два клієнти змінюють різні стовпці в одному рядку, які певним чином пов'язані між собою. На цій сторінці обговорюються механізми забезпечення узгодженості ваших даних в умовах таких одночасних змін.

## Оптимістичний паралелізм

EF Core реалізує оптимістичну паралелізм, яка передбачає, що конфлікти паралельності трапляються відносно рідко. На відміну від песимістичних підходів, які блокують дані заздалегідь і лише потім переходять до їх модифікації, оптимістичний паралельний підхід не приймає блокувань, але забезпечує збій модифікації даних під час збереження, якщо дані змінилися з моменту запиту. Про цю помилку паралельного виконання повідомляється застосунку, який відповідно обробляє її, можливо, повторюючи всю операцію з новими даними.

В EF Core оптимістичний паралелізм реалізується шляхом налаштування властивості як токена паралелізму. Токен паралелізму завантажується та відстежується під час запиту сутності – як і будь-яка інша властивість. Потім, коли під час SaveChanges() виконується операція оновлення або видалення, значення токена паралельного доступу в базі даних порівнюється з початковим значенням, зчитаним EF Core.

Щоб зрозуміти, як це працює, припустимо, що ми працюємо на SQL Server, і визначимо типовий тип сутності Person зі спеціальною властивістю Version:

```cs
public class Person
{
    public int PersonId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    [Timestamp]
    public byte[] Version { get; set; }

    public override string? ToString()
    {
        return $"{PersonId}\t{FirstName}\t{LastName}\t{Convert.ToHexString(Version)}\t"+base.ToString();
    }
}
```
```cs
static async Task PopulateDatabase(ApplicationDbContext context)
{
    context.People.AddRange(
        new Person { FirstName = "John", LastName = "Doe" },
        new Person { FirstName = "Jane", LastName = "Smith" },
        new Person { FirstName = "Alice", LastName = "Johnson" }
    );

    var countAdded = await context.SaveChangesAsync();
    Console.WriteLine(countAdded);
}
```
У SQL Server це налаштовує токен паралельного використання, який автоматично змінюється в базі даних щоразу, коли змінюється рядок (докладніше див. нижче). З урахуванням цієї конфігурації давайте розглянемо, що відбувається під час простої операції оновлення:

```cs

static async Task UpdatePerson(ApplicationDbContext context)
{
static async Task UpdatePerson(ApplicationDbContext context)
{
    Person person = await context.People.SingleAsync(p => p.FirstName == "John");
    Console.WriteLine(person);
    person.FirstName = "Paul";
    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
    Console.WriteLine(person);
}
}

```
```
1       John    00000000000007D1
1
1       Paul    00000000000007D4
```

1. На першому кроці з бази даних завантажується об'єкт Person; це включає токен паралелізму, який тепер, як завжди, відстежується EF разом з рештою властивостей.
2. Потім екземпляр Person певним чином модифікується — ми змінюємо властивість FirstName.
3. Потім ми даємо команду EF Core зберегти модифікацію. Оскільки токен паралельного доступу налаштовано, EF Core надсилає до бази даних наступний SQL-запит:

```sql
exec sp_executesql N'SET IMPLICIT_TRANSACTIONS OFF;
SET NOCOUNT ON;
UPDATE [People] SET [FirstName] = @p0
OUTPUT INSERTED.[Version]
WHERE [PersonId] = @p1 AND [Version] = @p2;
',N'@p1 int,@p0 nvarchar(4000),@p2 varbinary(8)',@p1=1,@p0=N'Paul',@p2=0x00000000000007D1
```
Зверніть увагу, що окрім PersonId у реченні WHERE, EF Core також додав умову для Version; це змінює рядок лише тоді, коли стовпець Version не змінювався з моменту запиту.

У звичайному ("оптимістичному") випадку одночасне оновлення не відбувається, і UPDATE успішно завершується, змінюючи рядок; база даних повідомляє EF Core, що один рядок був під зміною UPDATE, як і очікувалося. Однак, якщо відбулося одночасне оновлення, UPDATE не знаходить жодних відповідних рядків і повідомляє, що жодного з них не було під зміною. В результаті, SaveChanges() в EF Core генерує виняток DbUpdateConcurrencyException, який застосунок повинен перехопити та обробити належним чином. Методи виконання цього детально описані нижче в розділі «Вирішення конфліктів паралельного виконання».

```cs
    await ConcurrencyFailure();

static async Task ConcurrencyFailure()
{
    using var context1 = new ApplicationDbContextFactory().CreateDbContext(null);

    Person person1 = await context1.People.SingleAsync(p => p.FirstName == "Alice");
    Console.WriteLine(person1);
    person1.FirstName = "Alicia";

    using (var context2 = new ApplicationDbContextFactory().CreateDbContext(null))
    {
        Person person2 = await context2.People.SingleAsync(p => p.FirstName == "Alice");
        person2.FirstName = "Ally";
        Console.WriteLine(person2);
        await context2.SaveChangesAsync();
    }

    await context1.SaveChangesAsync();
}

```

Хоча у наведених вище прикладах обговорювалося оновлення існуючих сутностей, EF також викидає DbUpdateConcurrencyException під час спроби видалити рядок, який був одночасно змінений. Однак, цей виняток зазвичай ніколи не виникає під час додавання сутностей; хоча база даних дійсно може викликати порушення унікального обмеження, якщо вставляються рядки з однаковим ключем, це призводить до виникнення винятку, специфічного для постачальника, а не DbUpdateConcurrencyException.

## Токени паралельного доступу, згенеровані базою даних

У наведеному вище коді ми використовували атрибут [Timestamp] для зіставлення властивості зі стовпцем rowversion SQL Server. Оскільки rowversion автоматично змінюється під час оновлення рядка, він дуже корисний як токен паралельного виконання з мінімальними зусиллями, який захищає весь рядок. Налаштування стовпця SQL Server rowversion як токена паралельного виконання виконується наступним чином:

```cs
    [Timestamp]
    public byte[] Version { get; set; }
```

або

```cs
    modelBuilder.Entity<Person>()
        .Property(p => p.Version)
        .IsRowVersion();
```

```sql
CREATE TABLE [dbo].[People] (
    [PersonId]  INT            IDENTITY (1, 1) NOT NULL,
    [FirstName] NVARCHAR (MAX) NOT NULL,
    [LastName]  NVARCHAR (MAX) NOT NULL,
    [Version]   ROWVERSION     NOT NULL,
    CONSTRAINT [PK_People] PRIMARY KEY CLUSTERED ([PersonId] ASC)
);
```

Тип rowversion, показаний вище, є специфічною функцією SQL Server; деталі налаштування автоматично оновлюваного токена паралельного використання відрізняються залежно від бази даних, а деякі бази даних взагалі не підтримують їх (наприклад, SQLite). Зверніться до документації вашого постачальника для отримання точних деталей.

## Токени паралельного доступу, керовані застосунком

Замість того, щоб база даних автоматично керувала токеном паралелізму, ви можете керувати ним у коді програми. Це дозволяє використовувати оптимістичну паралельність у базах даних, таких як SQLite, де не існує власного автоматично оновлюваного типу. Але навіть на SQL Server токен паралельного доступу, керований програмою, може забезпечити детальний контроль над тим, які саме зміни стовпців призводять до повторного створення токена. Наприклад, у вас може бути властивість, що містить деяке кешоване або неважливе значення, і ви не хочете, щоб зміна цієї властивості спричинила конфлікт паралельності.

Наведений нижче код налаштовує властивість GUID як токен паралельності:

```cs
public class Person
{
    public int PersonId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }

    [ConcurrencyCheck]
    public Guid Version { get; set; }

    public override string? ToString()
    {
        return $"{PersonId}\t{FirstName}\t{LastName}\t{Version}\t"+base.ToString();
    }
}
```
або

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Person>()
        .Property(p => p.Version)
        .IsConcurrencyToken();
}
```


```sql
CREATE TABLE [dbo].[People] (
    [PersonId]  INT              IDENTITY (1, 1) NOT NULL,
    [FirstName] NVARCHAR (MAX)   NOT NULL,
    [LastName]  NVARCHAR (MAX)   NOT NULL,
    [Version]   UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_People] PRIMARY KEY CLUSTERED ([PersonId] ASC)
);
```
```cs
static async Task PopulateDatabase(ApplicationDbContext context)
{
    context.People.AddRange(
        new Person { FirstName = "John", LastName = "Doe", Version = Guid.NewGuid() },
        new Person { FirstName = "Jane", LastName = "Smith", Version = Guid.NewGuid() },
        new Person { FirstName = "Alice", LastName = "Johnson", Version = Guid.NewGuid() }
    );

    var countAdded = await context.SaveChangesAsync();
    Console.WriteLine(countAdded);
}
```
Оскільки ця властивість не генерується базою даних, її необхідно призначити в застосунку щоразу, коли зміни зберігаються:

```cs
static async Task UpdatePerson(ApplicationDbContext context)
{
    Person person = await context.People.SingleAsync(p => p.FirstName == "John");
    Console.WriteLine(person);
    person.FirstName = "Paul";
    person.Version = Guid.NewGuid();
    int count = await context.SaveChangesAsync();
    Console.WriteLine(count);
    Console.WriteLine(person);
}
```
```
1       John    Doe     38af3c1b-0a6c-4ac0-ae2b-b3e4abf75768    Draft.Models.Person
1
1       Paul    Doe     1e8115c9-e465-4762-b64a-c13d3a76c200    Draft.Models.Person
```
Якщо ви хочете, щоб завжди призначалося нове значення GUID, ви можете зробити це за допомогою SaveChanges interceptor(перехоплювача). Однак, однією з переваг ручного керування токеном паралельності є те, що ви можете точно контролювати, коли він буде перегенерований, щоб уникнути непотрібних конфліктів паралельності.

## Вирішення конфліктів паралельності

Незалежно від того, як налаштовано ваш токен паралельності, для реалізації оптимістичної паралельності ваша програма повинна належним чином обробляти випадок, коли виникає конфлікт паралельності та виникає виняток DbUpdateConcurrencyException; це називається вирішенням конфлікту паралельності.

Один із варіантів — просто повідомити користувача, що оновлення не вдалося через конфлікт змін; тоді користувач може завантажити нові дані та спробувати ще раз. Або, якщо ваша програма виконує автоматичне оновлення, вона може просто зациклитися та одразу повторити спробу після повторного запиту даних.

Більш складний спосіб вирішення конфліктів паралельного виконання — об’єднати зміни, що очікують на розгляд, з новими значеннями в базі даних. Точні деталі того, які значення об’єднуються, залежать від програми, і процес може керуватися інтерфейсом користувача, де відображаються обидва набори значень.

Існує три набори значень, які допоможуть вирішити конфлікт паралельного виконання:

* Current values(поточні значення) – це значення, які програма намагалася записати до бази даних.
* Original values(оригінальні значення) – це значення, які були спочатку отримані з бази даних, до внесення будь-яких змін.
* Database values(значення бази даних) – це значення, що наразі зберігаються в базі даних. 

Загальний підхід до вирішення конфлікту паралельності такий:

1. Перехопити DbUpdateConcurrencyException під час SaveChanges 
2. Використовуйте DbUpdateConcurrencyException.Entries для підготовки нового набору змін для відповідних сутностей.
3. Оновіть  original values токена паралельності, щоб відобразити поточні значення в базі даних.
4. Повторюйте процес, доки конфлікти не зникнуть.

У наступному прикладі Person.FirstName та Person.LastName налаштовано як токени паралельного виконання. У місці, де ви додаєте специфічну для програми логіку для вибору значення, яке потрібно зберегти, є коментар // TODO:

```cs
public class Person
{
    public int PersonId { get; set; }

    [ConcurrencyCheck]
    public string FirstName { get; set; }

    [ConcurrencyCheck]
    public string LastName { get; set; }

    public string PhoneNumber { get; set; }
}
```
```cs
static async Task PopulateDatabase(ApplicationDbContext context)
{
    context.People.Add(
        new Person { FirstName = "John", LastName = "Doe", PhoneNumber = "123-456-7890" }
    );

    var countAdded = await context.SaveChangesAsync();
    Console.WriteLine(countAdded);
}
```

```cs
static async Task ResolvingConcurrencyConflicts()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Person person = await context.People.SingleAsync(p => p.PersonId == 1);
    person.PhoneNumber = "999-999-9999";

    await context.Database.ExecuteSqlRawAsync("UPDATE dbo.People SET FirstName = 'Jane' WHERE PersonId = 1");

    bool saved = false;
    while(!saved)
    {
        try
        {
            int count = await context.SaveChangesAsync();
            Console.WriteLine(count);
            saved = true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                if (entry.Entity is Person)
                {
                    var proposedValues = entry.CurrentValues;
                    var databaseValues = await entry.GetDatabaseValuesAsync();

                    foreach (var property in proposedValues.Properties)
                    {
                        var proposedValue = proposedValues[property];
                        var databaseValue = databaseValues[property];

                        // TODO: decide which value should be written to database
                        // proposedValues[property] = databaseValues[property];
                        proposedValues[property] = proposedValues[property];
                    }

                    // Refresh original values to bypass next concurrency check
                    entry.OriginalValues.SetValues(databaseValues);
                }
                else
                {
                    throw new NotSupportedException(
                        "Don't know how to handle concurrency conflicts for "
                        + entry.Metadata.Name);
                }
            }
        }
    }
}
```
Аналогічний приклад з іншим налаштуванням сутності Person:

```cs
public class Person
{
    public int PersonId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string PhoneNumber { get; set; }
    [Timestamp]
    public byte[] Version { get; set; }

    public override string? ToString()
    {
        return $"{PersonId}\t{FirstName}\t{LastName}\t{PhoneNumber}\t{Convert.ToHexString(Version)}\t";
    }
}
```

```cs
static async Task PopulateDatabase(ApplicationDbContext context)
{
    context.People.Add(
        new Person { FirstName = "John", LastName = "Doe", PhoneNumber = "123-456-7890" }
    );

    var countAdded = await context.SaveChangesAsync();
    Console.WriteLine(countAdded);
}
```

```cs

static async Task ResolvingConcurrencyConflicts()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Person person = await context.People.SingleAsync(p => p.PersonId == 1);
    Console.WriteLine("Person before change:" + person);

    person.PhoneNumber = "999-999-9999";

    await context.Database.ExecuteSqlRawAsync("UPDATE dbo.People SET FirstName = 'Jane' WHERE PersonId = 1");

    bool saved = false;
    while (!saved)
    {
        try
        {
            await context.SaveChangesAsync();
            saved = true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Console.WriteLine("Concurrency conflict detected. Resolving...");
            foreach (var entry in ex.Entries)
            {
                if (entry.Entity is Person)
                {
                    var proposedValues = entry.CurrentValues;
                    var databaseValues = await entry.GetDatabaseValuesAsync();

                    foreach (var property in proposedValues.Properties)
                    {
                        var proposedValue = proposedValues[property];
                        var databaseValue = databaseValues[property];

                        if (property.Name == nameof(Person.PhoneNumber))
                        {
                            // Keep the proposed value for PhoneNumber
                            proposedValues[property] = proposedValue;
                        }
                        else
                        {
                            // Use the database value for other properties
                            proposedValues[property] = databaseValue;
                        }
                    }

                    // Refresh original values to bypass next concurrency check
                    entry.OriginalValues.SetValues(databaseValues);
                }
                else
                {
                    throw new NotSupportedException("Concurrency conflict for an unknown entity type.");
                }
            }
        }
    }
    Console.WriteLine($"Person updated: {person}");
}
```
```
Person before change:1  John    Doe     123-456-7890    00000000000007D1
Concurrency conflict detected. Resolving...
Person updated: 1       Jane    Doe     999-999-9999    00000000000007D3
```


## Використання рівнів ізоляції для контролю паралельності

Оптимистична паралельність за допомогою токенів паралельності — це не єдиний спосіб забезпечити узгодженість даних в умовах одночасних змін.

Одним із механізмів забезпечення узгодженості є рівень ізоляції транзакцій повторюваного читання. У більшості баз даних цей рівень гарантує, що транзакція бачить дані в базі даних такими, якими вони були на момент початку транзакції, без впливу будь-якої наступної одночасної активності. Беручи наш базовий приклад вище, коли ми запитуємо Person для його оновлення якимось чином, база даних повинна переконатися, що жодні інші транзакції не втручаються в цей рядок бази даних, доки транзакція не завершиться. Залежно від реалізації вашої бази даних, це відбувається одним із двох способів:

1. Коли запитується рядок, ваша транзакція отримує спільне блокування. Будь-яка зовнішня транзакція, яка намагається оновити рядок, буде заблокована, доки ваша транзакція не завершиться. Це форма песимістичного блокування, яка реалізується рівнем ізоляції SQL Server "повторюване читання".
2. Замість блокування, база даних дозволяє зовнішній транзакції оновлювати рядок, але коли ваша власна транзакція намагається виконати оновлення, виникає помилка "серіалізації", яка вказує на конфлікт паралельності. Це форма оптимістичного блокування, подібна до функції токенів паралельності в EF, і реалізується рівнем ізоляції знімків SQL Server, а також рівнем ізоляції повторюваних читань PostgreSQL.

Зверніть увагу, що рівень ізоляції "серіалізується" забезпечує ті ж гарантії, що й повторюване читання (і додає додаткові), тому він функціонує так само, як і вищезазначене.

Використання вищого рівня ізоляції для керування конфліктами паралельності є простішим, не вимагає токенів паралельності та надає інші переваги; наприклад, повторювані зчитування гарантують, що ваша транзакція завжди бачить одні й ті ж дані в усіх запитах усередині транзакції, уникаючи невідповідностей. Однак цей підхід має свої недоліки.

По-перше, якщо ваша реалізація бази даних використовує блокування для реалізації рівня ізоляції, то інші транзакції, які намагаються змінити той самий рядок, повинні блокуватися на весь час виконання транзакції. Це може негативно вплинути на одночасну продуктивність (зробіть транзакцію короткою!), хоча зверніть увагу, що механізм EF викидає виняток і змушує вас повторити спробу, що також має певний вплив. Це стосується рівня повторюваного читання SQL Server, але не рівня знімка, який не блокує запитувані рядки.

Що ще важливіше, цей підхід вимагає транзакції, яка охоплює всі операції. Якщо ви, скажімо, запитуєте Person, щоб відобразити її дані користувачеві, а потім чекаєте, поки користувач внесе зміни, то транзакція повинна залишатися активною протягом потенційно тривалого часу, чого слід уникати в більшості випадків. Як результат, цей механізм зазвичай доцільний, коли всі операції, що містяться в ній, виконуються негайно, і транзакція не залежить від зовнішніх вхідних даних, що може збільшити її тривалість.