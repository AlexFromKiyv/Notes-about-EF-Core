# Чергування кількох моделей з однаковим типом DbContext

Модель, побудована в OnModelCreating, може використовувати властивість контексту, щоб змінити спосіб побудови моделі. Наприклад, припустимо, що ви хочете налаштувати сутність по-різному на основі певної властивості:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    if (UseIntProperty)
    {
        modelBuilder.Entity<ConfigurableEntity>().Ignore(e => e.StringProperty);
    }
    else
    {
        modelBuilder.Entity<ConfigurableEntity>().Ignore(e => e.IntProperty);
    }
}
```

На жаль, цей код не працюватиме як є, оскільки EF будує модель та запускає OnModelCreating лише один раз, кешуючи результат з міркувань продуктивності. Однак, ви можете підключитися до механізму кешування моделі, щоб повідомити EF про властивість, яка створює різні моделі.

## IModelCacheKeyFactory

EF використовує IModelCacheKeyFactory для генерації ключів кешу для моделей; за замовчуванням EF припускає, що для будь-якого заданого типу контексту модель буде однаковою, тому реалізація цього сервісу за замовчуванням повертає ключ, який містить лише тип контексту. Щоб створити різні моделі з одного типу контексту, потрібно замінити сервіс IModelCacheKeyFactory правильною реалізацією; згенерований ключ буде порівняно з іншими ключами моделі за допомогою методу Equals, враховуючи всі змінні, що впливають на модель.

Наступна реалізація враховує UseIntProperty під час створення ключа кешу моделі:

```cs
public class DynamicModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => context is DynamicContext dynamicContext
            ? (context.GetType(), dynamicContext.UseIntProperty, designTime)
            : (object)context.GetType();
}
```
Також потрібно реалізувати перевантаження методу Create, який також обробляє кешування моделі під час проектування. Як у наступному прикладі:

```cs
public class DynamicModelCacheKeyFactoryDesignTimeSupport : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => context is DynamicContext dynamicContext
            ? (context.GetType(), dynamicContext.UseIntProperty, designTime)
            : (object)context.GetType();

    public object Create(DbContext context)
        => Create(context, false);
}
```
Нарешті, зареєструйте свій новий IModelCacheKeyFactory у OnConfiguring вашого контексту:

```cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder
        .UseInMemoryDatabase("DynamicContext")
        .ReplaceService<IModelCacheKeyFactory, DynamicModelCacheKeyFactory>();
```