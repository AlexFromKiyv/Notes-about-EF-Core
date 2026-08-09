# Зіставлення користувацьких функцій

EF Core дозволяє використовувати користувацькі SQL-функції в запитах. Для цього функції потрібно зіставити з методом CLR під час налаштування моделі. Під час перетворення запиту LINQ на SQL викликається визначена користувачем функція замість функції CLR, з якою вона була зіставлена.

## Зіставлення методу з SQL-функцією

Щоб проілюструвати, як працює зіставлення користувацьких функцій, визначимо такі сутності:

```cs
public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
    public int? Rating { get; set; }

    public List<Post> Posts { get; set; }
}

public class Post
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public int Rating { get; set; }
    public int BlogId { get; set; }

    public Blog Blog { get; set; }
    public List<Comment> Comments { get; set; }
}

public class Comment
{
    public int CommentId { get; set; }
    public string Text { get; set; }
    public int Likes { get; set; }
    public int PostId { get; set; }

    public Post Post { get; set; }
}
```
І наступна конфігурація моделі:

```cs
modelBuilder.Entity<Blog>()
    .HasMany(b => b.Posts)
    .WithOne(p => p.Blog);

modelBuilder.Entity<Post>()
    .HasMany(p => p.Comments)
    .WithOne(c => c.Post);
```
Блог може містити багато постів, і кожен пост може мати багато коментарів.

Далі створіть користувацьку функцію CommentedPostCountForBlog, яка повертає кількість публікацій з принаймні одним коментарем для заданого блогу, на основі ідентифікатора блогу:

```sql
CREATE FUNCTION dbo.CommentedPostCountForBlog(@id int)
RETURNS int
AS
BEGIN
    RETURN (SELECT COUNT(*)
        FROM [Posts] AS [p]
        WHERE ([p].[BlogId] = @id) AND ((
            SELECT COUNT(*)
            FROM [Comments] AS [c]
            WHERE [p].[PostId] = [c].[PostId]) > 0));
END
```
Щоб використовувати цю функцію в EF Core, ми визначаємо наступний метод CLR, який ми зіставляємо з користувацькою функцією:

```cs
public int ActivePostCountForBlog(int blogId)
    => throw new NotSupportedException();
```

Тіло методу CLR не є важливим. Метод не буде викликано на стороні клієнта, окрім випадків, коли EF Core не зможе перекласти свої аргументи. Якщо аргументи можна перекласти, EF Core звертатиме увагу лише на сигнатуру методу.

Примітка

У цьому прикладі метод визначено в DbContext, але його також можна визначити як статичний метод всередині інших класів.

Це визначення функції тепер можна пов'язати з функцією, визначеною користувачем, у конфігурації моделі:

```cs
modelBuilder.HasDbFunction(typeof(BloggingContext).GetMethod(nameof(ActivePostCountForBlog), [typeof(int)]))
    .HasName("CommentedPostCountForBlog");
```

Тепер, виконуючи наступний запит:

```cs
    var query = from b in context.Blogs
                where context.ActivePostCountForBlog(b.BlogId) > 1
                select b;

    Console.WriteLine(query.ToQueryString());
```

Виведе цей SQL-запит:
```sql
SELECT [b].[BlogId], [b].[Rating], [b].[Url]
FROM [Blogs] AS [b]
WHERE [dbo].[CommentedPostCountForBlog]([b].[BlogId]) > 1
```

## Зіставлення методу з користувацьким SQL

EF Core також дозволяє використовувати користувацькі функції, які перетворюються на певний SQL. SQL-вираз надається за допомогою методу HasTranslation під час налаштування користувацьких функцій. 

У наведеному нижче прикладі ми створимо функцію, яка обчислює відсоткову різницю між двома цілими числами.

Метод CLR виглядає наступним чином:

```cs
public double PercentageDifference(double first, int second)
    => throw new NotSupportedException();
```
Визначення функції таке:

```cs
// 100 * ABS(first - second) / ((first + second) / 2)
modelBuilder.HasDbFunction(
        typeof(BloggingContext).GetMethod(nameof(PercentageDifference), [typeof(double), typeof(int)]))
    .HasTranslation(
        args =>
            new SqlBinaryExpression(
                ExpressionType.Multiply,
                new SqlConstantExpression(100, new IntTypeMapping("int", DbType.Int32)),
                new SqlBinaryExpression(
                    ExpressionType.Divide,
                    new SqlFunctionExpression(
                        "ABS",
                        [
                            new SqlBinaryExpression(
                                ExpressionType.Subtract,
                                args.First(),
                                args.Skip(1).First(),
                                args.First().Type,
                                args.First().TypeMapping)
                        ],
                        nullable: true,
                        argumentsPropagateNullability: [true, true],
                        type: args.First().Type,
                        typeMapping: args.First().TypeMapping),
                    new SqlBinaryExpression(
                        ExpressionType.Divide,
                        new SqlBinaryExpression(
                            ExpressionType.Add,
                            args.First(),
                            args.Skip(1).First(),
                            args.First().Type,
                            args.First().TypeMapping),
                        new SqlConstantExpression(2, new IntTypeMapping("int", DbType.Int32)),
                        args.First().Type,
                        args.First().TypeMapping),
                    args.First().Type,
                    args.First().TypeMapping),
                args.First().Type,
                args.First().TypeMapping));
```

Після визначення функції її можна використовувати в запиті. Замість виклику функції бази даних, EF Core перетворить тіло методу безпосередньо в SQL на основі дерева виразів SQL, побудованого з HasTranslation. Наступний запит LINQ:

```cs
var query = from p in context.Posts
             select context.PercentageDifference(p.BlogId, 3);
```
Виводить наступний SQL-код:

```sql
SELECT 100 * (ABS(CAST([p].[BlogId] AS float) - 3) / ((CAST([p].[BlogId] AS float) + 3) / 2))
FROM [Posts] AS [p]
```

## Налаштування можливості повернення значення null користувацькою функцією на основі її аргументів

Якщо користувацька функція може повертати значення null лише тоді, коли один або декілька її аргументів є значеннями null, EFCore надає спосіб вказати це, що призводить до підвищення продуктивності SQL. Це можна зробити, додавши виклик PropagatesNullability() до відповідної конфігурації моделі параметрів функції.

Щоб проілюструвати це, визначте користувацьку функцію ConcatStrings:

```sql
CREATE FUNCTION [dbo].[ConcatStrings] (@prm1 nvarchar(max), @prm2 nvarchar(max))
RETURNS nvarchar(max)
AS
BEGIN
    RETURN @prm1 + @prm2;
END
```
та два методи CLR, що відповідають йому:

```cs
public string ConcatStrings(string prm1, string prm2)
    => throw new InvalidOperationException();

public string ConcatStringsOptimized(string prm1, string prm2)
    => throw new InvalidOperationException();
```
Конфігурація моделі (всередині методу OnModelCreating) така:

```cs
modelBuilder
    .HasDbFunction(typeof(BloggingContext).GetMethod(nameof(ConcatStrings), [typeof(string), typeof(string)]))
    .HasName("ConcatStrings");

modelBuilder.HasDbFunction(
    typeof(BloggingContext).GetMethod(nameof(ConcatStringsOptimized), [typeof(string), typeof(string)]),
    b =>
    {
        b.HasName("ConcatStrings");
        b.HasParameter("prm1").PropagatesNullability();
        b.HasParameter("prm2").PropagatesNullability();
    });
```

Першу функцію налаштовано стандартним способом. Другу функцію налаштовано для використання оптимізації поширення нульових значень, що надає більше інформації про те, як функція поводиться з параметрами null.

Під час видачі наступних запитів:

```cs
    var query3 = context.Blogs.Where(e => context.ConcatStrings(e.Url, e.Rating.ToString()) != "https://mytravelblog.com/4");
    var query4 = context.Blogs.Where(
        e => context.ConcatStringsOptimized(e.Url, e.Rating.ToString()) != "https://mytravelblog.com/4");

    Console.WriteLine(query3.ToQueryString());
    Console.WriteLine();
    Console.WriteLine(query4.ToQueryString());
```
Ми отримуємо цей SQL-запит:
```sql
SELECT [b].[BlogId], [b].[Rating], [b].[Url]
FROM [Blogs] AS [b]
WHERE [dbo].[ConcatStrings]([b].[Url], COALESCE(CONVERT(varchar(11), [b].[Rating]), '')) <> N'https://mytravelblog.com/4' OR [dbo].[ConcatStrings]([b].[Url], COALESCE(CONVERT(varchar(11), [b].[Rating]), '')) IS NULL

SELECT [b].[BlogId], [b].[Rating], [b].[Url]
FROM [Blogs] AS [b]
WHERE [dbo].[ConcatStrings]([b].[Url], COALESCE(CONVERT(varchar(11), [b].[Rating]), '')) <> N'https://mytravelblog.com/4'
```
Другий запит не потребує повторного обчислення самої функції для перевірки її можливості повернення до нульового значення.

Примітка

Цю оптимізацію слід використовувати лише тоді, коли функція може повертати значення null, коли її параметри також мають значення null.

## Зіставлення функції, що запитується, з функцією, що повертає табличне значення

EF Core також підтримує зіставлення з функцією, що повертає табличне значення, за допомогою користувацького методу CLR, що повертає IQueryable типів сутностей, що дозволяє EF Core зіставляти TVF з параметрами. Цей процес схожий на зіставлення скалярної користувацької функції з SQL-функцією: нам потрібна TVF у базі даних, функція CLR, яка використовується в LINQ-запитах, і зіставлення між ними.

Як приклад, ми використаємо функцію, що повертає табличні значення та повертає всі публікації, до яких принаймні один коментар відповідає заданому порогу «Like»:

```sql
CREATE FUNCTION dbo.PostsWithPopularComments(@likeThreshold int)
RETURNS TABLE
AS
RETURN
(
    SELECT [p].[PostId], [p].[BlogId], [p].[Content], [p].[Rating], [p].[Title]
    FROM [Posts] AS [p]
    WHERE (
        SELECT COUNT(*)
        FROM [Comments] AS [c]
        WHERE ([p].[PostId] = [c].[PostId]) AND ([c].[Likes] >= @likeThreshold)) > 0
)
```
Сигнатура методу CLR виглядає наступним чином:

```cs
public IQueryable<Post> PostsWithPopularComments(int likeThreshold)
    => FromExpression(() => PostsWithPopularComments(likeThreshold));
```
Порада

Виклик FromExpression у тілі функції CLR дозволяє використовувати цю функцію замість звичайного DbSet.

А нижче наведено зіставлення:

```cs
modelBuilder.HasDbFunction(typeof(BloggingContext).GetMethod(nameof(PostsWithPopularComments), [typeof(int)]));
```

Примітка

Функція, до якої можна отримати запит, має бути зіставлена ​​з функцією, що повертає табличні значення, і не може використовувати HasTranslation.

Коли функція відображається, виконується наступний запит:

```cs
var likeThreshold = 3;
var query5 = from p in context.PostsWithPopularComments(likeThreshold)
             orderby p.Rating
             select p;
```

Створює:
```sql
DECLARE @__likeThreshold_1 int = 3;

SELECT [p].[PostId], [p].[BlogId], [p].[Content], [p].[Rating], [p].[Title]
FROM [dbo].[PostsWithPopularComments](@__likeThreshold_1) AS [p]
ORDER BY [p].[Rating]
```