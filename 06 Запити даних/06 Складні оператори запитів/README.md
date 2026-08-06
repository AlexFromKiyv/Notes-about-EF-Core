# Складні оператори запитів

Language Integrated Query (LINQ) містить багато складних операторів, які поєднують кілька джерел даних або виконують складну обробку. Не всі оператори LINQ мають відповідні переклади на стороні сервера. Іноді запит в одній формі перекладається на сервер, але якщо його написати в іншій формі, він не перекладається, навіть якщо результат той самий. На цій сторінці описано деякі складні оператори та їх підтримувані варіанти. У майбутніх випусках ми можемо розпізнавати більше шаблонів та додавати відповідні їм переклади. Також важливо пам’ятати, що підтримка перекладу відрізняється залежно від постачальника. Певний запит, який перекладається в SqlServer, може не працювати для баз даних SQLite.


## Приєднання (Join)


Оператор LINQ Join дозволяє з’єднати два джерела даних на основі селектора ключа для кожного джерела, генеруючи кортеж значень, коли ключ збігається. Це природно перекладається як INNER JOIN на реляційних базах даних. Хоча LINQ Join має зовнішні та внутрішні селектори ключів, база даних вимагає однієї умови з'єднання. Отже, EF Core генерує умову об'єднання, порівнюючи зовнішній селектор ключа з внутрішнім селектором ключа на рівність.

```cs
    var query = from photo in context.Set<PersonPhoto>()
                join person in context.Set<Person>()
                    on photo.PersonPhotoId equals person.PhotoId
                select new { person, photo };

    Console.WriteLine(query.ToQueryString());
```
```sql
SELECT [p0].[PersonId], [p0].[Name], [p0].[PhotoId], [p].[PersonPhotoId], [p].[Caption], [p].[Photo]
FROM [PersonPhotos] AS [p]
INNER JOIN [People] AS [p0] ON [p].[PersonPhotoId] = [p0].[PhotoId]
```

Крім того, якщо селектори ключів є анонімними типами, EF Core генерує умову об'єднання для порівняння рівності компонентів.

```cs
    var query = from photo in context.Set<PersonPhoto>()
                join person in context.Set<Person>()
                    on new { Id = (int?)photo.PersonPhotoId, photo.Caption }
                    equals new { Id = person.PhotoId, Caption = "SN" }
                select new { person, photo };

    Console.WriteLine(query.ToQueryString());
```
```sql
SELECT [p0].[PersonId], [p0].[Name], [p0].[PhotoId], [p].[PersonPhotoId], [p].[Caption], [p].[Photo]
FROM [PersonPhotos] AS [p]
INNER JOIN [People] AS [p0] ON [p].[PersonPhotoId] = [p0].[PhotoId] AND [p].[Caption] = N'SN'
```

## GroupJoin

Оператор LINQ GroupJoin дозволяє з'єднувати два джерела даних, подібно до Join, але він створює групу внутрішніх значень для зіставлення зовнішніх елементів. Виконання запиту, подібного до наведеного нижче прикладу, генерує результат Blog & IEnumerable<Post>. Оскільки бази даних (особливо реляційні) не мають способу представлення колекції клієнтських об'єктів, GroupJoin у багатьох випадках не транслюється для сервера.

Для виконання GroupJoin без спеціального селектора потрібно отримати всі дані з сервера.

```cs
    var query = from b in context.Set<Blog>()
                join p in context.Set<Post>()
                    on b.BlogId equals p.BlogId into grouping
                select new { b, grouping };

    Console.WriteLine(query.ToQueryString());

    foreach (var item in query)
    {
        Console.WriteLine($"Blog:{item.b.BlogId}\t{item.b.Url}");
        foreach (var post in item.grouping)
        {
            Console.WriteLine($"  Post: {post.Title}");
        }
    }
```
```sql
SELECT [b].[BlogId], [b].[OwnerId], [b].[Rating], [b].[Url], [p].[PostId], [p].[AuthorId], [p].[BlogId], [p].[Content], [p].[Rating], [p].[Title]
FROM [Blogs] AS [b]
LEFT JOIN [Posts] AS [p] ON [b].[BlogId] = [p].[BlogId]
ORDER BY [b].[BlogId]
```
```
Blog:1  https://devblogs.microsoft.com/dotnet
  Post: What's new
Blog:2  https://mytravelblog.com/
  Post: Around the World in Eighty Days
  Post: Glamping *is* the way
  Post: Travel in the time of pandemic
Blog:3  https://mytravelblog3.com/
```
Але якщо селектор обмежує вибір даних, то отримання всіх даних із сервера може спричинити проблеми з продуктивністю. Ось чому EF Core не перекладає GroupJoin.

```cs
    var query = from b in context.Set<Blog>()
                join p in context.Set<Post>()
                    on b.BlogId equals p.BlogId into grouping
                select new { b, Posts = grouping.Where(p => p.Content.Contains("e")).ToList() };
```
```sql
SELECT [b].[BlogId], [b].[OwnerId], [b].[Rating], [b].[Url], [p0].[PostId], [p0].[AuthorId], [p0].[BlogId], [p0].[Content], [p0].[Rating], [p0].[Title]
FROM [Blogs] AS [b]
LEFT JOIN (
    SELECT [p].[PostId], [p].[AuthorId], [p].[BlogId], [p].[Content], [p].[Rating], [p].[Title]
    FROM [Posts] AS [p]
    WHERE [p].[Content] LIKE N'%e%'
) AS [p0] ON [b].[BlogId] = [p0].[BlogId]
ORDER BY [b].[BlogId]
```
```
Blog:1  https://devblogs.microsoft.com/dotnet
  Post: What's new
Blog:2  https://mytravelblog.com/
  Post: Around the World in Eighty Days
  Post: Glamping *is* the way
  Post: Travel in the time of pandemic
Blog:3  https://mytravelblog3.com/
```


## SelectMany

Оператор LINQ SelectMany дозволяє перераховувати селектор колекції для кожного зовнішнього елемента та генерувати кортежі значень з кожного джерела даних. У певному сенсі, це об'єднання, але без жодної умови, тому кожен зовнішній елемент пов'язаний з елементом з джерела колекції. Залежно від того, як селектор колекції пов'язаний із зовнішнім джерелом даних, SelectMany може перетворитися на різні запити на стороні сервера.

### Селектор колекції не посилається на зовнішнє джерело

Коли селектор колекції не посилається ні на що із зовнішнього джерела, результатом є декартовий добуток обох джерел даних. Це перетворюється на CROSS JOIN у реляційних базах даних.

```cs
    var query = from b in context.Set<Blog>()
                from p in context.Set<Post>()
                select new { b, p };
```
```sql
SELECT [b].[BlogId], [b].[OwnerId], [b].[Rating], [b].[Url], [p].[PostId], [p].[AuthorId], [p].[BlogId], [p].[Content], [p].[Rating], [p].[Title]
FROM [Blogs] AS [b]
CROSS JOIN [Posts] AS [p]
```

### Селектор колекції посилається на зовнішній елемент у реченні where

Коли селектор колекції має речення where, яке посилається на зовнішній елемент, EF Core перетворює його на об'єднання бази даних та використовує предикат як умову об'єднання. Зазвичай цей випадок виникає під час використання навігації колекцією для зовнішнього елемента як селектора колекції. Якщо колекція порожня для зовнішнього елемента, то для цього зовнішнього елемента не буде згенеровано жодних результатів. Але якщо до селектора колекції застосовується DefaultIfEmpty, то зовнішній елемент буде пов'язаний зі значенням за замовчуванням внутрішнього елемента. Через цю відмінність, цей тип запитів перетворюється на INNER JOIN за відсутності DefaultIfEmpty та LEFT JOIN, коли застосовується DefaultIfEmpty.

```cs
var query = from b in context.Set<Blog>()
            from p in context.Set<Post>().Where(p => b.BlogId == p.BlogId)
            select new { b, p };
```
```sql
SELECT [b].[BlogId], [b].[OwnerId], [b].[Rating], [b].[Url], [p].[PostId], [p].[AuthorId], [p].[BlogId], [p].[Content], [p].[Rating], [p].[Title]
FROM [Blogs] AS [b]
INNER JOIN [Posts] AS [p] ON [b].[BlogId] = [p].[BlogId]
```
При INNER JOIN якщо Blog не має ні одного Post його не буде у списку.


```cs
var query = from b in context.Set<Blog>()
             from p in context.Set<Post>().Where(p => b.BlogId == p.BlogId).DefaultIfEmpty()
             select new { b, p };
```
```sql
SELECT [b].[BlogId], [b].[OwnerId], [b].[Rating], [b].[Url], [p].[PostId], [p].[AuthorId], [p].[BlogId], [p].[Content], [p].[Rating], [p].[Title]
FROM [Blogs] AS [b]
LEFT JOIN [Posts] AS [p] ON [b].[BlogId] = [p].[BlogId]
```
При LEFT JOIN якщо Blog не має ні одного Post він буде у списку.

### Селектор колекції посилається на зовнішній елемент у випадку, відмінному від where

Коли селектор колекції посилається на зовнішній елемент, який не знаходиться в реченні where (як у випадку вище), він не перетворюється в базі даних на JOIN. Ось чому нам потрібно обчислити селектор колекції для кожного зовнішнього елемента. Це перекладається як операції APPLY у багатьох реляційних базах даних. Якщо колекція порожня для зовнішнього елемента, то для цього зовнішнього елемента не буде згенеровано жодних результатів. Але якщо до селектора колекції застосовується DefaultIfEmpty, то зовнішній елемент буде пов'язаний зі значенням за замовчуванням внутрішнього елемента. Через цю відмінність, цей тип запитів перетворюється на CROSS APPLY за відсутності DefaultIfEmpty та OUTER APPLY, коли застосовується DefaultIfEmpty. Деякі бази даних, такі як SQLite, не підтримують оператори APPLY, тому цей тип запитів може не бути перетворений.

```cs
var query = from b in context.Set<Blog>()
            from p in context.Set<Post>().Select(p => b.Url + "=>" + p.Title)
            select new { b, p };
```
```sql
SELECT [b].[BlogId], [b].[OwnerId], [b].[Rating], [b].[Url], [b].[Url] + N'=>' + [p].[Title] AS [p]
FROM [Blogs] AS [b]
CROSS APPLY [Posts] AS [p]
```

```cs
    var query = from b in context.Set<Blog>()
                 from p in context.Set<Post>().Select(p => b.Url + "=>" + p.Title).DefaultIfEmpty()
                 select new { b, p };
```
```sql
SELECT [b].[BlogId], [b].[OwnerId], [b].[Rating], [b].[Url], [b].[Url] + N'=>' + [p].[Title] AS [p]
FROM [Blogs] AS [b]
OUTER APPLY [Posts] AS [p]
```

## GroupBy

Оператори LINQ GroupBy створюють результат типу IGrouping\<TKey, TElement\>, де TKey та TElement можуть бути будь-якого довільного типу. Крім того, IGrouping реалізує IEnumerable<TElement>, що означає, що ви можете створювати над ним коди за допомогою будь-якого оператора LINQ після групування. Оскільки жодна структура бази даних не може представляти IGrouping, оператори GroupBy у більшості випадків не мають перекладу. Коли до кожної групи застосовується агрегатний оператор, який повертає скаляр, його можна перетворити на SQL GROUP BY у реляційних базах даних. SQL GROUP BY також є обмежувальним. Він вимагає групування лише за скалярними значеннями. Проекція може містити лише стовпці ключа групування або будь-який агрегат, застосований до стовпця. EF Core ідентифікує цей шаблон і перетворює його на сервер, як у наступному прикладі:

```cs
var query = from p in context.Set<Post>()
            group p by p.AuthorId
            into g
            select new { g.Key, Count = g.Count() };
```
```sql
SELECT [p].[AuthorId] AS [Key], COUNT(*) AS [Count]
FROM [Posts] AS [p]
GROUP BY [p].[AuthorId]
```
EF Core також перекладає запити, де агрегатний оператор групування з’являється в операторі LINQ Where або OrderBy (або іншому порядку). У SQL для речення where використовується речення HAVING. Частина запиту перед застосуванням оператора GroupBy може бути будь-яким складним запитом, якщо його можна перевести на сервер. Крім того, після застосування агрегатних операторів до запиту на групування для видалення груп з результуючого джерела, ви можете складати запити поверх нього, як і будь-який інший запит.

```cs
    var query = from p in context.Set<Post>()
                group p by p.AuthorId
                into g
                where g.Count() > 1
                orderby g.Key
                select new { g.Key, Count = g.Count() };
```
```sql
SELECT [p].[AuthorId] AS [Key], COUNT(*) AS [Count]
FROM [Posts] AS [p]
GROUP BY [p].[AuthorId]
HAVING COUNT(*) > 1
ORDER BY [p].[AuthorId]
```
Агреговані оператори, що підтримуються EF Core, такі:

|.NET|SQL|
|----|---|
|Average(x => x.Property)|AVG(Property)|
|Count()|COUNT(*)|
|Max(x => x.Property)|MAX(Property)|
|Min(x => x.Property)|MIN(Property)|
|Sum(x => x.Property)|SUM(Property)|

Можуть підтримуватися додаткові агрегатні оператори. Перегляньте документацію вашого постачальника для отримання додаткових зіставлень функцій.

Навіть попри відсутність структури бази даних для представлення IGrouping, у деяких випадках EF Core 7.0 та новіші версії можуть створювати групи після повернення результатів з бази даних. Це схоже на те, як працює оператор Include під час включення пов’язаних колекцій. Наведений нижче запит LINQ використовує оператор GroupBy для групування результатів за значенням їхньої властивості .

```cs
    var query = context.Posts.GroupBy(p => p.AuthorId);

    Console.WriteLine(query.ToQueryString());

    var result = await query.ToListAsync();

    foreach (var group in result)
    {
        Console.WriteLine($"AuthorId: {group.Key}");
        foreach (var post in group)
        {
            Console.WriteLine($"  \tPostId: {post.PostId}, Title: {post.Title}");
        }
    }
```
```
SELECT [p].[AuthorId], [p].[PostId], [p].[BlogId], [p].[Content], [p].[Rating], [p].[Title]
FROM [Posts] AS [p]
ORDER BY [p].[AuthorId]
AuthorId: 1
        PostId: 1, Title: What's new
AuthorId: 2
        PostId: 2, Title: Around the World in Eighty Days
AuthorId: 3
        PostId: 3, Title: Glamping *is* the way
        PostId: 4, Title: Travel in the time of pandemic
```
У цьому випадку оператор GroupBy не перетворюється безпосередньо на речення GROUP BY в SQL, а натомість EF Core створює групування після того, як результати повертаються сервером.

## LEFT JOIN

Хоча ліве з'єднання (Left Join) не є оператором LINQ, реляційні бази даних мають концепцію лівого з'єднання (Left Join), яка часто використовується в запитах. Певний шаблон у запитах LINQ дає той самий результат, що й LEFT JOIN на сервері. EF Core ідентифікує такі шаблони та генерує еквівалентний LEFT JOIN на стороні сервера. Шаблон передбачає створення GroupJoin між обома джерелами даних, а потім вирівнювання групування за допомогою оператора SelectMany з DefaultIfEmpty на джерелі групування для збігу з null, коли внутрішній елемент не має пов'язаного елемента. У наступному прикладі показано, як виглядає цей шаблон і що він генерує.

```cs
    var query = from b in context.Set<Blog>()
                join p in context.Set<Post>()
                    on b.BlogId equals p.BlogId into grouping
                from p in grouping.DefaultIfEmpty()
                select new { b, p };

    Console.WriteLine(query.ToQueryString());

    var result = await query.ToListAsync();

    foreach (var item in result)
    {
        Console.WriteLine($"Blog: {item.b.BlogId}, {item.b.Url}\tPost: {(item.p != null ? item.p.Title : "No Post")}"); 
    }
```
```
SELECT [b].[BlogId], [b].[OwnerId], [b].[Rating], [b].[Url], [p].[PostId], [p].[AuthorId], [p].[BlogId], [p].[Content], [p].[Rating], [p].[Title]
FROM [Blogs] AS [b]
LEFT JOIN [Posts] AS [p] ON [b].[BlogId] = [p].[BlogId]
Blog: 1, https://devblogs.microsoft.com/dotnet  Post: What's new
Blog: 2, https://mytravelblog.com/      Post: Around the World in Eighty Days
Blog: 2, https://mytravelblog.com/      Post: Glamping *is* the way
Blog: 2, https://mytravelblog.com/      Post: Travel in the time of pandemic
Blog: 3, https://mytravelblog3.com/     Post: No Post
```
Наведений вище шаблон створює складну структуру в дереві виразів. Через це EF Core вимагає вирівнювання результатів групування оператора GroupJoin на кроці, що йде безпосередньо після цього оператора. Навіть якщо GroupJoin-DefaultIfEmpty-SelectMany використовується, але в іншому шаблоні, ми можемо не ідентифікувати його як ліве з'єднання.