# Пов’язані дані та серіалізація

Оскільки EF Core автоматично виправляє властивості навігації, у графі об'єктів можуть виникнути цикли. Наприклад, завантаження блогу та пов’язаних з ним публікацій призведе до створення об’єкта блогу, який посилається на колекцію публікацій. Кожен із цих постів міститиме посилання на блог. 

Деякі фреймворки серіалізації не дозволяють такі цикли. Наприклад, Json.NET викличе наступний виняток, якщо цикл знайдено.

```
Newtonsoft.Json.JsonSerializationException: Self referencing loop detected for property 'Blog' with type 'MyApplication.Models.Blog'.
```
System.Text.Json викличе аналогічний виняток, якщо буде знайдено цикл.

```
System.Text.Json.JsonException: A possible object cycle was detected. This can either be due to a cycle or if the object depth is larger than the maximum allowed depth of 32. Consider using ReferenceHandler.Preserve on JsonSerializerOptions to support cycles.
```
Якщо ви використовуєте Json.NET в ASP.NET Core, ви можете налаштувати Json.NET так, щоб він ігнорував цикли, знайдені в графі об'єктів. Це налаштування виконується в методі ConfigureServices(...) у Startup.cs.

```cs
public void ConfigureServices(IServiceCollection services)
{
    ...

    services.AddMvc()
        .AddJsonOptions(
            options => options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
        );

    ...
}
```
Якщо ви використовуєте System.Text.Json, ви можете налаштувати його ось так.

```cs
public void ConfigureServices(IServiceCollection services)
{
    ...

    services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });

    ...
}
```
Іншим варіантом є ігнорування властивостей навігації, які викликають цикл серіалізації JSON. Якщо ви використовуєте Json.NET, ви можете прикрасити одну з властивостей навігації атрибутом [JsonIgnore], який вказує Json.NET не перетинати цю властивість навігації під час серіалізації. Для System.Text.Json можна використовувати атрибут [JsonIgnore] у просторі імен System.Text.Json.Serialization, щоб досягти того ж ефекту.